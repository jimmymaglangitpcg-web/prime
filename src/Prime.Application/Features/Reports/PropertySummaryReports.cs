using Microsoft.EntityFrameworkCore;
using Prime.Application.Common.Interfaces;

namespace Prime.Application.Features.Reports;

/// <summary>What a "properties by …" summary groups the FAAS in force by.</summary>
public enum PropertySummaryKey
{
    Barangay,
    Classification,
    ActualUse,
    Zone,
}

/// <summary>
/// "Properties by barangay / classification / actual use / zone" (CLAUDE.md §57 Property; docs/analysis/reporting.md §4.2):
/// per group, the properties and units with a FAAS in force on the date, the land area, and the market and assessed values,
/// taxable and exempt. One GROUP BY in the database (§71). Classification and actual use are the TD's; barangay and zone are
/// the property's. A property whose units fall in several groups is counted in each; the totals count it once.
/// </summary>
public sealed class PropertySummaryReport(PropertySummaryKey key, IApplicationDbContext db, IFaasInForceQuery faasInForce) : IReport
{
    public string Code => key switch
    {
        PropertySummaryKey.Barangay => "PROPERTIES_BY_BARANGAY",
        PropertySummaryKey.Classification => "PROPERTIES_BY_CLASSIFICATION",
        PropertySummaryKey.ActualUse => "PROPERTIES_BY_ACTUAL_USE",
        _ => "PROPERTIES_BY_ZONE",
    };

    public string Title => $"Properties by {GroupTitle.ToLowerInvariant()}";

    public string Group => "Property";

    public string Description =>
        $"Per {GroupTitle.ToLowerInvariant()}: properties and units with a FAAS in force on the date, land area, and market and assessed values, taxable and exempt.";

    public IReadOnlyList<ReportParameter> Parameters { get; } = [ReportParameter.AsOf, ReportParameter.Municipality, ReportParameter.Barangay];

    private string GroupTitle => key switch
    {
        PropertySummaryKey.Barangay => "Barangay",
        PropertySummaryKey.Classification => "Classification",
        PropertySummaryKey.ActualUse => "Actual use",
        _ => "Zone",
    };

    public IReadOnlyList<ReportColumn> Columns =>
    [
        .. key == PropertySummaryKey.Barangay
            ? new[] { new ReportColumn("municipality", "Municipality", ReportColumnType.Text), new ReportColumn("barangay", "Barangay", ReportColumnType.Text) }
            : [new ReportColumn("code", "Code", ReportColumnType.Text), new ReportColumn("name", GroupTitle, ReportColumnType.Text)],
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

    public async Task<ReportRows> RunAsync(ReportScope scope, ReportWindow window, CancellationToken cancellationToken)
    {
        var groupBy = key switch
        {
            PropertySummaryKey.Barangay => FaasGroupBy.Barangay,
            PropertySummaryKey.Classification => FaasGroupBy.Classification,
            PropertySummaryKey.ActualUse => FaasGroupBy.ActualUse,
            _ => FaasGroupBy.Zone,
        };
        var summary = await faasInForce.SummaryAsync(new FaasScope(scope.AsOf, scope.MunicipalityId, scope.BarangayId), groupBy, cancellationToken);
        var groups = summary.Where(g => !g.IsTotal).ToList();
        var total = summary.FirstOrDefault(g => g.IsTotal) ?? new FaasGroup { IsTotal = true };

        var labels = await LabelsAsync(groups.Select(g => g.Key).OfType<Guid>().ToList(), cancellationToken);
        var rows = groups
            .Select(g => (Label: g.Key is { } k && labels.TryGetValue(k, out var l) ? l : NoGroupLabel, Row: g))
            .OrderBy(x => x.Label.Sort, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Label.Second, StringComparer.OrdinalIgnoreCase)
            .Select(x => Cells(x.Label.First, x.Label.Second, x.Row))
            .ToList();

        var notes = new List<string>
        {
            "Counts the units with a FAAS in force on the date (an approved Tax Declaration effective by then and not cancelled by then).",
        };
        if (key is PropertySummaryKey.Classification or PropertySummaryKey.ActualUse)
        {
            notes.Add("A property whose units have different classifications or uses is counted under each; the total counts it once.");
        }
        if (total.UnconvertedLandUnits > 0)
        {
            notes.Add($"{total.UnconvertedLandUnits} land unit(s) recorded in an area unit other than square metres or hectares are not included in the land area.");
        }
        return new ReportRows(rows.Skip(window.Skip).Take(window.Take).ToList(), rows.Count, Cells("Total", null, total), notes);
    }

    private static object?[] Cells(string first, string? second, FaasGroup g) =>
    [
        first, second, g.Properties, g.Units, g.LandAreaSqm, g.TaxableMarketValue, g.TaxableAssessedValue, g.ExemptMarketValue, g.ExemptAssessedValue,
        g.TaxableMarketValue + g.ExemptMarketValue, g.TaxableAssessedValue + g.ExemptAssessedValue,
    ];

    /// <summary>The two leading cells of a group, and the order groups are listed in.</summary>
    private sealed record Label(string First, string? Second, string Sort);

    private Label NoGroupLabel => key switch
    {
        PropertySummaryKey.Zone => new Label("—", "No zone", "~"),
        _ => new Label("—", "Not recorded", "~"),
    };

    private async Task<Dictionary<Guid, Label>> LabelsAsync(List<Guid> ids, CancellationToken ct) => key switch
    {
        PropertySummaryKey.Barangay => await db.Barangays.Where(b => ids.Contains(b.Id))
            .Select(b => new { b.Id, Municipality = b.Municipality!.Name, b.Name })
            .ToDictionaryAsync(b => b.Id, b => new Label(b.Municipality, b.Name, b.Municipality), ct),
        PropertySummaryKey.Classification => await db.Classifications.Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => new Label(c.Code, c.Name, $"{c.SortOrder:D6}{c.Code}"), ct),
        PropertySummaryKey.ActualUse => await db.ActualUses.Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => new Label(c.Code, c.Name, $"{c.SortOrder:D6}{c.Code}"), ct),
        _ => await db.Zones.Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => new Label(c.Code, c.Name, $"{c.SortOrder:D6}{c.Code}"), ct),
    };
}
