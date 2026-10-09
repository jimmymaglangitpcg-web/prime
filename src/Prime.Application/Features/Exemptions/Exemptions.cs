using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Assessments;
using Prime.Application.Features.Properties;
using Prime.Domain.Entities.Exemptions;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Exemptions;

/// <summary>"Exemptions" configuration section.</summary>
public sealed class ExemptionsOptions
{
    public const string SectionName = "Exemptions";

    /// <summary>Days from the declaration to file proof of exemption; LGC §206 (LAM Book III p.100) sets 30.</summary>
    public int ProofPeriodDays { get; set; } = 30;

    /// <summary>
    /// The transaction code of the reassessment an exemption decided after the assessment opens (Q3); its type's
    /// effectivity rule dates it. Empty: the reassessment takes effect from the exemption's own date.
    /// DOMAIN VERIFICATION REQUIRED: which LAM transaction code applies (Annex I-D).
    /// </summary>
    public string? ReassessmentTransactionCode { get; set; }
}

public sealed record CreateExemptionTypeRequest(
    string LegalBasis, DateOnly EffectiveDate, string? Remarks, string Code, string Name, string? Description,
    ExemptionAppliesTo AppliesTo, bool RequiresProof, decimal? AssessedValueCeiling);

public sealed record ExemptionTypeDto(
    Guid Id, string Code, string Name, string? Description, ExemptionAppliesTo AppliesTo, bool RequiresProof, decimal? AssessedValueCeiling,
    string LegalBasis, DateOnly EffectiveDate, DateOnly? EndDate, WorkflowStatus Status, Guid? CreatedBy, Guid? ApprovedBy, DateTimeOffset? ApprovedAt,
    string? Remarks);

/// <param name="ClaimedOn">The declaration date the proof period runs from; today when omitted.</param>
public sealed record ClaimExemptionRequest(
    Guid RpuId, Guid ExemptionTypeId, Guid? ActualUseId, string? PortionDescription, Guid? ClaimantTaxpayerId, DateOnly? ClaimedOn,
    string? Reference, string? Remarks);

/// <param name="ReceivedOn">The day the office received it; today when omitted.</param>
public sealed record AddExemptionEvidenceRequest(string Description, string? ReferenceNumber, DateOnly? DocumentDate, DateOnly? ReceivedOn);

public sealed record ApproveExemptionRequest(DateOnly EffectiveDate, DateOnly? ExpiryDate, string? Remarks);

public sealed record ExemptionReasonRequest(string Reason);

public sealed record EndExemptionRequest(DateOnly EndedOn, string Reason);

public sealed record ExemptionEvidenceDto(int Sequence, string Description, string? ReferenceNumber, DateOnly? DocumentDate, DateOnly ReceivedOn, string? ReceivedBy);

/// <param name="ProofOverdue">Still without proof after the due date: the unit stays listed as taxable (LGC §206).</param>
/// <param name="ProofLate">Proof filed after the due date (accepted; the unit is dropped from the taxable roll once approved).</param>
public sealed record PropertyExemptionDto(
    Guid Id, Guid PropertyId, string Pin, Guid RpuId, string RpuNumber, RpuType RpuType,
    Guid ExemptionTypeId, string TypeCode, string TypeName, string LegalBasis,
    Guid? ActualUseId, string? ActualUseName, string? PortionDescription, Guid? ClaimantTaxpayerId, string? ClaimantName,
    DateOnly ClaimedOn, DateOnly ProofDueDate, bool ProofOverdue, bool ProofLate, string? Reference, string? Remarks,
    ExemptionStatus Status, DateOnly? ProofFiledOn, DateOnly? EffectiveDate, DateOnly? ExpiryDate,
    Guid? CreatedBy, string? CreatedByName, DateTimeOffset CreatedAt, Guid? DecidedBy, string? DecidedByName, DateTimeOffset? DecidedAt, string? DecisionRemarks,
    DateOnly? EndedOn, string? EndReason, IReadOnlyList<ExemptionEvidenceDto> Evidence,
    Guid? ReassessmentId = null,
    /// <summary>Set by approve and end only: whether a reassessment was opened, and if not, why.</summary>
    string? ReassessmentNote = null,
    uint RowVersion = 0);

public sealed class CreateExemptionTypeRequestValidator : AbstractValidator<CreateExemptionTypeRequest>
{
    public CreateExemptionTypeRequestValidator()
    {
        RuleFor(x => x.LegalBasis).NotEmpty().MaximumLength(500);
        RuleFor(x => x.EffectiveDate).NotEqual(default(DateOnly)).WithMessage("effectiveDate is required.");
        RuleFor(x => x.Remarks).MaximumLength(1000);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.AppliesTo).Must(a => a > 0 && (a & ~ExemptionAppliesTo.All) == 0).WithMessage("appliesTo names at least one kind of unit.");
        RuleFor(x => x.AssessedValueCeiling).GreaterThan(0m).When(x => x.AssessedValueCeiling is not null);
    }
}

public interface IExemptionService
{
    Task<Result<ExemptionTypeDto>> CreateTypeAsync(CreateExemptionTypeRequest request, CancellationToken cancellationToken = default);
    Task<Result<ExemptionTypeDto>> ApproveTypeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ExemptionTypeDto>>> ListTypesAsync(bool inForceOnly, CancellationToken cancellationToken = default);

    Task<Result<PropertyExemptionDto>> ClaimAsync(ClaimExemptionRequest request, CancellationToken cancellationToken = default);
    Task<Result<PropertyExemptionDto>> AddEvidenceAsync(Guid id, AddExemptionEvidenceRequest request, CancellationToken cancellationToken = default);
    Task<Result<PropertyExemptionDto>> ApproveAsync(Guid id, ApproveExemptionRequest request, CancellationToken cancellationToken = default);
    Task<Result<PropertyExemptionDto>> RejectAsync(Guid id, string reason, CancellationToken cancellationToken = default);
    Task<Result<PropertyExemptionDto>> EndAsync(Guid id, EndExemptionRequest request, CancellationToken cancellationToken = default);
    Task<Result<PropertyExemptionDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<PropertyExemptionDto>>> ListByPropertyAsync(Guid propertyId, CancellationToken cancellationToken = default);

    /// <summary>The worklist: open claims (claimed or awaiting a decision), overdue proof first (Q5).</summary>
    Task<Result<IReadOnlyList<PropertyExemptionDto>>> ListOpenAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Exemption types and claims (docs/analysis/assessment-listing-exemptions.md §4.1, step L3-1a). A claim stays without
/// effect on the assessment until it is approved by someone other than whoever recorded it (CLAUDE.md §46). An
/// approved exemption marks the lines of assessments made while it is in force (<see cref="ExemptionTaxability"/>);
/// approving or ending one after the unit was assessed opens a reassessment (L3-1b, Q3).
/// </summary>
public sealed class ExemptionService(
    IApplicationDbContext db,
    IValidator<CreateExemptionTypeRequest> typeValidator,
    ICurrentUserService currentUser,
    IClock clock,
    IAssessmentService assessments,
    IOptions<ExemptionsOptions> options) : IExemptionService
{
    // --- Types (configuration) ---

    public async Task<Result<ExemptionTypeDto>> CreateTypeAsync(CreateExemptionTypeRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await typeValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<ExemptionTypeDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }
        var type = new ExemptionType
        {
            LegalBasis = request.LegalBasis.Trim(), EffectiveDate = request.EffectiveDate, Remarks = Clean(request.Remarks),
            Code = request.Code.Trim(), Name = request.Name.Trim(), Description = Clean(request.Description), AppliesTo = request.AppliesTo,
            RequiresProof = request.RequiresProof, AssessedValueCeiling = request.AssessedValueCeiling,
        };
        db.ExemptionTypes.Add(type);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(type));
    }

    public async Task<Result<ExemptionTypeDto>> ApproveTypeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var type = await db.ExemptionTypes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (type is null)
        {
            return Result.Failure<ExemptionTypeDto>("EXEMPTION_TYPE_NOT_FOUND", "No exemption type was found with the given id.");
        }
        if (await ConfigurationApproval.ApproveAsync(db, currentUser, db.ExemptionTypes.Where(x => x.Code == type.Code),
                type, "EXEMPTION_TYPE", cancellationToken) is { } failure)
        {
            return Result.Failure<ExemptionTypeDto>(failure.Code!, failure.Message!);
        }
        return Result.Success(ToDto(type));
    }

    public async Task<Result<IReadOnlyList<ExemptionTypeDto>>> ListTypesAsync(bool inForceOnly, CancellationToken cancellationToken = default)
    {
        var query = db.ExemptionTypes.AsQueryable();
        if (inForceOnly)
        {
            query = query.InForce(clock.Today);
        }
        var types = await query.OrderBy(x => x.Code).ThenByDescending(x => x.EffectiveDate).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<ExemptionTypeDto>>(types.Select(ToDto).ToList());
    }

    // --- Claims ---

    public async Task<Result<PropertyExemptionDto>> ClaimAsync(ClaimExemptionRequest r, CancellationToken cancellationToken = default)
    {
        if (r.PortionDescription?.Length > 500 || r.Reference?.Length > 500 || r.Remarks?.Length > 1000)
        {
            return Fail("VALIDATION_FAILED", "portionDescription and reference max 500, remarks max 1000.");
        }
        var ct = cancellationToken;
        var rpu = await db.RealPropertyUnits.AsNoTracking().FirstOrDefaultAsync(x => x.Id == r.RpuId, ct);
        if (rpu is null)
        {
            return Fail("RPU_NOT_FOUND", "The specified unit does not exist.");
        }
        var claimedOn = r.ClaimedOn ?? clock.Today;
        var type = await db.ExemptionTypes.InForce(claimedOn).FirstOrDefaultAsync(x => x.Id == r.ExemptionTypeId, ct);
        if (type is null)
        {
            return Fail("EXEMPTION_TYPE_NOT_IN_FORCE", $"The exemption type is not approved and in force on {claimedOn:yyyy-MM-dd}.");
        }
        if ((type.AppliesTo & Covers(rpu.RpuType)) == 0)
        {
            return Fail("EXEMPTION_TYPE_NOT_APPLICABLE", $"Exemption {type.Code} does not apply to a {rpu.RpuType} unit.");
        }
        if (r.ActualUseId is { } useId && !await db.ActualUses.AnyAsync(x => x.Id == useId, ct))
        {
            return Fail("ACTUAL_USE_NOT_FOUND", "The specified actual use does not exist.");
        }
        if (r.ClaimantTaxpayerId is { } claimant && !await db.Taxpayers.AnyAsync(x => x.Id == claimant, ct))
        {
            return Fail("TAXPAYER_NOT_FOUND", "The specified claimant does not exist.");
        }
        if (await db.PropertyExemptions.AnyAsync(x => x.RpuId == r.RpuId && x.ExemptionTypeId == r.ExemptionTypeId && x.ActualUseId == r.ActualUseId
                && (x.Status == ExemptionStatus.Claimed || x.Status == ExemptionStatus.ProofFiled || x.Status == ExemptionStatus.Approved), ct))
        {
            return Fail("EXEMPTION_ALREADY_CLAIMED", "This unit (or part) already has an open or approved claim of this exemption; end or decide it first.");
        }
        var claim = new PropertyExemption
        {
            PropertyId = rpu.PropertyId, RpuId = rpu.Id, ExemptionTypeId = type.Id, ActualUseId = r.ActualUseId, PortionDescription = Clean(r.PortionDescription),
            ClaimantTaxpayerId = r.ClaimantTaxpayerId, ClaimedOn = claimedOn, ProofDueDate = claimedOn.AddDays(options.Value.ProofPeriodDays),
            Reference = Clean(r.Reference), Remarks = Clean(r.Remarks),
            // A type that needs no documentary proof goes straight to the decision.
            Status = type.RequiresProof ? ExemptionStatus.Claimed : ExemptionStatus.ProofFiled,
            ProofFiledOn = type.RequiresProof ? null : claimedOn,
        };
        db.PropertyExemptions.Add(claim);
        await db.SaveChangesAsync(ct);
        return await GetAsync(claim.Id, ct);
    }

    public async Task<Result<PropertyExemptionDto>> AddEvidenceAsync(Guid id, AddExemptionEvidenceRequest r, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(r.Description) || r.Description.Length > 500 || r.ReferenceNumber?.Length > 100)
        {
            return Fail("VALIDATION_FAILED", "A description is required (max 500); referenceNumber max 100.");
        }
        var claim = await db.PropertyExemptions.Include(x => x.Evidence).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (claim is null)
        {
            return NotFound();
        }
        if (claim.Status is not (ExemptionStatus.Claimed or ExemptionStatus.ProofFiled))
        {
            return Fail("EXEMPTION_NOT_OPEN", $"Evidence is filed before the decision; this claim is {claim.Status}.");
        }
        var receivedOn = r.ReceivedOn ?? clock.Today;
        if (receivedOn < claim.ClaimedOn || receivedOn > clock.Today)
        {
            return Fail("VALIDATION_FAILED", "The date received is between the claim and today.");
        }
        // Through the set: a child added to the loaded collection would be taken as existing.
        db.ExemptionEvidence.Add(new ExemptionEvidence
        {
            PropertyExemptionId = claim.Id, Sequence = claim.Evidence.Count + 1, Description = r.Description.Trim(),
            ReferenceNumber = Clean(r.ReferenceNumber), DocumentDate = r.DocumentDate, ReceivedOn = receivedOn, ReceivedBy = currentUser.AppUserId,
        });
        if (claim.Status == ExemptionStatus.Claimed)
        {
            claim.Status = ExemptionStatus.ProofFiled;
            claim.ProofFiledOn = receivedOn;
        }
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(claim.Id, cancellationToken);
    }

    public async Task<Result<PropertyExemptionDto>> ApproveAsync(Guid id, ApproveExemptionRequest r, CancellationToken cancellationToken = default)
    {
        if (r.EffectiveDate == default || r.ExpiryDate < r.EffectiveDate || r.Remarks?.Length > 1000)
        {
            return Fail("VALIDATION_FAILED", "effectiveDate is required; the expiry cannot come before it; remarks max 1000.");
        }
        var claim = await db.PropertyExemptions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (claim is null)
        {
            return NotFound();
        }
        if (claim.Status != ExemptionStatus.ProofFiled)
        {
            return Fail("EXEMPTION_NOT_READY", claim.Status == ExemptionStatus.Claimed
                ? "No proof has been filed: the unit stays listed as taxable until it is (LGC §206)."
                : $"Only a claim with its proof filed can be approved; this one is {claim.Status}.");
        }
        if (DecisionRefusal(claim) is { } refusal)
        {
            return Fail(refusal.Code, refusal.Message);
        }
        claim.Status = ExemptionStatus.Approved;
        claim.EffectiveDate = r.EffectiveDate;
        claim.ExpiryDate = r.ExpiryDate;
        Decide(claim, r.Remarks);
        return await SaveWithReassessmentAsync(claim, r.EffectiveDate, "approved", cancellationToken);
    }

    public async Task<Result<PropertyExemptionDto>> RejectAsync(Guid id, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
        {
            return Fail("VALIDATION_FAILED", "A reason is required (max 1000).");
        }
        var claim = await db.PropertyExemptions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (claim is null)
        {
            return NotFound();
        }
        if (claim.Status is not (ExemptionStatus.Claimed or ExemptionStatus.ProofFiled))
        {
            return Fail("EXEMPTION_NOT_OPEN", $"Only an open claim can be rejected; this one is {claim.Status}.");
        }
        if (DecisionRefusal(claim) is { } refusal)
        {
            return Fail(refusal.Code, refusal.Message);
        }
        claim.Status = ExemptionStatus.Rejected;
        Decide(claim, reason.Trim());
        currentUser.Reason = reason;
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(claim.Id, cancellationToken);
    }

    public async Task<Result<PropertyExemptionDto>> EndAsync(Guid id, EndExemptionRequest r, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(r.Reason) || r.Reason.Length > 1000 || r.EndedOn == default)
        {
            return Fail("VALIDATION_FAILED", "The end date and a reason (max 1000) are required.");
        }
        var claim = await db.PropertyExemptions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (claim is null)
        {
            return NotFound();
        }
        if (claim.Status != ExemptionStatus.Approved)
        {
            return Fail("EXEMPTION_NOT_APPROVED", $"Only an approved exemption can end; this one is {claim.Status}.");
        }
        if (r.EndedOn < claim.EffectiveDate)
        {
            return Fail("VALIDATION_FAILED", $"The exemption took effect on {claim.EffectiveDate:yyyy-MM-dd}; it cannot end before that.");
        }
        claim.Status = ExemptionStatus.Ended;
        claim.EndedOn = r.EndedOn;
        claim.EndReason = r.Reason.Trim();
        currentUser.Reason = r.Reason;
        return await SaveWithReassessmentAsync(claim, r.EndedOn, "ended", cancellationToken);
    }

    public async Task<Result<PropertyExemptionDto>> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await MapAsync(db.PropertyExemptions.Where(x => x.Id == id), cancellationToken)).SingleOrDefault() is { } dto
            ? Result.Success(dto)
            : NotFound();

    public async Task<Result<IReadOnlyList<PropertyExemptionDto>>> ListByPropertyAsync(Guid propertyId, CancellationToken cancellationToken = default) =>
        Result.Success(await MapAsync(db.PropertyExemptions.Where(x => x.PropertyId == propertyId), cancellationToken));

    public async Task<Result<IReadOnlyList<PropertyExemptionDto>>> ListOpenAsync(CancellationToken cancellationToken = default)
    {
        var rows = await MapAsync(db.PropertyExemptions.Where(x => x.Status == ExemptionStatus.Claimed || x.Status == ExemptionStatus.ProofFiled)
            .OrderBy(x => x.ProofDueDate).Take(500), cancellationToken);
        return Result.Success<IReadOnlyList<PropertyExemptionDto>>(rows.OrderByDescending(x => x.ProofOverdue).ThenBy(x => x.ProofDueDate).ToList());
    }

    // --- Helpers ---

    /// <summary>Saves the decision and, where the unit's assessment in force is now marked wrongly, opens its reassessment (Q3), in one transaction.</summary>
    private async Task<Result<PropertyExemptionDto>> SaveWithReassessmentAsync(PropertyExemption claim, DateOnly from, string what, CancellationToken ct)
    {
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;
        await db.SaveChangesAsync(ct);
        var code = await db.ExemptionTypes.Where(x => x.Id == claim.ExemptionTypeId).Select(x => x.Code).FirstAsync(ct);
        var (draft, note) = await assessments.ReassessTaxabilityAsync(claim.RpuId, from, $"exemption {code} {what} with effect from {from:yyyy-MM-dd}.",
            options.Value.ReassessmentTransactionCode, ct);
        if (draft is not null)
        {
            await db.SaveChangesAsync(ct);
            claim.ReassessmentId = draft.Id;
            await db.SaveChangesAsync(ct);
        }
        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
        }
        return (await GetAsync(claim.Id, ct)) is { IsSuccess: true } dto ? Result.Success(dto.Value with { ReassessmentNote = note }) : NotFound();
    }

    private (string Code, string Message)? DecisionRefusal(PropertyExemption claim) =>
        MakerChecker.Refusal(currentUser, claim.CreatedBy, "CANNOT_DECIDE_OWN_EXEMPTION_CLAIM", "Whoever recorded the claim cannot also decide it (CLAUDE.md §46).");

    private void Decide(PropertyExemption claim, string? remarks)
    {
        claim.DecidedBy = currentUser.AppUserId;
        claim.DecidedAt = clock.UtcNow;
        claim.DecisionRemarks = Clean(remarks);
    }

    private static ExemptionAppliesTo Covers(RpuType type) => type switch
    {
        RpuType.Land => ExemptionAppliesTo.Land,
        RpuType.Building => ExemptionAppliesTo.Building,
        RpuType.Machinery => ExemptionAppliesTo.Machinery,
        _ => ExemptionAppliesTo.OtherImprovement,
    };

    private async Task<IReadOnlyList<PropertyExemptionDto>> MapAsync(IQueryable<PropertyExemption> query, CancellationToken ct)
    {
        var rows = await query.AsNoTracking().Include(x => x.ExemptionType).Include(x => x.Rpu).Include(x => x.Property)
            .Include(x => x.ActualUse).Include(x => x.ClaimantTaxpayer).Include(x => x.Evidence).ToListAsync(ct);
        var userIds = rows.SelectMany(x => new[] { x.CreatedBy, x.DecidedBy }.Concat(x.Evidence.Select(e => e.ReceivedBy))).OfType<Guid>().Distinct().ToList();
        var users = await db.AppUsers.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        string? Name(Guid? id) => id is { } x ? users.GetValueOrDefault(x) : null;
        var today = clock.Today;
        return rows.OrderByDescending(x => x.CreatedAt).Select(x => new PropertyExemptionDto(
            x.Id, x.PropertyId, x.Property!.PropertyIdentificationNumber, x.RpuId, x.Rpu!.RpuNumber, x.Rpu.RpuType,
            x.ExemptionTypeId, x.ExemptionType!.Code, x.ExemptionType.Name, x.ExemptionType.LegalBasis,
            x.ActualUseId, x.ActualUse?.Name, x.PortionDescription, x.ClaimantTaxpayerId,
            x.ClaimantTaxpayer is { } t ? TaxpayerNameFormatter.Format(t.TaxpayerType, t.LastName, t.FirstName, t.MiddleName, t.Suffix, t.CorporateName) : null,
            x.ClaimedOn, x.ProofDueDate, x.Status == ExemptionStatus.Claimed && today > x.ProofDueDate, x.ProofFiledOn > x.ProofDueDate,
            x.Reference, x.Remarks, x.Status, x.ProofFiledOn, x.EffectiveDate, x.ExpiryDate,
            x.CreatedBy, Name(x.CreatedBy), x.CreatedAt, x.DecidedBy, Name(x.DecidedBy), x.DecidedAt, x.DecisionRemarks, x.EndedOn, x.EndReason,
            x.Evidence.OrderBy(e => e.Sequence).Select(e => new ExemptionEvidenceDto(e.Sequence, e.Description, e.ReferenceNumber, e.DocumentDate,
                e.ReceivedOn, Name(e.ReceivedBy))).ToList(), x.ReassessmentId, RowVersion: x.RowVersion)).ToList();
    }

    private static ExemptionTypeDto ToDto(ExemptionType x) => new(
        x.Id, x.Code, x.Name, x.Description, x.AppliesTo, x.RequiresProof, x.AssessedValueCeiling, x.LegalBasis, x.EffectiveDate, x.EndDate, x.Status,
        x.CreatedBy, x.ApprovedBy, x.ApprovedAt, x.Remarks);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Result<PropertyExemptionDto> Fail(string code, string message) => Result.Failure<PropertyExemptionDto>(code, message);

    private static Result<PropertyExemptionDto> NotFound() => Fail("EXEMPTION_NOT_FOUND", "No exemption claim was found with the given id.");
}
