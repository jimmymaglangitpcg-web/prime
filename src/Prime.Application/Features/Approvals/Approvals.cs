using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities.Workflow;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Approvals;

public sealed record ApprovalStepRequest(int Sequence, string StepCode, string Label, string? SignatoryPosition);

public sealed record CreateApprovalChainRequest(
    string LegalBasis, DateOnly EffectiveDate, string? Remarks,
    ApprovalSubjectType SubjectType, string Name, IReadOnlyList<ApprovalStepRequest> Steps);

public sealed record ApprovalStepDto(int Sequence, string StepCode, string Label, string? SignatoryPosition);

public sealed record ApprovalChainDto(
    Guid Id, ApprovalSubjectType SubjectType, string Name, IReadOnlyList<ApprovalStepDto> Steps,
    string LegalBasis, DateOnly EffectiveDate, DateOnly? EndDate, WorkflowStatus Status,
    Guid? CreatedBy, DateTimeOffset CreatedAt, Guid? ApprovedBy, DateTimeOffset? ApprovedAt, string? Remarks);

public sealed record ApprovalRecordDto(
    Guid Id, ApprovalSubjectType SubjectType, Guid SubjectId, Guid ApprovalChainId, int StepSequence, string StepCode,
    string Label, string? SignatoryPosition, Guid? UserId, string SignatoryName, DateTimeOffset SignedAt, string? Remarks);

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
        });
        RuleFor(x => x.Steps)
            .Must(list => list.Select(s => s.Sequence).OrderBy(s => s).SequenceEqual(Enumerable.Range(1, list.Count)))
            .When(x => x.Steps is { Count: > 0 })
            .WithMessage("Step sequences must be 1..n with no gaps or duplicates.");
        RuleFor(x => x.Steps)
            .Must(list => list.Select(s => s.StepCode).Distinct().Count() == list.Count)
            .When(x => x.Steps is { Count: > 0 })
            .WithMessage("Step codes must be unique within a chain.");
    }
}

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
    /// </summary>
    Task<Result<ApprovalStepOutcome>> SignNextStepAsync(ApprovalSubjectType subjectType, Guid subjectId, Guid? creatorId,
        DateOnly asOf, string? remarks, CancellationToken cancellationToken = default);
}

/// <summary>docs/FORMS-REVISION-PLAN.md §4.5.</summary>
public sealed class ApprovalChainService(
    IApplicationDbContext db,
    IValidator<CreateApprovalChainRequest> validator,
    ICurrentUserService currentUser) : IApprovalChainService
{
    public async Task<Result<ApprovalChainDto>> CreateAsync(CreateApprovalChainRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<ApprovalChainDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }
        var chain = new ApprovalChain
        {
            LegalBasis = request.LegalBasis, EffectiveDate = request.EffectiveDate, Remarks = request.Remarks,
            SubjectType = request.SubjectType, Name = request.Name,
            Steps = request.Steps.OrderBy(s => s.Sequence).Select(s => new ApprovalChainStep
            {
                Sequence = s.Sequence, StepCode = s.StepCode, Label = s.Label,
                SignatoryPosition = string.IsNullOrWhiteSpace(s.SignatoryPosition) ? null : s.SignatoryPosition,
            }).ToList(),
        };
        db.ApprovalChains.Add(chain);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(chain));
    }

    public async Task<Result<ApprovalChainDto>> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var chain = await db.ApprovalChains.Include(x => x.Steps).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (chain is null)
        {
            return NotFound();
        }
        if (await ConfigurationApproval.ApproveAsync(db, currentUser, db.ApprovalChains.Where(x => x.SubjectType == chain.SubjectType),
                chain, "APPROVAL_CHAIN", cancellationToken) is { } failure)
        {
            return Result.Failure<ApprovalChainDto>(failure.Code!, failure.Message!);
        }
        return Result.Success(ToDto(chain));
    }

    public async Task<Result<ApprovalChainDto>> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.ApprovalChains.Include(x => x.Steps).FirstOrDefaultAsync(x => x.Id == id, cancellationToken) is { } chain
            ? Result.Success(ToDto(chain))
            : NotFound();

    public async Task<Result<IReadOnlyList<ApprovalChainDto>>> ListAsync(CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<ApprovalChainDto>>((await db.ApprovalChains.Include(x => x.Steps)
            .OrderBy(x => x.SubjectType).ThenByDescending(x => x.EffectiveDate).ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken)).Select(ToDto).ToList());

    public async Task<Result<IReadOnlyList<ApprovalRecordDto>>> ListRecordsAsync(ApprovalSubjectType subjectType, Guid subjectId,
        CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<ApprovalRecordDto>>((await db.ApprovalRecords
            .Where(x => x.SubjectType == subjectType && x.SubjectId == subjectId).OrderBy(x => x.StepSequence)
            .ToListAsync(cancellationToken)).Select(ToDto).ToList());

    public async Task<Result<ApprovalStepOutcome>> SignNextStepAsync(ApprovalSubjectType subjectType, Guid subjectId, Guid? creatorId,
        DateOnly asOf, string? remarks, CancellationToken cancellationToken = default)
    {
        var signed = await db.ApprovalRecords.Where(x => x.SubjectType == subjectType && x.SubjectId == subjectId)
            .OrderBy(x => x.StepSequence).ToListAsync(cancellationToken);

        // A record already in a chain finishes under that chain, even if a newer one was approved since.
        var chain = signed.Count > 0
            ? await db.ApprovalChains.Include(x => x.Steps).SingleAsync(x => x.Id == signed[0].ApprovalChainId, cancellationToken)
            : await db.ApprovalChains.Include(x => x.Steps).InForce(asOf).FirstOrDefaultAsync(x => x.SubjectType == subjectType, cancellationToken);
        if (chain is null)
        {
            return Result.Success(new ApprovalStepOutcome(false, false, null));
        }

        var steps = chain.Steps.OrderBy(s => s.Sequence).ToList();
        if (signed.Count >= steps.Count)
        {
            return Result.Failure<ApprovalStepOutcome>("APPROVAL_ALREADY_COMPLETE", "Every step of this approval has already been signed.");
        }
        var userId = currentUser.AppUserId;
        if (userId is not null && userId == creatorId)
        {
            return Result.Failure<ApprovalStepOutcome>("CANNOT_SIGN_OWN_RECORD", "The record's creator cannot sign its approval (CLAUDE.md §46).");
        }
        if (userId is not null && signed.Any(r => r.UserId == userId))
        {
            return Result.Failure<ApprovalStepOutcome>("APPROVAL_STEP_SAME_SIGNER",
                "You already signed an earlier step of this approval; each step needs a different person.");
        }

        var step = steps[signed.Count];
        var name = userId is null ? null : await db.AppUsers.Where(u => u.Id == userId).Select(u => u.DisplayName).FirstOrDefaultAsync(cancellationToken);
        var record = new ApprovalRecord
        {
            SubjectType = subjectType, SubjectId = subjectId, ApprovalChainId = chain.Id,
            StepSequence = step.Sequence, StepCode = step.StepCode, Label = step.Label, SignatoryPosition = step.SignatoryPosition,
            UserId = userId, SignatoryName = string.IsNullOrWhiteSpace(name) ? "(unknown user)" : name,
            SignedAt = DateTimeOffset.UtcNow, Remarks = remarks,
        };
        db.ApprovalRecords.Add(record);
        return Result.Success(new ApprovalStepOutcome(true, signed.Count + 1 == steps.Count, record));
    }

    private static Result<ApprovalChainDto> NotFound() =>
        Result.Failure<ApprovalChainDto>("APPROVAL_CHAIN_NOT_FOUND", "No approval chain was found with the given id.");

    private static ApprovalChainDto ToDto(ApprovalChain x) => new(
        x.Id, x.SubjectType, x.Name,
        x.Steps.OrderBy(s => s.Sequence).Select(s => new ApprovalStepDto(s.Sequence, s.StepCode, s.Label, s.SignatoryPosition)).ToList(),
        x.LegalBasis, x.EffectiveDate, x.EndDate, x.Status, x.CreatedBy, x.CreatedAt, x.ApprovedBy, x.ApprovedAt, x.Remarks);

    internal static ApprovalRecordDto ToDto(ApprovalRecord x) => new(
        x.Id, x.SubjectType, x.SubjectId, x.ApprovalChainId, x.StepSequence, x.StepCode, x.Label, x.SignatoryPosition,
        x.UserId, x.SignatoryName, x.SignedAt, x.Remarks);
}
