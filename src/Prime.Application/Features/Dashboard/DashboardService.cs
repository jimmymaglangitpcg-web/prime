using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Reports;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Dashboard;

/// <summary>The jurisdiction's totals as of the dashboard's date: registered properties and parcels, and the FAAS in force.</summary>
/// <param name="Properties">Active properties registered in the jurisdiction.</param>
/// <param name="Parcels">Parcels of those properties.</param>
/// <param name="PropertiesWithFaas">Properties with at least one FAAS in force.</param>
/// <param name="UnitsInForce">Units with a FAAS in force.</param>
/// <param name="UnconvertedLandUnits">Land units in an area unit other than square metres or hectares, left out of the land area.</param>
public sealed record DashboardFiguresDto(
    int Properties, int Parcels, int PropertiesWithFaas, int UnitsInForce, decimal LandAreaSqm, int UnconvertedLandUnits,
    decimal TaxableMarketValue, decimal ExemptMarketValue, decimal TaxableAssessedValue, decimal ExemptAssessedValue);

/// <summary>One bar of a dashboard chart. <see cref="IsOthers"/> is the sum of the groups beyond the first ten.</summary>
public sealed record DashboardGroupDto(Guid? Key, string Label, string? Detail, int Properties, decimal MarketValue, decimal AssessedValue, bool IsOthers = false);

/// <summary>An open general revision's progress over its items in the user's jurisdiction.</summary>
/// <param name="Valued">Items valued and assessed by a run.</param>
/// <param name="Posted">Items whose revision assessment is posted.</param>
/// <param name="Declared">Items whose revision assessment is declared by an approved Tax Declaration.</param>
public sealed record DashboardGeneralRevisionDto(
    Guid Id, int RevisionYear, GeneralRevisionStatus Status, string Scope, int Items, int Valued, int Failed, int Excluded, int Posted, int Declared);

public sealed record DashboardTransactionDto(
    Guid Id, Guid PropertyId, string Pin, string? TransactionNumber, string TypeName, WorkflowStatus Status, DateTimeOffset CreatedAt);

/// <param name="Kind">TaxDeclaration, Assessment or Transaction.</param>
/// <param name="Reference">The TD number, the assessment's year and unit, or the transaction's number and type.</param>
public sealed record DashboardApprovalDto(
    string Kind, Guid Id, Guid PropertyId, string Pin, string Reference, string? ApprovedBy, DateTimeOffset ApprovedAt);

/// <summary>The dashboard (CLAUDE.md §55; docs/analysis/reporting.md §4.3).</summary>
/// <param name="ComputedAt">When the cached figures were read; the lists are read on each request.</param>
public sealed record DashboardDto(
    DateOnly AsOf, DateTimeOffset ComputedAt, DashboardFiguresDto Figures,
    IReadOnlyList<DashboardGroupDto> ByClassification, IReadOnlyList<DashboardGroupDto> ByBarangay,
    IReadOnlyList<DashboardGeneralRevisionDto> GeneralRevisions,
    IReadOnlyList<DashboardTransactionDto> RecentTransactions, IReadOnlyList<DashboardApprovalDto> RecentApprovals);

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// The dashboard's figures for the user's jurisdiction, as of today (docs/analysis/reporting.md §4.3). Real data only: an
/// empty database shows zeros. The totals and charts are the FAAS in force, read in one pass (<see cref="IFaasInForceQuery"/>),
/// and are cached for <see cref="CacheDuration"/> per jurisdiction and date (Q8), since a province-wide pass takes seconds.
/// The recent lists are read on each request. The items awaiting the user's approval come from the approvals queue, which
/// depends on the user, not only the jurisdiction. Pending appeals are left out while L7 is deferred.
/// </summary>
public sealed class DashboardService(
    IApplicationDbContext db,
    IFaasInForceQuery faasInForce,
    IJurisdiction jurisdiction,
    IClock clock,
    IMemoryCache cache) : IDashboardService
{
    public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

    /// <summary>Bars per chart; the rest are summed into "Others".</summary>
    public const int ChartGroups = 10;

    private const int RecentCount = 10;

    private sealed record Cached(
        DateTimeOffset ComputedAt, DashboardFiguresDto Figures, IReadOnlyList<DashboardGroupDto> ByClassification,
        IReadOnlyList<DashboardGroupDto> ByBarangay, IReadOnlyList<DashboardGeneralRevisionDto> GeneralRevisions);

    public async Task<DashboardDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var today = clock.Today;
        var key = $"dashboard:{today:yyyy-MM-dd}:"
                  + (jurisdiction.Restricted ? string.Join(",", jurisdiction.MunicipalityIds.Order()) : "all");
        var cached = await cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await ComputeAsync(today, cancellationToken);
        });
        return new DashboardDto(today, cached!.ComputedAt, cached.Figures, cached.ByClassification, cached.ByBarangay, cached.GeneralRevisions,
            await RecentTransactionsAsync(cancellationToken), await RecentApprovalsAsync(cancellationToken));
    }

    private async Task<Cached> ComputeAsync(DateOnly today, CancellationToken ct)
    {
        var groups = await faasInForce.SummaryAsync(new FaasScope(today), [FaasGroupBy.Classification, FaasGroupBy.Barangay], ct);
        var total = groups.FirstOrDefault(g => g.IsTotal) ?? new FaasGroup { IsTotal = true };
        var properties = db.Properties.AsNoTracking().Where(p => p.Status == RecordStatus.Active);
        var figures = new DashboardFiguresDto(
            await properties.CountAsync(ct),
            await db.Parcels.AsNoTracking().CountAsync(x => properties.Any(p => p.Id == x.PropertyId), ct),
            total.Properties, total.Units, total.LandAreaSqm, total.UnconvertedLandUnits,
            total.TaxableMarketValue, total.ExemptMarketValue, total.TaxableAssessedValue, total.ExemptAssessedValue);

        var byClass = groups.Where(g => g.GroupBy == FaasGroupBy.Classification).ToList();
        var classIds = byClass.Select(g => g.Key).OfType<Guid>().ToList();
        var classes = await db.Classifications.AsNoTracking().Where(c => classIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => (c.Name, c.Code), ct);
        var byBarangay = groups.Where(g => g.GroupBy == FaasGroupBy.Barangay).ToList();
        var barangayIds = byBarangay.Select(g => g.Key).OfType<Guid>().ToList();
        var barangays = await db.Barangays.AsNoTracking().Where(b => barangayIds.Contains(b.Id))
            .Select(b => new { b.Id, b.Name, Municipality = b.Municipality!.Name })
            .ToDictionaryAsync(b => b.Id, b => (b.Name, b.Municipality), ct);

        return new Cached(
            clock.UtcNow,
            figures,
            Chart(byClass, k => classes.TryGetValue(k, out var c) ? (c.Name, c.Code) : ("Not recorded", null)),
            Chart(byBarangay, k => barangays.TryGetValue(k, out var b) ? (b.Name, b.Municipality) : ("Not recorded", null)),
            await GeneralRevisionsAsync(ct));
    }

    /// <summary>The groups by assessed value, largest first; beyond <see cref="ChartGroups"/>, one "Others" bar.</summary>
    public static IReadOnlyList<DashboardGroupDto> Chart(IEnumerable<FaasGroup> groups, Func<Guid, (string Label, string? Detail)> label)
    {
        var bars = groups
            .Select(g =>
            {
                var (name, detail) = g.Key is { } k ? label(k) : ("Not recorded", null);
                return new DashboardGroupDto(g.Key, name, detail, g.Properties, g.TaxableMarketValue + g.ExemptMarketValue,
                    g.TaxableAssessedValue + g.ExemptAssessedValue);
            })
            .OrderByDescending(b => b.AssessedValue).ThenByDescending(b => b.Properties).ThenBy(b => b.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (bars.Count <= ChartGroups + 1)
        {
            return bars;
        }
        var rest = bars.Skip(ChartGroups).ToList();
        return
        [
            .. bars.Take(ChartGroups),
            new DashboardGroupDto(null, $"Others ({rest.Count})", null, rest.Sum(b => b.Properties), rest.Sum(b => b.MarketValue),
                rest.Sum(b => b.AssessedValue), IsOthers: true),
        ];
    }

    /// <summary>Planned and in-progress programmes covering a municipality of the jurisdiction, counted over its items there.</summary>
    private async Task<IReadOnlyList<DashboardGeneralRevisionDto>> GeneralRevisionsAsync(CancellationToken ct)
    {
        var ids = jurisdiction.MunicipalityIds;
        var programmes = await db.GeneralRevisionProgrammes.AsNoTracking()
            .Where(p => p.Status == GeneralRevisionStatus.Planned || p.Status == GeneralRevisionStatus.InProgress)
            .Where(p => !jurisdiction.Restricted || p.Scope.Any(s => ids.Contains(s.MunicipalityId)))
            .OrderByDescending(p => p.RevisionYear).ThenByDescending(p => p.CreatedAt)
            .Select(p => new { p.Id, p.RevisionYear, p.Status, Scope = p.Scope.Select(s => s.Municipality!.Name).ToList() })
            .ToListAsync(ct);
        var result = new List<DashboardGeneralRevisionDto>();
        foreach (var p in programmes)
        {
            var items = db.GeneralRevisionItems.AsNoTracking().Where(x => x.GeneralRevisionProgrammeId == p.Id);
            var byStatus = await items.GroupBy(x => x.Status).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
            var posted = await items.CountAsync(x => x.Assessment != null && x.Assessment.Status == WorkflowStatus.Posted, ct);
            var declared = await items.CountAsync(x => x.AssessmentId != null
                && db.TaxDeclarations.Any(t => t.AssessmentId == x.AssessmentId && t.Status == WorkflowStatus.Approved), ct);
            result.Add(new DashboardGeneralRevisionDto(p.Id, p.RevisionYear, p.Status, string.Join(", ", p.Scope.Order()),
                byStatus.Values.Sum(), byStatus.GetValueOrDefault(GeneralRevisionItemStatus.Assessed), byStatus.GetValueOrDefault(GeneralRevisionItemStatus.Failed),
                byStatus.GetValueOrDefault(GeneralRevisionItemStatus.Excluded), posted, declared));
        }
        return result;
    }

    private async Task<IReadOnlyList<DashboardTransactionDto>> RecentTransactionsAsync(CancellationToken ct) =>
        await db.PropertyTransactions.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(RecentCount)
            .Select(x => new DashboardTransactionDto(x.Id, x.PropertyId, x.Property!.PropertyIdentificationNumber, x.TransactionNumber, x.TypeName,
                x.Status, x.CreatedAt))
            .ToListAsync(ct);

    /// <summary>The latest approvals of Tax Declarations, assessments and transactions in the jurisdiction.</summary>
    private async Task<IReadOnlyList<DashboardApprovalDto>> RecentApprovalsAsync(CancellationToken ct)
    {
        var tds = await db.TaxDeclarations.AsNoTracking().Where(x => x.ApprovedAt != null)
            .OrderByDescending(x => x.ApprovedAt).Take(RecentCount)
            .Select(x => new { Kind = "TaxDeclaration", x.Id, x.PropertyId, Pin = x.Property!.PropertyIdentificationNumber, Reference = x.TaxDeclarationNumber, x.ApprovedBy, At = x.ApprovedAt!.Value })
            .ToListAsync(ct);
        var assessments = await db.Assessments.AsNoTracking().Where(x => x.ApprovedAt != null)
            .OrderByDescending(x => x.ApprovedAt).Take(RecentCount)
            .Select(x => new { Kind = "Assessment", x.Id, x.PropertyId, Pin = x.Property!.PropertyIdentificationNumber, Reference = x.AssessmentYear + " · RPU " + x.Rpu!.RpuNumber, x.ApprovedBy, At = x.ApprovedAt!.Value })
            .ToListAsync(ct);
        var transactions = await db.PropertyTransactions.AsNoTracking().Where(x => x.ApprovedAt != null)
            .OrderByDescending(x => x.ApprovedAt).Take(RecentCount)
            .Select(x => new { Kind = "Transaction", x.Id, x.PropertyId, Pin = x.Property!.PropertyIdentificationNumber, Reference = (x.TransactionNumber ?? "—") + " · " + x.TypeName, x.ApprovedBy, At = x.ApprovedAt!.Value })
            .ToListAsync(ct);
        var latest = tds.Concat(assessments).Concat(transactions).OrderByDescending(x => x.At).Take(RecentCount).ToList();
        var userIds = latest.Select(x => x.ApprovedBy).OfType<Guid>().Distinct().ToList();
        var names = await db.AppUsers.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        return latest.Select(x => new DashboardApprovalDto(x.Kind, x.Id, x.PropertyId, x.Pin, x.Reference,
            x.ApprovedBy is { } u ? names.GetValueOrDefault(u) : null, x.At)).ToList();
    }
}
