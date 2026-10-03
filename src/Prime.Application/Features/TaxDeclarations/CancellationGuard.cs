using Microsoft.EntityFrameworkCore;
using Prime.Application.Common.Interfaces;

namespace Prime.Application.Features.TaxDeclarations;

/// <summary>
/// A pending court claim stops a cancellation (LAM 2025 Book III p.89: cancellation applies only where no adverse claim
/// is pending resolution in court; docs/analysis/assessment-listing-exemptions.md §4.4, Q12). Checked wherever a TD is
/// cancelled or replaced: directly, by a transaction, or by a TD that names it as the one it replaces.
/// </summary>
public static class CancellationGuard
{
    public const string Code = "TD_CANCELLATION_BLOCKED";

    /// <summary>Why one of <paramref name="taxDeclarationIds"/> cannot be cancelled now, or null.</summary>
    public static async Task<string?> BlockerAsync(IApplicationDbContext db, IReadOnlyCollection<Guid> taxDeclarationIds, CancellationToken ct)
    {
        if (taxDeclarationIds.Count == 0)
        {
            return null;
        }
        var blocking = await db.TaxDeclarationAnnotations.AsNoTracking()
            .Where(x => taxDeclarationIds.Contains(x.TaxDeclarationId) && x.LiftedAt == null && x.AnnotationType!.BlocksCancellation)
            .OrderBy(x => x.EffectiveDate)
            .Select(x => new
            {
                Type = x.AnnotationType!.Name, x.Text, x.ReferenceNumber,
                Td = db.TaxDeclarations.Where(t => t.Id == x.TaxDeclarationId).Select(t => t.TaxDeclarationNumber).FirstOrDefault(),
            })
            .FirstOrDefaultAsync(ct);
        return blocking is null ? null
            : $"TD {blocking.Td} carries an unlifted annotation \"{blocking.Type}\" ({blocking.Text}{(blocking.ReferenceNumber is { } r ? $", ref. {r}" : "")}); "
              + "it cannot be cancelled or replaced until the annotation is lifted, with the court's resolution as reference.";
    }
}
