using Microsoft.EntityFrameworkCore;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Common;
using Prime.Domain.Enums;

namespace Prime.Application.Common;

/// <summary>
/// Approval of an <see cref="EffectiveDatedConfiguration"/> version
/// (docs/FORMS-REVISION-PLAN.md §4) — the BillingRuleService pattern: only a
/// Draft can be approved, never by its creator (CLAUDE.md §46), it must start
/// after every approved version of the same scope, and the open predecessor
/// is closed the day before it starts, in one transaction.
/// </summary>
internal static class ConfigurationApproval
{
    /// <summary>Null on success, else a failed <see cref="Result"/> with <paramref name="codePrefix"/>-based codes.</summary>
    public static async Task<Result?> ApproveAsync<T>(
        IApplicationDbContext db, ICurrentUserService currentUser, IQueryable<T> sameScope, T item, string codePrefix, CancellationToken ct)
        where T : EffectiveDatedConfiguration
    {
        if (item.Status != WorkflowStatus.Draft)
        {
            return Result.Failure($"{codePrefix}_NOT_DRAFT", "Only a Draft version can be approved.");
        }
        if (currentUser.AppUserId is not null && item.CreatedBy == currentUser.AppUserId)
        {
            return Result.Failure($"CANNOT_APPROVE_OWN_{codePrefix}", "The creator cannot also approve it (maker-checker, CLAUDE.md §46).");
        }

        var approved = await sameScope.Where(x => x.Id != item.Id && x.Status == WorkflowStatus.Approved).ToListAsync(ct);
        if (approved.FirstOrDefault(x => x.EffectiveDate >= item.EffectiveDate) is { } later)
        {
            return Result.Failure($"{codePrefix}_EFFECTIVE_DATE_CONFLICT",
                $"An approved version already starts on {later.EffectiveDate:yyyy-MM-dd}; a new version must start after it.");
        }
        var open = approved.SingleOrDefault(x => x.EndDate is null);

        // Close the predecessor before approving: the one-open-approved index is not deferrable.
        var ownsTransaction = db.Database.CurrentTransaction is null;
        var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            if (open is not null)
            {
                open.EndDate = item.EffectiveDate.AddDays(-1);
                await db.SaveChangesAsync(ct);
            }
            item.Status = WorkflowStatus.Approved;
            item.ApprovedBy = currentUser.AppUserId;
            item.ApprovedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }
        }
        catch (DbUpdateException)
        {
            return Result.Failure($"{codePrefix}_APPROVAL_CONFLICT",
                "Another version for the same scope was approved at the same time. Nothing was changed; reload and try again.");
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
        return null;
    }

    public static IQueryable<T> InForce<T>(this IQueryable<T> query, DateOnly date) where T : EffectiveDatedConfiguration =>
        query.Where(x => x.Status == WorkflowStatus.Approved && x.EffectiveDate <= date && (x.EndDate == null || x.EndDate >= date));
}
