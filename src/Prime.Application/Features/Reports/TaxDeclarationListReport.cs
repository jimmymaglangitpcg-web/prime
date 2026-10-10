using Microsoft.EntityFrameworkCore;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Properties;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Reports;

/// <summary>
/// Tax Declaration list (CLAUDE.md §57 Assessment; docs/analysis/reporting.md §4.2, step R3): the TDs recorded in a period,
/// of a status, a barangay, a transaction code or a PIN, in TD number order. A TD is recorded when it is approved, or, not
/// yet approved, when it was drafted. Its values are those of the assessment it declares, else the unit's posted assessment
/// in force on its effectivity date, as the registers read them. Rows are read a page at a time.
/// </summary>
public sealed class TaxDeclarationListReport(IApplicationDbContext db, IClock clock) : IReport
{
    public string Code => "TD_LIST";

    public string Title => "Tax Declaration list";

    public string Group => "Assessment";

    public string Description =>
        "The Tax Declarations recorded in the period (approved, or drafted if not yet approved), by status, barangay, transaction code or PIN, with their values and what they cancel.";

    public IReadOnlyList<ReportParameter> Parameters { get; } =
        [ReportParameter.Period, ReportParameter.Municipality, ReportParameter.Barangay, ReportParameter.TdStatus, ReportParameter.TransactionCode, ReportParameter.Pin];

    public IReadOnlyList<ReportColumn> Columns { get; } =
    [
        new("tdNumber", "TD no.", ReportColumnType.Text),
        new("pin", "PIN", ReportColumnType.Text),
        new("kind", "Kind", ReportColumnType.Text),
        new("municipality", "Municipality", ReportColumnType.Text),
        new("barangay", "Barangay", ReportColumnType.Text),
        new("owners", "Owners", ReportColumnType.Text),
        new("classification", "Class", ReportColumnType.Text),
        new("actualUse", "Actual use", ReportColumnType.Text),
        new("taxability", "Taxability", ReportColumnType.Text),
        new("effectivity", "Effectivity", ReportColumnType.Date),
        new("transactionCode", "Transaction code", ReportColumnType.Text),
        new("status", "Status", ReportColumnType.Text),
        new("recordedOn", "Recorded on", ReportColumnType.Date),
        new("marketValue", "Market value", ReportColumnType.Money),
        new("assessedValue", "Assessed value", ReportColumnType.Money),
        new("previousTd", "Cancels TD no.", ReportColumnType.Text),
        new("cancelledOn", "Cancelled on", ReportColumnType.Date),
        new("cancelledBy", "Cancelled by TD no. / reason", ReportColumnType.Text),
    ];

    public async Task<ReportRows> RunAsync(ReportScope scope, ReportWindow window, CancellationToken cancellationToken)
    {
        var ct = cancellationToken;
        var tds = db.TaxDeclarations.AsNoTracking();
        if (scope.MunicipalityId is { } m)
        {
            tds = tds.Where(x => x.Property!.MunicipalityId == m);
        }
        if (scope.BarangayId is { } b)
        {
            tds = tds.Where(x => x.Property!.BarangayId == b);
        }
        if (scope.From is { } from && scope.To is { } to)
        {
            var start = clock.StartOfDay(from);
            var end = clock.StartOfDay(to.AddDays(1));
            tds = tds.Where(x => (x.ApprovedAt ?? x.CreatedAt) >= start && (x.ApprovedAt ?? x.CreatedAt) < end);
        }
        if (scope.Status is { } status)
        {
            tds = tds.Where(x => x.Status == status);
        }
        if (scope.TransactionCode is { } code)
        {
            tds = tds.Where(x => x.TransactionCode != null && x.TransactionCode.ToUpper() == code);
        }
        if (scope.Pin is { } pin)
        {
            tds = tds.Where(x => x.Property!.PropertyIdentificationNumber.StartsWith(pin));
        }

        var total = await tds.CountAsync(ct);
        var notes = new List<string>
        {
            "A Tax Declaration is recorded on the day it was approved; one not yet approved, on the day it was drafted.",
            "Values are those of the assessment the Tax Declaration declares, else the unit's posted assessment in force on its effectivity date. Owners are those on record on the day it was recorded.",
            "For totals of value, use the market and assessed value summary.",
        };
        if (window.MaxTotal is { } max && total > max)
        {
            return new ReportRows([], total, null, notes);
        }

        var page = await tds.OrderBy(x => x.TaxDeclarationNumber).ThenBy(x => x.Id).Skip(window.Skip).Take(window.Take)
            .Select(x => new
            {
                x.Id, x.TaxDeclarationNumber, x.PropertyId, x.RpuId, Pin = x.Property!.PropertyIdentificationNumber, x.Rpu!.RpuType,
                Municipality = x.Property.Municipality!.Name, Barangay = x.Property.Barangay!.Name,
                Classification = x.Classification!.Code, ActualUse = x.ActualUse!.Code, x.Taxability, x.EffectivityDate, x.TransactionCode, x.Status,
                x.ApprovedAt, x.CreatedAt, x.CancelledAt, x.CancellationReason, x.SupersededByTaxDeclarationId,
                PreviousTd = x.PreviousTaxDeclaration != null ? x.PreviousTaxDeclaration.TaxDeclarationNumber : null,
                DeclaredMv = x.Assessment != null ? (decimal?)x.Assessment.MarketValue : null,
                DeclaredAv = x.Assessment != null ? (decimal?)x.Assessment.AssessedValue : null,
                Declares = x.AssessmentId != null,
            })
            .ToListAsync(ct);

        var propertyIds = page.Select(x => x.PropertyId).Distinct().ToList();
        var parties = (await PropertyParties.Rows(db.PropertyTaxpayers.Where(x => propertyIds.Contains(x.PropertyId)
                && (x.Role == PropertyPartyRole.Owner || x.Role == PropertyPartyRole.UnknownOwner)))
            .ToListAsync(ct)).ToLookup(x => x.PropertyId);
        // A TD without a declared assessment: the unit's posted assessments, for the one in force on its effectivity.
        var undeclared = page.Where(x => !x.Declares).Select(x => x.RpuId).Distinct().ToList();
        var posted = (await db.Assessments.AsNoTracking().Where(a => undeclared.Contains(a.RpuId) && a.Status == WorkflowStatus.Posted)
                .Select(a => new { a.RpuId, a.EffectiveDate, a.CreatedAt, a.MarketValue, a.AssessedValue }).ToListAsync(ct))
            .ToLookup(a => a.RpuId);
        var successorIds = page.Select(x => x.SupersededByTaxDeclarationId).OfType<Guid>().ToList();
        var successors = await db.TaxDeclarations.Where(x => successorIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.TaxDeclarationNumber, ct);

        var rows = page.Select(x =>
        {
            var recorded = clock.LocalDate(x.ApprovedAt ?? x.CreatedAt);
            var owners = PropertyParties.Scope(parties[x.PropertyId].Where(o => o.StartDate <= recorded && (o.EndDate == null || o.EndDate > recorded)), x.RpuId);
            var inForce = x.Declares ? null : posted[x.RpuId].Where(a => a.EffectiveDate <= x.EffectivityDate)
                .OrderByDescending(a => a.EffectiveDate).ThenByDescending(a => a.CreatedAt).FirstOrDefault();
            var cancelledBy = x.SupersededByTaxDeclarationId is { } s && successors.TryGetValue(s, out var number) ? number : x.CancellationReason;
            return new object?[]
            {
                x.TaxDeclarationNumber, x.Pin, ValueSummaryReport.KindName(x.RpuType.ToString()), x.Municipality, x.Barangay,
                string.Join("; ", PropertyParties.Ordered(owners).Select(o => PropertyParties.ToDto(o).TaxpayerDisplayName).Distinct()),
                x.Classification, x.ActualUse, x.Taxability.ToString(), x.EffectivityDate, x.TransactionCode, x.Status.ToString(), recorded,
                x.Declares ? x.DeclaredMv : inForce?.MarketValue, x.Declares ? x.DeclaredAv : inForce?.AssessedValue,
                x.PreviousTd, x.CancelledAt is { } c ? clock.LocalDate(c) : null, cancelledBy,
            };
        }).ToList();
        object?[] totals = [$"Total: {total:N0} Tax Declarations", .. Enumerable.Repeat<object?>(null, Columns.Count - 1)];
        return new ReportRows(rows, total, totals, notes);
    }
}
