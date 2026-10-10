using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Offices;
using Prime.Domain.Entities.Workflow;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Approvals;

/// <param name="SignerOffice">Whose staff signs the step (docs/analysis/province-wide-operation.md §3.4); Any by default.</param>
/// <param name="RequiredRole">A role code the signer must hold in their office, e.g. ASSESSOR.</param>
/// <param name="IsFinalApproval">The step that makes the record final; only the last step may be.</param>
/// <param name="RequiresLicensedSignatory">Warn when the signer has no valid REA licence (records-and-forms.md Q10).</param>
public sealed record ApprovalStepRequest(int Sequence, string StepCode, string Label, string? SignatoryPosition,
    ApprovalSigner SignerOffice = ApprovalSigner.Any, string? RequiredRole = null, bool IsFinalApproval = false,
    bool RequiresLicensedSignatory = false);

/// <param name="OfficeId">The municipal office the chain is for; null for the provincial default.</param>
public sealed record CreateApprovalChainRequest(
    string LegalBasis, DateOnly EffectiveDate, string? Remarks,
    ApprovalSubjectType SubjectType, string Name, IReadOnlyList<ApprovalStepRequest> Steps, Guid? OfficeId = null);

public sealed record ApprovalStepDto(int Sequence, string StepCode, string Label, string? SignatoryPosition,
    ApprovalSigner SignerOffice, string? RequiredRole, bool IsFinalApproval, bool RequiresLicensedSignatory = false);

public sealed record ApprovalChainDto(
    Guid Id, ApprovalSubjectType SubjectType, string Name, IReadOnlyList<ApprovalStepDto> Steps,
    string LegalBasis, DateOnly EffectiveDate, DateOnly? EndDate, WorkflowStatus Status,
    Guid? CreatedBy, DateTimeOffset CreatedAt, Guid? ApprovedBy, DateTimeOffset? ApprovedAt, string? Remarks,
    Guid? OfficeId = null, string? OfficeCode = null);

public sealed record ApprovalRecordDto(
    Guid Id, ApprovalSubjectType SubjectType, Guid SubjectId, Guid ApprovalChainId, int StepSequence, string StepCode,
    string Label, string? SignatoryPosition, Guid? UserId, string SignatoryName, DateTimeOffset SignedAt, string? Remarks,
    Guid? SignerOfficeId = null, Guid? DelegationId = null, string? UnderDelegation = null,
    string? SignatoryLicenceNumber = null, DateOnly? SignatoryLicenceValidUntil = null, bool SignedWithoutValidLicence = false);

public sealed class CreateApprovalChainRequestValidator : AbstractValidator<CreateApprovalChainRequest>
{
    public CreateApprovalChainRequestValidator()
    {
        RuleFor(x => x.LegalBasis).NotEmpty().MaximumLength(500);
        RuleFor(x => x.EffectiveDate).NotEqual(default(DateOnly)).WithMessage("effectiveDate is required.");
        RuleFor(x => x.Remarks).MaximumLength(1000);
        RuleFor(x => x.SubjectType).IsInEnum();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Steps).NotEmpty();
        RuleForEach(x => x.Steps).ChildRules(s =>
        {
            s.RuleFor(x => x.StepCode).NotEmpty().MaximumLength(50).Matches("^[A-Z][A-Z0-9_]*$")
                .WithMessage("stepCode must be UPPER_SNAKE_CASE.");
            s.RuleFor(x => x.Label).NotEmpty().MaximumLength(200);
            s.RuleFor(x => x.SignatoryPosition).MaximumLength(200);
            s.RuleFor(x => x.SignerOffice).IsInEnum();
            s.RuleFor(x => x.RequiredRole).MaximumLength(50);
        });
        RuleFor(x => x.Steps)
            .Must(list => list.Select(s => s.Sequence).OrderBy(s => s).SequenceEqual(Enumerable.Range(1, list.Count)))
            .When(x => x.Steps is { Count: > 0 })
            .WithMessage("Step sequences must be 1..n with no gaps or duplicates.");
        RuleFor(x => x.Steps)
            .Must(list => list.Select(s => s.StepCode).Distinct().Count() == list.Count)
            .When(x => x.Steps is { Count: > 0 })
            .WithMessage("Step codes must be unique within a chain.");
        RuleFor(x => x.Steps)
            .Must(list => list.Where(s => s.IsFinalApproval).All(s => s.Sequence == list.Max(t => t.Sequence)))
            .When(x => x.Steps is { Count: > 0 })
            .WithMessage("Only the last step may be the final approval.");
    }
}

/// <summary>A record waiting for an approval step the current user may sign now (§3.4).</summary>
/// <param name="Reference">The TD number, the assessment's year and RPU, or the transaction number.</param>
/// <param name="StepLabel">The step to sign; for a record without a chain, the two-person approval.</param>
/// <param name="UnderDelegation">The delegation the signature would be given under, if any.</param>
/// <param name="RowVersion">The record's row version, echoed in If-Match when signing (production-hardening.md §4.4).</param>
public sealed record ApprovalQueueItemDto(
    ApprovalSubjectType SubjectType, Guid SubjectId, Guid PropertyId, string Pin, string Reference, string StepLabel,
    string? UnderDelegation, DateTimeOffset CreatedAt, uint RowVersion = 0);

/// <summary>
/// What happened when a user signed the next step of a record's approval.
/// <see cref="ChainInForce"/> false means no chain is configured and the
/// caller applies its ordinary maker-checker.
/// </summary>
public sealed record ApprovalStepOutcome(bool ChainInForce, bool Completed, ApprovalRecord? Record);

public interface IApprovalChainService
{
    Task<Result<ApprovalChainDto>> CreateAsync(CreateApprovalChainRequest request, CancellationToken cancellationToken = default);
    Task<Result<ApprovalChainDto>> ApproveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<ApprovalChainDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ApprovalChainDto>>> ListAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ApprovalRecordDto>>> ListRecordsAsync(ApprovalSubjectType subjectType, Guid subjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs the next step of <paramref name="subjectId"/>'s chain as the
    /// current user, adding (not saving) an <see cref="ApprovalRecord"/>; the
    /// caller saves it with its own status change. Separation of duties: the
    /// record's creator and anyone who signed an earlier step cannot sign.
    /// The chain is the one of the office covering the record, else the
    /// provincial default; each step's signer office and role are enforced,
    /// and a final provincial step goes to the preparing office's Assessor
    /// while a delegation is in force on <paramref name="asOf"/> (§3.4).
    /// </summary>
    Task<Result<ApprovalStepOutcome>> SignNextStepAsync(ApprovalSubjectType subjectType, Guid subjectId, Guid? creatorId,
        DateOnly asOf, string? remarks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records pending review, in the user's jurisdiction, whose next step the current user may sign on
    /// <paramref name="asOf"/>: the same rules as signing. The <see cref="QueueLimit"/> oldest of each kind are checked.
    /// </summary>
    /// <param name="municipalityId">Only records of this municipality (the provincial consolidated view, §3.7).</param>
    Task<Result<IReadOnlyList<ApprovalQueueItemDto>>> ListAwaitingAsync(DateOnly asOf, CancellationToken cancellationToken = default, Guid? municipalityId = null);
}

/// <summary>docs/FORMS-REVISION-PLAN.md §4.5; offices and delegation: docs/analysis/province-wide-operation.md §3.4.</summary>
public sealed class ApprovalChainService(
    IApplicationDbContext db,
    IValidator<CreateApprovalChainRequest> validator,
    ICurrentUserService currentUser,
    IOfficeContext officeContext,
    IApprovalDelegationService delegations) : IApprovalChainService
{
    public async Task<Result<ApprovalChainDto>> CreateAsync(CreateApprovalChainRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<ApprovalChainDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }
        if (request.OfficeId is { } officeId
            && await db.Offices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == officeId, cancellationToken) is not { Kind: OfficeKind.Municipal })
        {
            return Result.Failure<ApprovalChainDto>("OFFICE_NOT_MUNICIPAL",
                "A chain belongs to a municipal office, or to none (the provincial default).");
        }
        var roles = request.Steps.Select(s => s.RequiredRole).OfType<string>().Where(r => r.Length > 0).Distinct().ToList();
        var known = await db.Roles.Where(r => roles.Contains(r.Code)).Select(r => r.Code).ToListAsync(cancellationToken);
        if (roles.Except(known).ToList() is { Count: > 0 } unknown)
        {
            return Result.Failure<ApprovalChainDto>("ROLE_NOT_FOUND", $"Unknown role(s): {string.Join(", ", unknown)}.");
        }
        var chain = new ApprovalChain
        {
            LegalBasis = request.LegalBasis, EffectiveDate = request.EffectiveDate, Remarks = request.Remarks,
            SubjectType = request.SubjectType, Name = request.Name, OfficeId = request.OfficeId,
            Steps = request.Steps.OrderBy(s => s.Sequence).Select(s => new ApprovalChainStep
            {
                Sequence = s.Sequence, StepCode = s.StepCode, Label = s.Label,
                SignatoryPosition = string.IsNullOrWhiteSpace(s.SignatoryPosition) ? null : s.SignatoryPosition,
                SignerOffice = s.SignerOffice, RequiredRole = string.IsNullOrWhiteSpace(s.RequiredRole) ? null : s.RequiredRole,
                IsFinalApproval = s.IsFinalApproval, RequiresLicensedSignatory = s.RequiresLicensedSignatory,
            }).ToList(),
        };
        db.ApprovalChains.Add(chain);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(chain.Id, cancellationToken);
    }

    public async Task<Result<ApprovalChainDto>> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var chain = await db.ApprovalChains.Include(x => x.Steps).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (chain is null)
        {
            return NotFound();
        }
        // Scope: the subject and the office (the provincial default is its own scope).
        if (await ConfigurationApproval.ApproveAsync(db, currentUser,
                db.ApprovalChains.Where(x => x.SubjectType == chain.SubjectType && x.OfficeId == chain.OfficeId),
                chain, "APPROVAL_CHAIN", cancellationToken) is { } failure)
        {
            return Result.Failure<ApprovalChainDto>(failure.Code!, failure.Message!);
        }
        return await GetAsync(id, cancellationToken);
    }

    public async Task<Result<ApprovalChainDto>> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.ApprovalChains.AsNoTracking().Include(x => x.Steps).Include(x => x.Office).FirstOrDefaultAsync(x => x.Id == id, cancellationToken) is { } chain
            ? Result.Success(ToDto(chain))
            : NotFound();

    public async Task<Result<IReadOnlyList<ApprovalChainDto>>> ListAsync(CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<ApprovalChainDto>>((await db.ApprovalChains.AsNoTracking().Include(x => x.Steps).Include(x => x.Office)
            .OrderBy(x => x.SubjectType).ThenBy(x => x.Office!.Code).ThenByDescending(x => x.EffectiveDate).ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken)).Select(ToDto).ToList());

    public async Task<Result<IReadOnlyList<ApprovalRecordDto>>> ListRecordsAsync(ApprovalSubjectType subjectType, Guid subjectId,
        CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<ApprovalRecordDto>>((await db.ApprovalRecords
            .Where(x => x.SubjectType == subjectType && x.SubjectId == subjectId).OrderBy(x => x.StepSequence)
            .ToListAsync(cancellationToken)).Select(ToDto).ToList());

    public async Task<Result<ApprovalStepOutcome>> SignNextStepAsync(ApprovalSubjectType subjectType, Guid subjectId, Guid? creatorId,
        DateOnly asOf, string? remarks, CancellationToken cancellationToken = default)
    {
        // Every signature, with or without a chain, names its signer (docs/analysis/workflow-security.md G7).
        if (currentUser.AppUserId is null)
        {
            return Result.Failure<ApprovalStepOutcome>(MakerChecker.UnknownUserCode, MakerChecker.UnknownUserMessage);
        }
        var plan = await PlanNextStepAsync(subjectType, subjectId, creatorId, asOf, cancellationToken);
        if (plan.IsFailure)
        {
            return Result.Failure<ApprovalStepOutcome>(plan.Code!, plan.Message!);
        }
        var p = plan.Value;
        if (p.Chain is null)
        {
            return Result.Success(new ApprovalStepOutcome(false, false, null));
        }

        var userId = currentUser.AppUserId;
        var signer = userId is null ? null : await db.AppUsers.Where(u => u.Id == userId)
            .Select(u => new { u.DisplayName, u.ReaLicenceNumber, u.ReaLicenceValidUntil }).FirstOrDefaultAsync(cancellationToken);
        var name = signer?.DisplayName;
        var licensed = signer?.ReaLicenceNumber is not null && signer.ReaLicenceValidUntil >= asOf;
        var record = new ApprovalRecord
        {
            SubjectType = subjectType, SubjectId = subjectId, ApprovalChainId = p.Chain.Id,
            StepSequence = p.Step!.Sequence, StepCode = p.Step.StepCode, Label = p.Step.Label,
            // Under a delegation the municipal Assessor signs in their own capacity.
            SignatoryPosition = p.Delegation is not null ? p.PreparingOfficeHeadPosition ?? p.Step.SignatoryPosition : p.Step.SignatoryPosition,
            UserId = userId, SignatoryName = string.IsNullOrWhiteSpace(name) ? "(unknown user)" : name,
            SignedAt = DateTimeOffset.UtcNow, Remarks = remarks,
            SignerOfficeId = p.SignerOfficeId,
            DelegationId = p.Delegation?.Id,
            UnderDelegation = p.Delegation is { } d
                ? $"{d.InstrumentReference} dated {d.InstrumentDate:yyyy-MM-dd} of {d.DelegatingOfficialName}, {d.DelegatingOfficialPosition}"
                : null,
            SignatoryLicenceNumber = licensed ? signer!.ReaLicenceNumber : null,
            SignatoryLicenceValidUntil = licensed ? signer!.ReaLicenceValidUntil : null,
            SignedWithoutValidLicence = p.Step.RequiresLicensedSignatory && !licensed,
        };
        db.ApprovalRecords.Add(record);
        return Result.Success(new ApprovalStepOutcome(true, p.IsLast, record));
    }

    /// <summary>How many pending records of each kind the queue checks (each is evaluated like a signature).</summary>
    public const int QueueLimit = 200;

    public async Task<Result<IReadOnlyList<ApprovalQueueItemDto>>> ListAwaitingAsync(DateOnly asOf, CancellationToken cancellationToken = default,
        Guid? municipalityId = null)
    {
        // Pending records in the jurisdiction (the query filters apply). TDs drafted under a transaction are approved with it.
        var candidates = new List<(ApprovalSubjectType Type, Guid Id, Guid? CreatedBy, Guid PropertyId, string Pin, string Reference, DateTimeOffset CreatedAt,
            uint RowVersion, Guid MunicipalityId, RpuType? Kind)>();
        candidates.AddRange((await db.TaxDeclarations.AsNoTracking()
                .Where(x => x.Status == WorkflowStatus.PendingReview && x.PropertyTransactionId == null)
                .Where(x => municipalityId == null || x.Property!.MunicipalityId == municipalityId)
                .OrderBy(x => x.CreatedAt).Take(QueueLimit)
                .Select(x => new { x.Id, x.CreatedBy, x.PropertyId, x.Property!.PropertyIdentificationNumber, x.TaxDeclarationNumber, x.CreatedAt, x.RowVersion,
                    x.Property.MunicipalityId, x.Rpu!.RpuType })
                .ToListAsync(cancellationToken))
            .Select(x => (ApprovalSubjectType.TaxDeclaration, x.Id, x.CreatedBy, x.PropertyId, x.PropertyIdentificationNumber, $"TD {x.TaxDeclarationNumber}",
                x.CreatedAt, x.RowVersion, x.MunicipalityId, (RpuType?)x.RpuType)));
        candidates.AddRange((await db.Assessments.AsNoTracking()
                .Where(x => x.Status == WorkflowStatus.PendingReview)
                .Where(x => municipalityId == null || x.Property!.MunicipalityId == municipalityId)
                .OrderBy(x => x.CreatedAt).Take(QueueLimit)
                .Select(x => new { x.Id, x.CreatedBy, x.PropertyId, x.Property!.PropertyIdentificationNumber, x.AssessmentYear, x.Rpu!.RpuNumber, x.CreatedAt,
                    x.RowVersion, x.Property.MunicipalityId, x.Rpu.RpuType })
                .ToListAsync(cancellationToken))
            .Select(x => (ApprovalSubjectType.Assessment, x.Id, x.CreatedBy, x.PropertyId, x.PropertyIdentificationNumber,
                $"Assessment {x.AssessmentYear}, RPU {x.RpuNumber}", x.CreatedAt, x.RowVersion, x.MunicipalityId, (RpuType?)x.RpuType)));
        candidates.AddRange((await db.PropertyTransactions.AsNoTracking()
                .Where(x => x.Status == WorkflowStatus.PendingReview)
                .Where(x => municipalityId == null || x.Property!.MunicipalityId == municipalityId)
                .OrderBy(x => x.CreatedAt).Take(QueueLimit)
                .Select(x => new { x.Id, x.CreatedBy, x.PropertyId, x.Property!.PropertyIdentificationNumber, x.TransactionNumber, x.CreatedAt, x.RowVersion,
                    x.Property.MunicipalityId })
                .ToListAsync(cancellationToken))
            // A transaction concerns the property as a whole: only delegations covering every kind apply.
            .Select(x => (ApprovalSubjectType.PropertyTransaction, x.Id, x.CreatedBy, x.PropertyId, x.PropertyIdentificationNumber,
                $"Transaction {x.TransactionNumber ?? "(unnumbered)"}", x.CreatedAt, x.RowVersion, x.MunicipalityId, (RpuType?)null)));

        // What each candidate's routing needs, loaded once for the whole queue rather than per record (production-hardening.md §9, H4).
        var ids = candidates.Select(c => c.Id).ToList();
        var signedBySubject = (await db.ApprovalRecords.AsNoTracking().Where(x => ids.Contains(x.SubjectId)).ToListAsync(cancellationToken))
            .ToLookup(x => (x.SubjectType, x.SubjectId));
        var municipalities = candidates.Select(c => c.MunicipalityId).Distinct().ToList();
        var preparingByMunicipality = (await db.OfficeJurisdictions.AsNoTracking().InForce(asOf).Where(j => municipalities.Contains(j.MunicipalityId))
                .Select(j => new { j.MunicipalityId, Preparing = new PreparingOffice(j.OfficeId, j.Office!.Code, j.Office.HeadPosition) })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.MunicipalityId).ToDictionary(g => g.Key, g => g.First().Preparing);
        var types = candidates.Select(c => c.Type).Distinct().ToList();
        var chainsInForce = await db.ApprovalChains.AsNoTracking().Include(x => x.Steps).InForce(asOf).Where(x => types.Contains(x.SubjectType))
            .ToListAsync(cancellationToken);
        var startedChainIds = signedBySubject.SelectMany(g => g).Select(r => r.ApprovalChainId).Distinct().ToList();
        var startedChains = await db.ApprovalChains.AsNoTracking().Include(x => x.Steps).Where(x => startedChainIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var delegationCache = new Dictionary<(Guid, ApprovalSubjectType, RpuType?), ApprovalDelegationDto?>();

        var userId = currentUser.AppUserId;
        var items = new List<ApprovalQueueItemDto>();
        foreach (var c in candidates)
        {
            var signed = signedBySubject[(c.Type, c.Id)].OrderBy(x => x.StepSequence).ToList();
            var preparing = preparingByMunicipality.GetValueOrDefault(c.MunicipalityId);
            var chain = signed.Count > 0
                ? startedChains[signed[0].ApprovalChainId]
                : (preparing is not null ? chainsInForce.FirstOrDefault(x => x.SubjectType == c.Type && x.OfficeId == preparing.OfficeId) : null)
                    ?? chainsInForce.FirstOrDefault(x => x.SubjectType == c.Type && x.OfficeId == null);
            var type = c.Type;
            var plan = await DecideNextStepAsync(new StepInputs(signed, chain, preparing, c.Kind), c.CreatedBy, async (officeId, kind) =>
            {
                if (!delegationCache.TryGetValue((officeId, type, kind), out var found))
                {
                    found = await delegations.FindInForceAsync(officeId, type, kind, asOf, cancellationToken);
                    delegationCache[(officeId, type, kind)] = found;
                }
                return found;
            }, cancellationToken);
            if (plan.IsFailure)
            {
                continue; // not this user's step
            }
            if (plan.Value.Chain is null)
            {
                // No chain in force: the two-person approval, by anyone but the creator.
                if (userId is null || userId != c.CreatedBy)
                {
                    items.Add(new ApprovalQueueItemDto(c.Type, c.Id, c.PropertyId, c.Pin, c.Reference, "Approve (two-person check)", null, c.CreatedAt, c.RowVersion));
                }
                continue;
            }
            items.Add(new ApprovalQueueItemDto(c.Type, c.Id, c.PropertyId, c.Pin, c.Reference, plan.Value.Step!.Label,
                plan.Value.Delegation?.InstrumentReference, c.CreatedAt, c.RowVersion));
        }
        return Result.Success<IReadOnlyList<ApprovalQueueItemDto>>(items.OrderBy(i => i.CreatedAt).ToList());
    }

    // --- Routing (§3.4) ---

    /// <summary>The chain and next step for a record, and whether the current user may sign it now.</summary>
    private sealed record StepPlan(ApprovalChain? Chain, ApprovalChainStep? Step, bool IsLast, ApprovalDelegationDto? Delegation,
        Guid? SignerOfficeId, string? PreparingOfficeHeadPosition);

    /// <summary>The office covering the record's municipality, which prepared it.</summary>
    private sealed record PreparingOffice(Guid OfficeId, string Code, string? HeadPosition);

    /// <summary>What deciding a record's next step needs: the steps signed so far, the chain, the preparing office and the property kind.</summary>
    private sealed record StepInputs(IReadOnlyList<ApprovalRecord> Signed, ApprovalChain? Chain, PreparingOffice? Preparing, RpuType? Kind);

    private async Task<Result<StepPlan>> PlanNextStepAsync(ApprovalSubjectType subjectType, Guid subjectId, Guid? creatorId, DateOnly asOf,
        CancellationToken ct)
    {
        var signed = await db.ApprovalRecords.Where(x => x.SubjectType == subjectType && x.SubjectId == subjectId)
            .OrderBy(x => x.StepSequence).ToListAsync(ct);
        var (municipalityId, kind) = await SubjectAsync(subjectType, subjectId, ct);
        var preparing = municipalityId is { } m
            ? await db.OfficeJurisdictions.AsNoTracking().InForce(asOf).Where(j => j.MunicipalityId == m)
                .Select(j => new PreparingOffice(j.OfficeId, j.Office!.Code, j.Office.HeadPosition)).FirstOrDefaultAsync(ct)
            : null;

        // A record already in a chain finishes under that chain, even if a newer one was approved since.
        // Otherwise: the preparing office's chain in force, else the provincial default.
        ApprovalChain? chain;
        if (signed.Count > 0)
        {
            chain = await db.ApprovalChains.Include(x => x.Steps).SingleAsync(x => x.Id == signed[0].ApprovalChainId, ct);
        }
        else
        {
            var inForce = db.ApprovalChains.Include(x => x.Steps).InForce(asOf).Where(x => x.SubjectType == subjectType);
            chain = (preparing is not null ? await inForce.FirstOrDefaultAsync(x => x.OfficeId == preparing.OfficeId, ct) : null)
                ?? await inForce.FirstOrDefaultAsync(x => x.OfficeId == null, ct);
        }
        return await DecideNextStepAsync(new StepInputs(signed, chain, preparing, kind), creatorId,
            (officeId, k) => delegations.FindInForceAsync(officeId, subjectType, k, asOf, ct), ct);
    }

    /// <summary>
    /// The next step of a record and whether the current user may sign it now, from its loaded inputs. Signing loads them for
    /// one record (<see cref="PlanNextStepAsync"/>); the approval queue loads them for all its candidates at once.
    /// </summary>
    private async Task<Result<StepPlan>> DecideNextStepAsync(StepInputs inputs, Guid? creatorId,
        Func<Guid, RpuType?, Task<ApprovalDelegationDto?>> findDelegation, CancellationToken ct)
    {
        var (signed, chain, preparing, kind) = inputs;
        if (chain is null)
        {
            return Result.Success(new StepPlan(null, null, false, null, null, null));
        }

        var steps = chain.Steps.OrderBy(s => s.Sequence).ToList();
        if (signed.Count >= steps.Count)
        {
            return Result.Failure<StepPlan>("APPROVAL_ALREADY_COMPLETE", "Every step of this approval has already been signed.");
        }
        var userId = currentUser.AppUserId;
        if (userId is not null && userId == creatorId)
        {
            return Result.Failure<StepPlan>("CANNOT_SIGN_OWN_RECORD", "The record's creator cannot sign its approval (CLAUDE.md §46).");
        }
        if (userId is not null && signed.Any(r => r.UserId == userId))
        {
            return Result.Failure<StepPlan>("APPROVAL_STEP_SAME_SIGNER",
                "You already signed an earlier step of this approval; each step needs a different person.");
        }

        var step = steps[signed.Count];
        ApprovalDelegationDto? delegation = null;
        if (step.IsFinalApproval && step.SignerOffice == ApprovalSigner.ProvincialOffice && preparing is not null)
        {
            delegation = await findDelegation(preparing.OfficeId, kind);
        }

        Guid? signerOfficeId = null;
        if (userId is not null)
        {
            var scope = await officeContext.GetAsync(ct);
            signerOfficeId = scope.OfficeId;
            var refusal = delegation is not null
                ? scope.OfficeId == preparing!.OfficeId && scope.HasRole(RoleCodes.Assessor)
                    ? null
                    : $"This final approval is delegated to {preparing.Code} ({delegation.InstrumentReference}); only that office's Assessor signs it. A delegation is not passed on."
                : step.SignerOffice switch
                {
                    ApprovalSigner.PreparingOffice when preparing is null =>
                        "No office covers this record's municipality, so its preparing office cannot sign. Assign the municipality to an office.",
                    ApprovalSigner.PreparingOffice when scope.OfficeId != preparing!.OfficeId =>
                        $"The step \"{step.Label}\" is signed by the preparing office ({preparing.Code}).",
                    ApprovalSigner.ProvincialOffice when scope.OfficeKind != OfficeKind.Provincial =>
                        $"The step \"{step.Label}\" is signed by the Provincial Assessor's Office.",
                    _ => null,
                };
            if (refusal is null && step.RequiredRole is { } role && delegation is null && !scope.HasRole(role))
            {
                refusal = $"The step \"{step.Label}\" needs the {role} role in the signing office.";
            }
            if (refusal is not null)
            {
                return Result.Failure<StepPlan>("APPROVAL_STEP_FORBIDDEN", refusal);
            }
        }
        return Result.Success(new StepPlan(chain, step, signed.Count + 1 == steps.Count, delegation, signerOfficeId, preparing?.HeadPosition));
    }

    /// <summary>The record's municipality and, for FAAS/TD records, its property kind (delegations may be limited by kind).</summary>
    private async Task<(Guid? MunicipalityId, RpuType? Kind)> SubjectAsync(ApprovalSubjectType type, Guid id, CancellationToken ct)
    {
        switch (type)
        {
            case ApprovalSubjectType.Assessment:
                var a = await db.Assessments.Where(x => x.Id == id)
                    .Select(x => new { x.Property!.MunicipalityId, x.Rpu!.RpuType }).FirstOrDefaultAsync(ct);
                return (a?.MunicipalityId, a?.RpuType);
            case ApprovalSubjectType.TaxDeclaration:
                var t = await db.TaxDeclarations.Where(x => x.Id == id)
                    .Select(x => new { x.Property!.MunicipalityId, x.Rpu!.RpuType }).FirstOrDefaultAsync(ct);
                return (t?.MunicipalityId, t?.RpuType);
            default:
                // A transaction concerns the property as a whole: only delegations covering every kind apply.
                var p = await db.PropertyTransactions.Where(x => x.Id == id).Select(x => (Guid?)x.Property!.MunicipalityId).FirstOrDefaultAsync(ct);
                return (p, null);
        }
    }

    private static Result<ApprovalChainDto> NotFound() =>
        Result.Failure<ApprovalChainDto>("APPROVAL_CHAIN_NOT_FOUND", "No approval chain was found with the given id.");

    private static ApprovalChainDto ToDto(ApprovalChain x) => new(
        x.Id, x.SubjectType, x.Name,
        x.Steps.OrderBy(s => s.Sequence).Select(s => new ApprovalStepDto(s.Sequence, s.StepCode, s.Label, s.SignatoryPosition,
            s.SignerOffice, s.RequiredRole, s.IsFinalApproval, s.RequiresLicensedSignatory)).ToList(),
        x.LegalBasis, x.EffectiveDate, x.EndDate, x.Status, x.CreatedBy, x.CreatedAt, x.ApprovedBy, x.ApprovedAt, x.Remarks,
        x.OfficeId, x.Office?.Code);

    internal static ApprovalRecordDto ToDto(ApprovalRecord x) => new(
        x.Id, x.SubjectType, x.SubjectId, x.ApprovalChainId, x.StepSequence, x.StepCode, x.Label, x.SignatoryPosition,
        x.UserId, x.SignatoryName, x.SignedAt, x.Remarks, x.SignerOfficeId, x.DelegationId, x.UnderDelegation,
        x.SignatoryLicenceNumber, x.SignatoryLicenceValidUntil, x.SignedWithoutValidLicence);
}
