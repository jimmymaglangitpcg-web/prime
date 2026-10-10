using Microsoft.EntityFrameworkCore;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Properties;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Reports;

/// <summary>
/// Property inventory (CLAUDE.md §57 Property; docs/analysis/reporting.md §4.2): one row per property registered by the
/// date, in PIN order, with its owners and its FAAS in force on the date. Rows are read a page at a time; the totals are one
/// aggregate query.
/// </summary>
public sealed class PropertyInventoryReport(IApplicationDbContext db, IClock clock, IFaasInForceQuery faasInForce) : IReport
{
    public string Code => "PROPERTY_INVENTORY";

    public string Title => "Property inventory";

    public string Group => "Property";

    public string Description =>
        "Every property registered by the date, in PIN order: location, owners, parcels, units and land TDs in force, land area and values.";

    public IReadOnlyList<ReportParameter> Parameters { get; } = [ReportParameter.AsOf, ReportParameter.Municipality, ReportParameter.Barangay];

    public IReadOnlyList<ReportColumn> Columns { get; } =
    [
        new("pin", "PIN", ReportColumnType.Text),
        new("municipality", "Municipality", ReportColumnType.Text),
        new("barangay", "Barangay", ReportColumnType.Text),
        new("location", "Street / sitio", ReportColumnType.Text),
        new("lotBlock", "Lot / block", ReportColumnType.Text),
        new("titleNumber", "Title no.", ReportColumnType.Text),
        new("owners", "Owners", ReportColumnType.Text),
        new("parcels", "Parcels", ReportColumnType.Integer),
        new("units", "Units (FAAS in force)", ReportColumnType.Integer),
        new("landTds", "Land TD no.", ReportColumnType.Text),
        new("landArea", "Land area (sq m)", ReportColumnType.Area),
        new("marketValue", "Market value", ReportColumnType.Money),
        new("taxableAssessedValue", "Assessed value, taxable", ReportColumnType.Money),
        new("exemptAssessedValue", "Assessed value, exempt", ReportColumnType.Money),
        new("status", "Status", ReportColumnType.Text),
    ];

    public async Task<ReportRows> RunAsync(ReportScope scope, ReportWindow window, CancellationToken cancellationToken)
    {
        var nextDay = clock.StartOfDay(scope.AsOf.AddDays(1));
        var properties = db.Properties.Where(p => p.CreatedAt < nextDay);
        if (scope.MunicipalityId is { } m)
        {
            properties = properties.Where(p => p.MunicipalityId == m);
        }
        if (scope.BarangayId is { } b)
        {
            properties = properties.Where(p => p.BarangayId == b);
        }
        var total = await properties.CountAsync(cancellationToken);
        var notes = new List<string>
        {
            "Owners are those on record on the date. Units, land TDs and values are the FAAS in force on the date.",
        };
        if (window.MaxTotal is { } max && total > max)
        {
            return new ReportRows([], total, null, notes);
        }

        var page = await properties.OrderBy(p => p.PropertyIdentificationNumber).ThenBy(p => p.Id).Skip(window.Skip).Take(window.Take)
            .Select(p => new
            {
                p.Id, p.PropertyIdentificationNumber, Municipality = p.Municipality!.Name, Barangay = p.Barangay!.Name, p.Street, p.Sitio,
                p.LotNumber, p.BlockNumber, p.TitleNumber, p.Status,
            })
            .ToListAsync(cancellationToken);
        var ids = page.Select(p => p.Id).ToList();
        var owners = (await PropertyParties.Rows(db.PropertyTaxpayers.Where(x => ids.Contains(x.PropertyId) && x.RpuId == null
                && (x.Role == PropertyPartyRole.Owner || x.Role == PropertyPartyRole.UnknownOwner)
                && x.StartDate <= scope.AsOf && (x.EndDate == null || x.EndDate > scope.AsOf)))
            .ToListAsync(cancellationToken)).ToLookup(x => x.PropertyId);
        var parcels = await db.Parcels.Where(x => ids.Contains(x.PropertyId)).GroupBy(x => x.PropertyId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        var values = (await faasInForce.ListAsync(new FaasScope(scope.AsOf, PropertyIds: ids), cancellationToken)).ToLookup(x => x.PropertyId);

        var rows = page.Select(p =>
        {
            var units = values[p.Id].ToList();
            return new object?[]
            {
                p.PropertyIdentificationNumber, p.Municipality, p.Barangay,
                Join(p.Street, p.Sitio),
                p.LotNumber is null && p.BlockNumber is null ? null : $"{p.LotNumber ?? "—"} / {p.BlockNumber ?? "—"}",
                p.TitleNumber,
                string.Join("; ", PropertyParties.Ordered(owners[p.Id]).Select(o => PropertyParties.ToDto(o).TaxpayerDisplayName).Distinct()),
                parcels.GetValueOrDefault(p.Id),
                units.Count,
                string.Join("; ", units.Where(u => u.IsLand).Select(u => u.TaxDeclarationNumber).Order(StringComparer.Ordinal)),
                units.Count(u => u.IsLand) == 0 ? null : units.Sum(u => u.LandAreaSqm ?? 0m),
                units.Sum(u => u.TaxableMarketValue + u.ExemptMarketValue),
                units.Sum(u => u.TaxableAssessedValue),
                units.Sum(u => u.ExemptAssessedValue),
                p.Status.ToString(),
            };
        }).ToList<object?[]>();

        var sums = (await faasInForce.SummaryAsync(new FaasScope(scope.AsOf, scope.MunicipalityId, scope.BarangayId), FaasGroupBy.None, cancellationToken))
            .FirstOrDefault() ?? new FaasGroup { IsTotal = true };
        var parcelTotal = await db.Parcels.CountAsync(x => properties.Any(p => p.Id == x.PropertyId), cancellationToken);
        object?[] totals =
        [
            $"Total: {total:N0} properties", null, null, null, null, null, null, parcelTotal, sums.Units, null, sums.LandAreaSqm,
            sums.TaxableMarketValue + sums.ExemptMarketValue, sums.TaxableAssessedValue, sums.ExemptAssessedValue, null,
        ];
        return new ReportRows(rows, total, totals, notes);
    }

    private static string? Join(params string?[] parts)
    {
        var present = parts.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        return present.Count == 0 ? null : string.Join(", ", present);
    }
}
