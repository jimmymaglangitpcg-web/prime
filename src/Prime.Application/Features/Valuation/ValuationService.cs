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
/// <see cref="WorkflowStatus.Approved"/> <see cref="SmvSchedule"/> in force on
/// the valuation date — <c>asOf</c>, else today; docs/analysis/valuation-foundation.md §4.1) and delegates the actual math to the pure
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
    public async Task<Result<ValuationDto>> ComputeForLandAsync(Guid landId, CancellationToken cancellationToken = default, DateOnly? asOf = null)
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

        var date = asOf ?? clock.Today;
        var location = await LocationAsync(land.PropertyId, cancellationToken);
        var facts = new LandFacts(land.RoadTypeId, land.IsCornerLot, land.DistanceToAllWeatherRoadKm, land.DistanceToPoblacionKm, land.IsSubdivisionLot);
        var lines = new List<(ValuationLine Line, SmvSchedule? Schedule, ValuationCalculationResult Calc)>();
        // A land recorded without strips is valued as one strip of its own fields, adjustments included.
        // Each strip is priced by its valuation key (class and sub-class) and assessed by its own (§4.3).
        var strips = land.Strips.OrderBy(x => x.Sequence).ToList();
        var rows = strips.Count > 0
            ? strips.Select(x => (Source: ValuationLineSource.LandStrip, SourceId: x.Id, StripId: (Guid?)x.Id, x.Sequence, x.ClassificationId,
                x.SubClassificationId, x.ActualUseId, ZoneId: x.ZoneId ?? land.ZoneId, x.Area,
                PricedClassificationId: x.ValuationClassificationId ?? x.ClassificationId,
                PricedSubClassificationId: x.ValuationClassificationId is null && x.ValuationSubClassificationId is null ? x.SubClassificationId : x.ValuationSubClassificationId,
                x.DepthBand)).ToList()
            : [(ValuationLineSource.Land, land.Id, null, 1, land.ClassificationId, land.SubClassificationId, land.ActualUseId, land.ZoneId, land.Area,
                land.ClassificationId, land.SubClassificationId, null)];
        // An independent appraisal of the land replaces its strips' SMV values, shared by area (§4.7).
        var appraisals = await CurrentAppraisalsAsync(land.RpuId, cancellationToken);
        var landAppraisal = appraisals.GetValueOrDefault(land.Id);
        if (landAppraisal is not null)
        {
            var total = rows.Sum(r => r.Area);
            var shares = ValuationCalculator.SpreadByArea(landAppraisal.Value, rows.Select(r => r.Area).ToList());
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                lines.Add((Appraised(landAppraisal, new ValuationLine
                {
                    Source = row.Source, SourceId = row.SourceId, ClassificationId = row.ClassificationId, SubClassificationId = row.SubClassificationId,
                    ActualUseId = row.ActualUseId, Quantity = row.Area, Unit = land.AreaUnit,
                }), null, Rounded(AppraisedValue(landAppraisal, total == 0 ? 1m : row.Area / total, shares[i]))));
            }
        }
        foreach (var row in rows.Where(_ => landAppraisal is null))
        {
            var schedule = await ResolveScheduleAsync(row.PricedClassificationId, propertyType.Id, null, location.MunicipalityId,
                new SmvRateKey(row.PricedSubClassificationId, row.ZoneId, location.BarangayId, row.ActualUseId), date, cancellationToken);
            if (schedule is null)
            {
                return Result.Failure<ValuationDto>("SMV_SCHEDULE_NOT_FOUND",
                    $"No approved SMV schedule covering this municipality matches land strip {row.Sequence}'s classification/sub-class/zone as of {date:yyyy-MM-dd}.");
            }
            // Each named factor's percentage comes from its rule and the land's facts (valuation-foundation.md §4.4).
            var adjustments = new List<LandAdjustmentInput>();
            foreach (var adjustment in land.Adjustments.Where(a => a.LandStripId == null || a.LandStripId == row.StripId).OrderBy(a => a.FactorCode))
            {
                var factor = await db.AdjustmentFactors.InForce(date).Include(f => f.Rows)
                    .Where(f => f.SmvId == schedule.SmvId && f.Code == adjustment.FactorCode
                        && (f.ClassificationId == null || f.ClassificationId == row.ClassificationId))
                    .OrderByDescending(f => f.ClassificationId != null)
                    .FirstOrDefaultAsync(cancellationToken);
                if (factor is null)
                {
                    return Result.Failure<ValuationDto>("ADJUSTMENT_FACTOR_NOT_FOUND",
                        $"No approved adjustment factor '{adjustment.FactorCode}' for this classification is in force under the SMV that prices land strip {row.Sequence}.");
                }
                var outcome = AdjustmentRules.Evaluate(factor, facts, row.DepthBand);
                if (outcome.Problem is { } problem)
                {
                    return Result.Failure<ValuationDto>("ADJUSTMENT_NOT_APPLICABLE", $"Land strip {row.Sequence}: {problem}");
                }
                if (outcome.Applies)
                {
                    adjustments.Add(new LandAdjustmentInput(factor.Code, factor.Name, outcome.Percent));
                }
            }
            var pricedApart = row.PricedClassificationId != row.ClassificationId || row.PricedSubClassificationId != row.SubClassificationId;
            lines.Add((new ValuationLine
            {
                Source = row.Source, SourceId = row.SourceId, ClassificationId = row.ClassificationId,
                SubClassificationId = row.SubClassificationId, ActualUseId = row.ActualUseId, Quantity = row.Area, Unit = land.AreaUnit,
                PricedClassificationId = pricedApart ? row.PricedClassificationId : null,
                PricedSubClassificationId = pricedApart ? row.PricedSubClassificationId : null,
            }, schedule, Rounded(ValuationCalculator.CalculateLandStrip(row.Area, schedule, land.LocationFactor, adjustments))));
        }

        // The land's own improvements; separately owned ones are valued under their own unit.
        var improvements = await ImprovementLinesAsync(land, land.Improvements.Where(i => i.SeparateRpuId == null), propertyType.Id, location, date, cancellationToken);
        if (improvements.IsFailure)
        {
            return Result.Failure<ValuationDto>(improvements.Code!, improvements.Message!);
        }
        lines.AddRange(improvements.Value);

        var valuation = PersistRated(land.RpuId, land.PropertyId, land.Id, lines, date);
        land.MarketValue = valuation.ComputedMarketValue;
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(await MapAsync(valuation.Id, cancellationToken));
    }

    /// <summary>
    /// Values a separately owned improvement unit (an <c>OtherImprovement</c> RPU): the trees,
    /// plants and other improvements recorded on its land as belonging to it, at the SMV's rates
    /// for their kinds (LAM Bk III p.72; docs/analysis/valuation-foundation.md §4.4). Assessed like
    /// land improvements, under their own classification and use or the land's principal strip's.
    /// </summary>
    private async Task<Result<ValuationDto>> ComputeForSeparateImprovementsAsync(RealPropertyUnit rpu, CancellationToken cancellationToken, DateOnly? asOf)
    {
        var owned = await db.LandImprovements.Include(i => i.ImprovementKind).Where(i => i.SeparateRpuId == rpu.Id).ToListAsync(cancellationToken);
        if (owned.Count == 0)
        {
            return Result.Failure<ValuationDto>("OTHER_IMPROVEMENT_NOT_RECORDED",
                "No trees, plants or other improvements are recorded on the land as belonging to this unit.");
        }
        var land = await db.Lands.Include(x => x.Strips).FirstAsync(x => x.Id == owned[0].LandId, cancellationToken);
        var propertyType = await db.PropertyTypes.FirstOrDefaultAsync(x => x.Code == PropertyTypeCodes.Land, cancellationToken);
        if (propertyType is null)
        {
            return Result.Failure<ValuationDto>("PROPERTY_TYPE_NOT_CONFIGURED", $"No PropertyType with code '{PropertyTypeCodes.Land}' is configured.");
        }
        var date = asOf ?? clock.Today;
        var lines = await ImprovementLinesAsync(land, owned, propertyType.Id, await LocationAsync(land.PropertyId, cancellationToken), date, cancellationToken);
        if (lines.IsFailure)
        {
            return Result.Failure<ValuationDto>(lines.Code!, lines.Message!);
        }
        var valuation = PersistRated(rpu.Id, rpu.PropertyId, land.Id, lines.Value, date);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(await MapAsync(valuation.Id, cancellationToken));
    }

    /// <summary>Improvement lines at the rate for their kind, assessed under their own classification and use, or the principal strip's.</summary>
    private async Task<Result<List<(ValuationLine Line, SmvSchedule? Schedule, ValuationCalculationResult Calc)>>> ImprovementLinesAsync(Land land,
        IEnumerable<LandImprovement> improvements, Guid propertyTypeId, (Guid? MunicipalityId, Guid? BarangayId) location, DateOnly date, CancellationToken ct)
    {
        var lines = new List<(ValuationLine Line, SmvSchedule? Schedule, ValuationCalculationResult Calc)>();
        var principal = land.Strips.OrderByDescending(x => x.Area).ThenBy(x => x.Sequence).FirstOrDefault();
        foreach (var improvement in improvements.OrderBy(x => x.Sequence))
        {
            var classificationId = improvement.ClassificationId ?? principal?.ClassificationId ?? land.ClassificationId;
            var actualUseId = improvement.ActualUseId ?? principal?.ActualUseId ?? land.ActualUseId;
            var schedule = await ResolveScheduleAsync(classificationId, propertyTypeId, improvement.ImprovementKindId, location.MunicipalityId,
                new SmvRateKey(null, land.ZoneId, location.BarangayId, actualUseId), date, ct);
            if (schedule is null)
            {
                return Result.Failure<List<(ValuationLine, SmvSchedule?, ValuationCalculationResult)>>("SMV_SCHEDULE_NOT_FOUND",
                    $"No approved SMV schedule gives a rate for '{improvement.ImprovementKind!.Name}' under this classification/actual use as of {date:yyyy-MM-dd}.");
            }
            lines.Add((new ValuationLine
            {
                Source = ValuationLineSource.LandImprovement, SourceId = improvement.Id, ClassificationId = classificationId, ActualUseId = actualUseId,
                Description = improvement.ImprovementKind!.Name + (improvement.IsProductive switch { true => " (productive)", false => " (non-productive)", _ => "" }),
                Quantity = improvement.Quantity,
            }, schedule, Rounded(ValuationCalculator.CalculateImprovement(improvement.Quantity, schedule))));
        }
        return Result.Success(lines);
    }

    /// <summary>
    /// Land lines: each rate-based line notes its rate and schedule (an appraised line has none); the valuation
    /// names the SMV of the first rate-based line.
    /// </summary>
    private Prime.Domain.Entities.Valuation PersistRated(Guid rpuId, Guid propertyId, Guid landId,
        List<(ValuationLine Line, SmvSchedule? Schedule, ValuationCalculationResult Calc)> lines, DateOnly date)
    {
        foreach (var (line, schedule, _) in lines)
        {
            if (schedule is null)
            {
                continue;
            }
            line.Unit ??= schedule.Unit;
            line.UnitValue = schedule.MarketValue;
            line.SmvScheduleId = schedule.Id;
        }
        var first = lines.FirstOrDefault(x => x.Schedule is not null).Schedule;
        return PersistLines(rpuId, propertyId, ValuationSourceType.Land, landId, lines.Select(x => (x.Line, x.Calc)).ToList(),
            first?.SmvId, lines.Count == 1 ? first?.Id : null, date);
    }

    /// <summary>The current independent appraisals of a unit's subjects, by subject id (§4.7).</summary>
    private async Task<Dictionary<Guid, IndependentAppraisal>> CurrentAppraisalsAsync(Guid rpuId, CancellationToken ct) =>
        await db.IndependentAppraisals.AsNoTracking().Include(x => x.Inputs).Where(x => x.RpuId == rpuId && x.IsCurrent)
            .ToDictionaryAsync(x => x.SubjectId, ct);

    /// <summary>A row's share of an appraised value, with the appraisal's named inputs (§4.7).</summary>
    private static ValuationCalculationResult AppraisedValue(IndependentAppraisal appraisal, decimal share, decimal value) =>
        ValuationCalculator.FromIndependentAppraisal(appraisal.Value, share, value,
            appraisal.Inputs.OrderBy(i => i.Sequence).Select(i => (i.Name + (i.Unit is null ? "" : $" ({i.Unit})"), i.Value)).ToList());

    /// <summary>Marks a line as valued by the appraisal, naming its approach.</summary>
    private static ValuationLine Appraised(IndependentAppraisal appraisal, ValuationLine line)
    {
        line.IndependentAppraisalId = appraisal.Id;
        var note = $"Independent appraisal, {appraisal.Approach.ToString().ToLowerInvariant()} approach";
        line.Description = line.Description is null ? note : $"{line.Description} — {note}";
        return line;
    }

    /// <summary>The row's market value rounded to the configured step, if any (valuation-foundation.md §4.4, [C5]).</summary>
    private ValuationCalculationResult Rounded(ValuationCalculationResult result) =>
        ValuationCalculator.WithRounding(result, options.Value.MarketValueRoundingStep);

    /// <summary>
    /// Values a building by use portion (MRPAAO Att. 2; docs/analysis/mrpaao-forms-model.md
    /// §8.3), each portion keeping its classification and use for the level. Where an SMV in
    /// force covering the property has construction costs, each portion is valued on them
    /// (docs/analysis/valuation-foundation.md §4.5): floor area × BUCC for the structural type,
    /// plus the extra items priced from the SMV, × completion, less depreciation. Otherwise, as
    /// before: the floor area at the rate for its classification and use, plus the entered cost
    /// of its additional items, × completion. Items not tied to a portion are spread by floor
    /// area. A building with no portions is one portion under its Tax Declaration's.
    /// </summary>
    /// <param name="transactionTypeId">The transaction it is valued for: whether its type allows a new depreciation.</param>
    /// <param name="generalRevision">A general revision: a new depreciation (LAM Bk III p.73).</param>
    public async Task<Result<ValuationDto>> ComputeForBuildingAsync(Guid buildingId, CancellationToken cancellationToken = default, DateOnly? asOf = null,
        Guid? transactionTypeId = null, bool generalRevision = false, DateOnly? rulesAsOf = null)
    {
        var building = await db.Buildings.Include(x => x.UsePortions).Include(x => x.Components).ThenInclude(c => c.ComponentType)
            .Include(x => x.StructuralType)
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

        var date = asOf ?? clock.Today;
        // The rules (SMV, costs, tables) of another date, e.g. the current ones for every back-tax period (§4.8, Q14); age counts to the valuation date.
        var rules = rulesAsOf ?? date;
        var location = await LocationAsync(building.PropertyId, cancellationToken);
        if (transactionTypeId is { } typeId && !await db.TransactionTypes.InForce(clock.Today).AnyAsync(x => x.Id == typeId, cancellationToken))
        {
            return Result.Failure<ValuationDto>("TRANSACTION_TYPE_NOT_IN_FORCE", "The transaction type is not one in force today.");
        }

        // An independent appraisal of the building replaces the SMV, shared over the portions by floor area (§4.7).
        var appraisals = await CurrentAppraisalsAsync(building.RpuId, cancellationToken);
        if (appraisals.GetValueOrDefault(building.Id) is { } buildingAppraisal)
        {
            var total = portions.Sum(p => p.Area);
            var shares = ValuationCalculator.SpreadByArea(buildingAppraisal.Value, portions.Select(p => p.Area).ToList());
            var appraisedLines = portions.Select((p, i) => (Appraised(buildingAppraisal, new ValuationLine
            {
                Source = p.Source, SourceId = p.SourceId, ClassificationId = p.ClassificationId, SubClassificationId = p.SubClassificationId,
                ActualUseId = p.ActualUseId, Quantity = p.Area, Unit = "sqm",
            }), Rounded(AppraisedValue(buildingAppraisal, total == 0 ? 1m : p.Area / total, shares[i])))).ToList();
            var appraisedValuation = PersistLines(building.RpuId, building.PropertyId, ValuationSourceType.Building, building.Id, appraisedLines, null, null, date);
            appraisedValuation.TransactionTypeId = transactionTypeId;
            building.MarketValue = appraisedValuation.ComputedMarketValue;
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success(await MapAsync(appraisedValuation.Id, cancellationToken));
        }

        // The SMV's construction costs, if the SMV in force covering the property has any (§4.5).
        var costs = await db.SmvBuildingCosts.InForce(rules).Include(x => x.Smv)
            .Where(x => x.Smv!.Status == WorkflowStatus.Approved && x.Smv.EffectivityDate <= rules
                && (!x.Smv.Coverage.Any() || x.Smv.Coverage.Any(c => c.MunicipalityId == location.MunicipalityId)))
            .ToListAsync(cancellationToken);
        if (costs.Count > 0)
        {
            return await ValueBuildingByCostAsync(building, portions.Select(p => (p.Source, p.SourceId, p.PortionId, p.ClassificationId, p.SubClassificationId, p.ActualUseId, p.Area)).ToList(),
                costs, appraisals, date, rules, transactionTypeId, generalRevision, cancellationToken);
        }

        // Additional items: those tied to a portion go to it; the rest are spread by floor area.
        var items = building.Components.Where(c => c.IsAdditionalItem).ToList();
        decimal ItemCost(BuildingComponent c) => appraisals.GetValueOrDefault(c.Id)?.Value ?? c.Cost ?? 0m;
        var spread = ValuationCalculator.SpreadByArea(items.Where(c => c.BuildingUsePortionId == null).Sum(ItemCost),
            portions.Select(p => p.Area).ToList());

        var lines = new List<(ValuationLine Line, SmvSchedule Schedule, ValuationCalculationResult Calc)>();
        for (var i = 0; i < portions.Count; i++)
        {
            var p = portions[i];
            // Zone-based rates exist on Land only (§24): buildings resolve a zone-agnostic schedule.
            var schedule = await ResolveScheduleAsync(p.ClassificationId, propertyType.Id, null, location.MunicipalityId,
                new SmvRateKey(null, null, null, p.ActualUseId), rules, cancellationToken);
            if (schedule is null)
            {
                return Result.Failure<ValuationDto>("SMV_SCHEDULE_NOT_FOUND",
                    portions.Count == 1 && p.PortionId is null
                        ? $"No approved SMV schedule matches this Building's Tax Declaration classification/actual use as of {rules:yyyy-MM-dd}."
                        : $"No approved SMV schedule matches use portion {p.Sequence}'s classification/actual use as of {rules:yyyy-MM-dd}.");
            }
            var additional = spread[i] + items.Where(c => c.BuildingUsePortionId != null && c.BuildingUsePortionId == p.PortionId).Sum(ItemCost);
            lines.Add((new ValuationLine
            {
                Source = p.Source, SourceId = p.SourceId, ClassificationId = p.ClassificationId, SubClassificationId = p.SubClassificationId,
                ActualUseId = p.ActualUseId, Quantity = p.Area, Unit = schedule.Unit, UnitValue = schedule.MarketValue, SmvScheduleId = schedule.Id,
            }, schedule, Rounded(ValuationCalculator.CalculateBuildingPortion(p.Area, schedule, additional, building.CompletionPercentage))));
        }

        var valuation = PersistLines(building.RpuId, building.PropertyId, ValuationSourceType.Building, building.Id,
            lines.Select(x => (x.Line, x.Calc)).ToList(), lines[0].Schedule.SmvId, lines.Count == 1 ? lines[0].Schedule.Id : null, date);
        valuation.TransactionTypeId = transactionTypeId;
        valuation.RulesAsOf = rules != date ? rules : null;
        building.MarketValue = valuation.ComputedMarketValue;
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(await MapAsync(valuation.Id, cancellationToken));
    }

    /// <summary>
    /// A building valued on the SMV's construction cost (docs/analysis/valuation-foundation.md §4.5).
    /// The SMV is the latest in force with construction costs; its BUCC for the structural type, the
    /// building's kind and the portion's classification (the most specific row wins), its extra-item
    /// costs, and its depreciation table for the structural type. Whatever the SMV does not give
    /// stops the valuation with the reason; an independent appraisal is step L1-7.
    /// </summary>
    private async Task<Result<ValuationDto>> ValueBuildingByCostAsync(Building building,
        List<(ValuationLineSource Source, Guid SourceId, Guid? PortionId, Guid ClassificationId, Guid? SubClassificationId, Guid ActualUseId, decimal Area)> portions,
        List<SmvBuildingCost> costs, Dictionary<Guid, IndependentAppraisal> appraisals, DateOnly date, DateOnly rules, Guid? transactionTypeId, bool generalRevision,
        CancellationToken ct)
    {
        var smv = costs.Select(x => x.Smv!).OrderByDescending(x => x.EffectivityDate).First();
        var structure = building.StructuralType!.Name;
        var ofStructure = costs.Where(x => x.SmvId == smv.Id && x.StructuralTypeId == building.StructuralTypeId
            && (x.BuildingTypeId == null || x.BuildingTypeId == building.BuildingTypeId)).ToList();
        // Each portion's BUCC: the most specific row for the building's kind and the portion's classification.
        var buccs = new List<SmvBuildingCost>();
        for (var i = 0; i < portions.Count; i++)
        {
            var bucc = ofStructure.Where(x => x.ClassificationId == null || x.ClassificationId == portions[i].ClassificationId)
                .OrderByDescending(x => x.BuildingTypeId != null).ThenByDescending(x => x.ClassificationId != null).FirstOrDefault();
            if (bucc is null)
            {
                return Result.Failure<ValuationDto>("BUILDING_COST_NOT_FOUND",
                    $"The SMV {smv.Reference} gives no construction cost for structural type {structure} and this building's kind"
                    + (portions.Count > 1 ? $" (use portion {i + 1})" : "") + $" as of {rules:yyyy-MM-dd}. A building outside the SMV needs an independent appraisal.");
            }
            buccs.Add(bucc);
        }

        // Extra items, each priced from the SMV: those tied to a portion go to it; the rest are spread by floor area.
        var areas = portions.Select(p => p.Area).ToList();
        var shares = portions.Select(_ => new List<(string Code, decimal Cost)>()).ToList();
        foreach (var item in building.Components.Where(c => c.IsAdditionalItem))
        {
            var name = item.ComponentType!.Name + (item.Description is { } d ? $" ({d})" : "");
            // An item outside the SMV is valued by its independent appraisal (§4.7).
            var itemAppraisal = appraisals.GetValueOrDefault(item.Id);
            var price = itemAppraisal is not null ? null
                : await db.SmvExtraItemCosts.InForce(rules).FirstOrDefaultAsync(x => x.SmvId == smv.Id && x.ComponentTypeId == item.ComponentTypeId, ct);
            if (itemAppraisal is null && price is null)
            {
                return Result.Failure<ValuationDto>("EXTRA_ITEM_COST_NOT_FOUND",
                    $"The SMV {smv.Reference} gives no cost for the extra item {name} as of {rules:yyyy-MM-dd}. An item outside the SMV needs an independent appraisal.");
            }
            if (itemAppraisal is null && item.Quantity is not > 0)
            {
                return Result.Failure<ValuationDto>("EXTRA_ITEM_QUANTITY_REQUIRED",
                    $"The extra item {name} is priced from the SMV per {price!.Unit}; enter its quantity.");
            }
            var cost = itemAppraisal?.Value ?? item.Quantity!.Value * price!.UnitCost;
            var code = item.ComponentType.Code;
            if (item.BuildingUsePortionId is { } portionId)
            {
                shares[portions.FindIndex(p => p.PortionId == portionId)].Add((code, cost));
            }
            else
            {
                var spread = ValuationCalculator.SpreadByArea(cost, areas);
                for (var i = 0; i < portions.Count; i++)
                {
                    shares[i].Add((code, spread[i]));
                }
            }
        }

        // Depreciation: a new one for the building's age where allowed, else the last posted one's percent (Q10).
        var allowsNew = generalRevision
            || transactionTypeId is { } typeId && await db.TransactionTypes.AnyAsync(x => x.Id == typeId && x.AllowsNewDepreciation, ct);
        var carried = allowsNew ? null : await LastPostedDepreciationPercentAsync(building.RpuId, ct);
        BuildingDepreciationInput depreciation;
        if (carried is { } percent)
        {
            depreciation = new BuildingDepreciationInput(percent, null, true, false);
        }
        else
        {
            var table = await db.SmvDepreciationSchedules.InForce(rules).Include(x => x.Rows)
                .FirstOrDefaultAsync(x => x.SmvId == smv.Id && x.StructuralTypeId == building.StructuralTypeId, ct);
            if (table is null)
            {
                return Result.Failure<ValuationDto>("DEPRECIATION_TABLE_NOT_FOUND",
                    $"The SMV {smv.Reference} has no approved depreciation table for structural type {structure} in force on {rules:yyyy-MM-dd}.");
            }
            if (BuildingDepreciation.Age(building, date) is not { } age)
            {
                return Result.Failure<ValuationDto>("BUILDING_AGE_UNKNOWN",
                    "The building's age is read from the year it was completed, else constructed, else occupied; record one of them.");
            }
            var outcome = BuildingDepreciation.Percent(table.Reading, table.Rows, table.MinimumRemainingPercent, age);
            if (outcome.Problem is { } problem)
            {
                return Result.Failure<ValuationDto>("DEPRECIATION_NOT_APPLICABLE", problem);
            }
            depreciation = new BuildingDepreciationInput(outcome.Percent, age, false, outcome.Capped);
        }

        var lines = new List<(ValuationLine Line, ValuationCalculationResult Calc)>();
        for (var i = 0; i < portions.Count; i++)
        {
            var (p, bucc) = (portions[i], buccs[i]);
            lines.Add((new ValuationLine
            {
                Source = p.Source, SourceId = p.SourceId, ClassificationId = p.ClassificationId, SubClassificationId = p.SubClassificationId,
                ActualUseId = p.ActualUseId, Quantity = p.Area, Unit = "sqm", UnitValue = bucc.CostPerSquareMetre, Description = structure,
            }, Rounded(ValuationCalculator.CalculateBuildingByCost(p.Area, bucc.CostPerSquareMetre, shares[i], building.CompletionPercentage, depreciation))));
        }

        var valuation = PersistLines(building.RpuId, building.PropertyId, ValuationSourceType.Building, building.Id, lines, smv.Id, null, date);
        valuation.TransactionTypeId = transactionTypeId;
        valuation.RulesAsOf = rules != date ? rules : null;
        building.MarketValue = valuation.ComputedMarketValue;
        building.Depreciation = depreciation.Percent;
        building.DepreciatedValue = valuation.ComputedMarketValue;
        await db.SaveChangesAsync(ct);
        return Result.Success(await MapAsync(valuation.Id, ct));
    }

    /// <summary>The depreciation percent of the building unit's last posted valuation, if it recorded one.</summary>
    private async Task<decimal?> LastPostedDepreciationPercentAsync(Guid rpuId, CancellationToken ct)
    {
        var valuationId = await db.Assessments.Where(a => a.RpuId == rpuId && a.Status == WorkflowStatus.Posted)
            .OrderByDescending(a => a.EffectiveDate).ThenByDescending(a => a.PostedAt).Select(a => (Guid?)a.ValuationId).FirstOrDefaultAsync(ct);
        if (valuationId is null)
        {
            return null;
        }
        var breakdowns = await db.ValuationLines.Where(l => l.ValuationId == valuationId).OrderBy(l => l.Sequence).Select(l => l.BreakdownJson).ToListAsync(ct);
        foreach (var json in breakdowns)
        {
            if (JsonSerializer.Deserialize<Dictionary<string, decimal>>(json ?? "{}") is { } b && b.TryGetValue("DepreciationPercent", out var percent))
            {
                return percent;
            }
        }
        return null;
    }

    /// <summary>
    /// Values every machine of the machine's unit, one line each (MRPAAO Att. 3;
    /// LGC §224–225, unchanged per machine). A machine without its own
    /// classification or use is assessed under the unit's Tax Declaration's.
    /// </summary>
    public async Task<Result<ValuationDto>> ComputeForMachineryAsync(Guid machineryId, CancellationToken cancellationToken = default, DateOnly? asOf = null,
        DateOnly? rulesAsOf = null)
    {
        var machinery = await db.MachineryUnits.FirstOrDefaultAsync(x => x.Id == machineryId, cancellationToken);
        if (machinery is null)
        {
            return Result.Failure<ValuationDto>("MACHINERY_NOT_FOUND", "No Machinery record was found with the given id.");
        }
        var machines = await db.MachineryUnits.Include(x => x.CostItems).Where(x => x.RpuId == machinery.RpuId).OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var date = asOf ?? clock.Today;
        var parameters = new MachineryValuationParameters(options.Value.MachineryMinimumRemainingValuePercent!.Value,
            options.Value.MachineryMaximumYearlyDepreciationPercent);
        var lines = new List<(ValuationLine Line, ValuationCalculationResult Calc)>();
        var appraisals = await CurrentAppraisalsAsync(machinery.RpuId, cancellationToken);
        foreach (var machine in machines)
        {
            var name = string.Join(" ", new[] { machine.Brand, machine.Model, machine.SerialNumber }.Where(x => !string.IsNullOrWhiteSpace(x)));
            var machineAppraisal = appraisals.GetValueOrDefault(machine.Id);
            ValuationCalculationResult calc;
            if (machineAppraisal is not null)
            {
                calc = Rounded(AppraisedValue(machineAppraisal, 1m, machineAppraisal.Value));
            }
            else if (!machine.IsBrandNew && machine.PriceIndexSeries is not null)
            {
                var derived = await DeriveMachineryAsync(machine, parameters, date, rulesAsOf ?? date, name.Length > 0 ? name : "A machine", cancellationToken);
                if (derived.IsFailure)
                {
                    return Result.Failure<ValuationDto>(derived.Code!, derived.Message!);
                }
                calc = Rounded(derived.Value);
            }
            else
            {
                if (ValuationCalculator.MissingMachineryInputs(machine) is { } missing)
                {
                    return Result.Failure<ValuationDto>("MACHINERY_VALUATION_INPUTS_MISSING",
                        $"Machinery that is not brand-new is valued from its replacement or reproduction cost and its remaining vs. estimated economic life (LGC §224(a)), or derived from a price index. {(name.Length > 0 ? name : "A machine")} is missing: {missing}.");
                }
                calc = Rounded(ValuationCalculator.CalculateMachinery(machine, parameters));
            }
            var line = new ValuationLine
            {
                Source = ValuationLineSource.Machinery, SourceId = machine.Id,
                ClassificationId = machine.ClassificationId, ActualUseId = machine.ActualUseId,
                Description = string.Join(" ", new[] { machine.Brand, machine.Model, machine.Description }.Where(x => !string.IsNullOrWhiteSpace(x))) is { Length: > 0 } d ? d : null,
            };
            lines.Add((machineAppraisal is null ? line : Appraised(machineAppraisal, line), calc));
            machine.MarketValue = calc.MarketValue;
            if (calc.Breakdown.TryGetValue("DepreciationPercent", out var depreciationPercent))
            {
                machine.Depreciation = depreciationPercent;
            }
        }

        // Valuation.SourceId names the unit's first machine; the lines name each machine.
        var valuation = PersistLines(machinery.RpuId, machinery.PropertyId, ValuationSourceType.Machinery, machines[0].Id, lines, null, null, date);
        valuation.RulesAsOf = rulesAsOf is { } r && r != date ? r : null;
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(await MapAsync(valuation.Id, cancellationToken));
    }

    /// <summary>
    /// The derived replacement cost's inputs for one machine (LAM Bk III pp.73–75; valuation-foundation.md §4.6):
    /// the approved price indices of its series for the acquisition and valuation years and, for imported
    /// machinery, the approved exchange rates on or before the acquisition and valuation dates. Whatever is
    /// missing stops the valuation with the reason.
    /// </summary>
    private async Task<Result<ValuationCalculationResult>> DeriveMachineryAsync(Machinery machine, MachineryValuationParameters parameters, DateOnly date, DateOnly rules,
        string name, CancellationToken ct)
    {
        Result<ValuationCalculationResult> Fail(string code, string message) => Result.Failure<ValuationCalculationResult>(code, $"{name}: {message}");
        if (parameters.MaximumYearlyDepreciationPercent is null)
        {
            return Fail("MACHINERY_DEPRECIATION_LIMIT_NOT_CONFIGURED",
                "the derived replacement cost needs Valuation:MachineryMaximumYearlyDepreciationPercent (LGC §225) configured.");
        }
        if (machine.DateAcquired is not { } acquired || machine.EconomicLifeYears is not > 0)
        {
            return Fail("MACHINERY_VALUATION_INPUTS_MISSING", "the derived replacement cost needs the date acquired and the estimated economic life (years, > 0).");
        }
        var series = machine.PriceIndexSeries!;
        var indices = await db.PriceIndices.AsNoTracking()
            .Where(x => x.Series == series && x.Status == WorkflowStatus.Approved && (x.Year == acquired.Year || x.Year == rules.Year))
            .ToDictionaryAsync(x => x.Year, x => x.Value, ct);
        foreach (var year in new[] { acquired.Year, rules.Year }.Distinct())
        {
            if (!indices.ContainsKey(year))
            {
                return Fail("PRICE_INDEX_NOT_FOUND", $"no approved {series} price index for {year}.");
            }
        }
        decimal? rateAtAcquisition = null, rateAtValuation = null;
        if (machine.IsImported)
        {
            var currency = machine.AcquisitionCurrency!;
            async Task<decimal?> RateOn(DateOnly d) => await db.ExchangeRates.AsNoTracking()
                .Where(x => x.Currency == currency && x.Status == WorkflowStatus.Approved && x.RateDate <= d)
                .OrderByDescending(x => x.RateDate).Select(x => (decimal?)x.PesosPerUnit).FirstOrDefaultAsync(ct);
            rateAtAcquisition = await RateOn(acquired);
            rateAtValuation = await RateOn(rules);
            if (rateAtAcquisition is null || rateAtValuation is null)
            {
                return Fail("EXCHANGE_RATE_NOT_FOUND",
                    $"no approved {currency} exchange rate on or before {(rateAtAcquisition is null ? acquired : rules):yyyy-MM-dd}.");
            }
        }
        // Cost, insurance and freight is converted and trended; the other expenses are added at their recorded cost.
        var freightInsurance = machine.CostItems.Where(i => i.Kind is MachineryCostItemKind.Freight or MachineryCostItemKind.Insurance).Sum(i => i.Amount);
        var otherItems = machine.CostItems.Where(i => i.Kind is not (MachineryCostItemKind.Freight or MachineryCostItemKind.Insurance)).Sum(i => i.Amount);
        var input = new MachineryDerivationInput(
            machine.AcquisitionCost + freightInsurance, (machine.InstallationCost ?? 0m) + (machine.OtherCost ?? 0m) + otherItems,
            rateAtAcquisition, rateAtValuation, indices[acquired.Year], indices[rules.Year],
            ValuationCalculator.YearsBetween(machine.DateInstalled ?? acquired, date), machine.EconomicLifeYears.Value, machine.IsInOperation);
        return Result.Success(ValuationCalculator.CalculateMachineryDerived(input, parameters));
    }

    /// <summary>
    /// Values the whole unit — every row it holds — in one valuation
    /// (docs/analysis/mrpaao-forms-model.md §8.4). Used by general revision.
    /// </summary>
    public async Task<Result<ValuationDto>> ComputeForRpuAsync(Guid rpuId, CancellationToken cancellationToken = default, DateOnly? asOf = null,
        Guid? transactionTypeId = null, bool generalRevision = false, DateOnly? buildingRulesAsOf = null, DateOnly? machineryRulesAsOf = null)
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
                    : await ComputeForLandAsync(land.Value, cancellationToken, asOf);
            case RpuType.Building:
                var building = await db.Buildings.Where(x => x.RpuId == rpuId).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);
                return building is null
                    ? Result.Failure<ValuationDto>("BUILDING_NOT_FOUND", "No Building record exists for this RPU.")
                    : await ComputeForBuildingAsync(building.Value, cancellationToken, asOf, transactionTypeId, generalRevision, buildingRulesAsOf);
            case RpuType.OtherImprovement:
                return await ComputeForSeparateImprovementsAsync(rpu, cancellationToken, asOf);
            case RpuType.Machinery:
                var machinery = await db.MachineryUnits.Where(x => x.RpuId == rpuId).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);
                return machinery is null
                    ? Result.Failure<ValuationDto>("MACHINERY_NOT_FOUND", "No Machinery record exists for this RPU.")
                    : await ComputeForMachineryAsync(machinery.Value, cancellationToken, asOf, machineryRulesAsOf);
            default:
                return Result.Failure<ValuationDto>("UNSUPPORTED_RPU_TYPE", $"RPU type '{rpu.RpuType}' is not yet valuable.");
        }
    }

    public async Task<Result<ValuationDto>> GetByIdAsync(Guid valuationId, CancellationToken cancellationToken = default)
    {
        var valuation = await WithDetails().FirstOrDefaultAsync(x => x.Id == valuationId, cancellationToken);
        return valuation is null
            ? Result.Failure<ValuationDto>("VALUATION_NOT_FOUND", "No Valuation was found with the given id.")
            : Result.Success(ToDto(valuation));
    }

    public async Task<Result<IReadOnlyList<ValuationDto>>> ListByRpuAsync(Guid rpuId, CancellationToken cancellationToken = default)
    {
        var valuations = await WithDetails()
            .Where(x => x.RpuId == rpuId)
            .OrderByDescending(x => x.ComputedAt)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ValuationDto>>(
            valuations.Select(ToDto).ToList());
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

    /// <summary>The municipality and barangay the property lies in: which SMV covers it, and barangay rates.</summary>
    private async Task<(Guid? MunicipalityId, Guid? BarangayId)> LocationAsync(Guid propertyId, CancellationToken ct)
    {
        var p = await db.Properties.Where(x => x.Id == propertyId).Select(x => new { x.MunicipalityId, x.BarangayId }).FirstOrDefaultAsync(ct);
        return (p?.MunicipalityId, p?.BarangayId);
    }

    /// <summary>
    /// The rate for a row: approved, in force on <paramref name="asOf"/>, under an approved SMV in
    /// force then that covers <paramref name="municipalityId"/>, chosen by <see cref="SmvRateSelector"/>
    /// (docs/analysis/valuation-foundation.md §4.3). <paramref name="improvementKindId"/> null selects
    /// land and building rates only; set, the rate for that improvement kind.
    /// </summary>
    private async Task<SmvSchedule?> ResolveScheduleAsync(Guid classificationId, Guid propertyTypeId, Guid? improvementKindId, Guid? municipalityId,
        SmvRateKey key, DateOnly asOf, CancellationToken cancellationToken)
    {
        var candidates = await db.SmvSchedules.Include(x => x.Smv)
            .Where(x => x.ImprovementKindId == improvementKindId
                && x.ClassificationId == classificationId
                && x.PropertyTypeId == propertyTypeId
                && x.Status == WorkflowStatus.Approved
                && x.EffectiveDate <= asOf
                // EndDate is inclusive: superseding a schedule ends it the day before its successor (SmvService).
                && (x.EndDate == null || x.EndDate >= asOf)
                && x.Smv!.Status == WorkflowStatus.Approved
                && x.Smv.EffectivityDate <= asOf
                // No coverage rows: the SMV covers the whole province.
                && (!x.Smv.Coverage.Any() || x.Smv.Coverage.Any(c => c.MunicipalityId == municipalityId)))
            .ToListAsync(cancellationToken);
        return SmvRateSelector.Select(candidates, key);
    }

    private IQueryable<Prime.Domain.Entities.Valuation> WithDetails() => db.Valuations.AsNoTracking()
        .Include(x => x.Smv)
        .Include(x => x.Lines).ThenInclude(l => l.Classification)
        .Include(x => x.Lines).ThenInclude(l => l.SubClassification)
        .Include(x => x.Lines).ThenInclude(l => l.ActualUse)
        .Include(x => x.Lines).ThenInclude(l => l.PricedClassification)
        .Include(x => x.Lines).ThenInclude(l => l.PricedSubClassification);

    private async Task<ValuationDto> MapAsync(Guid valuationId, CancellationToken cancellationToken) =>
        ToDto(await WithDetails().SingleAsync(x => x.Id == valuationId, cancellationToken));

    private static ValuationDto ToDto(Prime.Domain.Entities.Valuation v) => new(
        v.Id,
        v.RpuId,
        v.PropertyId,
        v.SourceType,
        v.SourceId,
        v.SmvId,
        v.SmvScheduleId,
        v.ValuationMethod,
        v.ComputedMarketValue,
        JsonSerializer.Deserialize<Dictionary<string, decimal>>(v.BreakdownJson) ?? [],
        v.EffectiveDate,
        v.ComputedAt,
        v.Lines.OrderBy(l => l.Sequence).Select(l => new ValuationLineDto(
            l.Sequence, l.Source, l.SourceId, l.Description, l.Classification?.Name, l.SubClassification?.Name, l.ActualUse?.Name,
            l.Quantity, l.Unit, l.UnitValue, l.SmvScheduleId, l.MarketValue, ValuationBreakdown.Ordered(l.BreakdownJson),
            l.PricedClassification?.Name, l.PricedSubClassification?.Name, l.IndependentAppraisalId)).ToList(),
        v.Smv?.Reference,
        v.Smv?.RevisionYear,
        v.RulesAsOf);
}
