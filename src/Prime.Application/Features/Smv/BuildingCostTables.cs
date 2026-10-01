using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.DomainServices;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Smv;

/// <summary>A base unit construction cost of the SMV (docs/analysis/valuation-foundation.md §4.5). Null kind or classification: every one.</summary>
public sealed record CreateBuildingCostRequest(
    Guid SmvId, Guid StructuralTypeId, Guid? BuildingTypeId, Guid? ClassificationId, decimal CostPerSquareMetre,
    string LegalBasis, DateOnly EffectiveDate, string? Remarks);

public sealed record BuildingCostDto(
    Guid Id, Guid SmvId, string SmvReference, Guid StructuralTypeId, string StructuralTypeName, Guid? BuildingTypeId, string? BuildingTypeName,
    Guid? ClassificationId, string? ClassificationName, decimal CostPerSquareMetre,
    string LegalBasis, DateOnly EffectiveDate, DateOnly? EndDate, WorkflowStatus Status, Guid? CreatedBy, Guid? ApprovedBy, DateTimeOffset? ApprovedAt, string? Remarks);

/// <summary>An extra item's cost per unit under the SMV.</summary>
public sealed record CreateExtraItemCostRequest(
    Guid SmvId, Guid ComponentTypeId, string Unit, decimal UnitCost, string LegalBasis, DateOnly EffectiveDate, string? Remarks);

public sealed record ExtraItemCostDto(
    Guid Id, Guid SmvId, string SmvReference, Guid ComponentTypeId, string ComponentTypeName, string Unit, decimal UnitCost,
    string LegalBasis, DateOnly EffectiveDate, DateOnly? EndDate, WorkflowStatus Status, Guid? CreatedBy, Guid? ApprovedBy, DateTimeOffset? ApprovedAt, string? Remarks);

/// <summary>One age band: from age (inclusive) to age (inclusive; null = no limit).</summary>
public sealed record DepreciationRowRequest(int FromAge, int? ToAge, decimal Percent);

public sealed record CreateDepreciationScheduleRequest(
    Guid SmvId, Guid StructuralTypeId, DepreciationReading Reading, decimal MinimumRemainingPercent,
    IReadOnlyList<DepreciationRowRequest> Rows, string LegalBasis, DateOnly EffectiveDate, string? Remarks);

public sealed record DepreciationRowDto(int Sequence, int FromAge, int? ToAge, decimal Percent);

public sealed record DepreciationScheduleDto(
    Guid Id, Guid SmvId, string SmvReference, Guid StructuralTypeId, string StructuralTypeName, DepreciationReading Reading,
    decimal MinimumRemainingPercent, IReadOnlyList<DepreciationRowDto> Rows,
    string LegalBasis, DateOnly EffectiveDate, DateOnly? EndDate, WorkflowStatus Status, Guid? CreatedBy, Guid? ApprovedBy, DateTimeOffset? ApprovedAt, string? Remarks);

public interface IBuildingCostTableService
{
    Task<Result<BuildingCostDto>> CreateBuildingCostAsync(CreateBuildingCostRequest request, CancellationToken ct = default);
    Task<Result<BuildingCostDto>> ApproveBuildingCostAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<BuildingCostDto>>> ListBuildingCostsAsync(Guid? smvId, CancellationToken ct = default);

    Task<Result<ExtraItemCostDto>> CreateExtraItemCostAsync(CreateExtraItemCostRequest request, CancellationToken ct = default);
    Task<Result<ExtraItemCostDto>> ApproveExtraItemCostAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ExtraItemCostDto>>> ListExtraItemCostsAsync(Guid? smvId, CancellationToken ct = default);

    Task<Result<DepreciationScheduleDto>> CreateDepreciationScheduleAsync(CreateDepreciationScheduleRequest request, CancellationToken ct = default);
    Task<Result<DepreciationScheduleDto>> ApproveDepreciationScheduleAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<DepreciationScheduleDto>>> ListDepreciationSchedulesAsync(Guid? smvId, CancellationToken ct = default);
}

/// <summary>
/// The SMV's building tables — construction costs, extra-item costs, depreciation tables
/// (docs/analysis/valuation-foundation.md §4.5). A Draft is approved by a second user; approving a
/// new version of the same scope ends the previous one. Every cost and percent is LGU data.
/// </summary>
public sealed class BuildingCostTableService(IApplicationDbContext db, ICurrentUserService currentUser) : IBuildingCostTableService
{
    public async Task<Result<BuildingCostDto>> CreateBuildingCostAsync(CreateBuildingCostRequest request, CancellationToken ct = default)
    {
        if (CommonProblem(request.LegalBasis, request.EffectiveDate) is not null || request.CostPerSquareMetre <= 0m)
        {
            return Result.Failure<BuildingCostDto>("VALIDATION_FAILED", "costPerSquareMetre must be above 0; legalBasis (max 500) and effectiveDate are required.");
        }
        if (await ReferenceProblemAsync(request.SmvId, request.StructuralTypeId, ct) is { } missing)
        {
            return Result.Failure<BuildingCostDto>(missing.Code!, missing.Message!);
        }
        if (request.BuildingTypeId is { } kind && !await db.BuildingTypes.AnyAsync(x => x.Id == kind, ct))
        {
            return Result.Failure<BuildingCostDto>("BUILDING_TYPE_NOT_FOUND", "The specified building kind does not exist.");
        }
        if (request.ClassificationId is { } c && !await db.Classifications.AnyAsync(x => x.Id == c, ct))
        {
            return Result.Failure<BuildingCostDto>("CLASSIFICATION_NOT_FOUND", "The specified classification does not exist.");
        }
        var row = new SmvBuildingCost
        {
            SmvId = request.SmvId, StructuralTypeId = request.StructuralTypeId, BuildingTypeId = request.BuildingTypeId,
            ClassificationId = request.ClassificationId, CostPerSquareMetre = request.CostPerSquareMetre,
            LegalBasis = request.LegalBasis.Trim(), EffectiveDate = request.EffectiveDate, Remarks = request.Remarks,
        };
        db.SmvBuildingCosts.Add(row);
        await db.SaveChangesAsync(ct);
        return Result.Success(await BuildingCostAsync(row.Id, ct));
    }

    public async Task<Result<BuildingCostDto>> ApproveBuildingCostAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.SmvBuildingCosts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null)
        {
            return Result.Failure<BuildingCostDto>("BUILDING_COST_NOT_FOUND", "No construction cost was found with the given id.");
        }
        var scope = db.SmvBuildingCosts.Where(x => x.SmvId == row.SmvId && x.StructuralTypeId == row.StructuralTypeId
            && x.BuildingTypeId == row.BuildingTypeId && x.ClassificationId == row.ClassificationId);
        if (await ConfigurationApproval.ApproveAsync(db, currentUser, scope, row, "BUILDING_COST", ct) is { } failure)
        {
            return Result.Failure<BuildingCostDto>(failure.Code!, failure.Message!);
        }
        return Result.Success(await BuildingCostAsync(id, ct));
    }

    public async Task<Result<IReadOnlyList<BuildingCostDto>>> ListBuildingCostsAsync(Guid? smvId, CancellationToken ct = default)
    {
        var rows = await BuildingCosts().Where(x => smvId == null || x.SmvId == smvId)
            .OrderBy(x => x.StructuralType!.Name).ThenBy(x => x.BuildingType!.Name).ThenByDescending(x => x.EffectiveDate).ToListAsync(ct);
        return Result.Success<IReadOnlyList<BuildingCostDto>>(rows.Select(ToDto).ToList());
    }

    public async Task<Result<ExtraItemCostDto>> CreateExtraItemCostAsync(CreateExtraItemCostRequest request, CancellationToken ct = default)
    {
        var unit = request.Unit?.Trim() ?? "";
        if (CommonProblem(request.LegalBasis, request.EffectiveDate) is not null || request.UnitCost <= 0m || unit.Length is 0 or > 30)
        {
            return Result.Failure<ExtraItemCostDto>("VALIDATION_FAILED", "unit (max 30) and a unitCost above 0 are required; legalBasis (max 500) and effectiveDate too.");
        }
        if (!await db.Smvs.AnyAsync(x => x.Id == request.SmvId, ct))
        {
            return Result.Failure<ExtraItemCostDto>("SMV_NOT_FOUND", "No SMV was found with the given id.");
        }
        if (!await db.BuildingComponentTypes.AnyAsync(x => x.Id == request.ComponentTypeId, ct))
        {
            return Result.Failure<ExtraItemCostDto>("COMPONENT_TYPE_NOT_FOUND", "The specified component type does not exist.");
        }
        var row = new SmvExtraItemCost
        {
            SmvId = request.SmvId, ComponentTypeId = request.ComponentTypeId, Unit = unit, UnitCost = request.UnitCost,
            LegalBasis = request.LegalBasis.Trim(), EffectiveDate = request.EffectiveDate, Remarks = request.Remarks,
        };
        db.SmvExtraItemCosts.Add(row);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ExtraItemCostAsync(row.Id, ct));
    }

    public async Task<Result<ExtraItemCostDto>> ApproveExtraItemCostAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.SmvExtraItemCosts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null)
        {
            return Result.Failure<ExtraItemCostDto>("EXTRA_ITEM_COST_NOT_FOUND", "No extra-item cost was found with the given id.");
        }
        var scope = db.SmvExtraItemCosts.Where(x => x.SmvId == row.SmvId && x.ComponentTypeId == row.ComponentTypeId);
        if (await ConfigurationApproval.ApproveAsync(db, currentUser, scope, row, "EXTRA_ITEM_COST", ct) is { } failure)
        {
            return Result.Failure<ExtraItemCostDto>(failure.Code!, failure.Message!);
        }
        return Result.Success(await ExtraItemCostAsync(id, ct));
    }

    public async Task<Result<IReadOnlyList<ExtraItemCostDto>>> ListExtraItemCostsAsync(Guid? smvId, CancellationToken ct = default)
    {
        var rows = await ExtraItemCosts().Where(x => smvId == null || x.SmvId == smvId)
            .OrderBy(x => x.ComponentType!.Name).ThenByDescending(x => x.EffectiveDate).ToListAsync(ct);
        return Result.Success<IReadOnlyList<ExtraItemCostDto>>(rows.Select(ToDto).ToList());
    }

    public async Task<Result<DepreciationScheduleDto>> CreateDepreciationScheduleAsync(CreateDepreciationScheduleRequest request, CancellationToken ct = default)
    {
        if (CommonProblem(request.LegalBasis, request.EffectiveDate) is not null || !Enum.IsDefined(request.Reading)
            || request.MinimumRemainingPercent is < 0m or > 100m)
        {
            return Result.Failure<DepreciationScheduleDto>("VALIDATION_FAILED",
                "reading must be Cumulative or YearlyWithinBand and minimumRemainingPercent from 0 to 100; legalBasis (max 500) and effectiveDate are required.");
        }
        var rows = request.Rows ?? [];
        if (BuildingDepreciation.BandsProblem(rows.Select(r => (r.FromAge, r.ToAge, r.Percent)).ToList()) is { } bands)
        {
            return Result.Failure<DepreciationScheduleDto>("VALIDATION_FAILED", bands);
        }
        if (await ReferenceProblemAsync(request.SmvId, request.StructuralTypeId, ct) is { } missing)
        {
            return Result.Failure<DepreciationScheduleDto>(missing.Code!, missing.Message!);
        }
        var schedule = new SmvDepreciationSchedule
        {
            SmvId = request.SmvId, StructuralTypeId = request.StructuralTypeId, Reading = request.Reading,
            MinimumRemainingPercent = request.MinimumRemainingPercent,
            Rows = rows.OrderBy(r => r.FromAge).Select((r, i) => new SmvDepreciationRow { Sequence = i + 1, FromAge = r.FromAge, ToAge = r.ToAge, Percent = r.Percent }).ToList(),
            LegalBasis = request.LegalBasis.Trim(), EffectiveDate = request.EffectiveDate, Remarks = request.Remarks,
        };
        db.SmvDepreciationSchedules.Add(schedule);
        await db.SaveChangesAsync(ct);
        return Result.Success(await DepreciationScheduleAsync(schedule.Id, ct));
    }

    public async Task<Result<DepreciationScheduleDto>> ApproveDepreciationScheduleAsync(Guid id, CancellationToken ct = default)
    {
        var schedule = await db.SmvDepreciationSchedules.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (schedule is null)
        {
            return Result.Failure<DepreciationScheduleDto>("DEPRECIATION_SCHEDULE_NOT_FOUND", "No depreciation table was found with the given id.");
        }
        var scope = db.SmvDepreciationSchedules.Where(x => x.SmvId == schedule.SmvId && x.StructuralTypeId == schedule.StructuralTypeId);
        if (await ConfigurationApproval.ApproveAsync(db, currentUser, scope, schedule, "DEPRECIATION_SCHEDULE", ct) is { } failure)
        {
            return Result.Failure<DepreciationScheduleDto>(failure.Code!, failure.Message!);
        }
        return Result.Success(await DepreciationScheduleAsync(id, ct));
    }

    public async Task<Result<IReadOnlyList<DepreciationScheduleDto>>> ListDepreciationSchedulesAsync(Guid? smvId, CancellationToken ct = default)
    {
        var rows = await DepreciationSchedules().Where(x => smvId == null || x.SmvId == smvId)
            .OrderBy(x => x.StructuralType!.Name).ThenByDescending(x => x.EffectiveDate).ToListAsync(ct);
        return Result.Success<IReadOnlyList<DepreciationScheduleDto>>(rows.Select(ToDto).ToList());
    }

    private static string? CommonProblem(string? legalBasis, DateOnly effectiveDate) =>
        string.IsNullOrWhiteSpace(legalBasis) || legalBasis.Length > 500 || effectiveDate == default ? "invalid" : null;

    private async Task<Result?> ReferenceProblemAsync(Guid smvId, Guid structuralTypeId, CancellationToken ct)
    {
        if (!await db.Smvs.AnyAsync(x => x.Id == smvId, ct))
        {
            return Result.Failure("SMV_NOT_FOUND", "No SMV was found with the given id.");
        }
        if (!await db.StructuralTypes.AnyAsync(x => x.Id == structuralTypeId, ct))
        {
            return Result.Failure("STRUCTURAL_TYPE_NOT_FOUND", "The specified structural type does not exist.");
        }
        return null;
    }

    private IQueryable<SmvBuildingCost> BuildingCosts() => db.SmvBuildingCosts.AsNoTracking()
        .Include(x => x.Smv).Include(x => x.StructuralType).Include(x => x.BuildingType).Include(x => x.Classification);

    private IQueryable<SmvExtraItemCost> ExtraItemCosts() => db.SmvExtraItemCosts.AsNoTracking().Include(x => x.Smv).Include(x => x.ComponentType);

    private IQueryable<SmvDepreciationSchedule> DepreciationSchedules() => db.SmvDepreciationSchedules.AsNoTracking()
        .Include(x => x.Smv).Include(x => x.StructuralType).Include(x => x.Rows);

    private async Task<BuildingCostDto> BuildingCostAsync(Guid id, CancellationToken ct) => ToDto(await BuildingCosts().SingleAsync(x => x.Id == id, ct));
    private async Task<ExtraItemCostDto> ExtraItemCostAsync(Guid id, CancellationToken ct) => ToDto(await ExtraItemCosts().SingleAsync(x => x.Id == id, ct));
    private async Task<DepreciationScheduleDto> DepreciationScheduleAsync(Guid id, CancellationToken ct) => ToDto(await DepreciationSchedules().SingleAsync(x => x.Id == id, ct));

    private static BuildingCostDto ToDto(SmvBuildingCost x) => new(
        x.Id, x.SmvId, x.Smv!.Reference, x.StructuralTypeId, x.StructuralType!.Name, x.BuildingTypeId, x.BuildingType?.Name,
        x.ClassificationId, x.Classification?.Name, x.CostPerSquareMetre,
        x.LegalBasis, x.EffectiveDate, x.EndDate, x.Status, x.CreatedBy, x.ApprovedBy, x.ApprovedAt, x.Remarks);

    private static ExtraItemCostDto ToDto(SmvExtraItemCost x) => new(
        x.Id, x.SmvId, x.Smv!.Reference, x.ComponentTypeId, x.ComponentType!.Name, x.Unit, x.UnitCost,
        x.LegalBasis, x.EffectiveDate, x.EndDate, x.Status, x.CreatedBy, x.ApprovedBy, x.ApprovedAt, x.Remarks);

    private static DepreciationScheduleDto ToDto(SmvDepreciationSchedule x) => new(
        x.Id, x.SmvId, x.Smv!.Reference, x.StructuralTypeId, x.StructuralType!.Name, x.Reading, x.MinimumRemainingPercent,
        x.Rows.OrderBy(r => r.Sequence).Select(r => new DepreciationRowDto(r.Sequence, r.FromAge, r.ToAge, r.Percent)).ToList(),
        x.LegalBasis, x.EffectiveDate, x.EndDate, x.Status, x.CreatedBy, x.ApprovedBy, x.ApprovedAt, x.Remarks);
}
