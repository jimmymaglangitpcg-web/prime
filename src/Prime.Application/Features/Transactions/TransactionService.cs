using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Approvals;
using Prime.Application.Features.Numbering;
using Prime.Application.Features.Properties;
using Prime.Application.Features.TaxDeclarations;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Transactions;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Transactions;

public sealed class CreateTransactionTypeRequestValidator : AbstractValidator<CreateTransactionTypeRequest>
{
    public CreateTransactionTypeRequestValidator()
    {
        RuleFor(x => x.LegalBasis).NotEmpty().MaximumLength(500);
        RuleFor(x => x.EffectiveDate).NotEqual(default(DateOnly)).WithMessage("effectiveDate is required.");
        RuleFor(x => x.Remarks).MaximumLength(1000);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleForEach(x => x.Requirements).ChildRules(r =>
        {
            r.RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
            r.RuleFor(x => x.Label).NotEmpty().MaximumLength(300);
            r.RuleFor(x => x.LegalBasis).MaximumLength(500);
        });
        RuleFor(x => x.Requirements)
            .Must(list => list.Select(s => s.Sequence).OrderBy(s => s).SequenceEqual(Enumerable.Range(1, list.Count)))
            .When(x => x.Requirements is { Count: > 0 })
            .WithMessage("Requirement sequences must be 1..n with no gaps or duplicates.");
    }
}

public sealed class OpenTransactionRequestValidator : AbstractValidator<OpenTransactionRequest>
{
    public OpenTransactionRequestValidator()
    {
        RuleFor(x => x.TransactionTypeId).NotEmpty();
        RuleFor(x => x.PropertyId).NotEmpty();
        RuleFor(x => x.EffectiveDate).NotEqual(default(DateOnly)).WithMessage("effectiveDate is required.");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
        RuleForEach(x => x.NewParties).ChildRules(p =>
        {
            p.RuleFor(x => x.Role).IsInEnum();
            p.RuleFor(x => x.TaxpayerId).Null().When(x => x.Role == PropertyPartyRole.UnknownOwner);
            p.RuleFor(x => x.TaxpayerId).NotEmpty().When(x => x.Role != PropertyPartyRole.UnknownOwner);
            p.RuleFor(x => x.OwnershipTypeId).NotEmpty().When(x => x.Role == PropertyPartyRole.Owner);
            p.RuleFor(x => x.OwnershipTypeId).Null().When(x => x.Role != PropertyPartyRole.Owner);
            p.RuleFor(x => x.OwnershipPercentage).InclusiveBetween(0, 100);
        });
    }
}

public interface ITransactionService
{
    Task<Result<TransactionTypeDto>> CreateTypeAsync(CreateTransactionTypeRequest request, CancellationToken cancellationToken = default);
    Task<Result<TransactionTypeDto>> ApproveTypeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<TransactionTypeDto>>> ListTypesAsync(bool inForceOnly, CancellationToken cancellationToken = default);

    Task<Result<PropertyTransactionDto>> OpenAsync(OpenTransactionRequest request, CancellationToken cancellationToken = default);
    Task<Result<PropertyTransactionDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Transactions filed on the property or naming it as a related property.</summary>
    Task<Result<IReadOnlyList<PropertyTransactionDto>>> ListByPropertyAsync(Guid propertyId, CancellationToken cancellationToken = default);
    Task<Result<PropertyTransactionDto>> SatisfyRequirementAsync(Guid id, Guid requirementId, SatisfyRequirementRequest request, CancellationToken cancellationToken = default);
    Task<Result<PropertyTransactionDto>> SubmitAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<PropertyTransactionDto>> ApproveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<PropertyTransactionDto>> RejectAsync(Guid id, string reason, CancellationToken cancellationToken = default);
    Task<Result<PropertyTransactionDto>> WithdrawAsync(Guid id, string reason, CancellationToken cancellationToken = default);
}

/// <summary>
/// Property transactions and their catalogue (CLAUDE.md §34–§37;
/// docs/FORMS-REVISION-PLAN.md §4.7, A5). Approval applies everything at
/// once, in one database transaction: the TDs it lists for cancellation
/// are cancelled, its own TDs are approved (cancelling the TDs they
/// replace), and a transfer ends the current owners and starts the new
/// parties — each change pointing back to the transaction.
/// </summary>
public sealed class TransactionService(
    IApplicationDbContext db,
    IValidator<CreateTransactionTypeRequest> typeValidator,
    IValidator<OpenTransactionRequest> openValidator,
    ICurrentUserService currentUser,
    IApprovalChainService approvals,
    INumberingService numbering,
    IClock clock,
    IOptions<FaasOptions> faas) : ITransactionService
{
    // --- Catalogue ---

    public async Task<Result<TransactionTypeDto>> CreateTypeAsync(CreateTransactionTypeRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await typeValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<TransactionTypeDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }
        var type = new TransactionType
        {
            LegalBasis = request.LegalBasis, EffectiveDate = request.EffectiveDate, Remarks = request.Remarks,
            Code = request.Code.Trim(), Name = request.Name, Kind = request.Kind, Rank = request.Rank, Description = request.Description,
            Requirements = request.Requirements.OrderBy(r => r.Sequence).Select(r => new TransactionTypeRequirement
            {
                Sequence = r.Sequence, Code = r.Code, Label = r.Label, IsMandatory = r.IsMandatory, LegalBasis = r.LegalBasis,
            }).ToList(),
        };
        db.TransactionTypes.Add(type);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(type));
    }

    public async Task<Result<TransactionTypeDto>> ApproveTypeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var type = await db.TransactionTypes.Include(x => x.Requirements).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (type is null)
        {
            return Result.Failure<TransactionTypeDto>("TRANSACTION_TYPE_NOT_FOUND", "No transaction type was found with the given id.");
        }
        if (await ConfigurationApproval.ApproveAsync(db, currentUser, db.TransactionTypes.Where(x => x.Code == type.Code),
                type, "TRANSACTION_TYPE", cancellationToken) is { } failure)
        {
            return Result.Failure<TransactionTypeDto>(failure.Code!, failure.Message!);
        }
        return Result.Success(ToDto(type));
    }

    public async Task<Result<IReadOnlyList<TransactionTypeDto>>> ListTypesAsync(bool inForceOnly, CancellationToken cancellationToken = default)
    {
        var query = db.TransactionTypes.Include(x => x.Requirements).AsQueryable();
        if (inForceOnly)
        {
            query = query.InForce(clock.Today);
        }
        var types = await query.OrderBy(x => x.Rank ?? int.MaxValue).ThenBy(x => x.Code).ThenByDescending(x => x.EffectiveDate)
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<TransactionTypeDto>>(types.Select(ToDto).ToList());
    }

    // --- Transactions ---

    public async Task<Result<PropertyTransactionDto>> OpenAsync(OpenTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await openValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Fail("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }
        var type = await db.TransactionTypes.Include(x => x.Requirements).InForce(clock.Today)
            .FirstOrDefaultAsync(x => x.Id == request.TransactionTypeId, cancellationToken);
        if (type is null)
        {
            return Fail("TRANSACTION_TYPE_NOT_IN_FORCE", "The transaction type is not an approved type in force today.");
        }
        if (!await db.Properties.AnyAsync(x => x.Id == request.PropertyId, cancellationToken))
        {
            return Fail("PROPERTY_NOT_FOUND", "No property was found with the given id.");
        }

        var related = request.RelatedProperties ?? [];
        if (related.Any(r => r.PropertyId == request.PropertyId) || related.Select(r => r.PropertyId).Distinct().Count() != related.Count)
        {
            return Fail("VALIDATION_FAILED", "Related properties must be distinct and differ from the transaction's property.");
        }
        foreach (var r in related)
        {
            if (!await db.Properties.AnyAsync(x => x.Id == r.PropertyId, cancellationToken))
            {
                return Fail("PROPERTY_NOT_FOUND", $"Related property {r.PropertyId} does not exist.");
            }
        }

        var parties = request.NewParties ?? [];
        if (parties.Count > 0 && type.Kind != PropertyTransactionKind.Transfer)
        {
            return Fail("TRANSACTION_PARTIES_NOT_ALLOWED", "Only a transfer changes the property's parties.");
        }
        if (request.TransferRpuId is { } unitId)
        {
            if (type.Kind != PropertyTransactionKind.Transfer)
            {
                return Fail("TRANSACTION_UNIT_NOT_ALLOWED", "Only a transfer can target one unit.");
            }
            // The land passes with the property itself; a unit transfer is for what stands on it.
            if (!await db.RealPropertyUnits.AnyAsync(x => x.Id == unitId && x.PropertyId == request.PropertyId && x.RpuType != RpuType.Land, cancellationToken))
            {
                return Fail("TRANSACTION_UNIT_INVALID", "The unit must be a building, machinery or other-improvement RPU of the transaction's property.");
            }
        }
        foreach (var p in parties)
        {
            if (p.TaxpayerId is { } tp && !await db.Taxpayers.AnyAsync(x => x.Id == tp, cancellationToken))
            {
                return Fail("TAXPAYER_NOT_FOUND", $"Taxpayer {tp} does not exist.");
            }
        }

        var propertyIds = related.Select(r => r.PropertyId).Append(request.PropertyId).ToList();
        var cancelIds = (request.CancelTaxDeclarationIds ?? []).Distinct().ToList();
        var toCancel = await db.TaxDeclarations.Where(x => cancelIds.Contains(x.Id)).ToListAsync(cancellationToken);
        if (toCancel.Count != cancelIds.Count || toCancel.Any(t => !propertyIds.Contains(t.PropertyId) || t.Status != WorkflowStatus.Approved))
        {
            return Fail("TRANSACTION_TD_NOT_CANCELLABLE",
                "Every TD to cancel must be a current (Approved) TD of the transaction's property or one of its related properties.");
        }

        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        var context = await NumberContexts.ForPropertyAsync(db, request.PropertyId, request.EffectiveDate.Year, cancellationToken);
        var number = await numbering.GenerateIfConfiguredAsync(NumberedDocumentKind.PropertyTransaction, context, clock.Today, cancellationToken);
        if (number.IsFailure)
        {
            return Fail(number.Code!, number.Message!);
        }

        var tx = new PropertyTransaction
        {
            TransactionTypeId = type.Id, TypeCode = type.Code, TypeName = type.Name, Kind = type.Kind,
            TransactionNumber = number.Value, PropertyId = request.PropertyId, EffectiveDate = request.EffectiveDate,
            Description = request.Description.Trim(), TransferRpuId = request.TransferRpuId,
            Requirements = type.Requirements.OrderBy(r => r.Sequence).Select(r => new PropertyTransactionRequirement
            {
                Sequence = r.Sequence, Code = r.Code, Label = r.Label, IsMandatory = r.IsMandatory, LegalBasis = r.LegalBasis,
            }).ToList(),
            NewParties = parties.Select(p => new PropertyTransactionParty
            {
                Role = p.Role, TaxpayerId = p.TaxpayerId, OwnershipTypeId = p.OwnershipTypeId,
                OwnershipPercentage = p.Role == PropertyPartyRole.UnknownOwner ? 0 : p.OwnershipPercentage,
            }).ToList(),
            TdCancellations = cancelIds.Select(id => new PropertyTransactionTdCancellation { TaxDeclarationId = id }).ToList(),
            RelatedProperties = related.Select(r => new PropertyTransactionProperty { PropertyId = r.PropertyId, Role = r.Role }).ToList(),
        };
        db.PropertyTransactions.Add(tx);
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        return Result.Success(await MapAsync(tx.Id, cancellationToken));
    }

    public async Task<Result<PropertyTransactionDto>> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.PropertyTransactions.AnyAsync(x => x.Id == id, cancellationToken)
            ? Result.Success(await MapAsync(id, cancellationToken))
            : NotFound();

    public async Task<Result<IReadOnlyList<PropertyTransactionDto>>> ListByPropertyAsync(Guid propertyId, CancellationToken cancellationToken = default)
    {
        var ids = await db.PropertyTransactions
            .Where(x => x.PropertyId == propertyId || x.RelatedProperties.Any(r => r.PropertyId == propertyId))
            .OrderByDescending(x => x.EffectiveDate).ThenByDescending(x => x.CreatedAt)
            .Select(x => x.Id).ToListAsync(cancellationToken);
        var list = new List<PropertyTransactionDto>();
        foreach (var id in ids)
        {
            list.Add(await MapAsync(id, cancellationToken));
        }
        return Result.Success<IReadOnlyList<PropertyTransactionDto>>(list);
    }

    public async Task<Result<PropertyTransactionDto>> SatisfyRequirementAsync(Guid id, Guid requirementId, SatisfyRequirementRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.EvidenceReference) || request.EvidenceReference.Length > 200 || request.Note?.Length > 1000)
        {
            return Fail("VALIDATION_FAILED", "evidenceReference is required (max 200); note max 1000.");
        }
        var tx = await db.PropertyTransactions.Include(x => x.Requirements).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (tx is null)
        {
            return NotFound();
        }
        if (tx.Status is not (WorkflowStatus.Draft or WorkflowStatus.PendingReview))
        {
            return Fail("PROPERTY_TRANSACTION_CLOSED", $"A {tx.Status} transaction cannot be changed.");
        }
        var requirement = tx.Requirements.SingleOrDefault(r => r.Id == requirementId);
        if (requirement is null)
        {
            return Fail("TRANSACTION_REQUIREMENT_NOT_FOUND", "The requirement does not belong to this transaction.");
        }
        if (requirement.SatisfiedAt is not null)
        {
            return Fail("TRANSACTION_REQUIREMENT_ALREADY_SATISFIED", "This requirement is already satisfied.");
        }
        requirement.SatisfiedAt = clock.UtcNow;
        requirement.SatisfiedBy = currentUser.AppUserId;
        requirement.EvidenceReference = request.EvidenceReference.Trim();
        requirement.Note = request.Note;
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(await MapAsync(id, cancellationToken));
    }

    public async Task<Result<PropertyTransactionDto>> SubmitAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tx = await LoadAsync(id, cancellationToken);
        if (tx is null)
        {
            return NotFound();
        }
        if (tx.Status != WorkflowStatus.Draft)
        {
            return Fail("PROPERTY_TRANSACTION_NOT_DRAFT", "Only a Draft transaction can be submitted.");
        }
        if (tx.Requirements.Where(r => r.IsMandatory && r.SatisfiedAt is null).Select(r => r.Label).ToList() is { Count: > 0 } open)
        {
            return Fail("TRANSACTION_REQUIREMENTS_UNMET", $"Mandatory requirements not yet satisfied: {string.Join("; ", open)}.");
        }
        var tds = await db.TaxDeclarations.Where(x => x.PropertyTransactionId == id).ToListAsync(cancellationToken);
        if (tds.Count == 0 && tx.TdCancellations.Count == 0 && tx.NewParties.Count == 0)
        {
            return Fail("TRANSACTION_EMPTY", "The transaction does nothing yet: add a Tax Declaration, a TD to cancel, or (for a transfer) the new parties.");
        }
        if (tx.Kind == PropertyTransactionKind.Transfer && PartiesProblem(tx.NewParties) is { } problem)
        {
            return Fail("TRANSFER_PARTIES_INVALID", problem);
        }
        if (tx.Kind == PropertyTransactionKind.Transfer && await LatePartyStartAsync(tx, cancellationToken) is { } late)
        {
            return Fail("TRANSFER_EFFECTIVE_DATE_CONFLICT", $"A current party started on {late:yyyy-MM-dd}; the transfer must take effect after that.");
        }
        tx.Status = WorkflowStatus.PendingReview;
        tx.SubmittedAt = clock.UtcNow;
        foreach (var td in tds.Where(t => t.Status == WorkflowStatus.Draft))
        {
            td.Status = WorkflowStatus.PendingReview;
        }
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(await MapAsync(id, cancellationToken));
    }

    public async Task<Result<PropertyTransactionDto>> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tx = await LoadAsync(id, cancellationToken);
        if (tx is null)
        {
            return NotFound();
        }
        if (tx.Status != WorkflowStatus.PendingReview)
        {
            return Fail("PROPERTY_TRANSACTION_NOT_PENDING_REVIEW", "Only a transaction pending review can be approved.");
        }

        var step = await approvals.SignNextStepAsync(ApprovalSubjectType.PropertyTransaction, tx.Id, tx.CreatedBy, clock.Today, null, cancellationToken);
        if (step.IsFailure)
        {
            return Fail(step.Code!, step.Message!);
        }
        if (!step.Value.ChainInForce && currentUser.AppUserId is not null && tx.CreatedBy == currentUser.AppUserId)
        {
            return Fail("CANNOT_APPROVE_OWN_PROPERTY_TRANSACTION", "The transaction's creator cannot also approve it (CLAUDE.md §46).");
        }
        if (step.Value.ChainInForce && !step.Value.Completed)
        {
            await db.SaveChangesAsync(cancellationToken); // the signed step
            return Result.Success(await MapAsync(id, cancellationToken));
        }

        var userId = currentUser.AppUserId;
        var now = clock.UtcNow;
        var label = tx.TransactionNumber ?? $"{tx.TypeCode} effective {tx.EffectiveDate:yyyy-MM-dd}";
        var ownsTransaction = db.Database.CurrentTransaction is null;
        var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            // 1. TDs the transaction cancels outright — first, so a new TD on the same RPU can become the approved one.
            var cancelIds = tx.TdCancellations.Select(c => c.TaxDeclarationId).ToList();
            foreach (var td in await db.TaxDeclarations.Where(x => cancelIds.Contains(x.Id)).ToListAsync(cancellationToken))
            {
                if (td.Status != WorkflowStatus.Approved)
                {
                    return await AbortAsync(transaction, "TRANSACTION_TD_NOT_CANCELLABLE",
                        $"TD {td.TaxDeclarationNumber} is no longer the current TD ({td.Status}); the transaction cannot cancel it.");
                }
                TaxDeclarationApproval.Cancel(td, userId, now, $"Cancelled by transaction {label}.");
            }
            await db.SaveChangesAsync(cancellationToken);

            // 2. The transaction's own TDs, which cancel the TDs they replace.
            var own = await db.TaxDeclarations.Where(x => x.PropertyTransactionId == tx.Id && x.Status == WorkflowStatus.PendingReview)
                .OrderBy(x => x.RevisionNumber).ToListAsync(cancellationToken);
            foreach (var td in own)
            {
                var check = await TaxDeclarationApproval.CheckAsync(db, td, faas.Value.RequireAssessmentOnTd, cancellationToken);
                if (check.IsFailure)
                {
                    return await AbortAsync(transaction, check.Code!, $"TD {td.TaxDeclarationNumber}: {check.Message}");
                }
                await TaxDeclarationApproval.ApplyAsync(db, td, check.Value, userId, now, cancellationToken);
            }

            // 3. Transfer: end the current owners (and any unknown-owner declaration); start the new parties.
            if (tx.Kind == PropertyTransactionKind.Transfer)
            {
                var ending = await EndingPartiesQuery(tx).ToListAsync(cancellationToken);
                if (ending.FirstOrDefault(x => x.StartDate >= tx.EffectiveDate) is { } late)
                {
                    return await AbortAsync(transaction, "TRANSFER_EFFECTIVE_DATE_CONFLICT",
                        $"A current party started on {late.StartDate:yyyy-MM-dd}; the transfer must take effect after that.");
                }
                foreach (var row in ending)
                {
                    row.IsCurrent = false;
                    row.EndDate = tx.EffectiveDate.AddDays(-1);
                    row.EndReason = $"Transferred by transaction {label}";
                    row.EndedByTransactionId = tx.Id;
                }
                db.PropertyTaxpayers.AddRange(tx.NewParties.Select(p => new PropertyTaxpayer
                {
                    PropertyId = tx.PropertyId, RpuId = tx.TransferRpuId, Role = p.Role, TaxpayerId = p.TaxpayerId, OwnershipTypeId = p.OwnershipTypeId,
                    OwnershipPercentage = p.OwnershipPercentage, StartDate = tx.EffectiveDate, IsCurrent = true,
                    StartedByTransactionId = tx.Id,
                }));
                await db.SaveChangesAsync(cancellationToken);
            }

            tx.Status = WorkflowStatus.Approved;
            tx.ApprovedBy = userId;
            tx.ApprovedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateException)
        {
            return Fail("PROPERTY_TRANSACTION_APPROVAL_CONFLICT",
                "Another change to these records happened at the same time. Nothing was changed; reload and try again.");
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
        return Result.Success(await MapAsync(id, cancellationToken));
    }

    public Task<Result<PropertyTransactionDto>> RejectAsync(Guid id, string reason, CancellationToken cancellationToken = default) =>
        CloseAsync(id, reason, WorkflowStatus.Rejected, [WorkflowStatus.PendingReview], cancellationToken);

    public Task<Result<PropertyTransactionDto>> WithdrawAsync(Guid id, string reason, CancellationToken cancellationToken = default) =>
        CloseAsync(id, reason, WorkflowStatus.Cancelled, [WorkflowStatus.Draft, WorkflowStatus.PendingReview], cancellationToken);

    /// <summary>Rejects or withdraws the transaction; its own TDs, which never took effect, are rejected with it.</summary>
    private async Task<Result<PropertyTransactionDto>> CloseAsync(Guid id, string reason, WorkflowStatus to, WorkflowStatus[] from, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
        {
            return Fail("VALIDATION_FAILED", "A reason is required (max 1000).");
        }
        var tx = await db.PropertyTransactions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (tx is null)
        {
            return NotFound();
        }
        if (!from.Contains(tx.Status))
        {
            return Fail("PROPERTY_TRANSACTION_INVALID_STATE", $"A {tx.Status} transaction cannot be {(to == WorkflowStatus.Rejected ? "rejected" : "withdrawn")}.");
        }
        tx.Status = to;
        tx.ClosedAt = clock.UtcNow;
        tx.ClosedBy = currentUser.AppUserId;
        tx.CloseReason = reason;
        foreach (var td in await db.TaxDeclarations.Where(x => x.PropertyTransactionId == id
                     && (x.Status == WorkflowStatus.Draft || x.Status == WorkflowStatus.PendingReview)).ToListAsync(ct))
        {
            td.Status = WorkflowStatus.Rejected;
            td.CancellationReason = $"Transaction {(to == WorkflowStatus.Rejected ? "rejected" : "withdrawn")}: {reason}";
        }
        currentUser.Reason = reason;
        await db.SaveChangesAsync(ct);
        return Result.Success(await MapAsync(id, ct));
    }

    /// <summary>
    /// A transfer must hand the property to someone: owners whose shares total
    /// exactly 100, or an unknown-owner declaration with no owners.
    /// DOMAIN VERIFICATION REQUIRED for partial transfers of co-owned property (not supported: the whole ownership passes).
    /// </summary>
    private static string? PartiesProblem(List<PropertyTransactionParty> parties)
    {
        var owners = parties.Where(p => p.Role == PropertyPartyRole.Owner).ToList();
        var unknown = parties.Count(p => p.Role == PropertyPartyRole.UnknownOwner);
        if (unknown > 1 || (unknown == 1 && owners.Count > 0))
        {
            return "Name either owners or a single unknown-owner declaration, not both.";
        }
        if (unknown == 0 && owners.Sum(o => o.OwnershipPercentage) != 100m)
        {
            return $"The new owners' shares total {owners.Sum(o => o.OwnershipPercentage)}%; they must total exactly 100%.";
        }
        return null;
    }

    private static async Task<Result<PropertyTransactionDto>> AbortAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction,
        string code, string message)
    {
        if (transaction is not null)
        {
            await transaction.RollbackAsync();
        }
        return Fail(code, message);
    }

    /// <summary>
    /// The parties a transfer ends: the current owners and any unknown-owner
    /// declaration of its scope — the whole property, or the one unit it
    /// targets. A unit that had no owners of its own ends nothing: it passes
    /// to its new owners while the land stays with the property's.
    /// </summary>
    private IQueryable<PropertyTaxpayer> EndingPartiesQuery(PropertyTransaction tx) =>
        db.PropertyTaxpayers.Where(x => x.PropertyId == tx.PropertyId && x.RpuId == tx.TransferRpuId && x.IsCurrent
            && (x.Role == PropertyPartyRole.Owner || x.Role == PropertyPartyRole.UnknownOwner));

    /// <summary>
    /// The start date of a party the transfer would end on or before it started (its end date is the day
    /// before the transfer takes effect), or null. Checked at submit and again at approval.
    /// </summary>
    private async Task<DateOnly?> LatePartyStartAsync(PropertyTransaction tx, CancellationToken ct) =>
        await EndingPartiesQuery(tx).Where(x => x.StartDate >= tx.EffectiveDate)
            .Select(x => (DateOnly?)x.StartDate).FirstOrDefaultAsync(ct);

    private Task<PropertyTransaction?> LoadAsync(Guid id, CancellationToken ct) =>
        db.PropertyTransactions.Include(x => x.Requirements).Include(x => x.NewParties).Include(x => x.TdCancellations)
            .Include(x => x.RelatedProperties).FirstOrDefaultAsync(x => x.Id == id, ct);

    private async Task<PropertyTransactionDto> MapAsync(Guid id, CancellationToken ct)
    {
        var tx = await db.PropertyTransactions.AsNoTracking()
            .Include(x => x.Property).Include(x => x.Requirements)
            .Include(x => x.NewParties).ThenInclude(p => p.Taxpayer)
            .Include(x => x.TdCancellations).ThenInclude(c => c.TaxDeclaration)
            .Include(x => x.RelatedProperties).ThenInclude(r => r.Property)
            .Include(x => x.TaxClearance)
            .SingleAsync(x => x.Id == id, ct);
        var issued = await db.TaxDeclarations.AsNoTracking().Where(x => x.PropertyTransactionId == id)
            .OrderBy(x => x.TaxDeclarationNumber)
            .Select(x => new TransactionTdDto(x.Id, x.TaxDeclarationNumber, x.PropertyId, x.Status)).ToListAsync(ct);

        return new PropertyTransactionDto(
            tx.Id, tx.TransactionNumber, tx.TransactionTypeId, tx.TypeCode, tx.TypeName, tx.Kind,
            tx.PropertyId, tx.Property!.PropertyIdentificationNumber, tx.EffectiveDate, tx.Description,
            tx.Status, tx.CreatedAt, tx.CreatedBy, tx.SubmittedAt, tx.ApprovedBy, tx.ApprovedAt, tx.ClosedAt, tx.CloseReason,
            tx.Requirements.OrderBy(r => r.Sequence).Select(r => new TransactionRequirementStatusDto(
                r.Id, r.Sequence, r.Code, r.Label, r.IsMandatory, r.LegalBasis, r.SatisfiedAt, r.SatisfiedBy, r.EvidenceReference, r.Note)).ToList(),
            tx.NewParties.Select(p => new TransactionPartyDto(p.Id, p.Role, p.TaxpayerId,
                p.Taxpayer is { } t
                    ? TaxpayerNameFormatter.Format(t.TaxpayerType, t.LastName, t.FirstName, t.MiddleName, t.Suffix, t.CorporateName)
                    : PropertyParties.UnknownOwnerName,
                p.OwnershipTypeId, p.OwnershipPercentage)).ToList(),
            issued,
            tx.TdCancellations.Select(c => new TransactionTdDto(c.TaxDeclarationId, c.TaxDeclaration!.TaxDeclarationNumber,
                c.TaxDeclaration.PropertyId, c.TaxDeclaration.Status)).ToList(),
            tx.RelatedProperties.Select(r => new TransactionPropertyDto(r.PropertyId, r.Property!.PropertyIdentificationNumber, r.Role)).ToList(),
            tx.TransferRpuId,
            tx.TaxClearance is { } c ? new TransferTaxClearanceDto(c.CarNumber, c.CarDate, c.TransferorName, c.TransferorTin, c.TransfereeTin,
                c.CapitalGainsTax, c.CapitalGainsTaxReceipt, c.CapitalGainsTaxDate, c.DocumentaryStampTax, c.DocumentaryStampTaxReceipt,
                c.DocumentaryStampTaxDate, c.TransferTax, c.TransferTaxReceipt, c.TransferTaxDate, c.Remarks) : null);
    }

    private static Result<PropertyTransactionDto> NotFound() => Fail("PROPERTY_TRANSACTION_NOT_FOUND", "No property transaction was found with the given id.");

    private static Result<PropertyTransactionDto> Fail(string code, string message) => Result.Failure<PropertyTransactionDto>(code, message);

    private static TransactionTypeDto ToDto(TransactionType x) => new(
        x.Id, x.Code, x.Name, x.Kind, x.Rank, x.Description,
        x.Requirements.OrderBy(r => r.Sequence).Select(r => new TransactionRequirementDto(r.Sequence, r.Code, r.Label, r.IsMandatory, r.LegalBasis)).ToList(),
        x.LegalBasis, x.EffectiveDate, x.EndDate, x.Status, x.CreatedBy, x.CreatedAt, x.ApprovedBy, x.ApprovedAt, x.Remarks);
}
