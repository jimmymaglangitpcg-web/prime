using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities.Offices;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Offices;

// --- Requests and DTOs (docs/analysis/province-wide-operation.md §3.4, Q6–Q7) ---

/// <param name="PropertyKinds">Empty covers every kind.</param>
/// <param name="RenewsDelegationId">The approved delegation of the same office this one renews.</param>
public sealed record CreateApprovalDelegationRequest(
    Guid OfficeId, string DelegatingOfficialName, string DelegatingOfficialPosition, string InstrumentReference, DateOnly InstrumentDate,
    IReadOnlyList<ApprovalSubjectType> SubjectTypes, IReadOnlyList<RpuType>? PropertyKinds, DateOnly ValidFrom, DateOnly ValidTo,
    Guid? RenewsDelegationId, string? Remarks);

public sealed record RejectApprovalDelegationRequest(string Reason);

/// <param name="RevokedFrom">The first day the delegation no longer applies; today or later (approvals already signed under it stand).</param>
public sealed record RevokeApprovalDelegationRequest(DateOnly RevokedFrom, string Reason);

/// <summary>Where a delegation stands on a date.</summary>
public enum DelegationState
{
    Draft,
    Rejected,
    Scheduled,
    InForce,
    Expired,
    Revoked,
}

public sealed record ApprovalDelegationDto(
    Guid Id, Guid OfficeId, string OfficeCode, string OfficeName, string DelegatingOfficialName, string DelegatingOfficialPosition,
    string InstrumentReference, DateOnly InstrumentDate, IReadOnlyList<ApprovalSubjectType> SubjectTypes, IReadOnlyList<RpuType> PropertyKinds,
    DateOnly ValidFrom, DateOnly ValidTo, WorkflowStatus Status, DelegationState State, Guid? RenewsDelegationId,
    DateOnly? RevokedFrom, DateTimeOffset? RevokedAt, Guid? RevokedBy, string? RevocationReason, string? RejectionReason, string? Remarks,
    Guid? CreatedBy, DateTimeOffset CreatedAt, Guid? ApprovedBy, DateTimeOffset? ApprovedAt);

public sealed class CreateApprovalDelegationRequestValidator : AbstractValidator<CreateApprovalDelegationRequest>
{
    public CreateApprovalDelegationRequestValidator()
    {
        RuleFor(x => x.DelegatingOfficialName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DelegatingOfficialPosition).NotEmpty().MaximumLength(200);
        RuleFor(x => x.InstrumentReference).NotEmpty().MaximumLength(300);
        RuleFor(x => x.InstrumentDate).NotEqual(default(DateOnly)).WithMessage("instrumentDate is required.");
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("validFrom is required.");
        RuleFor(x => x.ValidTo).NotEqual(default(DateOnly)).WithMessage("validTo is required: a delegation is for a period.");
        RuleFor(x => x.ValidTo).GreaterThanOrEqualTo(x => x.ValidFrom).WithMessage("validTo cannot be before validFrom.");
        RuleFor(x => x.SubjectTypes).NotEmpty().WithMessage("Name at least one kind of record the delegation covers.");
        RuleForEach(x => x.SubjectTypes).IsInEnum();
        RuleForEach(x => x.PropertyKinds).IsInEnum();
        RuleFor(x => x.Remarks).MaximumLength(1000);
    }
}

public interface IApprovalDelegationService
{
    Task<Result<IReadOnlyList<ApprovalDelegationDto>>> ListAsync(Guid? officeId, CancellationToken cancellationToken = default);
    Task<Result<ApprovalDelegationDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<ApprovalDelegationDto>> CreateAsync(CreateApprovalDelegationRequest request, CancellationToken cancellationToken = default);
    Task<Result<ApprovalDelegationDto>> ApproveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<ApprovalDelegationDto>> RejectAsync(Guid id, RejectApprovalDelegationRequest request, CancellationToken cancellationToken = default);
    Task<Result<ApprovalDelegationDto>> RevokeAsync(Guid id, RevokeApprovalDelegationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// The delegation in force for <paramref name="officeId"/> on <paramref name="date"/> covering the record type
    /// (and the property kind, when given); null when the province approves (step LP-4 routes by it).
    /// </summary>
    Task<ApprovalDelegationDto?> FindInForceAsync(Guid officeId, ApprovalSubjectType subjectType, RpuType? propertyKind, DateOnly date,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Delegations of final approval (docs/analysis/province-wide-operation.md §3.4, Q6–Q7).
/// Only provincial users create, approve and revoke them; a second provincial
/// user approves (CLAUDE.md §46). Approved delegations of one office may not
/// overlap in both period and records covered. Revocation takes effect from a
/// day not in the past. Nothing is deleted.
/// </summary>
public sealed class ApprovalDelegationService(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IOfficeContext officeContext,
    IClock clock,
    IValidator<CreateApprovalDelegationRequest> validator) : IApprovalDelegationService
{
    public async Task<Result<IReadOnlyList<ApprovalDelegationDto>>> ListAsync(Guid? officeId, CancellationToken cancellationToken = default)
    {
        var query = db.ApprovalDelegations.AsNoTracking().Include(x => x.Office).AsQueryable();
        if (officeId is not null)
        {
            query = query.Where(x => x.OfficeId == officeId);
        }
        var rows = await query.OrderBy(x => x.Office!.Code).ThenByDescending(x => x.ValidFrom).ToListAsync(cancellationToken);
        var today = clock.Today;
        return Result.Success<IReadOnlyList<ApprovalDelegationDto>>(rows.Select(x => ToDto(x, today)).ToList());
    }

    public async Task<Result<ApprovalDelegationDto>> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.ApprovalDelegations.AsNoTracking().Include(x => x.Office).FirstOrDefaultAsync(x => x.Id == id, cancellationToken) is { } d
            ? Result.Success(ToDto(d, clock.Today))
            : NotFound();

    public async Task<Result<ApprovalDelegationDto>> CreateAsync(CreateApprovalDelegationRequest request, CancellationToken cancellationToken = default)
    {
        if (await ProvincialOnlyAsync(cancellationToken) is { } forbidden)
        {
            return forbidden;
        }
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<ApprovalDelegationDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }
        var office = await db.Offices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.OfficeId, cancellationToken);
        if (office is null)
        {
            return Result.Failure<ApprovalDelegationDto>("OFFICE_NOT_FOUND", "No office was found with the given id.");
        }
        if (office.Kind != OfficeKind.Municipal || office.Status != RecordStatus.Active)
        {
            return Result.Failure<ApprovalDelegationDto>("OFFICE_NOT_MUNICIPAL", "Final approval is delegated to an active municipal office only.");
        }
        if (request.RenewsDelegationId is { } renewsId)
        {
            var renewed = await db.ApprovalDelegations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == renewsId, cancellationToken);
            if (renewed is null || renewed.OfficeId != request.OfficeId || renewed.Status != WorkflowStatus.Approved)
            {
                return Result.Failure<ApprovalDelegationDto>("DELEGATION_RENEWAL_INVALID", "A renewal names an approved delegation of the same office.");
            }
            if (request.ValidFrom <= renewed.ValidFrom)
            {
                return Result.Failure<ApprovalDelegationDto>("DELEGATION_RENEWAL_INVALID", "A renewal starts after the delegation it renews started.");
            }
        }

        var delegation = new ApprovalDelegation
        {
            OfficeId = request.OfficeId,
            DelegatingOfficialName = request.DelegatingOfficialName.Trim(),
            DelegatingOfficialPosition = request.DelegatingOfficialPosition.Trim(),
            InstrumentReference = request.InstrumentReference.Trim(),
            InstrumentDate = request.InstrumentDate,
            SubjectTypes = request.SubjectTypes.Distinct().Order().ToList(),
            PropertyKinds = (request.PropertyKinds ?? []).Distinct().Order().ToList(),
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            RenewsDelegationId = request.RenewsDelegationId,
            Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim(),
        };
        if (await OverlapAsync(delegation, cancellationToken) is { } overlap)
        {
            return overlap;
        }
        db.ApprovalDelegations.Add(delegation);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(delegation.Id, cancellationToken);
    }

    public async Task<Result<ApprovalDelegationDto>> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (await ProvincialOnlyAsync(cancellationToken) is { } forbidden)
        {
            return forbidden;
        }
        var delegation = await db.ApprovalDelegations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (delegation is null)
        {
            return NotFound();
        }
        if (delegation.Status != WorkflowStatus.Draft)
        {
            return Result.Failure<ApprovalDelegationDto>("DELEGATION_NOT_DRAFT", "Only a draft delegation can be approved.");
        }
        if (currentUser.AppUserId is not null && delegation.CreatedBy == currentUser.AppUserId)
        {
            return Result.Failure<ApprovalDelegationDto>("CANNOT_APPROVE_OWN_DELEGATION", "The user who entered the delegation cannot also approve it (CLAUDE.md §46).");
        }
        if (await OverlapAsync(delegation, cancellationToken) is { } overlap)
        {
            return overlap;
        }
        delegation.Status = WorkflowStatus.Approved;
        delegation.ApprovedBy = currentUser.AppUserId;
        delegation.ApprovedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<Result<ApprovalDelegationDto>> RejectAsync(Guid id, RejectApprovalDelegationRequest request, CancellationToken cancellationToken = default)
    {
        if (await ProvincialOnlyAsync(cancellationToken) is { } forbidden)
        {
            return forbidden;
        }
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000)
        {
            return Result.Failure<ApprovalDelegationDto>("VALIDATION_FAILED", "A reason is required (at most 1000 characters).");
        }
        var delegation = await db.ApprovalDelegations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (delegation is null)
        {
            return NotFound();
        }
        if (delegation.Status != WorkflowStatus.Draft)
        {
            return Result.Failure<ApprovalDelegationDto>("DELEGATION_NOT_DRAFT", "Only a draft delegation can be rejected; revoke an approved one.");
        }
        delegation.Status = WorkflowStatus.Rejected;
        delegation.RejectionReason = request.Reason.Trim();
        await SaveWithReasonAsync(request.Reason, cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<Result<ApprovalDelegationDto>> RevokeAsync(Guid id, RevokeApprovalDelegationRequest request, CancellationToken cancellationToken = default)
    {
        if (await ProvincialOnlyAsync(cancellationToken) is { } forbidden)
        {
            return forbidden;
        }
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000)
        {
            return Result.Failure<ApprovalDelegationDto>("VALIDATION_FAILED", "A reason is required (at most 1000 characters).");
        }
        var delegation = await db.ApprovalDelegations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (delegation is null)
        {
            return NotFound();
        }
        if (delegation.Status != WorkflowStatus.Approved || delegation.RevokedFrom is not null)
        {
            return Result.Failure<ApprovalDelegationDto>("DELEGATION_NOT_REVOCABLE", "Only an approved delegation that is not already revoked can be revoked.");
        }
        var today = clock.Today;
        if (request.RevokedFrom < today)
        {
            return Result.Failure<ApprovalDelegationDto>("VALIDATION_FAILED", "A revocation takes effect today or later; approvals already signed under the delegation stand.");
        }
        if (request.RevokedFrom > delegation.ValidTo)
        {
            return Result.Failure<ApprovalDelegationDto>("VALIDATION_FAILED", $"The delegation already ends on {delegation.ValidTo:yyyy-MM-dd}.");
        }
        delegation.RevokedFrom = request.RevokedFrom < delegation.ValidFrom ? delegation.ValidFrom : request.RevokedFrom;
        delegation.RevokedAt = DateTimeOffset.UtcNow;
        delegation.RevokedBy = currentUser.AppUserId;
        delegation.RevocationReason = request.Reason.Trim();
        await SaveWithReasonAsync(request.Reason, cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<ApprovalDelegationDto?> FindInForceAsync(Guid officeId, ApprovalSubjectType subjectType, RpuType? propertyKind, DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var candidates = await db.ApprovalDelegations.AsNoTracking().Include(x => x.Office)
            .Where(x => x.OfficeId == officeId && x.Status == WorkflowStatus.Approved && x.ValidFrom <= date && x.ValidTo >= date
                && (x.RevokedFrom == null || x.RevokedFrom > date))
            .ToListAsync(cancellationToken);
        var match = candidates.FirstOrDefault(x => x.SubjectTypes.Contains(subjectType)
            && (x.PropertyKinds.Count == 0 || (propertyKind is { } kind && x.PropertyKinds.Contains(kind))));
        return match is null ? null : ToDto(match, date);
    }

    // --- Rules ---

    /// <summary>Delegations are the province's to give (Q6): a municipal or unassigned user may not create, decide or revoke them.</summary>
    private async Task<Result<ApprovalDelegationDto>?> ProvincialOnlyAsync(CancellationToken ct)
    {
        if (currentUser.AppUserId is null)
        {
            return null; // system processes
        }
        var scope = await officeContext.GetAsync(ct);
        return scope.Assigned && scope.ProvinceWide
            ? null
            : Result.Failure<ApprovalDelegationDto>("DELEGATION_FORBIDDEN", "Only the provincial office gives, approves and revokes delegations.");
    }

    /// <summary>Approved delegations of one office may not overlap in both period (up to any revocation) and records covered; checked on entry and again on approval.</summary>
    private async Task<Result<ApprovalDelegationDto>?> OverlapAsync(ApprovalDelegation delegation, CancellationToken ct)
    {
        var others = await db.ApprovalDelegations.AsNoTracking()
            .Where(x => x.OfficeId == delegation.OfficeId && x.Id != delegation.Id && x.Status == WorkflowStatus.Approved)
            .ToListAsync(ct);
        var clash = others.FirstOrDefault(x => x.ValidFrom <= delegation.ValidTo && delegation.ValidFrom <= x.EffectiveEnd
            && x.SubjectTypes.Intersect(delegation.SubjectTypes).Any()
            && (x.PropertyKinds.Count == 0 || delegation.PropertyKinds.Count == 0 || x.PropertyKinds.Intersect(delegation.PropertyKinds).Any()));
        return clash is null
            ? null
            : Result.Failure<ApprovalDelegationDto>("DELEGATION_OVERLAP_CONFLICT",
                $"An approved delegation ({clash.InstrumentReference}) already covers some of these records from {clash.ValidFrom:yyyy-MM-dd} to {clash.EffectiveEnd:yyyy-MM-dd}. " +
                "Start after it, or revoke it first.");
    }

    private async Task SaveWithReasonAsync(string reason, CancellationToken ct)
    {
        currentUser.Reason = reason.Trim();
        try
        {
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            currentUser.Reason = null;
        }
    }

    public static DelegationState StateOn(ApprovalDelegation x, DateOnly date) => x.Status switch
    {
        WorkflowStatus.Draft => DelegationState.Draft,
        WorkflowStatus.Rejected => DelegationState.Rejected,
        _ when x.RevokedFrom is { } r && r <= date => DelegationState.Revoked,
        _ when date < x.ValidFrom => DelegationState.Scheduled,
        _ when date > x.ValidTo => DelegationState.Expired,
        _ => DelegationState.InForce,
    };

    private static ApprovalDelegationDto ToDto(ApprovalDelegation x, DateOnly asOf) => new(
        x.Id, x.OfficeId, x.Office!.Code, x.Office.Name, x.DelegatingOfficialName, x.DelegatingOfficialPosition,
        x.InstrumentReference, x.InstrumentDate, x.SubjectTypes, x.PropertyKinds, x.ValidFrom, x.ValidTo, x.Status, StateOn(x, asOf),
        x.RenewsDelegationId, x.RevokedFrom, x.RevokedAt, x.RevokedBy, x.RevocationReason, x.RejectionReason, x.Remarks,
        x.CreatedBy, x.CreatedAt, x.ApprovedBy, x.ApprovedAt);

    private static Result<ApprovalDelegationDto> NotFound() =>
        Result.Failure<ApprovalDelegationDto>("DELEGATION_NOT_FOUND", "No approval delegation was found with the given id.");
}
