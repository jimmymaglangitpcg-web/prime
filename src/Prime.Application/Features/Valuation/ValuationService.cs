using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.DomainServices;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Valuation;

/// <summary>
/// Resolves the DB-side inputs (the entity being valued, the one
/// <see cref="WorkflowStatus.Approved"/> <see cref="SmvSchedule"/> effective
/// today) and delegates the actual math to the pure
/// <see cref="ValuationCalculator"/>, then persists a
/// <see cref="Domain.Entities.Valuation"/> breakdown row (CLAUDE.md §31).
/// Never recomputes/duplicates the calculation elsewhere (Rule 9) — Phase
/// 6's AssessmentService is expected to call this service's output rather
/// than reimplementing valuation.
///
/// <see cref="SmvSchedule"/>/<see cref="AssessmentLevel"/> are keyed by
/// <c>PropertyType</c>, which Land/Building/Machinery don't carry directly —
/// each asset type's "which PropertyType am I" is resolved by a fixed
/// well-known <c>Code</c> (see the constants below), matching the existing
/// convention that <c>Code</c>, not the display <c>Name</c>, is a lookup
/// row's stable identifier (docs/DOMAIN-MODEL.md §3.10).
/// </summary>
public sealed class ValuationService(IApplicationDbContext db, IOptions<ValuationOptions> options, IClock clock) : IValuationService
{

    public async Task<Result<ValuationDto>> ComputeForLandAsync(Guid landId, CancellationToken cancellationToken = default)
    {
        var land = await db.Lands.FirstOrDefaultAsync(x => x.Id == landId, cancellationToken);
        if (land is null)
        {
            return Result.Failure<ValuationDto>("LAND_NOT_FOUND", "No Land record was found with the given id.");
        }

        var propertyType = await db.PropertyTypes.FirstOrDefaultAsync(x => x.Code == PropertyTypeCodes.Land, cancellationToken);
        if (propertyType is null)
        {
            return Result.Failure<ValuationDto>("PROPERTY_TYPE_NOT_CONFIGURED", $"No PropertyType with code '{PropertyTypeCodes.Land}' is configured.");
        }

        var asOf = clock.Today;
        var schedule = await ResolveScheduleAsync(land.ClassificationId, land.ActualUseId, propertyType.Id, land.ZoneId, asOf, cancellationToken);
        if (schedule is null)
        {
            return Result.Failure<ValuationDto>("SMV_SCHEDULE_NOT_FOUND", "No approved SMV schedule matches this Land's classification/actual use/zone as of today.");
        }

        var calc = ValuationCalculator.CalculateLand(land, schedule);
        var valuation = Persist(land.RpuId, land.PropertyId, ValuationSourceType.Land, land.Id, schedule, calc, asOf);
        land.MarketValue = calc.MarketValue;
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(valuation, calc.Breakdown));
    }

    public async Task<Result<ValuationDto>> ComputeForBuildingAsync(Guid buildingId, CancellationToken cancellationToken = default)
    {
        var building = await db.Buildings.FirstOrDefaultAsync(x => x.Id == buildingId, cancellationToken);
        if (building is null)
        {
            return Result.Failure<ValuationDto>("BUILDING_NOT_FOUND", "No Building record was found with the given id.");
        }

        // Building doesn't carry Classification/ActualUse itself (§25) — it
        // borrows them from its own current Tax Declaration, the same way a
        // building's assessed classification is a Tax Declaration concept.
        var taxDeclaration = await TaxDeclarationLookup.GetCurrentAsync(db, building.RpuId, cancellationToken);
        if (taxDeclaration is null)
        {
            return Result.Failure<ValuationDto>("TAX_DECLARATION_NOT_FOUND", "A Tax Declaration (for its classification/actual use) is required before a Building can be valued.");
        }

        var propertyType = await db.PropertyTypes.FirstOrDefaultAsync(x => x.Code == PropertyTypeCodes.Building, cancellationToken);
        if (propertyType is null)
        {
            return Result.Failure<ValuationDto>("PROPERTY_TYPE_NOT_CONFIGURED", $"No PropertyType with code '{PropertyTypeCodes.Building}' is configured.");
        }

        var asOf = clock.Today;
        // Zone-based rate differentiation only exists on Land in the current
        // domain model (§24) — Buildings resolve a zone-agnostic schedule.
        var schedule = await ResolveScheduleAsync(taxDeclaration.ClassificationId, taxDeclaration.ActualUseId, propertyType.Id, null, asOf, cancellationToken);
        if (schedule is null)
        {
            return Result.Failure<ValuationDto>("SMV_SCHEDULE_NOT_FOUND", "No approved SMV schedule matches this Building's Tax Declaration classification/actual use as of today.");
        }

        var calc = ValuationCalculator.CalculateBuilding(building, schedule);
        var valuation = Persist(building.RpuId, building.PropertyId, ValuationSourceType.Building, building.Id, schedule, calc, asOf);
        building.MarketValue = calc.MarketValue;
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(valuation, calc.Breakdown));
    }

    public async Task<Result<ValuationDto>> ComputeForMachineryAsync(Guid machineryId, CancellationToken cancellationToken = default)
    {
        var machinery = await db.MachineryUnits.FirstOrDefaultAsync(x => x.Id == machineryId, cancellationToken);
        if (machinery is null)
        {
            return Result.Failure<ValuationDto>("MACHINERY_NOT_FOUND", "No Machinery record was found with the given id.");
        }

        if (ValuationCalculator.MissingMachineryInputs(machinery) is { } missing)
        {
            return Result.Failure<ValuationDto>("MACHINERY_VALUATION_INPUTS_MISSING",
                $"Machinery that is not brand-new is valued from its replacement or reproduction cost and its remaining vs. estimated economic life (LGC §224(a)). Missing: {missing}.");
        }

        var asOf = clock.Today;
        var parameters = new MachineryValuationParameters(options.Value.MachineryMinimumRemainingValuePercent!.Value);
        var calc = ValuationCalculator.CalculateMachinery(machinery, parameters);
        var valuation = Persist(machinery.RpuId, machinery.PropertyId, ValuationSourceType.Machinery, machinery.Id, null, calc, asOf);
        machinery.MarketValue = calc.MarketValue;
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(valuation, calc.Breakdown));
    }

    public async Task<Result<ValuationDto>> GetByIdAsync(Guid valuationId, CancellationToken cancellationToken = default)
    {
        var valuation = await db.Valuations.FirstOrDefaultAsync(x => x.Id == valuationId, cancellationToken);
        return valuation is null
            ? Result.Failure<ValuationDto>("VALUATION_NOT_FOUND", "No Valuation was found with the given id.")
            : Result.Success(ToDto(valuation, DeserializeBreakdown(valuation.BreakdownJson)));
    }

    public async Task<Result<IReadOnlyList<ValuationDto>>> ListByRpuAsync(Guid rpuId, CancellationToken cancellationToken = default)
    {
        var valuations = await db.Valuations
            .Where(x => x.RpuId == rpuId)
            .OrderByDescending(x => x.ComputedAt)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ValuationDto>>(
            valuations.Select(x => ToDto(x, DeserializeBreakdown(x.BreakdownJson))).ToList());
    }

    private Prime.Domain.Entities.Valuation Persist(
        Guid rpuId,
        Guid propertyId,
        ValuationSourceType sourceType,
        Guid sourceId,
        SmvSchedule? schedule,
        ValuationCalculationResult calc,
        DateOnly effectiveDate)
    {
        var valuation = new Prime.Domain.Entities.Valuation
        {
            RpuId = rpuId,
            PropertyId = propertyId,
            SourceType = sourceType,
            SourceId = sourceId,
            SmvId = schedule?.SmvId,
            SmvScheduleId = schedule?.Id,
            ValuationMethod = calc.Method,
            ComputedMarketValue = calc.MarketValue,
            BreakdownJson = JsonSerializer.Serialize(calc.Breakdown),
            EffectiveDate = effectiveDate,
            ComputedAt = DateTimeOffset.UtcNow,
        };

        db.Valuations.Add(valuation);
        return valuation;
    }

    private async Task<SmvSchedule?> ResolveScheduleAsync(
        Guid classificationId, Guid actualUseId, Guid propertyTypeId, Guid? zoneId, DateOnly asOf, CancellationToken cancellationToken)
    {
        var candidates = await db.SmvSchedules
            .Where(x => x.ClassificationId == classificationId
                && x.ActualUseId == actualUseId
                && x.PropertyTypeId == propertyTypeId
                && x.Status == WorkflowStatus.Approved
                && x.EffectiveDate <= asOf
                && (x.EndDate == null || x.EndDate > asOf)
                && (x.ZoneId == zoneId || x.ZoneId == null))
            .ToListAsync(cancellationToken);

        // Prefer an exact zone match over a zone-agnostic schedule.
        return candidates.FirstOrDefault(x => x.ZoneId == zoneId) ?? candidates.FirstOrDefault(x => x.ZoneId == null);
    }

    private static Dictionary<string, decimal> DeserializeBreakdown(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, decimal>>(json) ?? [];

    private static ValuationDto ToDto(Prime.Domain.Entities.Valuation v, IReadOnlyDictionary<string, decimal> breakdown) => new(
        v.Id,
        v.RpuId,
        v.PropertyId,
        v.SourceType,
        v.SourceId,
        v.SmvId,
        v.SmvScheduleId,
        v.ValuationMethod,
        v.ComputedMarketValue,
        breakdown,
        v.EffectiveDate,
        v.ComputedAt);
}
