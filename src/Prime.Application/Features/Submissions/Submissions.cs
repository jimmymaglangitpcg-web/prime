using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Forms;
using Prime.Application.Features.Offices;
using Prime.Application.Features.Registers;
using Prime.Domain.Entities.Registers;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Submissions;

// --- Municipality → province (docs/analysis/province-wide-operation.md §3.7, LP-6) ---

/// <summary>One approved TD (with its FAAS) as the province receives it: the frozen printed copies, or none if no form was in force.</summary>
public sealed record ApprovedDocumentDto(
    Guid TaxDeclarationId, string TaxDeclarationNumber, RpuType Kind, Guid PropertyId, string Pin, Guid MunicipalityId, string Municipality,
    string Barangay, DateTimeOffset ApprovedAt, string? ApprovedBy, WorkflowStatus CurrentStatus,
    Guid? TaxDeclarationFormId, Guid? FaasFormId);

public sealed record CreateRollSubmissionRequest(Guid MunicipalityId, int Year, int Month, string? Remarks);

public sealed record ReviewRollSubmissionRequest(string? Remarks);

public sealed record RollSubmissionItemDto(Guid Id, RegisterKind Kind, Guid BarangayId, string Barangay, int EntryCount, Guid RegisterRunId, Guid IssuedFormId);

public sealed record RollSubmissionDto(
    Guid Id, Guid MunicipalityId, string Municipality, Guid OfficeId, string OfficeCode, int Year, int Month,
    AssessmentRollSubmissionStatus Status, string? Remarks, DateTimeOffset SubmittedAt, string? SubmittedBy,
    DateTimeOffset? ReviewedAt, string? ReviewedBy, string? ReviewRemarks, IReadOnlyList<RollSubmissionItemDto> Items);

public interface IApprovedDocumentIssuer
{
    /// <summary>
    /// Freezes the printed TD, and the FAAS when the TD declares an assessment, of TDs just
    /// finally approved (LP-6 decision: approval is the submission). Best effort: a form not
    /// in force, or one refusing to issue, is logged and skipped — approval never fails on printing.
    /// </summary>
    Task IssueAsync(IReadOnlyCollection<Guid> taxDeclarationIds, CancellationToken cancellationToken = default);
}

public interface ISubmissionService
{
    Task<Result<IReadOnlyList<ApprovedDocumentDto>>> ListApprovedAsync(Guid? municipalityId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<RollSubmissionDto>>> ListRollsAsync(Guid? municipalityId, CancellationToken cancellationToken = default);
    Task<Result<RollSubmissionDto>> GetRollAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<RollSubmissionDto>> SubmitRollAsync(CreateRollSubmissionRequest request, CancellationToken cancellationToken = default);
    Task<Result<RollSubmissionDto>> AcknowledgeRollAsync(Guid id, ReviewRollSubmissionRequest request, CancellationToken cancellationToken = default);
    Task<Result<RollSubmissionDto>> ReturnRollAsync(Guid id, ReviewRollSubmissionRequest request, CancellationToken cancellationToken = default);
}

public sealed class ApprovedDocumentIssuer(IApplicationDbContext db, IFormService forms, ILogger<ApprovedDocumentIssuer> logger) : IApprovedDocumentIssuer
{
    /// <summary>The FAAS layout per kind of unit (MRPAAO: land and other improvements share one).</summary>
    public static string FaasFormCode(RpuType kind) => kind switch
    {
        RpuType.Building => "FAAS_BUILDING",
        RpuType.Machinery => "FAAS_MACHINERY",
        _ => "FAAS_LAND",
    };

    public async Task IssueAsync(IReadOnlyCollection<Guid> taxDeclarationIds, CancellationToken cancellationToken = default)
    {
        var tds = await db.TaxDeclarations.AsNoTracking().Where(x => taxDeclarationIds.Contains(x.Id) && x.Status == WorkflowStatus.Approved)
            .Select(x => new { x.Id, x.TaxDeclarationNumber, x.AssessmentId, x.Rpu!.RpuType }).ToListAsync(cancellationToken);
        foreach (var td in tds)
        {
            var codes = td.AssessmentId is null ? new[] { "TAX_DECLARATION" } : new[] { "TAX_DECLARATION", FaasFormCode(td.RpuType) };
            foreach (var code in codes)
            {
                var issued = await forms.IssueAsync(new IssueFormRequest(code, td.Id), cancellationToken);
                if (issued.IsFailure)
                {
                    logger.LogWarning("Approved TD {TaxDeclarationId}: form {FormCode} not issued at approval ({Code}).", td.Id, code, issued.Code);
                }
            }
        }
    }
}

public sealed class SubmissionService(
    IApplicationDbContext db,
    IJurisdiction jurisdiction,
    IOfficeContext officeContext,
    IFormService forms,
    IEnumerable<IFormDataProvider> providers,
    ICurrentUserService currentUser,
    IClock clock) : ISubmissionService
{
    private const int ApprovedListLimit = 500;

    // --- Approved FAAS/TD (approval = submission) ---

    public async Task<Result<IReadOnlyList<ApprovedDocumentDto>>> ListApprovedAsync(Guid? municipalityId, DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (from == default || to == default || from > to || to.DayNumber - from.DayNumber > 366)
        {
            return Result.Failure<IReadOnlyList<ApprovedDocumentDto>>("VALIDATION_FAILED", "Give a period of at most a year: from on or before to.");
        }
        // Approval instants are UTC; widen by a day each side, then keep those approved on the LGU's calendar dates.
        var start = new DateTimeOffset(from.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var end = new DateTimeOffset(to.AddDays(2).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var rows = (await db.TaxDeclarations.AsNoTracking()
                .Where(x => x.ApprovedAt != null && x.ApprovedAt >= start && x.ApprovedAt < end)
                .Where(x => municipalityId == null || x.Property!.MunicipalityId == municipalityId)
                .OrderByDescending(x => x.ApprovedAt).Take(ApprovedListLimit + 50)
                .Select(x => new
                {
                    x.Id, x.TaxDeclarationNumber, x.Rpu!.RpuType, x.PropertyId, Pin = x.Property!.PropertyIdentificationNumber,
                    x.Property.MunicipalityId, Municipality = x.Property.Municipality!.Name, Barangay = x.Property.Barangay!.Name,
                    ApprovedAt = x.ApprovedAt!.Value, x.ApprovedBy, x.Status,
                })
                .ToListAsync(cancellationToken))
            .Where(x => clock.LocalDate(x.ApprovedAt) >= from && clock.LocalDate(x.ApprovedAt) <= to)
            .Take(ApprovedListLimit).ToList();

        var ids = rows.Select(r => r.Id).ToList();
        var issued = await db.IssuedForms.AsNoTracking()
            .Where(f => ids.Contains(f.SubjectId) && f.Status == WorkflowStatus.Posted
                && (f.SubjectType == FormSubjectType.TaxDeclaration || f.SubjectType == FormSubjectType.Faas))
            .Select(f => new { f.Id, f.SubjectId, f.SubjectType, f.IssuedAt }).ToListAsync(cancellationToken);
        Guid? Latest(Guid td, FormSubjectType type) =>
            issued.Where(f => f.SubjectId == td && f.SubjectType == type).OrderByDescending(f => f.IssuedAt).Select(f => (Guid?)f.Id).FirstOrDefault();
        var approverIds = rows.Select(r => r.ApprovedBy).OfType<Guid>().Distinct().ToList();
        var approvers = await db.AppUsers.Where(u => approverIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        return Result.Success<IReadOnlyList<ApprovedDocumentDto>>(rows.Select(r => new ApprovedDocumentDto(
            r.Id, r.TaxDeclarationNumber, r.RpuType, r.PropertyId, r.Pin, r.MunicipalityId, r.Municipality, r.Barangay, r.ApprovedAt,
            r.ApprovedBy is { } a ? approvers.GetValueOrDefault(a) : null, r.Status,
            Latest(r.Id, FormSubjectType.TaxDeclaration), Latest(r.Id, FormSubjectType.Faas))).ToList());
    }

    // --- Monthly assessment roll ---

    public async Task<Result<IReadOnlyList<RollSubmissionDto>>> ListRollsAsync(Guid? municipalityId, CancellationToken cancellationToken = default) =>
        Result.Success(await MapAsync(db.AssessmentRollSubmissions
            .Where(x => municipalityId == null || x.MunicipalityId == municipalityId)
            .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month).ThenByDescending(x => x.CreatedAt).Take(500), cancellationToken));

    public async Task<Result<RollSubmissionDto>> GetRollAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await MapAsync(db.AssessmentRollSubmissions.Where(x => x.Id == id), cancellationToken)).SingleOrDefault() is { } dto
            ? Result.Success(dto)
            : RollNotFound();

    public async Task<Result<RollSubmissionDto>> SubmitRollAsync(CreateRollSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        var ct = cancellationToken;
        if (request.Month is < 1 or > 12 || request.Year is < 1900 or > 9999 || request.Remarks?.Length > 1000)
        {
            return Fail("VALIDATION_FAILED", "Give a year and a month (1–12); remarks at most 1000 characters.");
        }
        var first = new DateOnly(request.Year, request.Month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var today = clock.Today;
        if (last >= today)
        {
            return Fail("ROLL_MONTH_NOT_OVER", $"The roll for {first:MMMM yyyy} can be submitted once the month is over.");
        }
        if (!await db.Municipalities.AnyAsync(x => x.Id == request.MunicipalityId, ct))
        {
            return Fail("MUNICIPALITY_NOT_FOUND", "The specified city/municipality does not exist.");
        }
        if (!jurisdiction.Allows(request.MunicipalityId))
        {
            return Fail(JurisdictionErrors.Code, JurisdictionErrors.Message);
        }
        // The municipal office covering the town submits; the province receives (§3.7).
        var scope = await officeContext.GetAsync(ct);
        var covering = await db.OfficeJurisdictions.AsNoTracking().InForce(today).Where(j => j.MunicipalityId == request.MunicipalityId)
            .Select(j => (Guid?)j.OfficeId).FirstOrDefaultAsync(ct);
        if (covering is null || scope.OfficeId != covering)
        {
            return Fail("ROLL_SUBMISSION_FORBIDDEN", "Only staff of the municipal office covering this town submit its monthly assessment roll.");
        }
        if (await OpenSubmissionExistsAsync(request.MunicipalityId, request.Year, request.Month, ct))
        {
            return Fail("ROLL_SUBMISSION_EXISTS", $"The roll for {first:MMMM yyyy} was already submitted; a new one can follow only if the province returns it.");
        }

        var provider = providers.Single(p => p.SubjectType == FormSubjectType.Register);
        var barangays = await db.Barangays.AsNoTracking().Where(b => b.MunicipalityId == request.MunicipalityId)
            .OrderBy(b => b.Name).Select(b => b.Id).ToListAsync(ct);
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;

        var submission = new AssessmentRollSubmission
        {
            MunicipalityId = request.MunicipalityId, OfficeId = covering.Value, Year = request.Year, Month = request.Month,
            Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim(),
        };
        db.AssessmentRollSubmissions.Add(submission);
        foreach (var barangayId in barangays)
        {
            foreach (var kind in new[] { RegisterKind.AssessmentRollTaxable, RegisterKind.AssessmentRollExempt })
            {
                // A month supplement: FAAS entered in the month (docs/analysis/mrpaao-forms-model.md §15).
                var run = new RegisterRun
                {
                    Kind = kind, BarangayId = barangayId, FromDate = first, AsOf = last,
                    Remarks = $"Monthly assessment roll {request.Year}-{request.Month:00}",
                };
                db.RegisterRuns.Add(run);
                await db.SaveChangesAsync(ct);
                var data = await provider.BuildAsync(run.Id, ct);
                var entries = data?.Data["rows"] is JsonArray rows ? rows.Count : 0;
                if (entries == 0)
                {
                    // Never issued: a barangay without entries that month is not part of the roll.
                    db.RegisterRuns.Remove(run);
                    await db.SaveChangesAsync(ct);
                    continue;
                }
                var issued = await forms.IssueAsync(new IssueFormRequest(RegisterService.FormCode(kind), run.Id), ct);
                if (issued.IsFailure)
                {
                    return Fail(issued.Code!, $"The {RegisterService.FormCode(kind)} roll could not be issued: {issued.Message} Nothing was submitted.");
                }
                // Through the set: the submission is already saved, and a child added to its collection would be taken as existing.
                db.AssessmentRollSubmissionItems.Add(new AssessmentRollSubmissionItem
                {
                    SubmissionId = submission.Id, RegisterRunId = run.Id, IssuedFormId = issued.Value.Id, BarangayId = barangayId, Kind = kind, EntryCount = entries,
                });
            }
        }
        try
        {
            await db.SaveChangesAsync(ct);
            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }
        }
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException) // a row-version conflict is a 409 CONCURRENCY_CONFLICT
        {
            return Fail("ROLL_SUBMISSION_EXISTS", $"The roll for {first:MMMM yyyy} was submitted at the same time by someone else.");
        }
        return await GetRollAsync(submission.Id, ct);
    }

    public Task<Result<RollSubmissionDto>> AcknowledgeRollAsync(Guid id, ReviewRollSubmissionRequest request, CancellationToken cancellationToken = default) =>
        ReviewAsync(id, AssessmentRollSubmissionStatus.Acknowledged, request.Remarks, cancellationToken);

    public Task<Result<RollSubmissionDto>> ReturnRollAsync(Guid id, ReviewRollSubmissionRequest request, CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(request.Remarks)
            ? Task.FromResult(Fail("VALIDATION_FAILED", "Say why the roll is returned, so the municipality can correct it."))
            : ReviewAsync(id, AssessmentRollSubmissionStatus.Returned, request.Remarks, cancellationToken);

    private async Task<Result<RollSubmissionDto>> ReviewAsync(Guid id, AssessmentRollSubmissionStatus outcome, string? remarks, CancellationToken ct)
    {
        if (remarks?.Length > 1000)
        {
            return Fail("VALIDATION_FAILED", "Remarks at most 1000 characters.");
        }
        var submission = await db.AssessmentRollSubmissions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (submission is null)
        {
            return RollNotFound();
        }
        var scope = await officeContext.GetAsync(ct);
        if (!(scope.ProvinceWide || scope.OfficeKind == OfficeKind.Provincial))
        {
            return Fail("ROLL_REVIEW_FORBIDDEN", "Only the provincial office acknowledges or returns a monthly assessment roll.");
        }
        if (submission.Status != AssessmentRollSubmissionStatus.Submitted)
        {
            return Fail("ROLL_SUBMISSION_NOT_PENDING", $"This roll was already {submission.Status.ToString().ToLowerInvariant()}.");
        }
        submission.Status = outcome;
        submission.ReviewedAt = clock.UtcNow;
        submission.ReviewedBy = currentUser.AppUserId;
        submission.ReviewRemarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
        currentUser.Reason = submission.ReviewRemarks;
        await db.SaveChangesAsync(ct);
        return await GetRollAsync(id, ct);
    }

    private Task<bool> OpenSubmissionExistsAsync(Guid municipalityId, int year, int month, CancellationToken ct) =>
        db.AssessmentRollSubmissions.IgnoreQueryFilters().AnyAsync(x => x.MunicipalityId == municipalityId && x.Year == year && x.Month == month
            && x.Status != AssessmentRollSubmissionStatus.Returned, ct);

    private async Task<IReadOnlyList<RollSubmissionDto>> MapAsync(IQueryable<AssessmentRollSubmission> query, CancellationToken ct)
    {
        var rows = await query.AsNoTracking().Include(x => x.Municipality).Include(x => x.Office)
            .Include(x => x.Items).ThenInclude(i => i.Barangay).ToListAsync(ct);
        var userIds = rows.SelectMany(r => new[] { r.CreatedBy, r.ReviewedBy }).OfType<Guid>().Distinct().ToList();
        var users = await db.AppUsers.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        string? Name(Guid? id) => id is { } u ? users.GetValueOrDefault(u) : null;
        return rows.Select(x => new RollSubmissionDto(
            x.Id, x.MunicipalityId, x.Municipality!.Name, x.OfficeId, x.Office!.Code, x.Year, x.Month, x.Status, x.Remarks,
            x.CreatedAt, Name(x.CreatedBy), x.ReviewedAt, Name(x.ReviewedBy), x.ReviewRemarks,
            x.Items.OrderBy(i => i.Barangay!.Name).ThenBy(i => i.Kind)
                .Select(i => new RollSubmissionItemDto(i.Id, i.Kind, i.BarangayId, i.Barangay!.Name, i.EntryCount, i.RegisterRunId, i.IssuedFormId)).ToList()))
            .ToList();
    }

    private static Result<RollSubmissionDto> Fail(string code, string message) => Result.Failure<RollSubmissionDto>(code, message);

    private static Result<RollSubmissionDto> RollNotFound() =>
        Result.Failure<RollSubmissionDto>("ROLL_SUBMISSION_NOT_FOUND", "No monthly assessment roll submission was found with the given id.");
}
