using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.TaxDeclarations;

/// <summary>
/// The final approval of a Tax Declaration, shared by direct TD approval and
/// transaction approval (docs/FORMS-REVISION-PLAN.md A4–A5), so both apply
/// the same rules: one Approved TD per RPU, and approving a TD that names a
/// previous TD cancels that one ("Cancelled by TD No. …"). The caller owns
/// the database transaction.
/// </summary>
internal static class TaxDeclarationApproval
{
    /// <summary>The TD <paramref name="td"/> replaces (null if none), or why it cannot be approved.</summary>
    public static async Task<Result<TaxDeclaration?>> CheckAsync(IApplicationDbContext db, TaxDeclaration td, CancellationToken ct)
    {
        var current = await db.TaxDeclarations.FirstOrDefaultAsync(
            x => x.RpuId == td.RpuId && x.Id != td.Id && x.Status == WorkflowStatus.Approved, ct);
        if (current is not null && current.Id != td.PreviousTaxDeclarationId)
        {
            return Result.Failure<TaxDeclaration?>("TAX_DECLARATION_CURRENT_EXISTS",
                $"TD {current.TaxDeclarationNumber} is the current declaration for this RPU; a new TD must name it as the previous TD, which it then cancels.");
        }
        var previous = td.PreviousTaxDeclarationId is { } previousId
            ? await db.TaxDeclarations.FirstOrDefaultAsync(x => x.Id == previousId, ct)
            : null;
        if (previous is { Status: WorkflowStatus.Cancelled or WorkflowStatus.Voided })
        {
            return Result.Failure<TaxDeclaration?>("PREVIOUS_TAX_DECLARATION_CANCELLED",
                $"TD {previous.TaxDeclarationNumber} was cancelled after this TD was drafted; this TD can no longer replace it.");
        }
        return Result.Success(previous);
    }

    /// <summary>
    /// Cancels <paramref name="previous"/> (saved first — the one-approved-TD-per-RPU
    /// index is not deferrable), then approves <paramref name="td"/> and saves.
    /// </summary>
    public static async Task ApplyAsync(IApplicationDbContext db, TaxDeclaration td, TaxDeclaration? previous, Guid? userId,
        DateTimeOffset now, CancellationToken ct)
    {
        if (previous is not null)
        {
            Cancel(previous, userId, now, $"Cancelled by TD No. {td.TaxDeclarationNumber}.");
            previous.SupersededByTaxDeclarationId = td.Id;
            await db.SaveChangesAsync(ct);
        }
        td.Status = WorkflowStatus.Approved;
        td.ApprovedBy = userId;
        td.ApprovedAt = now;
        await db.SaveChangesAsync(ct);
    }

    public static void Cancel(TaxDeclaration td, Guid? userId, DateTimeOffset now, string reason)
    {
        td.Status = WorkflowStatus.Cancelled;
        td.CancelledAt = now;
        td.CancelledBy = userId;
        td.CancellationReason = reason;
    }
}
