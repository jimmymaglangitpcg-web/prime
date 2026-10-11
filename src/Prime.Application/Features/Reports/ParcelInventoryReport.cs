using Microsoft.EntityFrameworkCore;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Reports;

/// <summary>
/// Parcel inventory (CLAUDE.md §57 GIS; docs/analysis/reporting.md §4.2, step R6): the active parcels, by barangay, tax map
/// section and parcel number, with their PIN, lot and survey identifiers, declared area and whether they are mapped (have a
/// geometry). Current records: parcels are not effective-dated. Rows a page at a time; totals in one aggregate query.
/// </summary>
public sealed class ParcelInventoryReport(IApplicationDbContext db) : IReport
{
    public string Code => "PARCEL_INVENTORY";
    public string Title => "Parcel inventory";
    public string Group => "GIS";

    public string Description =>
        "The active parcels by barangay, section and parcel number: PIN, lot, block, survey and cadastral numbers, declared area, and whether each is on the tax map.";

    public IReadOnlyList<ReportParameter> Parameters { get; } = [ReportParameter.Municipality, ReportParameter.Barangay];

    public IReadOnlyList<ReportColumn> Columns { get; } =
    [
        new("pin", "PIN", ReportColumnType.Text),
        new("municipality", "Municipality", ReportColumnType.Text),
        new("barangay", "Barangay", ReportColumnType.Text),
        new("section", "Section", ReportColumnType.Text),
        new("parcelNumber", "Parcel no.", ReportColumnType.Integer),
        new("lot", "Lot", ReportColumnType.Text),
        new("block", "Block", ReportColumnType.Text),
        new("survey", "Survey no.", ReportColumnType.Text),
        new("cadastral", "Cadastral no.", ReportColumnType.Text),
        new("area", "Declared area (sq m)", ReportColumnType.Area),
        new("mapped", "On the tax map", ReportColumnType.Text),
    ];

    public async Task<ReportRows> RunAsync(ReportScope scope, ReportWindow window, CancellationToken cancellationToken)
    {
        var parcels = db.Parcels.AsNoTracking().Where(p => p.Status == RecordStatus.Active);
        if (scope.MunicipalityId is { } m)
        {
            parcels = parcels.Where(p => p.Barangay!.MunicipalityId == m);
        }
        if (scope.BarangayId is { } b)
        {
            parcels = parcels.Where(p => p.BarangayId == b);
        }
        var sums = await parcels.GroupBy(_ => 1).Select(g => new
        {
            Count = g.Count(),
            Area = g.Sum(p => p.Area ?? 0m),
            Mapped = g.Count(p => p.Geometry != null),
            WithoutArea = g.Count(p => p.Area == null),
        }).FirstOrDefaultAsync(cancellationToken) ?? new { Count = 0, Area = 0m, Mapped = 0, WithoutArea = 0 };

        var notes = new List<string>
        {
            "Active parcels as recorded now (parcels retired by a subdivision, consolidation or cancellation are left out). The area is the declared area, not the drawn one.",
        };
        if (sums.Count > sums.Mapped)
        {
            notes.Add($"{sums.Count - sums.Mapped:N0} parcel(s) have no geometry: they are not on the tax map yet.");
        }
        if (sums.WithoutArea > 0)
        {
            notes.Add($"{sums.WithoutArea:N0} parcel(s) have no declared area.");
        }
        object?[] totals = [$"Total: {sums.Count:N0} parcels", null, null, null, null, null, null, null, null, sums.Area, $"{sums.Mapped:N0} mapped"];
        if (window.MaxTotal is { } max && sums.Count > max)
        {
            return new ReportRows([], sums.Count, totals, notes);
        }

        var page = await parcels
            .OrderBy(p => p.Barangay!.Municipality!.Name).ThenBy(p => p.Barangay!.Name).ThenBy(p => p.Section == null ? null : p.Section.IndexNumber)
            .ThenBy(p => p.ParcelNumber).ThenBy(p => p.Property!.PropertyIdentificationNumber).ThenBy(p => p.Id)
            .Skip(window.Skip).Take(window.Take)
            .Select(p => new
            {
                Pin = p.Property!.PropertyIdentificationNumber, Municipality = p.Barangay!.Municipality!.Name, Barangay = p.Barangay.Name,
                Section = p.Section == null ? null : p.Section.IndexNumber, p.ParcelNumber, p.LotNumber, p.BlockNumber, p.SurveyNumber,
                Cadastral = p.CadastralNumber ?? p.Property.CadastralNumber, p.Area, Mapped = p.Geometry != null,
            })
            .ToListAsync(cancellationToken);
        var rows = page.Select(p => new object?[]
        {
            p.Pin, p.Municipality, p.Barangay, p.Section, p.ParcelNumber, p.LotNumber, p.BlockNumber, p.SurveyNumber, p.Cadastral, p.Area,
            p.Mapped ? "Yes" : "No",
        }).ToList();
        return new ReportRows(rows, sums.Count, totals, notes);
    }
}
