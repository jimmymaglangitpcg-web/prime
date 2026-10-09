using Microsoft.EntityFrameworkCore;

namespace Prime.Application.Common;

/// <summary>
/// Server-side pagination envelope — every list endpoint uses this, never
/// an unbounded array (CLAUDE.md §71/§56).
/// </summary>
public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int TotalCount { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    /// <summary>The list stopped counting at <see cref="TotalCount"/>: there are more rows (a capped count, e.g. the audit trail's).</summary>
    public bool TotalIsLowerBound { get; init; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>Common paging/sorting request shape for list endpoints.</summary>
public class PagedRequest
{
    private const int MaxPageSize = 100;
    private int _pageSize = 20;

    public int Page { get; set; } = 1;

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value is < 1 or > MaxPageSize ? Math.Clamp(value, 1, MaxPageSize) : value;
    }
}

/// <summary>
/// A count that stops at <see cref="Limit"/>, for lists over tables that only grow (production-hardening.md §9, H4). An exact
/// count of a broad filter reads every match: 0.6 s over 1.2 million audit rows, up to 1 s for a broad search of 250,000
/// properties, and more each year. Beyond the limit the list says there are more and the user narrows the filter.
/// </summary>
public static class CappedCount
{
    /// <summary>The most rows a list counts, and so the deepest page it serves.</summary>
    public const int Limit = 10_000;

    /// <summary>The count up to the limit, whether there are more, and the page to serve (one past the limit is the last).</summary>
    public static async Task<(int Total, bool More, int Page)> CountAsync<T>(IQueryable<T> query, int page, int pageSize, CancellationToken ct)
    {
        var counted = await query.Take(Limit + 1).CountAsync(ct);
        return (Math.Min(counted, Limit), counted > Limit, Math.Min(Math.Max(page, 1), Limit / pageSize + 1));
    }
}
