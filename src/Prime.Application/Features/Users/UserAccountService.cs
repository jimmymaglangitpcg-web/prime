using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Offices;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Entities.Offices;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Users;

// --- Requests and DTOs (docs/analysis/workflow-security.md §4.2) ---

public sealed record SignUpOfficeOption(Guid Id, string Code, string Name, OfficeKind Kind);

/// <summary>What an applicant may ask for: the active offices and the roles, and which roles may be held province-wide.</summary>
public sealed record SignUpOptionsDto(IReadOnlyList<SignUpOfficeOption> Offices, IReadOnlyList<RoleDto> Roles, IReadOnlyList<string> ProvinceWideRoles);

/// <param name="OfficeId">The office the applicant works in; null for a province-wide post.</param>
public sealed record SubmitSignUpRequest(string FullName, string Position, Guid? OfficeId, IReadOnlyList<string> Roles, string? Note);

/// <summary>The office and roles the administrator gives, which may differ from those asked.</summary>
public sealed record ApproveSignUpRequest(Guid? OfficeId, IReadOnlyList<string> Roles, string? Remarks);

public sealed record DecisionReasonRequest(string? Reason);

public sealed record SignUpRequestDto(
    Guid Id, Guid AppUserId, string Email, string FullName, string Position, Guid? RequestedOfficeId, string? RequestedOfficeCode,
    string? RequestedOfficeName, IReadOnlyList<string> RequestedRoles, string? Note, WorkflowStatus Status, DateTimeOffset CreatedAt,
    Guid? DecidedBy, string? DecidedByName, DateTimeOffset? DecidedAt, string? DecisionReason, Guid? OfficeAssignmentId);

/// <param name="NewStatus">Inactive to disable the user, Active to enable them again.</param>
public sealed record ProposeUserStatusRequest(AppUserStatus NewStatus, string Reason);

public sealed record UserStatusChangeDto(
    Guid Id, Guid AppUserId, string UserName, AppUserStatus NewStatus, string Reason, WorkflowStatus Status,
    Guid? CreatedBy, string? CreatedByName, DateTimeOffset CreatedAt, Guid? DecidedBy, string? DecidedByName, DateTimeOffset? DecidedAt,
    string? DecisionReason);

public enum SessionEvent { SignIn, SignOut }

/// <param name="Reason">E.g. "idle" when the browser signed the user out after inactivity.</param>
public sealed record SessionEventRequest(SessionEvent Event, string? Reason);

public interface IUserAccountService
{
    Task<Result<SignUpOptionsDto>> GetSignUpOptionsAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<SignUpRequestDto>>> ListMySignUpRequestsAsync(CancellationToken ct = default);
    Task<Result<SignUpRequestDto>> SubmitSignUpAsync(SubmitSignUpRequest request, CancellationToken ct = default);
    Task<Result<IReadOnlyList<SignUpRequestDto>>> ListSignUpRequestsAsync(WorkflowStatus? status, CancellationToken ct = default);
    Task<Result<SignUpRequestDto>> ApproveSignUpAsync(Guid id, ApproveSignUpRequest request, CancellationToken ct = default);
    Task<Result<SignUpRequestDto>> RejectSignUpAsync(Guid id, DecisionReasonRequest request, CancellationToken ct = default);

    Task<Result<IReadOnlyList<UserStatusChangeDto>>> ListStatusChangesAsync(Guid? userId, CancellationToken ct = default);
    Task<Result<UserStatusChangeDto>> ProposeStatusChangeAsync(Guid userId, ProposeUserStatusRequest request, CancellationToken ct = default);
    Task<Result<UserStatusChangeDto>> ApproveStatusChangeAsync(Guid id, CancellationToken ct = default);
    Task<Result<UserStatusChangeDto>> RejectStatusChangeAsync(Guid id, DecisionReasonRequest request, CancellationToken ct = default);

    Task<Result<bool>> RecordSessionEventAsync(SessionEventRequest request, CancellationToken ct = default);

    /// <summary>
    /// The one-off set-up of a new installation's first SYSTEM_ADMIN (§4.2): the person signs up and signs in once,
    /// then the operator runs the API's <c>bootstrap-admin</c> command with their e-mail. Refused once an active
    /// SYSTEM_ADMIN exists. Never reachable through the API.
    /// </summary>
    Task<Result<string>> BootstrapFirstAdminAsync(string email, CancellationToken ct = default);
}

/// <summary>
/// Accounts (docs/analysis/workflow-security.md §4.2; decisions Q5, Q6). A new sign-in is a Pending user who may only
/// say who they are and ask for an account; a SYSTEM_ADMIN approves it with an office and roles, in force at once, or
/// rejects it. Disabling and re-enabling a user is proposed by one administrator and takes effect when a second approves.
/// Nothing is deleted.
/// </summary>
public sealed class UserAccountService(IApplicationDbContext db, ICurrentUserService currentUser, ISecurityEventLog securityEvents, IClock clock)
    : IUserAccountService
{
    public const string SignUpLegalBasis = "Sign-up request approved by a system administrator (docs/analysis/workflow-security.md §4.2)";
    public const string BootstrapLegalBasis = "First system administrator of the installation, set up by the bootstrap-admin command (workflow-security.md §4.2)";

    // --- Sign-up ---

    public async Task<Result<SignUpOptionsDto>> GetSignUpOptionsAsync(CancellationToken ct = default)
    {
        var offices = await db.Offices.AsNoTracking().Where(o => o.Status == RecordStatus.Active).OrderBy(o => o.Kind).ThenBy(o => o.Name)
            .Select(o => new SignUpOfficeOption(o.Id, o.Code, o.Name, o.Kind)).ToListAsync(ct);
        var roles = await db.Roles.AsNoTracking().OrderBy(r => r.Code).Select(r => new RoleDto(r.Code, r.Name)).ToListAsync(ct);
        return Result.Success(new SignUpOptionsDto(offices, roles, RoleCodes.ProvinceWide.Order(StringComparer.Ordinal).ToList()));
    }

    public async Task<Result<IReadOnlyList<SignUpRequestDto>>> ListMySignUpRequestsAsync(CancellationToken ct = default)
    {
        if (currentUser.AppUserId is not { } userId)
        {
            return Result.Success<IReadOnlyList<SignUpRequestDto>>([]);
        }
        return Result.Success(await RequestDtosAsync(db.SignUpRequests.Where(r => r.AppUserId == userId), ct));
    }

    public async Task<Result<SignUpRequestDto>> SubmitSignUpAsync(SubmitSignUpRequest request, CancellationToken ct = default)
    {
        var user = currentUser.AppUserId is { } userId ? await db.AppUsers.FirstOrDefaultAsync(u => u.Id == userId, ct) : null;
        if (user is null)
        {
            return Result.Failure<SignUpRequestDto>("USER_UNKNOWN", "Sign in before asking for an account.");
        }
        if (user.Status != AppUserStatus.Pending)
        {
            return Result.Failure<SignUpRequestDto>("SIGN_UP_NOT_PENDING", "Your account is not waiting for approval.");
        }
        var fullName = request.FullName?.Trim() ?? string.Empty;
        var position = request.Position?.Trim() ?? string.Empty;
        var roles = (request.Roles ?? []).Select(r => r.Trim()).ToList();
        if (fullName.Length is 0 or > 200 || position.Length is 0 or > 200 || request.Note?.Trim().Length > 1000
            || roles.Count == 0 || roles.Distinct().Count() != roles.Count)
        {
            return Result.Failure<SignUpRequestDto>("VALIDATION_FAILED",
                "Your full name and position (max 200 each) and at least one role, not repeated, are required; the note is at most 1000.");
        }
        var (_, refused) = await AssignmentRules.ResolveAsync(db, request.OfficeId, roles, ct);
        if (refused is not null)
        {
            return Result.Failure<SignUpRequestDto>(refused.Code!, refused.Message!);
        }
        if (await db.SignUpRequests.AnyAsync(r => r.AppUserId == user.Id && r.Status == WorkflowStatus.PendingReview, ct))
        {
            return Result.Failure<SignUpRequestDto>("SIGN_UP_REQUEST_PENDING", "You already have a request waiting for approval.");
        }
        var signUp = new SignUpRequest
        {
            AppUserId = user.Id, FullName = fullName, Position = position, RequestedOfficeId = request.OfficeId, RequestedRoles = roles,
            Note = Clean(request.Note),
        };
        db.SignUpRequests.Add(signUp);
        await db.SaveChangesAsync(ct);
        return Result.Success(await RequestDtoAsync(signUp.Id, ct));
    }

    public async Task<Result<IReadOnlyList<SignUpRequestDto>>> ListSignUpRequestsAsync(WorkflowStatus? status, CancellationToken ct = default)
    {
        var query = db.SignUpRequests.AsQueryable();
        if (status is not null)
        {
            query = query.Where(r => r.Status == status);
        }
        return Result.Success(await RequestDtosAsync(query, ct));
    }

    public async Task<Result<SignUpRequestDto>> ApproveSignUpAsync(Guid id, ApproveSignUpRequest request, CancellationToken ct = default)
    {
        var (signUp, user, refusal) = await OpenRequestAsync(id, ct);
        if (refusal is not null)
        {
            return refusal;
        }
        var roleCodes = (request.Roles ?? []).Select(r => r.Trim()).ToList();
        if (roleCodes.Count == 0 || roleCodes.Distinct().Count() != roleCodes.Count || request.Remarks?.Trim().Length > 1000)
        {
            return Result.Failure<SignUpRequestDto>("VALIDATION_FAILED", "At least one role, not repeated, is required; remarks are at most 1000.");
        }
        var (roles, refused) = await AssignmentRules.ResolveAsync(db, request.OfficeId, roleCodes, ct);
        if (refused is not null)
        {
            return Result.Failure<SignUpRequestDto>(refused.Code!, refused.Message!);
        }
        var today = clock.Today;
        if (await db.OfficeAssignments.AnyAsync(a => a.AppUserId == user!.Id && a.Status == WorkflowStatus.Approved
                && (a.EndDate == null || a.EndDate >= today), ct))
        {
            return Result.Failure<SignUpRequestDto>("USER_ALREADY_ASSIGNED", "This user already has an office assignment in force or to come.");
        }

        var assignment = new OfficeAssignment
        {
            // In force at once: the applicant asked (maker), the administrator approves (checker).
            AppUserId = user!.Id, OfficeId = request.OfficeId, EffectiveDate = today, LegalBasis = SignUpLegalBasis,
            Remarks = Clean(request.Remarks), Status = WorkflowStatus.Approved, ApprovedBy = currentUser.AppUserId, ApprovedAt = DateTimeOffset.UtcNow,
            CreatedBy = user.Id,
            Roles = roles.Select(r => new OfficeAssignmentRole { RoleId = r.Id }).ToList(),
        };
        // One save: the user, their assignment and the decision change together.
        db.OfficeAssignments.Add(assignment);
        user.Status = AppUserStatus.Active;
        user.DisplayName = signUp!.FullName;
        signUp.Status = WorkflowStatus.Approved;
        signUp.DecidedBy = currentUser.AppUserId;
        signUp.DecidedAt = DateTimeOffset.UtcNow;
        signUp.OfficeAssignmentId = assignment.Id;
        currentUser.Reason = "Sign-up approved";
        try
        {
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            currentUser.Reason = null;
        }
        return Result.Success(await RequestDtoAsync(id, ct));
    }

    public async Task<Result<SignUpRequestDto>> RejectSignUpAsync(Guid id, DecisionReasonRequest request, CancellationToken ct = default)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length > 500)
        {
            return Result.Failure<SignUpRequestDto>("VALIDATION_FAILED", "A reason (max 500) is required to reject a request.");
        }
        var (signUp, _, refusal) = await OpenRequestAsync(id, ct);
        if (refusal is not null)
        {
            return refusal;
        }
        signUp!.Status = WorkflowStatus.Rejected;
        signUp.DecidedBy = currentUser.AppUserId;
        signUp.DecidedAt = DateTimeOffset.UtcNow;
        signUp.DecisionReason = reason;
        currentUser.Reason = reason;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            currentUser.Reason = null;
        }
        return Result.Success(await RequestDtoAsync(id, ct));
    }

    /// <summary>An open request a known administrator other than the applicant may decide.</summary>
    private async Task<(SignUpRequest? SignUp, AppUser? User, Result<SignUpRequestDto>? Refusal)> OpenRequestAsync(Guid id, CancellationToken ct)
    {
        var signUp = await db.SignUpRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (signUp is null)
        {
            return (null, null, Result.Failure<SignUpRequestDto>("SIGN_UP_REQUEST_NOT_FOUND", "No sign-up request was found with the given id."));
        }
        if (signUp.Status != WorkflowStatus.PendingReview)
        {
            return (null, null, Result.Failure<SignUpRequestDto>("SIGN_UP_REQUEST_NOT_OPEN", "This request has already been decided."));
        }
        if (currentUser.AppUserId is null)
        {
            return (null, null, Result.Failure<SignUpRequestDto>("DECIDING_USER_UNKNOWN", "A sign-up request is decided by a signed-in administrator."));
        }
        if (currentUser.AppUserId == signUp.AppUserId)
        {
            return (null, null, Result.Failure<SignUpRequestDto>("CANNOT_DECIDE_OWN_SIGN_UP", "You cannot decide your own sign-up request (CLAUDE.md §46)."));
        }
        var user = await db.AppUsers.FirstAsync(u => u.Id == signUp.AppUserId, ct);
        if (user.Status != AppUserStatus.Pending)
        {
            return (null, null, Result.Failure<SignUpRequestDto>("SIGN_UP_NOT_PENDING", "This user's account is no longer waiting for approval."));
        }
        return (signUp, user, null);
    }

    // --- Disabling and enabling (Q6) ---

    public async Task<Result<IReadOnlyList<UserStatusChangeDto>>> ListStatusChangesAsync(Guid? userId, CancellationToken ct = default)
    {
        var query = db.UserStatusChanges.AsNoTracking();
        if (userId is not null)
        {
            query = query.Where(c => c.AppUserId == userId);
        }
        var rows = await query.Include(c => c.AppUser).OrderByDescending(c => c.CreatedAt).Take(100).ToListAsync(ct);
        var names = await NamesAsync(rows.SelectMany(c => new[] { c.CreatedBy, c.DecidedBy }), ct);
        return Result.Success<IReadOnlyList<UserStatusChangeDto>>(rows.Select(c => ToDto(c, names)).ToList());
    }

    public async Task<Result<UserStatusChangeDto>> ProposeStatusChangeAsync(Guid userId, ProposeUserStatusRequest request, CancellationToken ct = default)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length > 500 || request.NewStatus is not (AppUserStatus.Active or AppUserStatus.Inactive))
        {
            return Result.Failure<UserStatusChangeDto>("VALIDATION_FAILED", "A reason (max 500) is required, and the new status is Active or Inactive.");
        }
        var user = await db.AppUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return Result.Failure<UserStatusChangeDto>("USER_NOT_FOUND", "No user was found with the given id.");
        }
        if (currentUser.AppUserId == userId)
        {
            return Result.Failure<UserStatusChangeDto>("CANNOT_CHANGE_OWN_STATUS", "You cannot disable or enable your own account.");
        }
        if (user.Status == AppUserStatus.Pending)
        {
            return Result.Failure<UserStatusChangeDto>("USER_PENDING", "This account is waiting for its sign-up to be decided.");
        }
        if (user.Status == request.NewStatus)
        {
            return Result.Failure<UserStatusChangeDto>("USER_STATUS_UNCHANGED", $"The user is already {request.NewStatus}.");
        }
        if (await db.UserStatusChanges.AnyAsync(c => c.AppUserId == userId && c.Status == WorkflowStatus.Draft, ct))
        {
            return Result.Failure<UserStatusChangeDto>("USER_STATUS_CHANGE_PENDING", "This user already has a change waiting for approval.");
        }
        var change = new UserStatusChange { AppUserId = userId, NewStatus = request.NewStatus, Reason = reason };
        db.UserStatusChanges.Add(change);
        await db.SaveChangesAsync(ct);
        return Result.Success(await StatusChangeDtoAsync(change.Id, ct));
    }

    public async Task<Result<UserStatusChangeDto>> ApproveStatusChangeAsync(Guid id, CancellationToken ct = default)
    {
        var (change, refusal) = await OpenStatusChangeAsync(id, ct);
        if (refusal is not null)
        {
            return refusal;
        }
        var user = await db.AppUsers.FirstAsync(u => u.Id == change!.AppUserId, ct);
        if (user.Status is AppUserStatus.Pending || user.Status == change!.NewStatus)
        {
            return Result.Failure<UserStatusChangeDto>("USER_STATUS_UNCHANGED", "The user's status has changed since this was proposed; reject it and propose again.");
        }
        // In force at once: the next request of a disabled user is refused.
        user.Status = change.NewStatus;
        change.Status = WorkflowStatus.Approved;
        change.DecidedBy = currentUser.AppUserId;
        change.DecidedAt = DateTimeOffset.UtcNow;
        currentUser.Reason = change.Reason;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            currentUser.Reason = null;
        }
        return Result.Success(await StatusChangeDtoAsync(id, ct));
    }

    public async Task<Result<UserStatusChangeDto>> RejectStatusChangeAsync(Guid id, DecisionReasonRequest request, CancellationToken ct = default)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length > 500)
        {
            return Result.Failure<UserStatusChangeDto>("VALIDATION_FAILED", "A reason (max 500) is required to reject a change.");
        }
        var (change, refusal) = await OpenStatusChangeAsync(id, ct);
        if (refusal is not null)
        {
            return refusal;
        }
        change!.Status = WorkflowStatus.Rejected;
        change.DecidedBy = currentUser.AppUserId;
        change.DecidedAt = DateTimeOffset.UtcNow;
        change.DecisionReason = reason;
        currentUser.Reason = reason;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            currentUser.Reason = null;
        }
        return Result.Success(await StatusChangeDtoAsync(id, ct));
    }

    /// <summary>A Draft change a known second user, neither its author nor its subject, may decide.</summary>
    private async Task<(UserStatusChange? Change, Result<UserStatusChangeDto>? Refusal)> OpenStatusChangeAsync(Guid id, CancellationToken ct)
    {
        var change = await db.UserStatusChanges.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (change is null)
        {
            return (null, Result.Failure<UserStatusChangeDto>("USER_STATUS_CHANGE_NOT_FOUND", "No user status change was found with the given id."));
        }
        if (change.Status != WorkflowStatus.Draft)
        {
            return (null, Result.Failure<UserStatusChangeDto>("USER_STATUS_CHANGE_NOT_DRAFT", "This change has already been decided."));
        }
        if (currentUser.AppUserId is null || currentUser.AppUserId == change.CreatedBy || currentUser.AppUserId == change.AppUserId)
        {
            return (null, Result.Failure<UserStatusChangeDto>("CANNOT_APPROVE_OWN_USER_STATUS_CHANGE",
                "A second user, neither the author nor the user concerned, decides this change (maker-checker, CLAUDE.md §46)."));
        }
        return (change, null);
    }

    // --- Sign-in events ---

    public async Task<Result<bool>> RecordSessionEventAsync(SessionEventRequest request, CancellationToken ct = default)
    {
        if (currentUser.AppUserId is not { } userId)
        {
            return Result.Failure<bool>("USER_UNKNOWN", "No signed-in user.");
        }
        var reason = Clean(request.Reason);
        if (reason?.Length > 200 || !Enum.IsDefined(request.Event))
        {
            return Result.Failure<bool>("VALIDATION_FAILED", "The event is SignIn or SignOut; the reason is at most 200.");
        }
        await securityEvents.WriteAsync(request.Event == SessionEvent.SignIn ? AuditAction.Login : AuditAction.Logout, userId, reason, ct);
        return Result.Success(true);
    }

    // --- Bootstrap ---

    public async Task<Result<string>> BootstrapFirstAdminAsync(string email, CancellationToken ct = default)
    {
        var today = clock.Today;
        var adminExists = await db.OfficeAssignments.InForce(today)
            .AnyAsync(a => a.AppUser!.Status == AppUserStatus.Active && a.Roles.Any(r => r.Role!.Code == RoleCodes.SystemAdmin), ct);
        if (adminExists)
        {
            return Result.Failure<string>("BOOTSTRAP_NOT_NEEDED", "An active SYSTEM_ADMIN already exists; new accounts are approved through sign-up requests.");
        }
        var normalized = email.Trim().ToLowerInvariant();
        var matches = await db.AppUsers.Where(u => u.Email.ToLower() == normalized).ToListAsync(ct);
        if (matches.Count != 1)
        {
            return Result.Failure<string>("BOOTSTRAP_USER_NOT_FOUND", matches.Count == 0
                ? "No user with this e-mail has signed in yet. Sign up and sign in once, then run the command again."
                : "More than one user has this e-mail.");
        }
        var user = matches[0];
        if (user.Status == AppUserStatus.Inactive)
        {
            return Result.Failure<string>("USER_DISABLED", "This user is disabled.");
        }
        if (await db.OfficeAssignments.AnyAsync(a => a.AppUserId == user.Id && a.Status == WorkflowStatus.Approved && (a.EndDate == null || a.EndDate >= today), ct))
        {
            return Result.Failure<string>("USER_ALREADY_ASSIGNED", "This user already has an office assignment in force or to come.");
        }
        var role = await db.Roles.FirstAsync(r => r.Code == RoleCodes.SystemAdmin, ct);
        user.Status = AppUserStatus.Active;
        db.OfficeAssignments.Add(new OfficeAssignment
        {
            AppUserId = user.Id, OfficeId = null, EffectiveDate = today, LegalBasis = BootstrapLegalBasis,
            Status = WorkflowStatus.Approved, ApprovedAt = DateTimeOffset.UtcNow,
            Roles = [new OfficeAssignmentRole { RoleId = role.Id }],
        });
        currentUser.Reason = "bootstrap-admin command";
        try
        {
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            currentUser.Reason = null;
        }
        return Result.Success($"{user.Email} is now an active province-wide SYSTEM_ADMIN from {today:yyyy-MM-dd}.");
    }

    // --- Helpers ---

    private async Task<SignUpRequestDto> RequestDtoAsync(Guid id, CancellationToken ct) =>
        (await RequestDtosAsync(db.SignUpRequests.Where(r => r.Id == id), ct)).Single();

    private async Task<IReadOnlyList<SignUpRequestDto>> RequestDtosAsync(IQueryable<SignUpRequest> query, CancellationToken ct)
    {
        var rows = await query.AsNoTracking().Include(r => r.AppUser).Include(r => r.RequestedOffice)
            .OrderByDescending(r => r.CreatedAt).Take(200).ToListAsync(ct);
        var names = await NamesAsync(rows.Select(r => r.DecidedBy), ct);
        return rows.Select(r => new SignUpRequestDto(
            r.Id, r.AppUserId, r.AppUser!.Email, r.FullName, r.Position, r.RequestedOfficeId, r.RequestedOffice?.Code, r.RequestedOffice?.Name,
            r.RequestedRoles, r.Note, r.Status, r.CreatedAt, r.DecidedBy, r.DecidedBy is { } d ? names.GetValueOrDefault(d) : null, r.DecidedAt,
            r.DecisionReason, r.OfficeAssignmentId)).ToList();
    }

    private async Task<UserStatusChangeDto> StatusChangeDtoAsync(Guid id, CancellationToken ct)
    {
        var change = await db.UserStatusChanges.AsNoTracking().Include(c => c.AppUser).SingleAsync(c => c.Id == id, ct);
        return ToDto(change, await NamesAsync([change.CreatedBy, change.DecidedBy], ct));
    }

    private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var wanted = ids.OfType<Guid>().Distinct().ToList();
        return wanted.Count == 0 ? []
            : await db.AppUsers.AsNoTracking().Where(u => wanted.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
    }

    private static UserStatusChangeDto ToDto(UserStatusChange c, Dictionary<Guid, string> names) => new(
        c.Id, c.AppUserId, c.AppUser!.DisplayName, c.NewStatus, c.Reason, c.Status,
        c.CreatedBy, c.CreatedBy is { } a ? names.GetValueOrDefault(a) : null, c.CreatedAt,
        c.DecidedBy, c.DecidedBy is { } d ? names.GetValueOrDefault(d) : null, c.DecidedAt, c.DecisionReason);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
