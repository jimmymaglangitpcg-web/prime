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

        if (await db.Smvs.AnyAsync(x => x.OrdinanceNumber == request.OrdinanceNumber, cancellationToken))
        {
            return Result.Failure<SmvDto>("SMV_ORDINANCE_NUMBER_DUPLICATE", $"An SMV with ordinance number '{request.OrdinanceNumber}' already exists.");
        }

        var smv = new Domain.Entities.Smv
        {
            OrdinanceNumber = request.OrdinanceNumber,
            OrdinanceDate = request.OrdinanceDate,
            ApprovalDate = request.ApprovalDate,
            EffectivityDate = request.EffectivityDate,
            RevisionYear = request.RevisionYear,
            Description = request.Description,
            Status = WorkflowStatus.Draft,
        };

        db.Smvs.Add(smv);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(smv));
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

        smv.Status = WorkflowStatus.Approved;
        smv.ApprovedBy = currentUser.AppUserId;
        smv.ApprovedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(smv));
    }

    public async Task<Result<SmvDto>> GetByIdAsync(Guid smvId, CancellationToken cancellationToken = default)
    {
        var smv = await db.Smvs.FirstOrDefaultAsync(x => x.Id == smvId, cancellationToken);
        return smv is null
            ? Result.Failure<SmvDto>("SMV_NOT_FOUND", "No SMV was found with the given id.")
            : Result.Success(ToDto(smv));
    }

    public async Task<Result<SmvScheduleDto>> CreateScheduleAsync(Guid smvId, CreateSmvScheduleRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await createScheduleValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<SmvScheduleDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        if (!await db.Smvs.AnyAsync(x => x.Id == smvId, cancellationToken))
        {
            return Result.Failure<SmvScheduleDto>("SMV_NOT_FOUND", "No SMV was found with the given id.");
        }
        if (!await db.Classifications.AnyAsync(x => x.Id == request.ClassificationId, cancellationToken))
        {
            return Result.Failure<SmvScheduleDto>("CLASSIFICATION_NOT_FOUND", "The specified classification does not exist.");
        }
        if (!await db.ActualUses.AnyAsync(x => x.Id == request.ActualUseId, cancellationToken))
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

        // Never overwrite (CLAUDE.md §28): close any currently-open schedule for
        // the same classification/actual use/property type/zone key instead of
        // editing it in place.
        var currentlyOpen = await db.SmvSchedules.FirstOrDefaultAsync(
            x => x.ClassificationId == request.ClassificationId
                && x.ActualUseId == request.ActualUseId
                && x.PropertyTypeId == request.PropertyTypeId
                && x.ZoneId == request.ZoneId
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

        return Result.Success<IReadOnlyList<SmvScheduleDto>>(schedules.Select(ProjectScheduleToDto).ToList());
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
        .Include(x => x.Zone);

    private static SmvDto ToDto(Domain.Entities.Smv smv) => new(
        smv.Id,
        smv.OrdinanceNumber,
        smv.OrdinanceDate,
        smv.ApprovalDate,
        smv.EffectivityDate,
        smv.RevisionYear,
        smv.Status,
        smv.Description,
        smv.CreatedAt);

    private static SmvScheduleDto ProjectScheduleToDto(Domain.Entities.SmvSchedule x) => new(
        x.Id,
        x.SmvId,
        x.ClassificationId,
        x.Classification!.Name,
        x.ActualUseId,
        x.ActualUse!.Name,
        x.PropertyTypeId,
        x.PropertyType!.Name,
        x.ZoneId,
        x.Zone?.Name,
        x.Unit,
        x.MarketValue,
        x.MinimumValue,
        x.MaximumValue,
        x.EffectiveDate,
        x.EndDate,
        x.Status,
        x.CreatedAt);
}
