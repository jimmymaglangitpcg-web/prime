using Microsoft.EntityFrameworkCore;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Reports;

/// <summary>
/// The FAAS in force at the start and end of a period and what changed between (docs/analysis/reporting.md §10, Q13, Q14,
/// Q19): per municipality and kind of unit (and classification, for the half-yearly report), the units and assessed values,
/// taxable and exempt, in four blocks: at the start, assessed during the period, cancelled during the period, at the end.
/// "Assessed" and "cancelled" are read from the two in-force sets, so each row balances (start + assessed − cancelled =
/// end). The columns are PRIME's provisional layout; the LAM's comes in as content (CLAUDE.md §118).
/// </summary>
public abstract class AssessmentChangeReport(IApplicationDbContext db, IFaasInForceQuery faasInForce, IClock clock) : IReport
{
    /// <summary>The four kinds the report always lists for a municipality; other kinds appear when they have units.</summary>
    private static readonly RpuType[] StandardKinds = [RpuType.Land, RpuType.Building, RpuType.Machinery, RpuType.OtherImprovement];

    private static readonly (FaasChangeBlock Block, string Title)[] Blocks =
    [
        (FaasChangeBlock.Start, "At the start"),
        (FaasChangeBlock.Assessed, "Assessed"),
        (FaasChangeBlock.Cancelled, "Cancelled"),
        (FaasChangeBlock.End, "At the end"),
    ];

    public abstract string Code { get; }
    public abstract string Title { get; }
    public string Group => "BLGF";
    public abstract string Description { get; }
    public abstract IReadOnlyList<ReportParameter> Parameters { get; }

    /// <summary>Whether the rows go down to classifications within each kind.</summary>
    protected abstract bool ByClassification { get; }

    /// <summary>The period's name in the notes ("month", "half-year").</summary>
    protected abstract string PeriodName { get; }

    /// <summary>The period's length in months.</summary>
    protected abstract int Months { get; }

    public IReadOnlyList<ReportColumn> Columns => columns ??= BuildColumns();
    private IReadOnlyList<ReportColumn>? columns;

    private IReadOnlyList<ReportColumn> BuildColumns()
    {
        var list = new List<ReportColumn> { new("municipality", "LGU", ReportColumnType.Text), new("kind", "Kind", ReportColumnType.Text) };
        if (ByClassification)
        {
            list.Add(new("code", "Code", ReportColumnType.Text));
            list.Add(new("classification", "Classification", ReportColumnType.Text));
        }
        foreach (var (block, title) in Blocks)
        {
            var key = block.ToString();
            key = char.ToLowerInvariant(key[0]) + key[1..];
            list.Add(new($"{key}TaxableUnits", $"{title}: RPUs, taxable", ReportColumnType.Integer));
            list.Add(new($"{key}TaxableAssessedValue", $"{title}: assessed value, taxable", ReportColumnType.Money));
            list.Add(new($"{key}ExemptUnits", $"{title}: RPUs, exempt", ReportColumnType.Integer));
            list.Add(new($"{key}ExemptAssessedValue", $"{title}: assessed value, exempt", ReportColumnType.Money));
        }
        return list;
    }

    public async Task<ReportRows> RunAsync(ReportScope scope, ReportWindow window, CancellationToken cancellationToken)
    {
        var from = scope.From ?? throw new InvalidOperationException("The report's period was not set.");
        var groups = await faasInForce.ChangeSummaryAsync(new FaasScope(scope.AsOf, scope.MunicipalityId), from, ByClassification, cancellationToken);
        var municipalityIds = groups.Where(g => g.Level >= 1).Select(g => g.MunicipalityId).OfType<Guid>().Distinct().ToList();
        var municipalities = await db.Municipalities.Where(m => municipalityIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Name }).ToDictionaryAsync(m => m.Id, m => m.Name, cancellationToken);
        var classIds = groups.Select(g => g.ClassificationId).OfType<Guid>().Distinct().ToList();
        var classes = await db.Classifications.Where(c => classIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Code, c.Name, c.SortOrder }).ToDictionaryAsync(c => c.Id, cancellationToken);

        // The blocks of one group, keyed by its level, municipality, kind and classification.
        var byKey = groups.ToLookup(g => (g.Level, g.MunicipalityId, g.Kind, g.ClassificationId));
        object?[] Cells(string municipality, string kind, string? code, string? classification, int level, Guid? m, string? k, Guid? c)
        {
            var row = new List<object?> { municipality, kind };
            if (ByClassification)
            {
                row.Add(code);
                row.Add(classification);
            }
            var blocks = byKey[(level, m, k, c)].ToDictionary(g => g.Block);
            foreach (var (block, _) in Blocks)
            {
                var g = blocks.GetValueOrDefault(block);
                row.Add(g?.TaxableUnits ?? 0);
                row.Add(g?.TaxableAssessedValue ?? 0m);
                row.Add(g?.ExemptUnits ?? 0);
                row.Add(g?.ExemptAssessedValue ?? 0m);
            }
            return [.. row];
        }

        var rows = new List<object?[]>();
        foreach (var (id, name) in municipalities.OrderBy(m => m.Value, StringComparer.OrdinalIgnoreCase))
        {
            var kinds = groups.Where(g => g.Level == 2 && g.MunicipalityId == id).Select(g => g.Kind).OfType<string>()
                .Concat(StandardKinds.Select(k => k.ToString())).Distinct()
                .OrderBy(k => Enum.TryParse<RpuType>(k, out var t) ? (int)t : int.MaxValue);
            foreach (var kind in kinds)
            {
                var kindName = ValueSummaryReport.KindName(kind);
                if (ByClassification)
                {
                    rows.AddRange(groups.Where(g => g.Level == 3 && g.MunicipalityId == id && g.Kind == kind)
                        .Select(g => g.ClassificationId).Distinct()
                        .Select(c => (Id: c, Class: c is { } cid ? classes.GetValueOrDefault(cid) : null))
                        .OrderBy(x => x.Class?.SortOrder ?? int.MaxValue).ThenBy(x => x.Class?.Code, StringComparer.OrdinalIgnoreCase)
                        .Select(x => Cells(name, kindName, x.Class?.Code ?? "—", x.Class?.Name ?? "Not recorded", 3, id, kind, x.Id)));
                    rows.Add(Cells(name, kindName, null, $"Subtotal, {kindName.ToLowerInvariant()}", 2, id, kind, null));
                }
                else
                {
                    rows.Add(Cells(name, kindName, null, null, 2, id, kind, null));
                }
            }
            rows.Add(Cells(name, "Total", null, null, 1, id, null, null));
        }

        var totalLabel = scope.MunicipalityId is null ? "Total, all in your jurisdiction" : "Total";
        var totals = Cells(totalLabel, string.Empty, null, null, 0, null, null, null);
        return new ReportRows(rows.Skip(window.Skip).Take(window.Take).ToList(), rows.Count, totals, await NotesAsync(scope, from, groups, cancellationToken));
    }

    private async Task<List<string>> NotesAsync(ReportScope scope, DateOnly from, IReadOnlyList<FaasChangeGroup> groups, CancellationToken ct)
    {
        var to = scope.AsOf;
        var notes = new List<string>
        {
            $"At the start: the FAAS in force at the end of {from.AddDays(-1):yyyy-MM-dd}; at the end: those in force at the end of {to:yyyy-MM-dd} "
            + "(an approved Tax Declaration effective by then and not cancelled by then). Assessed: in force at the end and not at the start. "
            + "Cancelled: in force at the start and not at the end, including those replaced by a new FAAS. So each row balances: start + assessed − cancelled = end.",
            "A unit with a taxable and an exempt part is counted under taxable; its exempt assessed value is in the exempt column.",
        };
        if (to < ReportScope.PeriodEnd(from, Months))
        {
            notes.Add($"The {PeriodName} has not ended: the figures are to {to:yyyy-MM-dd}.");
        }
        var mixed = groups.FirstOrDefault(g => g.Level == 0 && g.Block == FaasChangeBlock.End)?.MixedUnits ?? 0;
        if (mixed > 0)
        {
            notes.Add($"{mixed:N0} unit(s) in force at the end have both a taxable and an exempt part.");
        }
        // A TD approved in the period but effective later is counted in the period it takes effect (Q13).
        var periodStart = clock.StartOfDay(from);
        var periodEnd = clock.StartOfDay(to.AddDays(1));
        var later = db.TaxDeclarations.Where(t => t.ApprovedAt >= periodStart && t.ApprovedAt < periodEnd && t.EffectivityDate > to
            && (t.Status == WorkflowStatus.Approved || t.Status == WorkflowStatus.Cancelled));
        if (scope.MunicipalityId is { } m)
        {
            later = later.Where(t => t.Property!.MunicipalityId == m);
        }
        var laterCount = await later.CountAsync(ct);
        if (laterCount > 0)
        {
            notes.Add($"{laterCount:N0} Tax Declaration(s) approved in the {PeriodName} take effect after it; they will be counted in the {PeriodName} they take effect.");
        }
        return notes;
    }
}

/// <summary>
/// The monthly report on real property assessments (MRRPA; LAM 2025 Book I pp.24–25, Annex I-Q; reporting.md §10, Q13,
/// Q14): per municipality and kind, the four blocks of <see cref="AssessmentChangeReport"/> over a calendar month.
/// </summary>
public sealed class MonthlyAssessmentReport(IApplicationDbContext db, IFaasInForceQuery faasInForce, IClock clock)
    : AssessmentChangeReport(db, faasInForce, clock)
{
    public override string Code => "MRRPA";
    public override string Title => "Monthly report on real property assessments (MRRPA)";

    public override string Description =>
        "Per municipality and kind of property: RPUs and assessed values, taxable and exempt, in force at the start of the month, assessed and cancelled during it, and in force at its end. Provisional layout.";

    public override IReadOnlyList<ReportParameter> Parameters { get; } = [ReportParameter.Month, ReportParameter.Municipality];
    protected override bool ByClassification => false;
    protected override string PeriodName => "month";
    protected override int Months => 1;
}

/// <summary>
/// The half-yearly report to the local chief executive and the Sanggunian (LAM 2025 Book I p.24; reporting.md §10, Q11,
/// Q19): the MRRPA's four blocks over January–June or July–December, by kind and classification. DOMAIN VERIFICATION
/// REQUIRED: the LAM names the report without a layout, and the province has not confirmed this one.
/// </summary>
public sealed class HalfYearlyAssessmentReport(IApplicationDbContext db, IFaasInForceQuery faasInForce, IClock clock)
    : AssessmentChangeReport(db, faasInForce, clock)
{
    public override string Code => "HALF_YEARLY_RPA";
    public override string Title => "Half-yearly report on real property assessments";

    public override string Description =>
        "Per municipality, kind and classification: RPUs and assessed values, taxable and exempt, at the start of the half-year, assessed and cancelled during it, and at its end. Provisional layout, to be confirmed by the province.";

    public override IReadOnlyList<ReportParameter> Parameters { get; } = [ReportParameter.HalfYear, ReportParameter.Municipality];
    protected override bool ByClassification => true;
    protected override string PeriodName => "half-year";
    protected override int Months => 6;
}
