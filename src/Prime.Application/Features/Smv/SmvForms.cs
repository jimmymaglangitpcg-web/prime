using Microsoft.EntityFrameworkCore;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Forms;
using Prime.Application.Features.SmvSimulations;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Smv;

/// <summary>
/// The SMV's own forms (LAM 2025 Book IV pp.114–115, Annexes IV-A, IV-E, IV-I to IV-L; docs/analysis/smv-preparation-general-revision.md
/// §4.2): Form 1 sub-class criteria, Form 5 land values by sub-class and location, Form 9 agricultural land values by crop, Form 10
/// construction costs, Form 11 depreciation, Form 12 extra items. The subject is the SMV; its open rows of every status are listed
/// with their status, so a proposed SMV prints as proposed. Land rows given per hectare print on Form 9, the others on Form 5
/// (by the row's unit; no class is singled out by PRIME).
/// </summary>
public sealed class SmvFormDataProvider(IApplicationDbContext db) : IFormDataProvider
{
    public FormSubjectType SubjectType => FormSubjectType.Smv;

    public async Task<FormSubjectData?> BuildAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var ct = cancellationToken;
        var smv = await db.Smvs.AsNoTracking().Include(x => x.Coverage).ThenInclude(c => c.Municipality).ThenInclude(m => m!.Province)
            .FirstOrDefaultAsync(x => x.Id == subjectId, ct);
        if (smv is null)
        {
            return null;
        }
        static bool Live(WorkflowStatus s) => s is not (WorkflowStatus.Rejected or WorkflowStatus.Cancelled or WorkflowStatus.Voided);
        var criteria = await db.SmvSubClassCriteria.AsNoTracking().Where(x => x.SmvId == smv.Id)
            .OrderBy(x => x.Classification!.Name).ThenBy(x => x.Sequence)
            .Select(x => new { classification = x.Classification!.Name, subClass = x.SubClassification!.Name, x.Sequence, x.Criteria })
            .ToListAsync(ct);
        // Each line of a criterion's text, for layouts that number them.
        var criteriaLines = criteria.Select(c => new
        {
            c.classification, c.subClass, c.Sequence, c.Criteria,
            lines = c.Criteria.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
        }).ToList();
        var rows = (await db.SmvSchedules.AsNoTracking().Where(x => x.SmvId == smv.Id && x.EndDate == null)
                .Select(x => new RateRow(
                    x.Classification!.Name, x.SubClassification != null ? x.SubClassification.Name : null, x.ActualUse != null ? x.ActualUse.Name : null,
                    x.Zone != null ? x.Zone.Name : null, x.Barangay != null ? x.Barangay.Name : null, x.ImprovementKind != null ? x.ImprovementKind.Name : null,
                    x.LocationDescription, x.CropDescription, x.Unit, x.MarketValue, x.MinimumValue, x.MaximumValue, x.EffectiveDate, x.Status))
                .ToListAsync(ct))
            .Where(x => Live(x.Status)).ToList();
        static object Row(RateRow x) => new
        {
            classification = x.Classification, subClass = x.SubClass, use = x.Use, zone = x.Zone, barangay = x.Barangay, location = x.Location,
            crop = x.Crop, unit = x.Unit, value = x.Value, minimum = x.Minimum, maximum = x.Maximum, effectiveDate = x.EffectiveDate, status = x.Status.ToString(),
        };
        var land = rows.Where(x => x.Kind is null).OrderBy(x => x.Classification).ThenByDescending(x => x.Value).ToList();
        var buildingCosts = (await db.SmvBuildingCosts.AsNoTracking().Where(x => x.SmvId == smv.Id && x.EndDate == null)
                .Select(x => new
                {
                    Structure = x.StructuralType!.Name, Kind = x.BuildingType != null ? x.BuildingType.Name : null,
                    Classification = x.Classification != null ? x.Classification.Name : null, x.CostPerSquareMetre, x.Status,
                }).ToListAsync(ct))
            .Where(x => Live(x.Status)).OrderBy(x => x.Kind).ThenBy(x => x.Structure).ToList();
        var depreciation = (await db.SmvDepreciationSchedules.AsNoTracking().Include(x => x.StructuralType).Include(x => x.Rows)
                .Where(x => x.SmvId == smv.Id && x.EndDate == null).ToListAsync(ct))
            .Where(x => Live(x.Status)).OrderBy(x => x.StructuralType!.Name).ToList();
        var extras = (await db.SmvExtraItemCosts.AsNoTracking().Where(x => x.SmvId == smv.Id && x.EndDate == null)
                .Select(x => new { Item = x.ComponentType!.Name, x.Unit, x.UnitCost, x.Status }).ToListAsync(ct))
            .Where(x => Live(x.Status)).OrderBy(x => x.Item).ToList();
        var preparation = await db.SmvPreparations.AsNoTracking().Where(x => x.ProposedSmvId == smv.Id)
            .Select(x => new { x.Title, x.DateOfValuation, x.BaseValuationDate, Status = x.Status.ToString(), Value = x.Status }).FirstOrDefaultAsync(ct);
        var preparationStatus = preparation?.Value;

        var agricultural = land.Where(x => ValuationTestService.RateUnitOf(x.Unit) == AreaMeasure.Hectare).ToList();
        // Form 9 as a matrix: each kind of land (the row's use) across its sub-classes, in sub-class order.
        var agriClasses = agricultural.Select(x => x.SubClass ?? "").Distinct().Order(StringComparer.Ordinal).ToList();
        var agriMatrix = new
        {
            classes = agriClasses,
            rows = agricultural.GroupBy(x => x.Use ?? x.Classification).OrderBy(g => g.Key)
                .Select(g => new { kind = g.Key, values = agriClasses.Select(c => g.FirstOrDefault(x => (x.SubClass ?? "") == c)?.Value).ToList() }).ToList(),
            productivity = agricultural.Where(x => x.Crop is not null).OrderBy(x => x.Use).ThenBy(x => x.SubClass)
                .Select(x => new { kind = x.Use ?? x.Classification, subClass = x.SubClass, crop = x.Crop }).ToList(),
        };
        // Form 10 as a matrix: structural types down, building designs (kinds) across; a cost for one classification is noted.
        var designs = buildingCosts.Select(x => x.Kind ?? "Any").Distinct().Order(StringComparer.Ordinal).ToList();
        var buildingMatrix = new
        {
            designs,
            rows = buildingCosts.GroupBy(x => x.Structure).OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new
                {
                    structure = g.Key,
                    values = designs.Select(d => string.Join("; ", g.Where(x => (x.Kind ?? "Any") == d)
                        .Select(x => x.CostPerSquareMetre.ToString("#,0.00", System.Globalization.CultureInfo.InvariantCulture)
                            + (x.Classification is null ? "" : $" ({x.Classification})")))).ToList(),
                }).ToList(),
        };
        // Form 11 as a matrix: structural types down, the age bands of every table across, then the minimum remaining value.
        var bands = depreciation.SelectMany(x => x.Rows).Select(r => (r.FromAge, r.ToAge)).Distinct().OrderBy(b => b.FromAge).ThenBy(b => b.ToAge ?? int.MaxValue).ToList();
        var depreciationMatrix = new
        {
            bands = bands.Select(b => new { from = b.FromAge, to = b.ToAge }).ToList(),
            rows = depreciation.Select(x => new
            {
                structure = x.StructuralType!.Name, reading = x.Reading.ToString(), minimumRemaining = x.MinimumRemainingPercent,
                values = bands.Select(b => x.Rows.FirstOrDefault(r => r.FromAge == b.FromAge && r.ToAge == b.ToAge)?.Percent).ToList(),
            }).ToList(),
        };

        var data = FormData.ToJson(new
        {
            agriMatrix,
            buildingMatrix,
            depreciationMatrix,
            smv = new
            {
                reference = smv.Reference, basis = smv.Basis.ToString(), revisionYear = smv.RevisionYear, status = smv.Status.ToString(),
                proposed = smv.Status != WorkflowStatus.Approved, effectivityDate = smv.EffectivityDate, description = smv.Description,
                certifiedOn = smv.CertifiedOn, certificationReference = smv.CertificationReference, publishedOn = smv.PublishedOn,
                coverage = smv.Coverage.Select(c => c.Municipality?.Name ?? "").Order().ToList(),
                province = smv.Coverage.Select(c => c.Municipality?.Province?.Name).FirstOrDefault(n => n is not null),
                preparation,
            },
            criteria = criteriaLines,
            land = land.Where(x => ValuationTestService.RateUnitOf(x.Unit) != AreaMeasure.Hectare).Select(x => Row(x)).ToList(),
            agricultural = land.Where(x => ValuationTestService.RateUnitOf(x.Unit) == AreaMeasure.Hectare).Select(x => Row(x)).ToList(),
            improvements = rows.Where(x => x.Kind is not null).OrderBy(x => x.Kind)
                .Select(x => new { kind = x.Kind, classification = x.Classification, use = x.Use, unit = x.Unit, value = x.Value, status = x.Status.ToString() }).ToList(),
            buildingCosts = buildingCosts.Select(x => new
            {
                structure = x.Structure, kind = x.Kind, classification = x.Classification, cost = x.CostPerSquareMetre, status = x.Status.ToString(),
            }).ToList(),
            depreciation = depreciation.Select(x => new
            {
                structure = x.StructuralType!.Name, reading = x.Reading.ToString(), minimumRemaining = x.MinimumRemainingPercent, status = x.Status.ToString(),
                rows = x.Rows.OrderBy(r => r.Sequence).Select(r => new { from = r.FromAge, to = r.ToAge, percent = r.Percent, remaining = 100m - r.Percent }).ToList(),
            }).ToList(),
            extraItems = extras.Select(x => new { item = x.Item, unit = x.Unit, cost = x.UnitCost, status = x.Status.ToString() }).ToList(),
        });
        // A frozen copy is the SMV as submitted (or as approved, outside a preparation); while it is being prepared it is previewed.
        var blocker = preparationStatus is { } s
            ? SmvFormIssue.Blocker(s)
            : smv.Status == WorkflowStatus.Approved ? null : "The SMV's forms are issued once the SMV is approved; until then they are previewed.";
        return new FormSubjectData(null, data, blocker);
    }

    private sealed record RateRow(string Classification, string? SubClass, string? Use, string? Zone, string? Barangay, string? Kind, string? Location,
        string? Crop, string Unit, decimal Value, decimal? Minimum, decimal? Maximum, DateOnly EffectiveDate, WorkflowStatus Status);
}

/// <summary>
/// The forms of a sales analysis (SMV Forms 2–4 for residential, commercial and industrial land, 6–8 for agricultural land; Annexes
/// IV-B to IV-D, IV-F to IV-H): the statement of sales, their tabulation, and the computation of the unit values — Tables 1 to 3 as
/// the analysis computes them (<see cref="ISalesAnalysisService"/>; no second calculation).
/// </summary>
public sealed class SalesAnalysisFormDataProvider(IApplicationDbContext db, ISalesAnalysisService analyses) : IFormDataProvider
{
    public FormSubjectType SubjectType => FormSubjectType.SalesAnalysis;

    public async Task<FormSubjectData?> BuildAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var result = await analyses.GetAsync(subjectId, cancellationToken);
        if (result.IsFailure)
        {
            return null;
        }
        var a = result.Value;
        var smv = await db.SmvPreparations.AsNoTracking().Where(x => x.Id == a.SmvPreparationId)
            .Select(x => new { x.RevisionYear, x.Title, x.DateOfValuation, Reference = x.ProposedSmv!.CertificationReference }).FirstAsync(cancellationToken);
        // From each sale's record: its document's file number and its kind (use or crop), as SMV Forms 2 and 6 list them.
        var ids = a.Sales.Select(s => s.MarketTransactionId).ToList();
        var recorded = await db.MarketTransactions.AsNoTracking().Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.DocumentFileNumber, Kind = x.ActualUse != null ? x.ActualUse.Name : null }).ToDictionaryAsync(x => x.Id, cancellationToken);
        var saleOf = a.Sales.ToDictionary(s => s.Id);
        var data = FormData.ToJson(new
        {
            analysis = new
            {
                classification = a.ClassificationName, use = a.ActualUseName, unit = a.AreaUnit == AreaMeasure.Hectare ? "per hectare" : "per sqm",
                areaUnit = a.AreaUnit == AreaMeasure.Hectare ? "ha" : "sqm", marketAreas = a.Municipalities, salesFrom = a.SalesFrom, salesTo = a.SalesTo,
                rounding = a.RoundingIncrement, width = a.EffectiveWidthPercent, widthEntered = a.RangeWidthPercent is not null,
                averageInterval = a.AverageIntervalPercent, baseValuationDate = a.BaseValuationDate, notes = a.Notes,
                revisionYear = smv.RevisionYear, preparation = smv.Title, dateOfValuation = smv.DateOfValuation, smv = smv.Reference,
                warnings = a.Warnings,
            },
            sales = a.Sales.Select(s => new
            {
                date = s.TransactionDate, location = string.Join(", ", new[] { s.Location, s.BarangayName }.Where(x => !string.IsNullOrEmpty(x))),
                td = s.TaxDeclarationNumber, pin = s.Pin, subClass = s.SubClassificationName, area = s.Area, price = s.Price, unitPrice = s.UnitPrice,
                factor = s.TimeFactor, other = s.OtherAdjustmentPercent, adjusted = s.AdjustedUnitPrice, rounded = s.RoundedUnitValue,
                excluded = s.ExclusionReason, leftOut = s.LeftOut, monthYear = s.TransactionDate.ToString("MMM yyyy", System.Globalization.CultureInfo.InvariantCulture),
                fileNumber = recorded.GetValueOrDefault(s.MarketTransactionId)?.DocumentFileNumber, kind = recorded.GetValueOrDefault(s.MarketTransactionId)?.Kind,
            }).ToList(),
            values = a.Values.Select(v => new
            {
                number = v.Number, price = saleOf.GetValueOrDefault(v.SaleId)?.Price, area = saleOf.GetValueOrDefault(v.SaleId)?.Area,
                date = saleOf.GetValueOrDefault(v.SaleId)?.TransactionDate, td = saleOf.GetValueOrDefault(v.SaleId)?.TaxDeclarationNumber,
                pin = saleOf.GetValueOrDefault(v.SaleId)?.Pin,
                unitPrice = saleOf.GetValueOrDefault(v.SaleId)?.UnitPrice, adjusted = v.AdjustedUnitPrice, rounded = v.RoundedUnitValue, interval = v.IntervalPercent,
            }).ToList(),
            ranges = a.Ranges.Select(r => new
            {
                number = r.Number, low = r.Low, mid = r.Mid, high = r.High, frequency = r.Frequency,
                subClass = a.Groups.FirstOrDefault(g => g.Id == r.GroupId)?.SubClassificationName,
            }).ToList(),
            groups = a.Groups.Select(g => new
            {
                sequence = g.Sequence, subClass = g.SubClassificationName, from = g.FromValue, to = g.ToValue, sales = g.Frequency, proposed = g.ProposedValue,
                adopted = g.AdoptedValue, adoptedAt = g.AdoptedAt, basis = g.Basis,
            }).ToList(),
        });
        return new FormSubjectData(null, data, SmvFormIssue.Blocker(a.PreparationStatus));
    }
}

/// <summary>When the SMV forms of a preparation may be issued (frozen): once submitted, as submitted; after a remand, again once resubmitted.</summary>
public static class SmvFormIssue
{
    public static string? Blocker(Domain.Entities.SmvPreparationStatus status) =>
        status is Domain.Entities.SmvPreparationStatus.Preparing or Domain.Entities.SmvPreparationStatus.PublishedForComment
            or Domain.Entities.SmvPreparationStatus.Remanded or Domain.Entities.SmvPreparationStatus.Cancelled
            ? "The SMV is still being prepared: its forms are previewed, and issued as submitted once the SMV is submitted (after a remand, cancel the earlier copy and issue again once resubmitted)."
            : null;
}
