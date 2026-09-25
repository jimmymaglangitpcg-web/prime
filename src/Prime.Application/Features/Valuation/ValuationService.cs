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

    /// <summary>
    /// Values the land's strips and improvements, one line each
    /// (docs/analysis/mrpaao-forms-model.md §8.3). Each strip is priced at the
    /// SMV rate for its classification, actual use and zone, then adjusted by
    /// the factors in force under that SMV; each improvement at the rate for
    /// its kind. A land with no strips is valued from its own fields.
    /// </summary>
    public async Task<Result<ValuationDto>> ComputeForLandAsync(Guid landId, CancellationToken cancellationToken = default)
    {
        var land = await db.Lands.Include(x => x.Strips).Include(x => x.Improvements).ThenInclude(i => i.ImprovementKind)
            .Include(x => x.Adjustments).FirstOrDefaultAsync(x => x.Id == landId, cancellationToken);
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
        var lines = new List<(ValuationLine Line, SmvSchedule Schedule, ValuationCalculationResult Calc)>();
        // A land recorded without strips is valued as one strip of its own fields, adjustments included.
        var strips = land.Strips.OrderBy(x => x.Sequence).ToList();
        var rows = strips.Count > 0
            ? strips.Select(x => (Source: ValuationLineSource.LandStrip, SourceId: x.Id, StripId: (Guid?)x.Id, x.Sequence, x.ClassificationId,
                x.SubClassificationId, x.ActualUseId, ZoneId: x.ZoneId ?? land.ZoneId, x.Area)).ToList()
            : [(ValuationLineSource.Land, land.Id, null, 1, land.ClassificationId, land.SubClassificationId, land.ActualUseId, land.ZoneId, land.Area)];
        foreach (var row in rows)
        {
            var schedule = await ResolveScheduleAsync(row.ClassificationId, row.ActualUseId, propertyType.Id, row.ZoneId, null, asOf, cancellationToken);
            if (schedule is null)
            {
                return Result.Failure<ValuationDto>("SMV_SCHEDULE_NOT_FOUND",
                    $"No approved SMV schedule matches land strip {row.Sequence}'s classification/actual use/zone as of today.");
            }
            var adjustments = new List<LandAdjustmentInput>();
            foreach (var adjustment in land.Adjustments.Where(a => a.LandStripId == null || a.LandStripId == row.StripId).OrderBy(a => a.FactorCode))
            {
                var factor = await db.AdjustmentFactors.InForce(asOf)
                    .Where(f => f.SmvId == schedule.SmvId && f.Code == adjustment.FactorCode
                        && (f.ClassificationId == null || f.ClassificationId == row.ClassificationId))
                    .OrderByDescending(f => f.ClassificationId != null)
                    .FirstOrDefaultAsync(cancellationToken);
                if (factor is null)
                {
                    return Result.Failure<ValuationDto>("ADJUSTMENT_FACTOR_NOT_FOUND",
                        $"No approved adjustment factor '{adjustment.FactorCode}' is in force under the SMV that prices land strip {row.Sequence}.");
                }
                adjustments.Add(new LandAdjustmentInput(factor.Code, factor.Name, factor.Percent));
            }
            lines.Add((new ValuationLine
            {
                Source = row.Source, SourceId = row.SourceId, ClassificationId = row.ClassificationId,
                SubClassificationId = row.SubClassificationId, ActualUseId = row.ActualUseId, Quantity = row.Area, Unit = land.AreaUnit,
            }, schedule, ValuationCalculator.CalculateLandStrip(row.Area, schedule, land.LocationFactor, adjustments)));
        }

        // Improvements are assessed under their own classification and use, or the principal strip's.
        var principal = strips.OrderByDescending(x => x.Area).ThenBy(x => x.Sequence).FirstOrDefault();
        foreach (var improvement in land.Improvements.OrderBy(x => x.Sequence))
        {
            var classificationId = improvement.ClassificationId ?? principal?.ClassificationId ?? land.ClassificationId;
            var actualUseId = improvement.ActualUseId ?? principal?.ActualUseId ?? land.ActualUseId;
            var schedule = await ResolveScheduleAsync(classificationId, actualUseId, propertyType.Id, land.ZoneId, improvement.ImprovementKindId, asOf, cancellationToken);
            if (schedule is null)
            {
                return Result.Failure<ValuationDto>("SMV_SCHEDULE_NOT_FOUND",
                    $"No approved SMV schedule gives a rate for '{improvement.ImprovementKind!.Name}' under this classification/actual use as of today.");
            }
            lines.Add((new ValuationLine
            {
                Source = ValuationLineSource.LandImprovement, SourceId = improvement.Id, ClassificationId = classificationId, ActualUseId = actualUseId,
                Description = improvement.ImprovementKind!.Name + (improvement.IsProductive switch { true => " (productive)", false => " (non-productive)", _ => "" }),
                Quantity = improvement.Quantity,
            }, schedule, ValuationCalculator.CalculateImprovement(improvement.Quantity, schedule)));
        }

        foreach (var (line, schedule, calc) in lines)
        {
            line.Unit ??= schedule.Unit;
            line.UnitValue = schedule.MarketValue;
            line.SmvScheduleId = schedule.Id;
        }
        var valuation = PersistLines(land.RpuId, land.PropertyId, ValuationSourceType.Land, land.Id, lines.Select(x => (x.Line, x.Calc)).ToList(),
            lines[0].Schedule.SmvId, lines.Count == 1 ? lines[0].Schedule.Id : null, asOf);
        land.MarketValue = valuation.ComputedMarketValue;
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(valuation, DeserializeBreakdown(valuation.BreakdownJson)));
    }

    /// <summary>
    /// Values a building by use portion (MRPAAO Att. 2; docs/analysis/mrpaao-forms-model.md
    /// §8.3): each portion's floor area at the SMV rate for its classification and
    /// use, plus its additional items (items not tied to a portion are spread by
    /// floor area), × completion. A building with no portions is one portion under
    /// its Tax Declaration's classification and use, as before.
    /// </summary>
    public async Task<Result<ValuationDto>> ComputeForBuildingAsync(Guid buildingId, CancellationToken cancellationToken = default)
    {
        var building = await db.Buildings.Include(x => x.UsePortions).Include(x => x.Components)
            .FirstOrDefaultAsync(x => x.Id == buildingId, cancellationToken);
        if (building is null)
        {
            return Result.Failure<ValuationDto>("BUILDING_NOT_FOUND", "No Building record was found with the given id.");
        }

        var propertyType = await db.PropertyTypes.FirstOrDefaultAsync(x => x.Code == PropertyTypeCodes.Building, cancellationToken);
        if (propertyType is null)
        {
            return Result.Failure<ValuationDto>("PROPERTY_TYPE_NOT_CONFIGURED", $"No PropertyType with code '{PropertyTypeCodes.Building}' is configured.");
        }

        var portions = building.UsePortions.OrderBy(x => x.Sequence)
            .Select(x => (Source: ValuationLineSource.BuildingUsePortion, SourceId: x.Id, PortionId: (Guid?)x.Id, x.Sequence,
                x.ClassificationId, SubClassificationId: (Guid?)null, x.ActualUseId, Area: x.FloorArea)).ToList();
        if (portions.Count == 0)
        {
            // Building doesn't carry a classification itself (§25): one portion under its current Tax Declaration's.
            var taxDeclaration = await TaxDeclarationLookup.GetCurrentAsync(db, building.RpuId, cancellationToken);
            if (taxDeclaration is null)
            {
                return Result.Failure<ValuationDto>("TAX_DECLARATION_NOT_FOUND", "A Tax Declaration (for its classification/actual use) is required before a Building can be valued.");
            }
            portions.Add((ValuationLineSource.Building, building.Id, null, 1, taxDeclaration.ClassificationId, taxDeclaration.SubClassificationId,
                taxDeclaration.ActualUseId, building.TotalFloorArea));
        }
        else if (portions.Sum(x => x.Area) != building.TotalFloorArea)
        {
            return Result.Failure<ValuationDto>("BUILDING_USE_PORTIONS_INCOMPLETE",
                $"The use portions cover {portions.Sum(x => x.Area):#,0.####} sqm of the building's {building.TotalFloorArea:#,0.####} sqm total floor area.");
        }

        // Additional items: those tied to a portion go to it; the rest are spread by floor area.
        var items = building.Components.Where(c => c.IsAdditionalItem).ToList();
        var spread = ValuationCalculator.SpreadByArea(items.Where(c => c.BuildingUsePortionId == null).Sum(c => c.Cost ?? 0m),
            portions.Select(p => p.Area).ToList());

        var asOf = clock.Today;
        var lines = new List<(ValuationLine Line, SmvSchedule Schedule, ValuationCalculationResult Calc)>();
        for (var i = 0; i < portions.Count; i++)
        {
            var p = portions[i];
            // Zone-based rates exist on Land only (§24): buildings resolve a zone-agnostic schedule.
            var schedule = await ResolveScheduleAsync(p.ClassificationId, p.ActualUseId, propertyType.Id, null, null, asOf, cancellationToken);
            if (schedule is null)
            {
                return Result.Failure<ValuationDto>("SMV_SCHEDULE_NOT_FOUND",
                    portions.Count == 1 && p.PortionId is null
                        ? "No approved SMV schedule matches this Building's Tax Declaration classification/actual use as of today."
                        : $"No approved SMV schedule matches use portion {p.Sequence}'s classification/actual use as of today.");
            }
            var additional = spread[i] + items.Where(c => c.BuildingUsePortionId != null && c.BuildingUsePortionId == p.PortionId).Sum(c => c.Cost ?? 0m);
            lines.Add((new ValuationLine
            {
                Source = p.Source, SourceId = p.SourceId, ClassificationId = p.ClassificationId, SubClassificationId = p.SubClassificationId,
                ActualUseId = p.ActualUseId, Quantity = p.Area, Unit = schedule.Unit, UnitValue = schedule.MarketValue, SmvScheduleId = schedule.Id,
            }, schedule, ValuationCalculator.CalculateBuildingPortion(p.Area, schedule, additional, building.CompletionPercentage)));
        }

        var valuation = PersistLines(building.RpuId, building.PropertyId, ValuationSourceType.Building, building.Id,
            lines.Select(x => (x.Line, x.Calc)).ToList(), lines[0].Schedule.SmvId, lines.Count == 1 ? lines[0].Schedule.Id : null, asOf);
        building.MarketValue = valuation.ComputedMarketValue;
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(valuation, DeserializeBreakdown(valuation.BreakdownJson)));
    }

    /// <summary>
    /// Values every machine of the machine's unit, one line each (MRPAAO Att. 3;
    /// LGC §224–225, unchanged per machine). A machine without its own
    /// classification or use is assessed under the unit's Tax Declaration's.
    /// </summary>
    public async Task<Result<ValuationDto>> ComputeForMachineryAsync(Guid machineryId, CancellationToken cancellationToken = default)
    {
        var machinery = await db.MachineryUnits.FirstOrDefaultAsync(x => x.Id == machineryId, cancellationToken);
        if (machinery is null)
        {
            return Result.Failure<ValuationDto>("MACHINERY_NOT_FOUND", "No Machinery record was found with the given id.");
        }
        var machines = await db.MachineryUnits.Where(x => x.RpuId == machinery.RpuId).OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken);

        var parameters = new MachineryValuationParameters(options.Value.MachineryMinimumRemainingValuePercent!.Value);
        var lines = new List<(ValuationLine Line, ValuationCalculationResult Calc)>();
        foreach (var machine in machines)
        {
            if (ValuationCalculator.MissingMachineryInputs(machine) is { } missing)
            {
                var name = string.Join(" ", new[] { machine.Brand, machine.Model, machine.SerialNumber }.Where(x => !string.IsNullOrWhiteSpace(x)));
                return Result.Failure<ValuationDto>("MACHINERY_VALUATION_INPUTS_MISSING",
                    $"Machinery that is not brand-new is valued from its replacement or reproduction cost and its remaining vs. estimated economic life (LGC §224(a)). {(name.Length > 0 ? name : "A machine")} is missing: {missing}.");
            }
            var calc = ValuationCalculator.CalculateMachinery(machine, parameters);
            lines.Add((new ValuationLine
            {
                Source = ValuationLineSource.Machinery, SourceId = machine.Id,
                ClassificationId = machine.ClassificationId, ActualUseId = machine.ActualUseId,
                Description = string.Join(" ", new[] { machine.Brand, machine.Model, machine.Description }.Where(x => !string.IsNullOrWhiteSpace(x))) is { Length: > 0 } d ? d : null,
            }, calc));
            machine.MarketValue = calc.MarketValue;
        }

        // Valuation.SourceId names the unit's first machine; the lines name each machine.
        var valuation = PersistLines(machinery.RpuId, machinery.PropertyId, ValuationSourceType.Machinery, machines[0].Id, lines, null, null, clock.Today);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(valuation, DeserializeBreakdown(valuation.BreakdownJson)));
    }

    /// <summary>
    /// Values the whole unit — every row it holds — in one valuation
    /// (docs/analysis/mrpaao-forms-model.md §8.4). Used by general revision.
    /// </summary>
    public async Task<Result<ValuationDto>> ComputeForRpuAsync(Guid rpuId, CancellationToken cancellationToken = default)
    {
        var rpu = await db.RealPropertyUnits.FirstOrDefaultAsync(x => x.Id == rpuId, cancellationToken);
        if (rpu is null)
        {
            return Result.Failure<ValuationDto>("RPU_NOT_FOUND", "No RPU was found with the given id.");
        }
        switch (rpu.RpuType)
        {
            case RpuType.Land:
                var land = await db.Lands.Where(x => x.RpuId == rpuId).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);
                return land is null
                    ? Result.Failure<ValuationDto>("LAND_NOT_FOUND", "No Land record exists for this RPU.")
                    : await ComputeForLandAsync(land.Value, cancellationToken);
            case RpuType.Building:
                var building = await db.Buildings.Where(x => x.RpuId == rpuId).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);
                return building is null
                    ? Result.Failure<ValuationDto>("BUILDING_NOT_FOUND", "No Building record exists for this RPU.")
                    : await ComputeForBuildingAsync(building.Value, cancellationToken);
            case RpuType.Machinery:
                var machinery = await db.MachineryUnits.Where(x => x.RpuId == rpuId).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);
                return machinery is null
                    ? Result.Failure<ValuationDto>("MACHINERY_NOT_FOUND", "No Machinery record exists for this RPU.")
                    : await ComputeForMachineryAsync(machinery.Value, cancellationToken);
            default:
                return Result.Failure<ValuationDto>("UNSUPPORTED_RPU_TYPE", $"RPU type '{rpu.RpuType}' is not yet valuable.");
        }
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

    /// <summary>
    /// A valuation of several rows: each line keeps its own calculation, and the
    /// valuation their total. A single line's breakdown is also the valuation's;
    /// several give the valuation a total-only breakdown.
    /// </summary>
    private Prime.Domain.Entities.Valuation PersistLines(Guid rpuId, Guid propertyId, ValuationSourceType sourceType, Guid sourceId,
        IReadOnlyList<(ValuationLine Line, ValuationCalculationResult Calc)> lines, Guid? smvId, Guid? smvScheduleId, DateOnly effectiveDate)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            lines[i].Line.Sequence = i + 1;
            lines[i].Line.MarketValue = lines[i].Calc.MarketValue;
            lines[i].Line.BreakdownJson = JsonSerializer.Serialize(lines[i].Calc.Breakdown);
        }
        var total = lines.Sum(x => x.Calc.MarketValue);
        var valuation = new Prime.Domain.Entities.Valuation
        {
            RpuId = rpuId,
            PropertyId = propertyId,
            SourceType = sourceType,
            SourceId = sourceId,
            SmvId = smvId,
            SmvScheduleId = smvScheduleId,
            ValuationMethod = lines[0].Calc.Method,
            ComputedMarketValue = total,
            BreakdownJson = lines.Count == 1
                ? JsonSerializer.Serialize(lines[0].Calc.Breakdown)
                : JsonSerializer.Serialize(new Dictionary<string, decimal> { ["MarketValue"] = total }),
            EffectiveDate = effectiveDate,
            ComputedAt = DateTimeOffset.UtcNow,
            Lines = lines.Select(x => x.Line).ToList(),
        };
        db.Valuations.Add(valuation);
        return valuation;
    }

    private Prime.Domain.Entities.Valuation Persist(
        Guid rpuId,
        Guid propertyId,
        ValuationSourceType sourceType,
        Guid sourceId,
        SmvSchedule? schedule,
        ValuationCalculationResult calc,
        DateOnly effectiveDate,
        ValuationLine line)
    {
        // One row valued: the line carries the calculation; the valuation its total.
        line.Sequence = 1;
        line.MarketValue = calc.MarketValue;
        line.BreakdownJson = JsonSerializer.Serialize(calc.Breakdown);
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
            Lines = [line],
        };

        db.Valuations.Add(valuation);
        return valuation;
    }

    /// <summary>
    /// The approved schedule in force for the key. <paramref name="improvementKindId"/>
    /// null selects land and building rates only; set, the rate for that improvement kind.
    /// </summary>
    private async Task<SmvSchedule?> ResolveScheduleAsync(
        Guid classificationId, Guid actualUseId, Guid propertyTypeId, Guid? zoneId, Guid? improvementKindId, DateOnly asOf, CancellationToken cancellationToken)
    {
        var candidates = await db.SmvSchedules
            .Where(x => x.ImprovementKindId == improvementKindId
                && x.ClassificationId == classificationId
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
