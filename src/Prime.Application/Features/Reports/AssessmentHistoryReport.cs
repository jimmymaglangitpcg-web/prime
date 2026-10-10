using Microsoft.EntityFrameworkCore;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Reports;

/// <summary>
/// Assessment history and reassessments (CLAUDE.md §57 Assessment, §76; docs/analysis/reporting.md §4.2, step R3): the
/// posted assessments made in a period, of a municipality, a barangay or a PIN, each beside the assessment it follows, with
/// the change in assessed value and its reason. An assessment is made on the day its approval completed (L1-2); one
/// made before that date was kept counts from its approval, else its posting, else its entry. The reassessments report keeps only those made under a
/// transaction type of the reassessment kind, with the cause the type asks for.
/// </summary>
public sealed class AssessmentHistoryReport(bool reassessmentsOnly, IApplicationDbContext db, IClock clock) : IReport
{
    public string Code => reassessmentsOnly ? "REASSESSMENTS" : "ASSESSMENT_HISTORY";

    public string Title => reassessmentsOnly ? "Reassessments" : "Assessment history";

    public string Group => "Assessment";

    public string Description => reassessmentsOnly
        ? "The reassessments made in the period, with the previous and new values, the change, and its cause."
        : "The assessments posted for the units of a PIN, barangay or municipality, made in the period, with the previous and new values, the change and the reason.";

    public IReadOnlyList<ReportParameter> Parameters { get; } = [ReportParameter.Period, ReportParameter.Municipality, ReportParameter.Barangay, ReportParameter.Pin];

    public IReadOnlyList<ReportColumn> Columns =>
    [
        new("pin", "PIN", ReportColumnType.Text),
        new("unit", "Unit no.", ReportColumnType.Text),
        new("kind", "Kind", ReportColumnType.Text),
        new("barangay", "Barangay", ReportColumnType.Text),
        new("faasNumber", "FAAS no.", ReportColumnType.Text),
        new("madeOn", "Made on", ReportColumnType.Date),
        new("effectiveDate", "Effective", ReportColumnType.Date),
        new("transactionCode", "Transaction code", ReportColumnType.Text),
        new("reason", reassessmentsOnly ? "Transaction / remarks" : "Reason", ReportColumnType.Text),
        .. reassessmentsOnly
            ? new[] { new ReportColumn("causeDate", "Cause date", ReportColumnType.Date), new ReportColumn("lateCause", "Made after the window", ReportColumnType.Text) }
            : [],
        new("previousEffective", "Previous effective", ReportColumnType.Date),
        new("previousMarketValue", "Previous market value", ReportColumnType.Money),
        new("previousAssessedValue", "Previous assessed value", ReportColumnType.Money),
        new("marketValue", "Market value", ReportColumnType.Money),
        new("assessedValue", "Assessed value", ReportColumnType.Money),
        new("change", "Change in assessed value", ReportColumnType.Money),
    ];

    public async Task<ReportRows> RunAsync(ReportScope scope, ReportWindow window, CancellationToken cancellationToken)
    {
        var ct = cancellationToken;
        var assessments = Scoped(scope);
        var total = await assessments.CountAsync(ct);
        var notes = new List<string>
        {
            "Posted assessments only. Made on is the day the assessment's approval completed; for one made before that day was recorded, the day it was approved, else posted, else entered.",
            "The previous assessment is the one the assessment follows in the unit's history; a unit's first assessment has none.",
        };
        if (reassessmentsOnly)
        {
            notes.Add("A reassessment is an assessment made under a transaction type of the reassessment kind. \"Made after the window\" marks one made later than the type's period after its cause.");
        }
        if (window.MaxTotal is { } max && total > max)
        {
            return new ReportRows([], total, null, notes);
        }

        var page = await assessments
            .OrderBy(a => a.Property!.PropertyIdentificationNumber).ThenBy(a => a.Rpu!.RpuNumber).ThenBy(a => a.EffectiveDate).ThenBy(a => a.CreatedAt).ThenBy(a => a.Id)
            .Skip(window.Skip).Take(window.Take)
            .Select(a => new
            {
                Pin = a.Property!.PropertyIdentificationNumber, Unit = a.Rpu!.RpuNumber, a.Rpu.RpuType, Barangay = a.Property.Barangay!.Name, a.FaasNumber,
                a.MadeOn, Recorded = a.ApprovedAt ?? a.PostedAt ?? a.CreatedAt, a.EffectiveDate, a.TransactionCode,
                TypeName = db.TransactionTypes.Where(t => t.Id == a.TransactionTypeId).Select(t => t.Name).FirstOrDefault(),
                General = a.RevisionReference != null, a.Remarks, a.CauseDate, a.CauseWindowExceeded,
                PreviousEffective = a.PreviousAssessment != null ? (DateOnly?)a.PreviousAssessment.EffectiveDate : null,
                PreviousMv = a.PreviousAssessment != null ? (decimal?)a.PreviousAssessment.MarketValue : null,
                PreviousAv = a.PreviousAssessment != null ? (decimal?)a.PreviousAssessment.AssessedValue : null,
                a.MarketValue, a.AssessedValue,
            })
            .ToListAsync(ct);

        var rows = page.Select(object?[] (a) =>
        [
            a.Pin, a.Unit, ValueSummaryReport.KindName(a.RpuType.ToString()), a.Barangay, a.FaasNumber,
            a.MadeOn ?? clock.LocalDate(a.Recorded), a.EffectiveDate, a.TransactionCode,
            Reason(a.TypeName, a.General, a.Remarks),
            .. reassessmentsOnly ? new object?[] { a.CauseDate, a.CauseWindowExceeded ? "Yes" : null } : [],
            a.PreviousEffective, a.PreviousMv, a.PreviousAv, a.MarketValue, a.AssessedValue, a.AssessedValue - (a.PreviousAv ?? 0m),
        ]).ToList();

        var sums = await assessments.GroupBy(_ => 1).Select(g => new
        {
            Previous = g.Sum(a => a.PreviousAssessment != null ? a.PreviousAssessment.AssessedValue : 0m),
            PreviousMarket = g.Sum(a => a.PreviousAssessment != null ? a.PreviousAssessment.MarketValue : 0m),
            Market = g.Sum(a => a.MarketValue),
            Assessed = g.Sum(a => a.AssessedValue),
        }).FirstOrDefaultAsync(ct);
        var totals = new object?[Columns.Count];
        totals[0] = $"Total: {total:N0} assessments";
        totals[^5] = sums?.PreviousMarket ?? 0m;
        totals[^4] = sums?.Previous ?? 0m;
        totals[^3] = sums?.Market ?? 0m;
        totals[^2] = sums?.Assessed ?? 0m;
        totals[^1] = (sums?.Assessed ?? 0m) - (sums?.Previous ?? 0m);
        return new ReportRows(rows, total, totals, notes);
    }

    /// <summary>The posted assessments of the scope made in the period.</summary>
    private IQueryable<Assessment> Scoped(ReportScope scope)
    {
        var assessments = db.Assessments.AsNoTracking().Where(a => a.Status == WorkflowStatus.Posted);
        if (scope.MunicipalityId is { } m)
        {
            assessments = assessments.Where(a => a.Property!.MunicipalityId == m);
        }
        if (scope.BarangayId is { } b)
        {
            assessments = assessments.Where(a => a.Property!.BarangayId == b);
        }
        if (scope.Pin is { } pin)
        {
            assessments = assessments.Where(a => a.Property!.PropertyIdentificationNumber.StartsWith(pin));
        }
        if (scope.From is { } from && scope.To is { } to)
        {
            var start = clock.StartOfDay(from);
            var end = clock.StartOfDay(to.AddDays(1));
            assessments = assessments.Where(a => a.MadeOn != null
                ? a.MadeOn >= from && a.MadeOn <= to
                : (a.ApprovedAt ?? a.PostedAt ?? a.CreatedAt) >= start && (a.ApprovedAt ?? a.PostedAt ?? a.CreatedAt) < end);
        }
        if (reassessmentsOnly)
        {
            assessments = assessments.Where(a => db.TransactionTypes.Any(t => t.Id == a.TransactionTypeId && t.Kind == PropertyTransactionKind.Reassessment));
        }
        return assessments;
    }

    private static string? Reason(string? typeName, bool generalRevision, string? remarks)
    {
        var parts = new[] { typeName ?? (generalRevision ? "General revision" : null), remarks }.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        return parts.Count == 0 ? null : string.Join(" — ", parts);
    }
}
