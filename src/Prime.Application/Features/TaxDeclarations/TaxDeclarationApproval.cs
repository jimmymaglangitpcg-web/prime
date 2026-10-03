using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Exemptions;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.TaxDeclarations;

/// <summary>
/// The final approval of a Tax Declaration, shared by direct TD approval and
/// transaction approval (docs/FORMS-REVISION-PLAN.md A4–A5), so both apply
/// the same rules: one Approved TD per RPU, and approving a TD that names a
/// previous TD cancels that one ("Cancelled by TD No. …") and carries its
/// unlifted annotations over. The caller owns the database transaction.
/// </summary>
internal static class TaxDeclarationApproval
{
    /// <summary>
    /// The TD <paramref name="td"/> replaces (null if none), or why it cannot be approved.
    /// A TD drafted before its assessment existed is bound here to the assessment
    /// in force; with <paramref name="requireAssessment"/> it cannot be approved without one.
    /// </summary>
    public static async Task<Result<TaxDeclaration?>> CheckAsync(IApplicationDbContext db, TaxDeclaration td, bool requireAssessment, CancellationToken ct)
    {
        td.AssessmentId ??= await FaasTaxDeclarations.AssessmentInForceAsync(db, td.RpuId, td.EffectivityDate, ct);
        if (td.AssessmentId is null && requireAssessment)
        {
            return Result.Failure<TaxDeclaration?>("TAX_DECLARATION_ASSESSMENT_REQUIRED",
                $"TD {td.TaxDeclarationNumber} declares no assessment; approve an assessment for its RPU first.");
        }
        // A TD declaring an assessment is as taxable as its lines (assessment-listing-exemptions.md Q2).
        if (td.AssessmentId is { } assessmentId && await ExemptionTaxability.OfAssessmentAsync(db, assessmentId, ct) is { } taxability)
        {
            td.Taxability = taxability;
        }
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
        if (td.RestoresTaxDeclarationId is { } restoredId
            && await db.TaxDeclarations.AnyAsync(x => x.Id != td.Id && x.RestoresTaxDeclarationId == restoredId && x.Status == WorkflowStatus.Approved, ct))
        {
            return Result.Failure<TaxDeclaration?>("TAX_DECLARATION_ALREADY_RESTORED",
                $"Another approved TD already restores the declaration TD {td.TaxDeclarationNumber} names.");
        }
        if (previous is { Status: WorkflowStatus.Cancelled or WorkflowStatus.Voided })
        {
            return Result.Failure<TaxDeclaration?>("PREVIOUS_TAX_DECLARATION_CANCELLED",
                $"TD {previous.TaxDeclarationNumber} was cancelled after this TD was drafted; this TD can no longer replace it.");
        }
        if (previous is not null && await CancellationGuard.BlockerAsync(db, [previous.Id], ct) is { } blocked)
        {
            return Result.Failure<TaxDeclaration?>(CancellationGuard.Code, blocked);
        }
        return Result.Success(previous);
    }

    /// <summary>
    /// Cancels <paramref name="previous"/> (saved first — the one-approved-TD-per-RPU
    /// index is not deferrable), then approves <paramref name="td"/> and saves.
    /// </summary>
    public static async Task ApplyAsync(IApplicationDbContext db, TaxDeclaration td, TaxDeclaration? previous, Guid? userId,
        DateTimeOffset now, DateOnly today, CancellationToken ct)
    {
        if (previous is not null)
        {
            Cancel(previous, userId, now, $"Cancelled by TD No. {td.TaxDeclarationNumber}.");
            previous.SupersededByTaxDeclarationId = td.Id;
            // A reassessment cancelling an assessment declared in a previous owner's name tells that owner (Q11).
            await Notices.NoticeOfCancellationService.PreviousOwnersAsync(db, previous, td, today, ct);
            await db.SaveChangesAsync(ct);
        }
        td.Status = WorkflowStatus.Approved;
        td.ApprovedBy = userId;
        td.ApprovedAt = now;
        // A restored declaration's unlifted annotations come back with it (§4.3).
        var sources = new[] { previous?.Id, td.RestoresTaxDeclarationId }.OfType<Guid>().ToList();
        if (sources.Count > 0)
        {
            await CarryAnnotationsAsync(db, sources, td.Id, ct);
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Copies to <paramref name="targetId"/> every annotation still in force (not lifted) on the replaced TDs
    /// <paramref name="sourceIds"/> whose type carries over, each pointing back to its source
    /// (docs/analysis/records-and-forms.md §4.4, Q7). One already carried to the target is not copied again.
    /// The caller saves.
    /// </summary>
    public static async Task CarryAnnotationsAsync(IApplicationDbContext db, IReadOnlyCollection<Guid> sourceIds, Guid targetId, CancellationToken ct)
    {
        if (sourceIds.Count == 0)
        {
            return;
        }
        var carried = await db.TaxDeclarationAnnotations.AsNoTracking()
            .Where(x => sourceIds.Contains(x.TaxDeclarationId) && x.LiftedAt == null && x.AnnotationType!.CarriesOver
                && !db.TaxDeclarationAnnotations.Any(c => c.TaxDeclarationId == targetId && c.CarriedFromAnnotationId == x.Id))
            .OrderBy(x => x.EffectiveDate).ThenBy(x => x.CreatedAt).ToListAsync(ct);
        db.TaxDeclarationAnnotations.AddRange(carried.Select(a => new TaxDeclarationAnnotation
        {
            TaxDeclarationId = targetId, AnnotationTypeId = a.AnnotationTypeId, Text = a.Text,
            ReferenceNumber = a.ReferenceNumber, ReferenceDate = a.ReferenceDate, EffectiveDate = a.EffectiveDate,
            CarriedFromAnnotationId = a.Id,
        }));
    }

    public static void Cancel(TaxDeclaration td, Guid? userId, DateTimeOffset now, string reason)
    {
        td.Status = WorkflowStatus.Cancelled;
        td.CancelledAt = now;
        td.CancelledBy = userId;
        td.CancellationReason = reason;
    }
}
