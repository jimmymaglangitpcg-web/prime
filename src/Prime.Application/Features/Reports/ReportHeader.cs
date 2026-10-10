using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Offices;

namespace Prime.Application.Features.Reports;

/// <summary>The header block of a report file (docs/analysis/reporting.md §4.1), shared by reports and run downloads.</summary>
internal static class ReportHeader
{
    /// <summary>LGU, office, title, parameters, when it was run and by whom.</summary>
    public static async Task<IReadOnlyList<string>> BuildAsync(IApplicationDbContext db, IOfficeContext office, ICurrentUserService currentUser, IClock clock,
        LguOptions lgu, string title, IReadOnlyList<string> parameterLines, CancellationToken ct)
    {
        var scope = await office.GetAsync(ct);
        var user = currentUser.AppUserId is { } id
            ? await db.AppUsers.Where(u => u.Id == id).Select(u => u.DisplayName).FirstOrDefaultAsync(ct)
            : null;
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(lgu.Name))
        {
            lines.Add(lgu.Name!);
        }
        if ((scope.OfficeName ?? lgu.Office) is { Length: > 0 } officeName)
        {
            lines.Add(officeName);
        }
        lines.Add(title);
        lines.AddRange(parameterLines);
        lines.Add($"Run: {clock.LocalDate(clock.UtcNow):yyyy-MM-dd} by {user ?? "system"}");
        return lines;
    }
}
