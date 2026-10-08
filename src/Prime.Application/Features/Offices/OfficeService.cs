using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Entities.Offices;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Offices;

// --- Requests and DTOs (docs/analysis/province-wide-operation.md §3.1–§3.2) ---

/// <param name="LguName">The local government printed above the office on its letterhead (§3.6).</param>
/// <param name="SanggunianName">The Sanggunian whose tax ordinance the office's TDs cite (records-and-forms.md Q8).</param>
public sealed record CreateOfficeRequest(string Code, string Name, OfficeKind Kind, string? HeadPosition, string? Address, string? Contact,
    string? LguName = null, string? SanggunianName = null);

/// <summary>The code and kind never change; everything else may.</summary>
public sealed record UpdateOfficeRequest(string Name, string? HeadPosition, string? Address, string? Contact, RecordStatus Status,
    string? LguName = null, string? SanggunianName = null);

public sealed record OfficeDto(Guid Id, string Code, string Name, OfficeKind Kind, string? HeadPosition, string? Address, string? Contact, RecordStatus Status,
    string? LguName, string? SanggunianName = null);

public sealed record CreateOfficeJurisdictionRequest(Guid OfficeId, Guid MunicipalityId, DateOnly EffectiveDate, string LegalBasis, string? Remarks);

public sealed record OfficeJurisdictionDto(
    Guid Id, Guid OfficeId, string OfficeCode, Guid MunicipalityId, string MunicipalityName, string MunicipalityPsgcCode,
    DateOnly EffectiveDate, DateOnly? EndDate, WorkflowStatus Status, string LegalBasis, string? Remarks,
    Guid? CreatedBy, DateTimeOffset CreatedAt, Guid? ApprovedBy, DateTimeOffset? ApprovedAt);

/// <param name="OfficeId">Null for a province-wide assignment (SYSTEM_ADMIN, AUDITOR only).</param>
public sealed record CreateOfficeAssignmentRequest(Guid AppUserId, Guid? OfficeId, IReadOnlyList<string> Roles, DateOnly EffectiveDate, string LegalBasis, string? Remarks);

public sealed record EndOfficeAssignmentRequest(DateOnly EndDate, string Reason);

public sealed record OfficeAssignmentDto(
    Guid Id, Guid AppUserId, string UserName, Guid? OfficeId, string? OfficeCode, IReadOnlyList<string> Roles,
    DateOnly EffectiveDate, DateOnly? EndDate, WorkflowStatus Status, string LegalBasis, string? Remarks,
    Guid? CreatedBy, DateTimeOffset CreatedAt, Guid? ApprovedBy, DateTimeOffset? ApprovedAt);

public sealed record RoleDto(string Code, string Name);

/// <summary>A user with their assignment in force today, if any.</summary>
public sealed record UserSummaryDto(Guid Id, string DisplayName, string Email, AppUserStatus Status, string? OfficeCode, bool ProvinceWide, IReadOnlyList<string> Roles,
    string? ReaLicenceNumber = null, DateOnly? ReaLicenceValidUntil = null);

/// <summary>
/// A user's Real Estate Appraiser licence, printed with their signature (LAM Bk I p.9; records-and-forms.md §4.1, Q10).
/// Both blank clears it. Changed with a reason (audited).
/// </summary>
public sealed record UpdateUserLicenceRequest(string? ReaLicenceNumber, DateOnly? ReaLicenceValidUntil, string Reason);

/// <summary>The signed-in user and their office scope.</summary>
public sealed record CurrentUserDto(
    Guid? UserId, string? DisplayName, Guid? OfficeId, string? OfficeCode, string? OfficeName, OfficeKind? OfficeKind,
    bool Assigned, bool ProvinceWide, IReadOnlyList<string> Roles, IReadOnlyList<Guid>? MunicipalityIds,
    /// <summary>What the user's roles allow (docs/analysis/workflow-security.md §4.1); the screens hide what is not allowed.</summary>
    IReadOnlyList<string>? Permissions = null,
    /// <summary>Pending until a SYSTEM_ADMIN approves the sign-up (workflow-security.md §4.2).</summary>
    AppUserStatus? Status = null,
    /// <summary>The user's roles need a second factor (Q7); <see cref="MfaSatisfied"/> says whether this sign-in has one.</summary>
    bool MfaRequired = false,
    bool MfaSatisfied = true,
    /// <summary>Minutes of inactivity before the browser signs out (Q9).</summary>
    int IdleMinutes = 0);

public sealed class CreateOfficeRequestValidator : AbstractValidator<CreateOfficeRequest>
{
    public CreateOfficeRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50).Matches("^[A-Z0-9][A-Z0-9_-]*$")
            .WithMessage("code is upper-case letters, digits, '-' or '_'.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.LguName).MaximumLength(200);
        RuleFor(x => x.SanggunianName).MaximumLength(200);
        RuleFor(x => x.HeadPosition).MaximumLength(200);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.Contact).MaximumLength(200);
    }
}

public sealed class UpdateOfficeRequestValidator : AbstractValidator<UpdateOfficeRequest>
{
    public UpdateOfficeRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LguName).MaximumLength(200);
        RuleFor(x => x.SanggunianName).MaximumLength(200);
        RuleFor(x => x.HeadPosition).MaximumLength(200);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.Contact).MaximumLength(200);
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class CreateOfficeJurisdictionRequestValidator : AbstractValidator<CreateOfficeJurisdictionRequest>
{
    public CreateOfficeJurisdictionRequestValidator()
    {
        RuleFor(x => x.EffectiveDate).NotEqual(default(DateOnly)).WithMessage("effectiveDate is required.");
        RuleFor(x => x.LegalBasis).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Remarks).MaximumLength(1000);
    }
}

public sealed class CreateOfficeAssignmentRequestValidator : AbstractValidator<CreateOfficeAssignmentRequest>
{
    public CreateOfficeAssignmentRequestValidator()
    {
        RuleFor(x => x.EffectiveDate).NotEqual(default(DateOnly)).WithMessage("effectiveDate is required.");
        RuleFor(x => x.LegalBasis).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Remarks).MaximumLength(1000);
        RuleFor(x => x.Roles).NotEmpty().WithMessage("At least one role is required.");
        RuleFor(x => x.Roles).Must(r => r.Distinct().Count() == r.Count).When(x => x.Roles is not null).WithMessage("Roles must not repeat.");
    }
}

public interface IOfficeService
{
    Task<Result<IReadOnlyList<OfficeDto>>> ListAsync(CancellationToken cancellationToken = default);
    Task<Result<OfficeDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<OfficeDto>> CreateAsync(CreateOfficeRequest request, CancellationToken cancellationToken = default);
    Task<Result<OfficeDto>> UpdateAsync(Guid id, UpdateOfficeRequest request, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<OfficeJurisdictionDto>>> ListJurisdictionsAsync(Guid? officeId, Guid? municipalityId, CancellationToken cancellationToken = default);
    Task<Result<OfficeJurisdictionDto>> CreateJurisdictionAsync(CreateOfficeJurisdictionRequest request, CancellationToken cancellationToken = default);
    Task<Result<OfficeJurisdictionDto>> ApproveJurisdictionAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<OfficeAssignmentDto>>> ListAssignmentsAsync(Guid? officeId, Guid? userId, CancellationToken cancellationToken = default);
    Task<Result<OfficeAssignmentDto>> CreateAssignmentAsync(CreateOfficeAssignmentRequest request, CancellationToken cancellationToken = default);
    Task<Result<OfficeAssignmentDto>> ApproveAssignmentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<OfficeAssignmentDto>> EndAssignmentAsync(Guid id, EndOfficeAssignmentRequest request, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<RoleDto>>> ListRolesAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<UserSummaryDto>>> ListUsersAsync(CancellationToken cancellationToken = default);
    Task<Result<UserSummaryDto>> UpdateUserLicenceAsync(Guid userId, UpdateUserLicenceRequest request, CancellationToken cancellationToken = default);
    Task<Result<CurrentUserDto>> GetCurrentAsync(CancellationToken cancellationToken = default);
}

/// <summary>The office and roles of an assignment, checked the same way wherever one is made (LP; sign-up approval, workflow-security.md §4.2).</summary>
internal static class AssignmentRules
{
    public static async Task<(List<Role> Roles, Result? Refused)> ResolveAsync(IApplicationDbContext db, Guid? officeId, IReadOnlyList<string> roleCodes,
        CancellationToken ct)
    {
        var roles = await db.Roles.Where(r => roleCodes.Contains(r.Code)).ToListAsync(ct);
        if (roleCodes.Except(roles.Select(r => r.Code)).ToList() is { Count: > 0 } unknown)
        {
            return (roles, Result.Failure("ROLE_NOT_FOUND", $"Unknown role(s): {string.Join(", ", unknown)}."));
        }
        if (officeId is null)
        {
            if (roleCodes.Any(r => !RoleCodes.ProvinceWide.Contains(r)))
            {
                return (roles, Result.Failure("ASSIGNMENT_OFFICE_REQUIRED",
                    $"Only {string.Join(" and ", RoleCodes.ProvinceWide.Order())} may be held province-wide; other roles are held within an office."));
            }
        }
        else if (await db.Offices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == officeId, ct) is not { Status: RecordStatus.Active })
        {
            return (roles, Result.Failure("OFFICE_NOT_FOUND", "No active office was found with the given id."));
        }
        return (roles, null);
    }
}

/// <summary>
/// Offices, their jurisdictions and user assignments
/// (docs/analysis/province-wide-operation.md §3.1–§3.2). Offices are plain
/// records: an office alone grants nothing. Jurisdictions and assignments
/// grant access, so they are effective-dated configuration: Draft, then
/// approved by a second user (CLAUDE.md §46), which ends the version before.
/// Nothing is deleted.
/// </summary>
public sealed class OfficeService(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IOfficeContext officeContext,
    Security.IPermissionService permissions,
    Prime.Application.Common.Security.SecuritySettings security,
    IClock clock,
    IValidator<CreateOfficeRequest> createValidator,
    IValidator<UpdateOfficeRequest> updateValidator,
    IValidator<CreateOfficeJurisdictionRequest> jurisdictionValidator,
    IValidator<CreateOfficeAssignmentRequest> assignmentValidator) : IOfficeService
{
    // --- Offices ---

    public async Task<Result<IReadOnlyList<OfficeDto>>> ListAsync(CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<OfficeDto>>((await db.Offices.AsNoTracking().OrderBy(x => x.Kind).ThenBy(x => x.Code)
            .ToListAsync(cancellationToken)).Select(ToDto).ToList());

    public async Task<Result<OfficeDto>> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.Offices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken) is { } office
            ? Result.Success(ToDto(office))
            : OfficeNotFound<OfficeDto>();

    public async Task<Result<OfficeDto>> CreateAsync(CreateOfficeRequest request, CancellationToken cancellationToken = default)
    {
        if (Invalid<OfficeDto>(await createValidator.ValidateAsync(request, cancellationToken)) is { } invalid)
        {
            return invalid;
        }
        if (await db.Offices.AnyAsync(x => x.Code == request.Code, cancellationToken))
        {
            return Result.Failure<OfficeDto>("OFFICE_CODE_DUPLICATE", $"An office with code {request.Code} already exists.");
        }
        if (request.Kind == OfficeKind.Provincial && await db.Offices.AnyAsync(x => x.Kind == OfficeKind.Provincial, cancellationToken))
        {
            return Result.Failure<OfficeDto>("OFFICE_PROVINCIAL_DUPLICATE", "The province already has its provincial office; there is exactly one.");
        }
        var office = new Office
        {
            Code = request.Code, Name = request.Name.Trim(), Kind = request.Kind,
            HeadPosition = Clean(request.HeadPosition), Address = Clean(request.Address), Contact = Clean(request.Contact),
            LguName = Clean(request.LguName), SanggunianName = Clean(request.SanggunianName),
        };
        db.Offices.Add(office);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(office));
    }

    public async Task<Result<OfficeDto>> UpdateAsync(Guid id, UpdateOfficeRequest request, CancellationToken cancellationToken = default)
    {
        if (Invalid<OfficeDto>(await updateValidator.ValidateAsync(request, cancellationToken)) is { } invalid)
        {
            return invalid;
        }
        var office = await db.Offices.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (office is null)
        {
            return OfficeNotFound<OfficeDto>();
        }
        if (request.Status != RecordStatus.Active && office.Status == RecordStatus.Active)
        {
            var today = clock.Today;
            if (await db.OfficeJurisdictions.InForce(today).AnyAsync(x => x.OfficeId == id, cancellationToken)
                || await db.OfficeAssignments.InForce(today).AnyAsync(x => x.OfficeId == id, cancellationToken))
            {
                return Result.Failure<OfficeDto>("OFFICE_IN_USE",
                    "The office still covers municipalities or has users assigned. End those first, then deactivate it.");
            }
        }
        office.Name = request.Name.Trim();
        office.HeadPosition = Clean(request.HeadPosition);
        office.Address = Clean(request.Address);
        office.Contact = Clean(request.Contact);
        office.LguName = Clean(request.LguName);
        office.SanggunianName = Clean(request.SanggunianName);
        office.Status = request.Status;
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(office));
    }

    // --- Jurisdictions ---

    public async Task<Result<IReadOnlyList<OfficeJurisdictionDto>>> ListJurisdictionsAsync(Guid? officeId, Guid? municipalityId,
        CancellationToken cancellationToken = default)
    {
        var query = db.OfficeJurisdictions.AsNoTracking().Include(x => x.Office).Include(x => x.Municipality).AsQueryable();
        if (officeId is not null)
        {
            query = query.Where(x => x.OfficeId == officeId);
        }
        if (municipalityId is not null)
        {
            query = query.Where(x => x.MunicipalityId == municipalityId);
        }
        var rows = await query.OrderBy(x => x.Municipality!.Name).ThenByDescending(x => x.EffectiveDate).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<OfficeJurisdictionDto>>(rows.Select(ToDto).ToList());
    }

    public async Task<Result<OfficeJurisdictionDto>> CreateJurisdictionAsync(CreateOfficeJurisdictionRequest request, CancellationToken cancellationToken = default)
    {
        if (Invalid<OfficeJurisdictionDto>(await jurisdictionValidator.ValidateAsync(request, cancellationToken)) is { } invalid)
        {
            return invalid;
        }
        var office = await db.Offices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.OfficeId, cancellationToken);
        if (office is null)
        {
            return OfficeNotFound<OfficeJurisdictionDto>();
        }
        if (office.Kind != OfficeKind.Municipal || office.Status != RecordStatus.Active)
        {
            return Result.Failure<OfficeJurisdictionDto>("OFFICE_NOT_MUNICIPAL",
                "Only an active municipal office is given municipalities; the provincial office covers the whole province.");
        }
        if (!await db.Municipalities.AnyAsync(x => x.Id == request.MunicipalityId, cancellationToken))
        {
            return Result.Failure<OfficeJurisdictionDto>("MUNICIPALITY_NOT_FOUND", "No city/municipality was found with the given id.");
        }
        var jurisdiction = new OfficeJurisdiction
        {
            OfficeId = request.OfficeId, MunicipalityId = request.MunicipalityId, EffectiveDate = request.EffectiveDate,
            LegalBasis = request.LegalBasis.Trim(), Remarks = Clean(request.Remarks),
        };
        db.OfficeJurisdictions.Add(jurisdiction);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(await JurisdictionDtoAsync(jurisdiction.Id, cancellationToken));
    }

    public async Task<Result<OfficeJurisdictionDto>> ApproveJurisdictionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var jurisdiction = await db.OfficeJurisdictions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (jurisdiction is null)
        {
            return Result.Failure<OfficeJurisdictionDto>("OFFICE_JURISDICTION_NOT_FOUND", "No office jurisdiction was found with the given id.");
        }
        // Scope: the municipality. Approving ends the office that covered it before.
        if (await ConfigurationApproval.ApproveAsync(db, currentUser, db.OfficeJurisdictions.Where(x => x.MunicipalityId == jurisdiction.MunicipalityId),
                jurisdiction, "OFFICE_JURISDICTION", cancellationToken) is { } failure)
        {
            return Result.Failure<OfficeJurisdictionDto>(failure.Code!, failure.Message!);
        }
        return Result.Success(await JurisdictionDtoAsync(id, cancellationToken));
    }

    // --- Assignments ---

    public async Task<Result<IReadOnlyList<OfficeAssignmentDto>>> ListAssignmentsAsync(Guid? officeId, Guid? userId, CancellationToken cancellationToken = default)
    {
        var query = AssignmentQuery();
        if (officeId is not null)
        {
            query = query.Where(x => x.OfficeId == officeId);
        }
        if (userId is not null)
        {
            query = query.Where(x => x.AppUserId == userId);
        }
        var rows = await query.OrderBy(x => x.AppUser!.DisplayName).ThenByDescending(x => x.EffectiveDate).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<OfficeAssignmentDto>>(rows.Select(ToDto).ToList());
    }

    public async Task<Result<OfficeAssignmentDto>> CreateAssignmentAsync(CreateOfficeAssignmentRequest request, CancellationToken cancellationToken = default)
    {
        if (Invalid<OfficeAssignmentDto>(await assignmentValidator.ValidateAsync(request, cancellationToken)) is { } invalid)
        {
            return invalid;
        }
        if (!await db.AppUsers.AnyAsync(x => x.Id == request.AppUserId, cancellationToken))
        {
            return Result.Failure<OfficeAssignmentDto>("APP_USER_NOT_FOUND", "No user was found with the given id.");
        }
        var (roles, refused) = await AssignmentRules.ResolveAsync(db, request.OfficeId, request.Roles, cancellationToken);
        if (refused is not null)
        {
            return Result.Failure<OfficeAssignmentDto>(refused.Code!, refused.Message!);
        }

        var assignment = new OfficeAssignment
        {
            AppUserId = request.AppUserId, OfficeId = request.OfficeId, EffectiveDate = request.EffectiveDate,
            LegalBasis = request.LegalBasis.Trim(), Remarks = Clean(request.Remarks),
            Roles = roles.Select(r => new OfficeAssignmentRole { RoleId = r.Id }).ToList(),
        };
        db.OfficeAssignments.Add(assignment);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(await AssignmentDtoAsync(assignment.Id, cancellationToken));
    }

    public async Task<Result<OfficeAssignmentDto>> ApproveAssignmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var assignment = await db.OfficeAssignments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (assignment is null)
        {
            return AssignmentNotFound();
        }
        if (MakerChecker.Refusal(currentUser, assignment.AppUserId, "CANNOT_APPROVE_OWN_OFFICE_ASSIGNMENT",
                "A user cannot approve their own office assignment (CLAUDE.md §46).") is { } refusal)
        {
            return Result.Failure<OfficeAssignmentDto>(refusal.Code, refusal.Message);
        }
        // Scope: the user. Approving ends their previous assignment.
        if (await ConfigurationApproval.ApproveAsync(db, currentUser, db.OfficeAssignments.Where(x => x.AppUserId == assignment.AppUserId),
                assignment, "OFFICE_ASSIGNMENT", cancellationToken) is { } failure)
        {
            return Result.Failure<OfficeAssignmentDto>(failure.Code!, failure.Message!);
        }
        return Result.Success(await AssignmentDtoAsync(id, cancellationToken));
    }

    /// <summary>Ends an approved assignment (the user leaves the office). Removing access needs no second user; it is audited with its reason.</summary>
    public async Task<Result<OfficeAssignmentDto>> EndAssignmentAsync(Guid id, EndOfficeAssignmentRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500)
        {
            return Result.Failure<OfficeAssignmentDto>("VALIDATION_FAILED", "A reason is required (at most 500 characters).");
        }
        var assignment = await db.OfficeAssignments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (assignment is null)
        {
            return AssignmentNotFound();
        }
        if (assignment.Status != WorkflowStatus.Approved || assignment.EndDate is not null)
        {
            return Result.Failure<OfficeAssignmentDto>("OFFICE_ASSIGNMENT_NOT_OPEN", "Only an approved assignment without an end date can be ended.");
        }
        if (request.EndDate < assignment.EffectiveDate)
        {
            return Result.Failure<OfficeAssignmentDto>("VALIDATION_FAILED", "The end date cannot be before the assignment started.");
        }
        assignment.EndDate = request.EndDate;
        currentUser.Reason = request.Reason.Trim();
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            currentUser.Reason = null;
        }
        return Result.Success(await AssignmentDtoAsync(id, cancellationToken));
    }

    // --- Users and roles ---

    public async Task<Result<IReadOnlyList<RoleDto>>> ListRolesAsync(CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<RoleDto>>((await db.Roles.AsNoTracking().OrderBy(r => r.Code).ToListAsync(cancellationToken))
            .Select(r => new RoleDto(r.Code, r.Name)).ToList());

    public async Task<Result<IReadOnlyList<UserSummaryDto>>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await db.AppUsers.AsNoTracking().OrderBy(u => u.DisplayName).ToListAsync(cancellationToken);
        var current = (await AssignmentQuery().InForce(clock.Today).ToListAsync(cancellationToken)).ToDictionary(a => a.AppUserId);
        return Result.Success<IReadOnlyList<UserSummaryDto>>(users.Select(u =>
        {
            var a = current.GetValueOrDefault(u.Id);
            return ToSummary(u, a);
        }).ToList());
    }

    public async Task<Result<UserSummaryDto>> UpdateUserLicenceAsync(Guid userId, UpdateUserLicenceRequest request, CancellationToken cancellationToken = default)
    {
        var number = Clean(request.ReaLicenceNumber);
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000 || number?.Length > 50
            || (number is null) != (request.ReaLicenceValidUntil is null))
        {
            return Result.Failure<UserSummaryDto>("VALIDATION_FAILED",
                "A reason (max 1000) is required; give the licence number (max 50) and its validity together, or neither.");
        }
        var user = await db.AppUsers.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<UserSummaryDto>("USER_NOT_FOUND", "No user was found with the given id.");
        }
        user.ReaLicenceNumber = number;
        user.ReaLicenceValidUntil = request.ReaLicenceValidUntil;
        currentUser.Reason = request.Reason.Trim();
        await db.SaveChangesAsync(cancellationToken);
        var assignment = await AssignmentQuery().InForce(clock.Today).FirstOrDefaultAsync(a => a.AppUserId == userId, cancellationToken);
        return Result.Success(ToSummary(user, assignment));
    }

    private static UserSummaryDto ToSummary(AppUser u, OfficeAssignment? a) => new(u.Id, u.DisplayName, u.Email, u.Status, a?.Office?.Code,
        a is not null && (a.Office is null || a.Office.Kind == OfficeKind.Provincial), RoleCodesOf(a), u.ReaLicenceNumber, u.ReaLicenceValidUntil);

    public async Task<Result<CurrentUserDto>> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var scope = await officeContext.GetAsync(cancellationToken);
        var user = scope.UserId is null ? null
            : await db.AppUsers.AsNoTracking().Where(u => u.Id == scope.UserId).Select(u => new { u.DisplayName, u.Status }).FirstOrDefaultAsync(cancellationToken);
        return Result.Success(new CurrentUserDto(scope.UserId, user?.DisplayName, scope.OfficeId, scope.OfficeCode, scope.OfficeName, scope.OfficeKind,
            scope.Assigned, scope.ProvinceWide, scope.Roles, scope.MunicipalityIds,
            (await permissions.GetAsync(cancellationToken)).Order(StringComparer.Ordinal).ToList(),
            user?.Status, security.RequiresMfa(scope.Roles), security.MfaSatisfied(scope.Roles, currentUser.AssuranceLevel), security.IdleMinutes));
    }

    // --- Helpers ---

    private IQueryable<OfficeAssignment> AssignmentQuery() => db.OfficeAssignments.AsNoTracking()
        .Include(x => x.AppUser).Include(x => x.Office).Include(x => x.Roles).ThenInclude(r => r.Role);

    private async Task<OfficeJurisdictionDto> JurisdictionDtoAsync(Guid id, CancellationToken ct) =>
        ToDto(await db.OfficeJurisdictions.AsNoTracking().Include(x => x.Office).Include(x => x.Municipality).SingleAsync(x => x.Id == id, ct));

    private async Task<OfficeAssignmentDto> AssignmentDtoAsync(Guid id, CancellationToken ct) =>
        ToDto(await AssignmentQuery().SingleAsync(x => x.Id == id, ct));

    private static IReadOnlyList<string> RoleCodesOf(OfficeAssignment? a) =>
        a?.Roles.Select(r => r.Role!.Code).Order(StringComparer.Ordinal).ToList() ?? [];

    private static OfficeDto ToDto(Office x) => new(x.Id, x.Code, x.Name, x.Kind, x.HeadPosition, x.Address, x.Contact, x.Status, x.LguName, x.SanggunianName);

    private static OfficeJurisdictionDto ToDto(OfficeJurisdiction x) => new(
        x.Id, x.OfficeId, x.Office!.Code, x.MunicipalityId, x.Municipality!.Name, x.Municipality.PsgcCode,
        x.EffectiveDate, x.EndDate, x.Status, x.LegalBasis, x.Remarks, x.CreatedBy, x.CreatedAt, x.ApprovedBy, x.ApprovedAt);

    private static OfficeAssignmentDto ToDto(OfficeAssignment x) => new(
        x.Id, x.AppUserId, x.AppUser!.DisplayName, x.OfficeId, x.Office?.Code, RoleCodesOf(x),
        x.EffectiveDate, x.EndDate, x.Status, x.LegalBasis, x.Remarks, x.CreatedBy, x.CreatedAt, x.ApprovedBy, x.ApprovedAt);

    private static Result<T>? Invalid<T>(FluentValidation.Results.ValidationResult validation) =>
        validation.IsValid ? null : Result.Failure<T>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

    private static Result<T> OfficeNotFound<T>() => Result.Failure<T>("OFFICE_NOT_FOUND", "No office was found with the given id.");

    private static Result<OfficeAssignmentDto> AssignmentNotFound() =>
        Result.Failure<OfficeAssignmentDto>("OFFICE_ASSIGNMENT_NOT_FOUND", "No office assignment was found with the given id.");

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
