using Microsoft.EntityFrameworkCore;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.LevyRates;
using Prime.Application.Features.ReportConfiguration;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Reports;

/// <summary>
/// The quarterly report on real property assessments (QRRPA; LAM 2025 Book I pp.24–25, Annex I-P; docs/analysis/reporting.md
/// §10, Q15–Q18, Q20): the FAAS in force at the quarter's end, in the rows of the approved row map (taxable by
/// classification, exempt by kind of exemption, properties with restrictions by group and classification, idle lands),
/// with land area, RPUs, market and assessed values by kind, the levy rates in force on the quarter's last day and the
/// collectibles. The year-end report is the fourth quarter's (Q20). The columns are PRIME's provisional layout; the
/// LAM's comes in as content (CLAUDE.md §118). Collectibles are report figures only: PRIME bills nothing (CLAUDE.md §0).
/// </summary>
public sealed class QuarterlyAssessmentReport(
    IApplicationDbContext db,
    IFaasInForceQuery faasInForce,
    IReportRowMapService rowMaps,
    ISystemParameterService parameters,
    ILevyRateService levyRates) : IReport
{
    public string Code => "QRRPA";
    public string Title => "Quarterly report on real property assessments (QRRPA)";
    public string Group => "BLGF";

    public string Description =>
        "The FAAS in force at the quarter's end in the configured rows: land area, RPUs, market and assessed values by kind, the levy rates in force and the collectibles. Provisional layout.";

    public IReadOnlyList<ReportParameter> Parameters { get; } = [ReportParameter.Quarter, ReportParameter.Municipality];

    public IReadOnlyList<ReportColumn> Columns { get; } =
    [
        new("group", "Group", ReportColumnType.Text),
        new("row", "Classification", ReportColumnType.Text),
        new("landArea", "Land area (sq m)", ReportColumnType.Area),
        new("rpuLand", "RPUs: land", ReportColumnType.Integer),
        new("rpuBuilding", "RPUs: building", ReportColumnType.Integer),
        new("rpuMachinery", "RPUs: machinery", ReportColumnType.Integer),
        new("rpuOther", "RPUs: other improvements", ReportColumnType.Integer),
        new("rpuTotal", "RPUs: total", ReportColumnType.Integer),
        new("mvLand", "Market value: land", ReportColumnType.Money),
        new("mvBuildingUpTo", "Market value: building at or below the threshold", ReportColumnType.Money),
        new("mvBuildingOver", "Market value: building over the threshold", ReportColumnType.Money),
        new("mvBuilding", "Market value: building", ReportColumnType.Money),
        new("mvMachinery", "Market value: machinery", ReportColumnType.Money),
        new("mvOther", "Market value: other improvements", ReportColumnType.Money),
        new("mvTotal", "Market value: total", ReportColumnType.Money),
        new("avLand", "Assessed value: land", ReportColumnType.Money),
        new("avBuilding", "Assessed value: building", ReportColumnType.Money),
        new("avMachinery", "Assessed value: machinery", ReportColumnType.Money),
        new("avOther", "Assessed value: other improvements", ReportColumnType.Money),
        new("avTotal", "Assessed value: total", ReportColumnType.Money),
        new("rateBasic", "Rate of levy: basic (%)", ReportColumnType.Percent),
        new("rateSef", "Rate of levy: SEF (%)", ReportColumnType.Percent),
        new("rateIdle", "Rate of levy: idle land (%)", ReportColumnType.Percent),
        new("collectBasic", "Collectibles: basic", ReportColumnType.Money),
        new("collectSef", "Collectibles: SEF", ReportColumnType.Money),
        new("collectTotal", "Collectibles: total", ReportColumnType.Money),
    ];

    /// <summary>The sums of one row of the report.</summary>
    private sealed class Line(string group, string label, bool splitsBuildings, bool exempt, bool idle = false)
    {
        public string Group { get; } = group;
        public string Label { get; } = label;
        public bool SplitsBuildings { get; } = splitsBuildings;
        public bool Exempt { get; } = exempt;
        public bool Idle { get; } = idle;
        public decimal LandArea;
        public int RpuLand, RpuBuilding, RpuMachinery, RpuOther;
        public decimal MvLand, MvBuildingUpTo, MvBuildingOver, MvBuilding, MvMachinery, MvOther;
        public decimal AvLand, AvBuilding, AvMachinery, AvOther;
        public decimal CollectBasic, CollectSef;
        public readonly HashSet<decimal> BasicRates = [], SefRates = [];

        public void Add(Line x)
        {
            LandArea += x.LandArea;
            (RpuLand, RpuBuilding, RpuMachinery, RpuOther) = (RpuLand + x.RpuLand, RpuBuilding + x.RpuBuilding, RpuMachinery + x.RpuMachinery, RpuOther + x.RpuOther);
            (MvLand, MvBuildingUpTo, MvBuildingOver, MvBuilding) = (MvLand + x.MvLand, MvBuildingUpTo + x.MvBuildingUpTo, MvBuildingOver + x.MvBuildingOver, MvBuilding + x.MvBuilding);
            (MvMachinery, MvOther) = (MvMachinery + x.MvMachinery, MvOther + x.MvOther);
            (AvLand, AvBuilding, AvMachinery, AvOther) = (AvLand + x.AvLand, AvBuilding + x.AvBuilding, AvMachinery + x.AvMachinery, AvOther + x.AvOther);
            (CollectBasic, CollectSef) = (CollectBasic + x.CollectBasic, CollectSef + x.CollectSef);
            BasicRates.UnionWith(x.BasicRates);
            SefRates.UnionWith(x.SefRates);
        }

        public object?[] Cells(string? group = null, string? label = null)
        {
            if (Idle)
            {
                return [group ?? Group, label ?? Label, .. Enumerable.Repeat<object?>(null, 24)];
            }
            var mvTotal = MvLand + MvBuildingUpTo + MvBuildingOver + MvBuilding + MvMachinery + MvOther;
            return
            [
                group ?? Group, label ?? Label, LandArea, RpuLand, RpuBuilding, RpuMachinery, RpuOther, RpuLand + RpuBuilding + RpuMachinery + RpuOther,
                MvLand, SplitsBuildings ? MvBuildingUpTo : null, SplitsBuildings ? MvBuildingOver : null, MvBuilding, MvMachinery, MvOther, mvTotal,
                AvLand, AvBuilding, AvMachinery, AvOther, AvLand + AvBuilding + AvMachinery + AvOther,
                Exempt || BasicRates.Count != 1 ? null : BasicRates.Single(), Exempt || SefRates.Count != 1 ? null : SefRates.Single(), null,
                Exempt ? null : CollectBasic, Exempt ? null : CollectSef, Exempt ? null : CollectBasic + CollectSef,
            ];
        }
    }

    public async Task<ReportRows> RunAsync(ReportScope scope, ReportWindow window, CancellationToken cancellationToken)
    {
        var asOf = scope.AsOf;
        var map = await rowMaps.InForceAsync(ReportRowMapCodes.Qrrpa, asOf, cancellationToken);
        var threshold = await parameters.InForceAsync(SystemParameterCatalog.QrrpaResidentialBuildingThreshold, asOf, cancellationToken);
        var rates = await levyRates.InForceAsync(asOf, cancellationToken);

        var classes = await db.Classifications.AsNoTracking().Select(c => new { c.Id, c.Code, c.Name, c.SortOrder }).ToListAsync(cancellationToken);
        var uses = await db.ActualUses.AsNoTracking().Select(u => new { u.Id, u.Code }).ToDictionaryAsync(u => u.Id, u => u.Code, cancellationToken);
        var exemptionTypes = await db.ExemptionTypes.AsNoTracking().Select(t => new { t.Id, t.Code, t.Name, t.EffectiveDate })
            .ToListAsync(cancellationToken);
        var annotationTypes = await db.AnnotationTypes.AsNoTracking().Select(a => new { a.Id, a.Code }).ToDictionaryAsync(a => a.Code, a => a.Id, cancellationToken);
        var classCode = classes.ToDictionary(c => c.Id, c => c.Code);
        var exemptionCode = exemptionTypes.ToDictionary(t => t.Id, t => t.Code);

        var definition = map?.Definition;
        var groups = definition?.Restrictions ?? [];
        // Restriction annotation types in the map's order, and the group each puts a unit in.
        var restrictionOf = new Dictionary<Guid, RestrictionGroupSpec>();
        foreach (var g in groups)
        {
            foreach (var code in g.AnnotationTypes)
            {
                if (annotationTypes.TryGetValue(code, out var id))
                {
                    restrictionOf.TryAdd(id, g);
                }
            }
        }

        var parts = await faasInForce.PartsAsync(new FaasScope(asOf, scope.MunicipalityId), restrictionOf.Keys.ToList(),
            threshold?.Value, cancellationToken);

        // The report's lines: the map's rows (else PRIME's classifications and exemption types), then a line per section
        // for what no row takes.
        var lines = new List<(ReportRowSection Section, string? Restriction, ReportRowSpec? Spec, Line Line)>();
        const string TaxableName = "Taxable", ExemptName = "Exempt", IdleName = "Idle lands";
        if (definition is not null)
        {
            foreach (var spec in definition.Rows)
            {
                var groupName = spec.Section switch
                {
                    ReportRowSection.Taxable => TaxableName,
                    ReportRowSection.Exempt => ExemptName,
                    ReportRowSection.IdleLand => IdleName,
                    _ => groups.First(g => g.Code == spec.Restriction).Label,
                };
                lines.Add((spec.Section, spec.Restriction, spec,
                    new Line(groupName, spec.Label, spec.SplitsBuildings && threshold is not null, spec.Section == ReportRowSection.Exempt,
                        spec.Section == ReportRowSection.IdleLand)));
            }
        }

        var unmapped = 0;
        Line LineFor(FaasPart part, out bool mapped)
        {
            mapped = true;
            var section = part.Exempt ? ReportRowSection.Exempt
                : part.RestrictionTypeId is { } r && restrictionOf.ContainsKey(r) ? ReportRowSection.Restricted : ReportRowSection.Taxable;
            var restriction = section == ReportRowSection.Restricted ? restrictionOf[part.RestrictionTypeId!.Value].Code : null;
            var cls = classCode.GetValueOrDefault(part.ClassificationId);
            var use = uses.GetValueOrDefault(part.ActualUseId);
            var exemption = part.ExemptionTypeId is { } e ? exemptionCode.GetValueOrDefault(e) : null;
            if (definition is not null)
            {
                var candidates = lines.Where(l => l.Section == section && l.Restriction == restriction && l.Spec is not null).ToList();
                var match = candidates.FirstOrDefault(l => !l.Spec!.Others
                        && Matches(l.Spec.Classifications, cls) && Matches(l.Spec.ActualUses, use) && Matches(l.Spec.ExemptionTypes, exemption))
                    .Line ?? candidates.FirstOrDefault(l => l.Spec!.Others).Line;
                if (match is not null)
                {
                    return match;
                }
                mapped = false;
            }
            // Without a map: PRIME's own rows; with one, a line for what the map does not take.
            var label = !mapped ? "Not in the row map"
                : section == ReportRowSection.Exempt
                    ? exemptionTypes.Where(t => t.Id == part.ExemptionTypeId).Select(t => t.Name).FirstOrDefault() ?? "Exemption not recorded"
                    : classes.FirstOrDefault(c => c.Id == part.ClassificationId)?.Name ?? "Classification not recorded";
            var groupName = section switch
            {
                ReportRowSection.Exempt => ExemptName,
                ReportRowSection.Restricted => restrictionOf[part.RestrictionTypeId!.Value].Label,
                _ => TaxableName,
            };
            var existing = lines.FirstOrDefault(l => l.Spec is null && l.Section == section && l.Restriction == restriction && l.Line.Label == label).Line;
            if (existing is not null)
            {
                return existing;
            }
            var line = new Line(groupName, label, false, section == ReportRowSection.Exempt);
            lines.Add((section, restriction, null, line));
            return line;
        }

        var missingRates = new HashSet<(Guid, Guid)>();
        foreach (var part in parts)
        {
            var line = LineFor(part, out var mapped);
            if (!mapped)
            {
                unmapped += part.Units;
            }
            var kind = Enum.TryParse<RpuType>(part.Kind, out var k) ? k : RpuType.OtherImprovement;
            switch (kind)
            {
                case RpuType.Land:
                    line.LandArea += part.LandAreaSqm;
                    line.RpuLand += part.Units;
                    line.MvLand += part.MarketValue;
                    line.AvLand += part.AssessedValue;
                    break;
                case RpuType.Building:
                    line.RpuBuilding += part.Units;
                    if (line.SplitsBuildings)
                    {
                        if (part.OverThreshold)
                        {
                            line.MvBuildingOver += part.MarketValue;
                        }
                        else
                        {
                            line.MvBuildingUpTo += part.MarketValue;
                        }
                    }
                    else
                    {
                        line.MvBuilding += part.MarketValue;
                    }
                    line.AvBuilding += part.AssessedValue;
                    break;
                case RpuType.Machinery:
                    line.RpuMachinery += part.Units;
                    line.MvMachinery += part.MarketValue;
                    line.AvMachinery += part.AssessedValue;
                    break;
                default:
                    line.RpuOther += part.Units;
                    line.MvOther += part.MarketValue;
                    line.AvOther += part.AssessedValue;
                    break;
            }
            if (!part.Exempt)
            {
                // Q18: taxable assessed value × the rate in force, to the centavo, per municipality and classification.
                var basic = rates.Find(LevyKind.Basic, part.MunicipalityId, part.ClassificationId);
                var sef = rates.Find(LevyKind.SpecialEducationFund, part.MunicipalityId, part.ClassificationId);
                if (basic is null || sef is null)
                {
                    missingRates.Add((part.MunicipalityId, part.ClassificationId));
                }
                if (basic is not null)
                {
                    line.BasicRates.Add(basic.RatePercent);
                    line.CollectBasic += Math.Round(part.AssessedValue * basic.RatePercent / 100m, 2, MidpointRounding.AwayFromZero);
                }
                if (sef is not null)
                {
                    line.SefRates.Add(sef.RatePercent);
                    line.CollectSef += Math.Round(part.AssessedValue * sef.RatePercent / 100m, 2, MidpointRounding.AwayFromZero);
                }
            }
        }

        // Without a map, PRIME's rows in the classifications' and exemption types' order.
        if (definition is null)
        {
            lines = lines.OrderBy(l => l.Section).ThenBy(l => l.Restriction)
                .ThenBy(l => classes.FirstOrDefault(c => c.Name == l.Line.Label)?.SortOrder ?? int.MaxValue)
                .ThenBy(l => l.Line.Label, StringComparer.OrdinalIgnoreCase).ToList();
        }

        var rows = new List<object?[]>();
        var grand = new Line("Total", "", false, false);
        void Section(ReportRowSection section, string totalLabel)
        {
            var inSection = lines.Where(l => l.Section == section).ToList();
            if (inSection.Count == 0)
            {
                return;
            }
            var total = new Line(section == ReportRowSection.Restricted ? "With restrictions" : inSection[0].Line.Group, totalLabel, inSection.Any(l => l.Line.SplitsBuildings), section == ReportRowSection.Exempt,
                section == ReportRowSection.IdleLand);
            foreach (var restriction in inSection.Select(l => l.Restriction).Distinct())
            {
                var groupLines = inSection.Where(l => l.Restriction == restriction).ToList();
                var subtotal = new Line(groupLines[0].Line.Group, $"Total, {groupLines[0].Line.Group}", groupLines.Any(l => l.Line.SplitsBuildings), false);
                foreach (var (_, _, _, line) in groupLines)
                {
                    rows.Add(line.Cells());
                    subtotal.Add(line);
                }
                if (restriction is not null)
                {
                    rows.Add(subtotal.Cells());
                }
                total.Add(subtotal);
            }
            rows.Add(total.Cells(total.Group, totalLabel));
            if (section != ReportRowSection.IdleLand)
            {
                grand.Add(total);
            }
        }
        Section(ReportRowSection.Taxable, "Total, taxable properties");
        Section(ReportRowSection.Exempt, "Total, exempt properties");
        Section(ReportRowSection.Restricted, "Total, properties with restrictions");
        Section(ReportRowSection.IdleLand, "Total, idle lands");
        // The grand total's rates are shown only where one rate applies throughout; exempt values have none.
        var totals = grand.Cells("Total", "All properties");

        var notes = new List<string>
        {
            $"The FAAS in force at the end of {asOf:yyyy-MM-dd} (an approved Tax Declaration effective by then and not cancelled by then). "
            + "Each is split into its taxable part and its exempt parts, as its assessment's lines (else its Tax Declaration) say; a unit with both is counted in both.",
            $"Barangays included: {parts.FirstOrDefault()?.Barangays ?? 0}.",
            map is null
                ? "No approved QRRPA row map is in force: the rows are PRIME's classifications and exemption types, and there are no restriction groups. Load or enter the row map (Administration, Report settings, QRRPA rows)."
                : $"Rows: {map.Name}, effective {map.EffectiveDate:yyyy-MM-dd} ({map.LegalBasis}).",
            threshold is null
                ? "The residential building value threshold is not set: building market values are in one column."
                : $"Residential buildings are split at a market value of {threshold.Value:#,0.00} ({threshold.LegalBasis}, effective {threshold.EffectiveDate:yyyy-MM-dd}).",
            $"Levy rates in force on {asOf:yyyy-MM-dd}. Collectible = taxable assessed value × rate, rounded to the centavo per municipality and classification; exempt rows have none. "
            + "A rate is shown only where one rate applies to the whole row. Report figures only: PRIME does not bill.",
            "Idle lands are not designated in PRIME yet: their rows and the idle-land collectible are left empty.",
        };
        if (groups.Count > 0)
        {
            notes.Add("A unit whose Tax Declaration carries a restriction annotation is counted under its restriction group, not under taxable; its exempt part stays under exempt. DOMAIN VERIFICATION REQUIRED.");
        }
        if (unmapped > 0)
        {
            notes.Add($"{unmapped:N0} unit part(s) fall in no row of the map and are listed as \"Not in the row map\".");
        }
        if (missingRates.Count > 0)
        {
            notes.Add($"{missingRates.Count:N0} municipality and classification pair(s) with taxable values have no basic or SEF rate in force: their collectibles are incomplete.");
        }
        if (parts.Any(p => p.UnconvertedLandUnits > 0))
        {
            notes.Add($"{parts.Sum(p => p.UnconvertedLandUnits):N0} land unit(s) recorded in an area unit other than square metres or hectares are not included in the land area.");
        }
        if (scope.From is { } from && asOf < ReportScope.PeriodEnd(from, 3))
        {
            notes.Add($"The quarter has not ended: the figures are as of {asOf:yyyy-MM-dd}.");
        }
        if (scope.From is { Month: 10 })
        {
            notes.Add("The fourth quarter's report is also the year-end report.");
        }
        return new ReportRows(rows.Skip(window.Skip).Take(window.Take).ToList(), rows.Count, totals, notes);
    }

    private static bool Matches(IReadOnlyList<string>? codes, string? code) => codes is not { Count: > 0 } || (code is not null && codes.Contains(code));
}
