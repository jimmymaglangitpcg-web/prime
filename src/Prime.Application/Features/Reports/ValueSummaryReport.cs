using Microsoft.EntityFrameworkCore;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Reports;

/// <summary>
/// Market and assessed value summary (CLAUDE.md §57 Assessment; docs/analysis/reporting.md §4.2, step R3): the FAAS in force
/// on the date per kind of unit (land, building, machinery, other) and classification, with a subtotal per kind and the
/// total. One GROUP BY in the database (§71). The classification is the TD's.
/// </summary>
public sealed class ValueSummaryReport(IApplicationDbContext db, IFaasInForceQuery faasInForce) : IReport
{
    public string Code => "VALUE_SUMMARY";

    public string Title => "Market and assessed value summary";

    public string Group => "Assessment";

    public string Description =>
        "Per kind of unit and classification: units with a FAAS in force on the date, land area, and market and assessed values, taxable and exempt, with subtotals per kind.";

    public IReadOnlyList<ReportParameter> Parameters { get; } = [ReportParameter.AsOf, ReportParameter.Municipality, ReportParameter.Barangay];

    public IReadOnlyList<ReportColumn> Columns { get; } =
    [
        new("kind", "Kind", ReportColumnType.Text),
        new("code", "Code", ReportColumnType.Text),
        new("classification", "Classification", ReportColumnType.Text),
        new("properties", "Properties", ReportColumnType.Integer),
        new("units", "Units (FAAS in force)", ReportColumnType.Integer),
        new("landArea", "Land area (sq m)", ReportColumnType.Area),
        new("taxableMarketValue", "Market value, taxable", ReportColumnType.Money),
        new("taxableAssessedValue", "Assessed value, taxable", ReportColumnType.Money),
        new("exemptMarketValue", "Market value, exempt", ReportColumnType.Money),
        new("exemptAssessedValue", "Assessed value, exempt", ReportColumnType.Money),
        new("marketValue", "Market value, total", ReportColumnType.Money),
        new("assessedValue", "Assessed value, total", ReportColumnType.Money),
    ];

    /// <summary>The printed name of a unit kind, as stored.</summary>
    public static string KindName(string? kind) => Enum.TryParse<RpuType>(kind, out var type)
        ? type switch
        {
            RpuType.Land => "Land",
            RpuType.Building => "Building",
            RpuType.Machinery => "Machinery",
            RpuType.OtherImprovement => "Other improvement",
            RpuType.MineralRight => "Mineral right",
            _ => type.ToString(),
        }
        : kind ?? "—";

    public async Task<ReportRows> RunAsync(ReportScope scope, ReportWindow window, CancellationToken cancellationToken)
    {
        var summary = await faasInForce.KindSummaryAsync(new FaasScope(scope.AsOf, scope.MunicipalityId, scope.BarangayId), cancellationToken);
        var total = summary.FirstOrDefault(g => g.IsTotal) ?? new FaasGroup { IsTotal = true };
        var ids = summary.Select(g => g.Key).OfType<Guid>().Distinct().ToList();
        var classes = await db.Classifications.Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, c.Code, c.Name, c.SortOrder }).ToDictionaryAsync(c => c.Id, cancellationToken);

        // Each kind in the order of RpuType, its classifications by their order, then the kind's subtotal.
        var rows = new List<object?[]>();
        foreach (var kind in summary.Where(g => !g.IsTotal && g.Key is null)
                     .OrderBy(g => Enum.TryParse<RpuType>(g.Kind, out var t) ? (int)t : int.MaxValue))
        {
            var name = KindName(kind.Kind);
            rows.AddRange(summary.Where(g => !g.IsTotal && g.Key is not null && g.Kind == kind.Kind)
                .Select(g => (Group: g, Class: classes.GetValueOrDefault(g.Key!.Value)))
                .OrderBy(x => x.Class?.SortOrder ?? int.MaxValue).ThenBy(x => x.Class?.Code, StringComparer.OrdinalIgnoreCase)
                .Select(x => Cells(name, x.Class?.Code ?? "—", x.Class?.Name ?? "Not recorded", x.Group)));
            rows.Add(Cells(name, null, $"Subtotal, {name.ToLowerInvariant()}", kind));
        }

        var notes = new List<string>
        {
            "Counts the units with a FAAS in force on the date (an approved Tax Declaration effective by then and not cancelled by then). The classification is the Tax Declaration's.",
            "A property with units of several kinds or classifications is counted under each; the total counts it once.",
        };
        if (total.UnconvertedLandUnits > 0)
        {
            notes.Add($"{total.UnconvertedLandUnits} land unit(s) recorded in an area unit other than square metres or hectares are not included in the land area.");
        }
        return new ReportRows(rows.Skip(window.Skip).Take(window.Take).ToList(), rows.Count, Cells("Total", null, null, total), notes);
    }

    private static object?[] Cells(string kind, string? code, string? name, FaasGroup g) =>
    [
        kind, code, name, g.Properties, g.Units, g.LandAreaSqm == 0 && g.Kind is not null && g.Kind != nameof(RpuType.Land) ? null : g.LandAreaSqm,
        g.TaxableMarketValue, g.TaxableAssessedValue, g.ExemptMarketValue, g.ExemptAssessedValue,
        g.TaxableMarketValue + g.ExemptMarketValue, g.TaxableAssessedValue + g.ExemptAssessedValue,
    ];
}
