using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Numbering;
using Prime.Domain.DomainServices;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Application.Features.PropertyIdentification;

public sealed record TerritorialChangeMappingRequest(Guid SourceBarangayId, Guid TargetBarangayId);

public sealed record CreateTerritorialChangeRequest(
    TerritorialChangeKind Kind, string LegalBasis, DateOnly EffectiveDate, TerritorialChangePinMode PinMode,
    IReadOnlyList<TerritorialChangeMappingRequest> Mappings, string? Remarks = null);

public sealed record TerritorialChangeMappingDto(Guid SourceBarangayId, string SourceBarangayName, Guid TargetBarangayId, string TargetBarangayName);

public sealed record TerritorialChangeItemDto(Guid PropertyId, string OldPin, string? NewPin, TerritorialChangeItemStatus Status, string? Error);

public sealed record TerritorialChangeDto(
    Guid Id, TerritorialChangeKind Kind, string LegalBasis, DateOnly EffectiveDate, TerritorialChangePinMode PinMode, WorkflowStatus Status,
    JobExecutionStatus? RunStatus, int TotalCount, int ProcessedCount, int FailedCount, Guid? CreatedBy, DateTimeOffset CreatedAt,
    Guid? ApprovedBy, DateTimeOffset? ApprovedAt, DateTimeOffset? CompletedAt, string? Remarks,
    IReadOnlyList<TerritorialChangeMappingDto> Mappings, IReadOnlyList<TerritorialChangeItemDto> Items);

public interface ITerritorialChangeService
{
    Task<Result<TerritorialChangeDto>> CreateAsync(CreateTerritorialChangeRequest request, CancellationToken ct = default);
    /// <summary>Approval by a second user lists the affected properties and starts the job.</summary>
    Task<Result<TerritorialChangeDto>> ApproveAsync(Guid id, CancellationToken ct = default);
    /// <summary>Runs the job again over its pending and failed properties.</summary>
    Task<Result<TerritorialChangeDto>> ResumeAsync(Guid id, CancellationToken ct = default);
    Task<Result<TerritorialChangeDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<TerritorialChangeDto>>> ListAsync(CancellationToken ct = default);
}

/// <summary>
/// Territorial changes (LAM Bk II p.40 §4.E–F; docs/analysis/identification-numbering.md §4.3, Q9): the properties of
/// the barangays a law or court order moves are given new PINs under the receiving barangays' index numbers, keeping
/// their section and parcel numbers (default) or with temporary PINs until re-tax-mapping. Maker-checker, then a
/// background job (CLAUDE.md §73) that records every old and new PIN and can be resumed.
/// </summary>
public sealed class TerritorialChangeService(IApplicationDbContext db, ICurrentUserService currentUser, IBackgroundJobScheduler scheduler)
    : ITerritorialChangeService
{
    /// <summary>The most items a job's detail lists; the counts cover them all.</summary>
    public const int ItemListLimit = 500;

    public async Task<Result<TerritorialChangeDto>> CreateAsync(CreateTerritorialChangeRequest request, CancellationToken ct = default)
    {
        var mappings = request.Mappings ?? [];
        if (!Enum.IsDefined(request.Kind) || !Enum.IsDefined(request.PinMode) || string.IsNullOrWhiteSpace(request.LegalBasis)
            || request.LegalBasis.Length > 500 || request.EffectiveDate == default || request.Remarks?.Length > 1000 || mappings.Count == 0
            || mappings.Select(m => m.SourceBarangayId).Distinct().Count() != mappings.Count || mappings.Any(m => m.SourceBarangayId == m.TargetBarangayId))
        {
            return Fail("VALIDATION_FAILED",
                "kind, pinMode, the legal basis (the law or court order; max 500), the effective date and at least one barangay mapping are required; each source barangay once, moving to another barangay.");
        }
        var ids = mappings.SelectMany(m => new[] { m.SourceBarangayId, m.TargetBarangayId }).Distinct().ToList();
        var barangays = await db.Barangays.Include(b => b.Municipality).ThenInclude(m => m!.Province).Where(b => ids.Contains(b.Id)).ToListAsync(ct);
        if (barangays.Count != ids.Count)
        {
            return Fail("BARANGAY_NOT_FOUND", "A barangay of the mappings does not exist.");
        }
        foreach (var target in mappings.Select(m => barangays.Single(b => b.Id == m.TargetBarangayId)))
        {
            // The new PINs need the receiving barangay's index numbers (set under Property Identification first).
            if (target.PinIndexNumber is null || target.Municipality!.PinIndexNumber is null || target.RetiredOn is not null)
            {
                return Fail("TARGET_INDEX_MISSING", $"Barangay {target.Name} must be active and have its index numbers (and its municipality's) before properties move to it.");
            }
        }
        var job = new TerritorialChangeJob
        {
            Kind = request.Kind, LegalBasis = request.LegalBasis.Trim(), EffectiveDate = request.EffectiveDate, PinMode = request.PinMode,
            Remarks = request.Remarks,
            Mappings = mappings.Select(m => new TerritorialChangeMapping { SourceBarangayId = m.SourceBarangayId, TargetBarangayId = m.TargetBarangayId }).ToList(),
        };
        db.TerritorialChangeJobs.Add(job);
        await db.SaveChangesAsync(ct);
        return await GetAsync(job.Id, ct);
    }

    public async Task<Result<TerritorialChangeDto>> ApproveAsync(Guid id, CancellationToken ct = default)
    {
        var job = await db.TerritorialChangeJobs.Include(x => x.Mappings).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (job is null)
        {
            return NotFound();
        }
        if (job.Status != WorkflowStatus.Draft)
        {
            return Fail("TERRITORIAL_CHANGE_NOT_DRAFT", "Only a Draft territorial change can be approved.");
        }
        if (currentUser.AppUserId is not null && job.CreatedBy == currentUser.AppUserId)
        {
            return Fail("CANNOT_APPROVE_OWN_TERRITORIAL_CHANGE", "The creator cannot also approve it (maker-checker, CLAUDE.md §46).");
        }
        var sources = job.Mappings.Select(m => m.SourceBarangayId).ToList();
        var properties = await db.Properties.Where(p => sources.Contains(p.BarangayId) && p.Status == RecordStatus.Active)
            .Select(p => new { p.Id, p.PropertyIdentificationNumber }).ToListAsync(ct);
        job.Items = properties.Select(p => new TerritorialChangeItem { PropertyId = p.Id, OldPin = p.PropertyIdentificationNumber }).ToList();
        foreach (var item in job.Items)
        {
            item.TerritorialChangeJobId = job.Id;
            db.TerritorialChangeItems.Add(item);
        }
        job.Status = WorkflowStatus.Approved;
        job.ApprovedBy = currentUser.AppUserId;
        job.ApprovedAt = DateTimeOffset.UtcNow;
        job.TotalCount = job.Items.Count;
        job.RunStatus = JobExecutionStatus.Queued;
        currentUser.Reason = $"Territorial change approved: {job.LegalBasis}";
        await db.SaveChangesAsync(ct);
        scheduler.Enqueue<TerritorialChangeJobRunner>(runner => runner.RunAsync(job.Id, CancellationToken.None));
        return await GetAsync(id, ct);
    }

    public async Task<Result<TerritorialChangeDto>> ResumeAsync(Guid id, CancellationToken ct = default)
    {
        var job = await db.TerritorialChangeJobs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (job is null)
        {
            return NotFound();
        }
        if (job.Status != WorkflowStatus.Approved || job.RunStatus is JobExecutionStatus.Running or JobExecutionStatus.Queued
            || !await db.TerritorialChangeItems.AnyAsync(x => x.TerritorialChangeJobId == id && x.Status != TerritorialChangeItemStatus.Done, ct))
        {
            return Fail("TERRITORIAL_CHANGE_NOT_RESUMABLE", "Only an approved territorial change that has stopped with properties left to move can be resumed.");
        }
        job.RunStatus = JobExecutionStatus.Queued;
        await db.SaveChangesAsync(ct);
        scheduler.Enqueue<TerritorialChangeJobRunner>(runner => runner.RunAsync(job.Id, CancellationToken.None));
        return await GetAsync(id, ct);
    }

    public async Task<Result<TerritorialChangeDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var job = await db.TerritorialChangeJobs.AsNoTracking().Include(x => x.Mappings).ThenInclude(m => m.SourceBarangay)
            .Include(x => x.Mappings).ThenInclude(m => m.TargetBarangay).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (job is null)
        {
            return NotFound();
        }
        var items = await db.TerritorialChangeItems.AsNoTracking().Where(x => x.TerritorialChangeJobId == id)
            .OrderBy(x => x.Status == TerritorialChangeItemStatus.Failed ? 0 : 1).ThenBy(x => x.OldPin).Take(ItemListLimit)
            .Select(x => new TerritorialChangeItemDto(x.PropertyId, x.OldPin, x.NewPin, x.Status, x.Error)).ToListAsync(ct);
        return Result.Success(ToDto(job, items));
    }

    public async Task<Result<IReadOnlyList<TerritorialChangeDto>>> ListAsync(CancellationToken ct = default)
    {
        var jobs = await db.TerritorialChangeJobs.AsNoTracking().Include(x => x.Mappings).ThenInclude(m => m.SourceBarangay)
            .Include(x => x.Mappings).ThenInclude(m => m.TargetBarangay).OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return Result.Success<IReadOnlyList<TerritorialChangeDto>>(jobs.Select(j => ToDto(j, [])).ToList());
    }

    private static TerritorialChangeDto ToDto(TerritorialChangeJob x, IReadOnlyList<TerritorialChangeItemDto> items) => new(
        x.Id, x.Kind, x.LegalBasis, x.EffectiveDate, x.PinMode, x.Status, x.RunStatus, x.TotalCount, x.ProcessedCount, x.FailedCount,
        x.CreatedBy, x.CreatedAt, x.ApprovedBy, x.ApprovedAt, x.CompletedAt, x.Remarks,
        x.Mappings.Select(m => new TerritorialChangeMappingDto(m.SourceBarangayId, m.SourceBarangay!.Name, m.TargetBarangayId, m.TargetBarangay!.Name)).ToList(),
        items);

    private static Result<TerritorialChangeDto> NotFound() => Fail("TERRITORIAL_CHANGE_NOT_FOUND", "No territorial change was found with the given id.");

    private static Result<TerritorialChangeDto> Fail(string code, string message) => Result.Failure<TerritorialChangeDto>(code, message);
}

/// <summary>
/// The territorial change's work, one property at a time, each in its own database transaction so a failure stops
/// nothing else and a rerun continues where it left off (Hangfire resolves this class per run; see
/// <c>GeneralRevisionJobRunner</c> for the acting-user pattern).
/// </summary>
public sealed class TerritorialChangeJobRunner(IApplicationDbContext db, ICurrentUserService currentUser, INumberSequenceAllocator allocator,
    INumberingService numbering, IClock clock)
{
    public async Task RunAsync(Guid jobId, CancellationToken ct)
    {
        var job = await db.TerritorialChangeJobs.Include(x => x.Mappings).FirstOrDefaultAsync(x => x.Id == jobId, ct);
        if (job is null || job.Status != WorkflowStatus.Approved)
        {
            return;
        }
        currentUser.ActAsForBackgroundJob(job.CreatedBy);
        job.RunStatus = JobExecutionStatus.Running;
        job.StartedAt ??= clock.UtcNow;
        await db.SaveChangesAsync(ct);

        var itemIds = await db.TerritorialChangeItems.Where(x => x.TerritorialChangeJobId == jobId && x.Status != TerritorialChangeItemStatus.Done)
            .OrderBy(x => x.OldPin).Select(x => x.Id).ToListAsync(ct);
        foreach (var itemId in itemIds)
        {
            await ProcessAsync(job, itemId, ct);
        }

        // A failed property clears the change tracker: work on a fresh copy of the job.
        job = await db.TerritorialChangeJobs.FirstAsync(x => x.Id == jobId, ct);
        job.ProcessedCount = await db.TerritorialChangeItems.CountAsync(x => x.TerritorialChangeJobId == jobId && x.Status == TerritorialChangeItemStatus.Done, ct);
        job.FailedCount = await db.TerritorialChangeItems.CountAsync(x => x.TerritorialChangeJobId == jobId && x.Status == TerritorialChangeItemStatus.Failed, ct);
        job.RunStatus = job.TotalCount > 0 && job.FailedCount == job.TotalCount ? JobExecutionStatus.Failed : JobExecutionStatus.Completed;
        job.CompletedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task ProcessAsync(TerritorialChangeJob job, Guid itemId, CancellationToken ct)
    {
        var item = await db.TerritorialChangeItems.FirstAsync(x => x.Id == itemId, ct);
        var ownsTransaction = db.Database.CurrentTransaction is null;
        var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;
        string? error;
        try
        {
            error = await MoveAsync(job, item, ct);
            if (error is null && transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }
        }
        catch (DbUpdateException ex)
        {
            error = $"Not saved: {ex.InnerException?.Message ?? ex.Message}";
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
        if (error is not null)
        {
            // The property's changes were rolled back; record the failure on its own.
            db.ClearChangeTracker();
            item = await db.TerritorialChangeItems.FirstAsync(x => x.Id == itemId, ct);
            item.Status = TerritorialChangeItemStatus.Failed;
            item.Error = error.Length > 1000 ? error[..1000] : error;
            item.ProcessedAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Moves one property: retires its PIN, re-locates it and its parcels, and gives the new PIN. Null on success, else why not.</summary>
    private async Task<string?> MoveAsync(TerritorialChangeJob job, TerritorialChangeItem item, CancellationToken ct)
    {
        var property = await db.Properties.FirstAsync(x => x.Id == item.PropertyId, ct);
        var mapping = job.Mappings.FirstOrDefault(m => m.SourceBarangayId == property.BarangayId);
        if (mapping is null)
        {
            return "The property is no longer in a barangay this change moves.";
        }
        var target = await db.Barangays.Include(b => b.Municipality).FirstAsync(b => b.Id == mapping.TargetBarangayId, ct);
        var current = await db.PinAssignments.Include(a => a.Section).FirstOrDefaultAsync(a => a.PropertyId == property.Id && a.RetiredAt == null, ct);
        var today = clock.Today;
        var reason = $"Territorial change ({job.Kind}) effective {job.EffectiveDate:yyyy-MM-dd}: {job.LegalBasis}";
        currentUser.Reason = reason;

        string pin;
        PinAssignment assignment;
        var parcels = await db.Parcels.Where(p => p.PropertyId == property.Id).ToListAsync(ct);
        if (job.PinMode == TerritorialChangePinMode.KeepParcelNumbers && current is { Kind: PinKind.Permanent, Section: not null, ParcelNumber: { } parcelNumber })
        {
            var scheme = await db.NumberingSchemes.InForce(today).FirstOrDefaultAsync(x => x.AppliesTo == NumberedDocumentKind.PropertyIdentificationNumber, ct);
            if (scheme is null || !NumberPattern.Tokens(scheme.Pattern).Contains("SECT"))
            {
                return "No PIN numbering scheme with a section ({SECT}) is in force.";
            }
            // The same section number in the receiving barangay (created when it has none yet).
            var sectionIndex = current.Section.IndexNumber;
            var section = await db.TaxMapSections.FirstOrDefaultAsync(s => s.BarangayId == target.Id && s.IndexNumber == sectionIndex, ct);
            if (section is null)
            {
                section = new TaxMapSection { BarangayId = target.Id, IndexNumber = sectionIndex, Remarks = $"Created by a territorial change ({job.LegalBasis})" };
                db.TaxMapSections.Add(section);
                await db.SaveChangesAsync(ct);
            }
            else if (section.RetiredOn is not null)
            {
                return $"Section {sectionIndex} of barangay {target.Name} is retired.";
            }
            var context = await PinContexts.ForBarangayAsync(db, target.Id, today.Year, sectionIndex, ct);
            if (NumberPattern.MissingValues(scheme.Pattern, context) is { Count: > 0 } missing)
            {
                return $"The new PIN needs {string.Join(", ", missing)}.";
            }
            if (parcelNumber > 0)
            {
                await allocator.ReserveAsync(scheme.Id, NumberPattern.ScopeKey(scheme.Pattern, context), parcelNumber, ct);
                pin = NumberPattern.Format(scheme.Pattern, context, parcelNumber);
            }
            else
            {
                pin = NumberPattern.FormatZero(scheme.Pattern, context); // structures over water
            }
            foreach (var parcel in parcels.Where(p => p.SectionId == current.SectionId))
            {
                parcel.SectionId = section.Id;
            }
            assignment = new PinAssignment
            {
                PropertyId = property.Id, Pin = pin, Kind = PinKind.Permanent, ParcelId = current.ParcelId, BarangayId = target.Id, SectionId = section.Id,
                ParcelNumber = parcelNumber, AssignedAt = clock.UtcNow,
            };
        }
        else
        {
            // Temporary PINs until the area is re-tax-mapped (or the property had no permanent PIN to keep).
            var context = await PinContexts.ForBarangayAsync(db, target.Id, today.Year, null, ct);
            var temporary = await numbering.GenerateIfConfiguredAsync(NumberedDocumentKind.TemporaryPin, context, today, ct);
            if (temporary.IsFailure)
            {
                return temporary.Message;
            }
            if (temporary.Value is null)
            {
                return "No TemporaryPin numbering scheme is in force.";
            }
            pin = temporary.Value;
            foreach (var parcel in parcels)
            {
                (parcel.SectionId, parcel.ParcelNumber) = (null, null);
            }
            assignment = new PinAssignment { PropertyId = property.Id, Pin = pin, Kind = PinKind.Temporary, BarangayId = target.Id, AssignedAt = clock.UtcNow };
        }
        if (await db.PinAssignments.IgnoreQueryFilters().AnyAsync(x => x.Pin == pin, ct)
            || await db.Properties.IgnoreQueryFilters().AnyAsync(x => x.Id != property.Id && x.PropertyIdentificationNumber == pin, ct))
        {
            return $"PIN {pin} has already been given; PINs are never reused.";
        }

        PermanentPins.Retire(db, property, current, clock.UtcNow, reason, null);
        await db.SaveChangesAsync(ct);
        foreach (var parcel in parcels)
        {
            parcel.BarangayId = target.Id;
        }
        property.BarangayId = target.Id;
        property.MunicipalityId = target.MunicipalityId;
        property.ProvinceId = target.Municipality!.ProvinceId;
        property.PropertyIdentificationNumber = pin;
        db.PinAssignments.Add(assignment);
        item.NewPin = pin;
        item.Status = TerritorialChangeItemStatus.Done;
        item.Error = null;
        item.ProcessedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return null;
    }
}
