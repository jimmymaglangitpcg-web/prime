using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Smv;

public sealed class SmvService(
    IApplicationDbContext db,
    IValidator<CreateSmvRequest> createSmvValidator,
    IValidator<CreateSmvScheduleRequest> createScheduleValidator,
    ICurrentUserService currentUser) : ISmvService
{
    public async Task<Result<SmvDto>> CreateSmvAsync(CreateSmvRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await createSmvValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<SmvDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var ordinanceNumber = string.IsNullOrWhiteSpace(request.OrdinanceNumber) ? null : request.OrdinanceNumber.Trim();
        var certification = string.IsNullOrWhiteSpace(request.CertificationReference) ? null : request.CertificationReference.Trim();
        if (ordinanceNumber is not null && await db.Smvs.AnyAsync(x => x.OrdinanceNumber == ordinanceNumber, cancellationToken))
        {
            return Result.Failure<SmvDto>("SMV_ORDINANCE_NUMBER_DUPLICATE", $"An SMV with ordinance number '{ordinanceNumber}' already exists.");
        }
        if (certification is not null && await db.Smvs.AnyAsync(x => x.CertificationReference == certification, cancellationToken))
        {
            return Result.Failure<SmvDto>("SMV_CERTIFICATION_DUPLICATE", $"An SMV with certification '{certification}' already exists.");
        }
        var municipalityIds = request.MunicipalityIds ?? [];
        if (municipalityIds.Count > 0
            && await db.Municipalities.CountAsync(x => municipalityIds.Contains(x.Id), cancellationToken) != municipalityIds.Count)
        {
            return Result.Failure<SmvDto>("MUNICIPALITY_NOT_FOUND", "A municipality in the coverage of the SMV does not exist.");
        }
        var revisionYear = request.RevisionYear;
        if (request.AmendsSmvId is { } amendedId)
        {
            // An amendment amends an approved SMV that is not itself an amendment, from a later date, within its coverage; it
            // carries the amended SMV's revision year (LAM 2025 Book IV Ch. III §4; smv-preparation-general-revision.md §4.7).
            var amended = await db.Smvs.AsNoTracking().Include(x => x.Coverage).FirstOrDefaultAsync(x => x.Id == amendedId, cancellationToken);
            if (amended is null)
            {
                return Result.Failure<SmvDto>("SMV_NOT_FOUND", "The SMV to amend does not exist.");
            }
            if (amended.Basis == SmvBasis.Amendment)
            {
                return Result.Failure<SmvDto>("SMV_AMENDMENT_OF_AMENDMENT", "Amend the SMV itself, not one of its amendments; a later amendment takes precedence over an earlier one.");
            }
            if (amended.Status != WorkflowStatus.Approved)
            {
                return Result.Failure<SmvDto>("SMV_NOT_APPROVED", "Only an approved SMV can be amended.");
            }
            if (request.EffectivityDate <= amended.EffectivityDate)
            {
                return Result.Failure<SmvDto>("SMV_AMENDMENT_EFFECTIVITY", "An amendment takes effect after the SMV it amends.");
            }
            var amendedCoverage = amended.Coverage.Select(c => c.MunicipalityId).ToList();
            if (amendedCoverage.Count > 0 && (municipalityIds.Count == 0 || municipalityIds.Any(m => !amendedCoverage.Contains(m))))
            {
                return Result.Failure<SmvDto>("SMV_AMENDMENT_COVERAGE", "An amendment covers only municipalities the amended SMV covers.");
            }
            revisionYear = amended.RevisionYear;
        }

        var smv = new Domain.Entities.Smv
        {
            Basis = request.Basis,
            OrdinanceNumber = ordinanceNumber,
            OrdinanceDate = request.OrdinanceDate,
            ApprovalDate = request.ApprovalDate,
            ProposedOn = request.ProposedOn,
            PublishedForCommentOn = request.PublishedForCommentOn,
            ConsultationsHeldOn = request.ConsultationsHeldOn,
            SubmittedToBlgfOn = request.SubmittedToBlgfOn,
            CertifiedOn = request.CertifiedOn,
            CertificationReference = certification,
            PublishedOn = request.PublishedOn,
            PublicationReference = string.IsNullOrWhiteSpace(request.PublicationReference) ? null : request.PublicationReference.Trim(),
            EffectivityDate = request.EffectivityDate,
            RevisionYear = revisionYear,
            Description = request.Description,
            AmendsSmvId = request.AmendsSmvId,
            AmendmentGround = request.AmendmentGround,
            Status = WorkflowStatus.Draft,
            Coverage = municipalityIds.Select(id => new Domain.Entities.SmvCoverage { MunicipalityId = id }).ToList(),
        };

        db.Smvs.Add(smv);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(await MapSmvAsync(smv.Id, cancellationToken));
    }

    public async Task<Result<SmvDto>> ApproveSmvAsync(Guid smvId, CancellationToken cancellationToken = default)
    {
        var smv = await db.Smvs.FirstOrDefaultAsync(x => x.Id == smvId, cancellationToken);
        if (smv is null)
        {
            return Result.Failure<SmvDto>("SMV_NOT_FOUND", "No SMV was found with the given id.");
        }
        if (smv.Status is WorkflowStatus.Approved or WorkflowStatus.Posted)
        {
            return Result.Failure<SmvDto>("SMV_ALREADY_APPROVED", "This SMV has already been approved.");
        }
        // Maker-checker (CLAUDE.md §46): the creator may not approve their own SMV.
        if (currentUser.AppUserId is not null && smv.CreatedBy == currentUser.AppUserId)
        {
            return Result.Failure<SmvDto>("CANNOT_APPROVE_OWN_SMV", "The SMV's creator cannot also approve it.");
        }
        // A certified SMV is entered with its certification; one prepared in PRIME once it is published, so its effectivity is
        // known (docs/analysis/smv-preparation-general-revision.md §4.2).
        if (smv.Basis is SmvBasis.Certified or SmvBasis.Amendment && string.IsNullOrWhiteSpace(smv.CertificationReference))
        {
            return Result.Failure<SmvDto>("SMV_CERTIFICATION_REQUIRED", "A certified SMV or an amendment is approved with its certification's reference.");
        }
        if (smv.AmendsSmvId is { } amendedId && !await db.Smvs.AnyAsync(x => x.Id == amendedId && x.Status == WorkflowStatus.Approved, cancellationToken))
        {
            return Result.Failure<SmvDto>("SMV_NOT_APPROVED", "The amended SMV is no longer approved.");
        }
        if (await db.SmvPreparations.AnyAsync(x => x.ProposedSmvId == smvId && x.Status != Domain.Entities.SmvPreparationStatus.Published, cancellationToken))
        {
            return Result.Failure<SmvDto>("SMV_NOT_PUBLISHED", "The SMV's preparation has not recorded the publication of the certified SMV.");
        }

        smv.Status = WorkflowStatus.Approved;
        smv.ApprovedBy = currentUser.AppUserId;
        smv.ApprovedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(await MapSmvAsync(smv.Id, cancellationToken));
    }

    public async Task<Result<SmvDto>> GetByIdAsync(Guid smvId, CancellationToken cancellationToken = default)
    {
        return await db.Smvs.AnyAsync(x => x.Id == smvId, cancellationToken)
            ? Result.Success(await MapSmvAsync(smvId, cancellationToken))
            : Result.Failure<SmvDto>("SMV_NOT_FOUND", "No SMV was found with the given id.");
    }

    private async Task<SmvDto> MapSmvAsync(Guid id, CancellationToken ct) =>
        ToDto(await db.Smvs.AsNoTracking().Include(x => x.AmendsSmv).Include(x => x.Coverage).ThenInclude(c => c.Municipality).SingleAsync(x => x.Id == id, ct));

    public async Task<Result<PagedResult<SmvDto>>> ListAsync(PagedRequest request, CancellationToken cancellationToken = default)
    {
        var query = db.Smvs.AsNoTracking().Include(x => x.AmendsSmv).Include(x => x.Coverage).ThenInclude(c => c.Municipality);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => x.EffectivityDate).ThenByDescending(x => x.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(cancellationToken);
        return Result.Success(new PagedResult<SmvDto> { Items = rows.Select(ToDto).ToList(), TotalCount = total, Page = request.Page, PageSize = request.PageSize });
    }

    public async Task<Result<SmvScheduleDto>> CreateScheduleAsync(Guid smvId, CreateSmvScheduleRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await createScheduleValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<SmvScheduleDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var smvCoverage = await db.Smvs.Where(x => x.Id == smvId).Select(x => new { Municipalities = x.Coverage.Select(c => c.MunicipalityId).ToList() })
            .FirstOrDefaultAsync(cancellationToken);
        if (smvCoverage is null)
        {
            return Result.Failure<SmvScheduleDto>("SMV_NOT_FOUND", "No SMV was found with the given id.");
        }
        if (!await db.Classifications.AnyAsync(x => x.Id == request.ClassificationId, cancellationToken))
        {
            return Result.Failure<SmvScheduleDto>("CLASSIFICATION_NOT_FOUND", "The specified classification does not exist.");
        }
        if (request.ActualUseId is { } actualUseId && !await db.ActualUses.AnyAsync(x => x.Id == actualUseId, cancellationToken))
        {
            return Result.Failure<SmvScheduleDto>("ACTUAL_USE_NOT_FOUND", "The specified actual use does not exist.");
        }
        if (!await db.PropertyTypes.AnyAsync(x => x.Id == request.PropertyTypeId, cancellationToken))
        {
            return Result.Failure<SmvScheduleDto>("PROPERTY_TYPE_NOT_FOUND", "The specified property type does not exist.");
        }
        if (request.ZoneId is not null && !await db.Zones.AnyAsync(x => x.Id == request.ZoneId, cancellationToken))
        {
            return Result.Failure<SmvScheduleDto>("ZONE_NOT_FOUND", "The specified zone does not exist.");
        }
        if (request.ImprovementKindId is not null && !await db.ImprovementKinds.AnyAsync(x => x.Id == request.ImprovementKindId, cancellationToken))
        {
            return Result.Failure<SmvScheduleDto>("IMPROVEMENT_KIND_NOT_FOUND", "The specified improvement kind does not exist.");
        }

        if (request.SubClassificationId is { } subId && !await db.SubClassifications.AnyAsync(x => x.Id == subId, cancellationToken))
        {
            return Result.Failure<SmvScheduleDto>("SUB_CLASSIFICATION_NOT_FOUND", "The specified sub-classification does not exist.");
        }
        if (request.BarangayId is { } barangayId)
        {
            var municipalityId = await db.Barangays.Where(x => x.Id == barangayId).Select(x => (Guid?)x.MunicipalityId).FirstOrDefaultAsync(cancellationToken);
            if (municipalityId is null)
            {
                return Result.Failure<SmvScheduleDto>("BARANGAY_NOT_FOUND", "The specified barangay does not exist.");
            }
            if (smvCoverage.Municipalities.Count > 0 && !smvCoverage.Municipalities.Contains(municipalityId.Value))
            {
                return Result.Failure<SmvScheduleDto>("BARANGAY_OUTSIDE_SMV_COVERAGE", "The barangay is in a municipality the SMV does not cover.");
            }
        }

        // Never overwrite (CLAUDE.md §28): close the currently-open rate of this SMV for the
        // same key instead of editing it in place. Rates of other SMVs are not touched: the
        // engine chooses between SMVs by their effectivity and coverage (valuation-foundation.md §4.3).
        var currentlyOpen = await db.SmvSchedules.FirstOrDefaultAsync(
            x => x.SmvId == smvId
                && x.ClassificationId == request.ClassificationId
                && x.ActualUseId == request.ActualUseId
                && x.PropertyTypeId == request.PropertyTypeId
                && x.SubClassificationId == request.SubClassificationId
                && x.ZoneId == request.ZoneId
                && x.BarangayId == request.BarangayId
                && x.ImprovementKindId == request.ImprovementKindId
                && x.EndDate == null,
            cancellationToken);

        if (currentlyOpen is not null)
        {
            if (request.EffectiveDate <= currentlyOpen.EffectiveDate)
            {
                return Result.Failure<SmvScheduleDto>(
                    "SCHEDULE_EFFECTIVE_DATE_CONFLICT",
                    "The new schedule's effective date must be after the currently-open schedule's effective date.");
            }
            currentlyOpen.EndDate = request.EffectiveDate.AddDays(-1);
        }

        var schedule = new Domain.Entities.SmvSchedule
        {
            SmvId = smvId,
            ClassificationId = request.ClassificationId,
            ActualUseId = request.ActualUseId,
            PropertyTypeId = request.PropertyTypeId,
            ZoneId = request.ZoneId,
            ImprovementKindId = request.ImprovementKindId,
            SubClassificationId = request.SubClassificationId,
            BarangayId = request.BarangayId,
            LocationDescription = string.IsNullOrWhiteSpace(request.LocationDescription) ? null : request.LocationDescription.Trim(),
            CropDescription = string.IsNullOrWhiteSpace(request.CropDescription) ? null : request.CropDescription.Trim(),
            Unit = request.Unit,
            MarketValue = request.MarketValue,
            MinimumValue = request.MinimumValue,
            MaximumValue = request.MaximumValue,
            EffectiveDate = request.EffectiveDate,
            Status = WorkflowStatus.Draft,
        };

        db.SmvSchedules.Add(schedule);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(await MapScheduleToDto(schedule.Id, cancellationToken)
            ?? throw new InvalidOperationException("SMV schedule was just created but could not be reloaded."));
    }

    public async Task<Result<SmvScheduleDto>> ApproveScheduleAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        var schedule = await db.SmvSchedules.FirstOrDefaultAsync(x => x.Id == scheduleId, cancellationToken);
        if (schedule is null)
        {
            return Result.Failure<SmvScheduleDto>("SMV_SCHEDULE_NOT_FOUND", "No SMV schedule was found with the given id.");
        }
        if (schedule.Status is WorkflowStatus.Approved or WorkflowStatus.Posted)
        {
            return Result.Failure<SmvScheduleDto>("SMV_SCHEDULE_ALREADY_APPROVED", "This SMV schedule has already been approved.");
        }
        // Maker-checker (CLAUDE.md §46): the creator may not approve their own schedule.
        if (currentUser.AppUserId is not null && schedule.CreatedBy == currentUser.AppUserId)
        {
            return Result.Failure<SmvScheduleDto>("CANNOT_APPROVE_OWN_SCHEDULE", "The schedule's creator cannot also approve it.");
        }

        schedule.Status = WorkflowStatus.Approved;
        schedule.ApprovedBy = currentUser.AppUserId;
        schedule.ApprovedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(await MapScheduleToDto(schedule.Id, cancellationToken)
            ?? throw new InvalidOperationException("SMV schedule was just approved but could not be reloaded."));
    }

    public async Task<Result<IReadOnlyList<SmvScheduleDto>>> ListSchedulesAsync(Guid smvId, CancellationToken cancellationToken = default)
    {
        var schedules = await IncludeReferences(db.SmvSchedules)
            .Where(x => x.SmvId == smvId)
            .OrderByDescending(x => x.EffectiveDate)
            .ToListAsync(cancellationToken);

        var dtos = schedules.Select(ProjectScheduleToDto).ToList();
        // An amendment's rows show the amended SMV's value each replaces: its open, approved row with the same key (§4.7).
        var amendedId = await db.Smvs.Where(x => x.Id == smvId).Select(x => x.AmendsSmvId).FirstOrDefaultAsync(cancellationToken);
        if (amendedId is not null)
        {
            var amended = (await db.SmvSchedules.AsNoTracking()
                    .Where(x => x.SmvId == amendedId && x.Status == WorkflowStatus.Approved && x.EndDate == null).ToListAsync(cancellationToken))
                .GroupBy(Domain.DomainServices.SmvRateSelector.RowKey)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.EffectiveDate).First().MarketValue);
            dtos = schedules.Zip(dtos, (e, d) => d with
            {
                ReplacesMarketValue = amended.TryGetValue(Domain.DomainServices.SmvRateSelector.RowKey(e), out var replaced) ? replaced : null,
            }).ToList();
        }
        return Result.Success<IReadOnlyList<SmvScheduleDto>>(dtos);
    }

    private async Task<SmvScheduleDto?> MapScheduleToDto(Guid id, CancellationToken cancellationToken)
    {
        var entity = await IncludeReferences(db.SmvSchedules).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : ProjectScheduleToDto(entity);
    }

    private static IQueryable<Domain.Entities.SmvSchedule> IncludeReferences(IQueryable<Domain.Entities.SmvSchedule> query) => query
        .Include(x => x.Classification)
        .Include(x => x.ActualUse)
        .Include(x => x.PropertyType)
        .Include(x => x.Zone)
        .Include(x => x.ImprovementKind)
        .Include(x => x.SubClassification)
        .Include(x => x.Barangay);

    private static SmvDto ToDto(Domain.Entities.Smv smv) => new(
        smv.Id,
        smv.OrdinanceNumber,
        smv.OrdinanceDate,
        smv.ApprovalDate,
        smv.EffectivityDate,
        smv.RevisionYear,
        smv.Status,
        smv.Description,
        smv.CreatedAt,
        smv.Basis,
        smv.Reference,
        smv.ProposedOn,
        smv.PublishedForCommentOn,
        smv.ConsultationsHeldOn,
        smv.SubmittedToBlgfOn,
        smv.CertifiedOn,
        smv.CertificationReference,
        smv.PublishedOn,
        smv.PublicationReference,
        smv.Coverage.Select(c => new SmvCoverageDto(c.MunicipalityId, c.Municipality?.Name ?? "")).OrderBy(c => c.MunicipalityName).ToList(),
        smv.AmendsSmvId,
        smv.AmendsSmv?.Reference,
        smv.AmendmentGround);

    private static SmvScheduleDto ProjectScheduleToDto(Domain.Entities.SmvSchedule x) => new(
        x.Id,
        x.SmvId,
        x.ClassificationId,
        x.Classification!.Name,
        x.ActualUseId,
        x.ActualUse?.Name,
        x.PropertyTypeId,
        x.PropertyType!.Name,
        x.ZoneId,
        x.Zone?.Name,
        x.ImprovementKindId,
        x.ImprovementKind?.Name,
        x.Unit,
        x.MarketValue,
        x.MinimumValue,
        x.MaximumValue,
        x.EffectiveDate,
        x.EndDate,
        x.Status,
        x.CreatedAt,
        x.SubClassificationId,
        x.SubClassification?.Name,
        x.BarangayId,
        x.Barangay?.Name,
        x.LocationDescription,
        x.CropDescription);
}
