using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Common.Security;
using Prime.Application.Features.Security;
using Prime.Application.Features.Users;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Entities.Offices;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Prime.WebApi.Authentication;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Phase 12 step P12-2 (docs/analysis/workflow-security.md §4.2): a new sign-in is pending and may only ask for an
/// account; a SYSTEM_ADMIN approves it with an office and roles, in force at once, or rejects it; a user is disabled and
/// enabled only through a second user's approval and a disabled user is refused; MFA-required roles need aal2; sign-in
/// events are audited; the first administrator is made by the bootstrap command only. DEMO users; service tests rolled back.
/// </summary>
public class UserAccountTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ApplicantSubject = "00000000-0000-0000-0000-0000000000a1";
    private const string DisabledSubject = "00000000-0000-0000-0000-0000000000a2";

    private sealed record Users(AppUser Applicant, AppUser Admin, AppUser OtherAdmin, Office Office);

    private static async Task<Users> SeedAsync(PrimeDbContext db)
    {
        var office = await db.Offices.FirstAsync(o => o.Kind == OfficeKind.Provincial && o.Status == RecordStatus.Active);
        var adminRole = await db.Roles.FirstAsync(r => r.Code == RoleCodes.SystemAdmin);
        AppUser NewUser(string name, AppUserStatus status) => new()
        {
            SupabaseUserId = Guid.NewGuid(), DisplayName = name, Email = $"demo-{Guid.NewGuid():N}@example.invalid", Status = status,
        };
        var applicant = NewUser("demo applicant", AppUserStatus.Pending);
        var admin = NewUser("DEMO admin A", AppUserStatus.Active);
        var other = NewUser("DEMO admin B", AppUserStatus.Active);
        db.AppUsers.AddRange(applicant, admin, other);
        foreach (var user in new[] { admin, other })
        {
            db.OfficeAssignments.Add(new OfficeAssignment
            {
                AppUserId = user.Id, OfficeId = null, EffectiveDate = new DateOnly(2020, 1, 1), LegalBasis = "DEMO", Status = WorkflowStatus.Approved,
                ApprovedAt = DateTimeOffset.UtcNow, Roles = [new OfficeAssignmentRole { RoleId = adminRole.Id }],
            });
        }
        await db.SaveChangesAsync();
        return new Users(applicant, admin, other, office);
    }

    [Fact]
    public async Task A_pending_user_asks_and_a_system_admin_approves_with_office_and_roles_in_force_at_once()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var _ = await db.Database.BeginTransactionAsync();
        var currentUser = scope.ServiceProvider.GetRequiredService<CurrentUserService>();
        var service = scope.ServiceProvider.GetRequiredService<IUserAccountService>();
        var users = await SeedAsync(db);

        // Pending: no permissions, even if an assignment existed.
        currentUser.AppUserId = users.Applicant.Id;
        (await scope.ServiceProvider.GetRequiredService<IPermissionService>().GetAsync()).ShouldBeEmpty();

        // The request: validated like an assignment; one open at a time.
        (await service.SubmitSignUpAsync(new SubmitSignUpRequest(" ", "Clerk", users.Office.Id, [RoleCodes.Appraiser], null))).Code.ShouldBe("VALIDATION_FAILED");
        (await service.SubmitSignUpAsync(new SubmitSignUpRequest("DEMO Applicant", "Clerk", null, [RoleCodes.Appraiser], null))).Code
            .ShouldBe("ASSIGNMENT_OFFICE_REQUIRED");
        (await service.SubmitSignUpAsync(new SubmitSignUpRequest("DEMO Applicant", "Clerk", users.Office.Id, ["NO_SUCH"], null))).Code.ShouldBe("ROLE_NOT_FOUND");
        var asked = await service.SubmitSignUpAsync(new SubmitSignUpRequest("DEMO Applicant", "Assessment Clerk", users.Office.Id,
            [RoleCodes.Appraiser, RoleCodes.AssessmentReviewer], "DEMO"));
        asked.IsSuccess.ShouldBeTrue(asked.Message);
        asked.Value.Status.ShouldBe(WorkflowStatus.PendingReview);
        (await service.SubmitSignUpAsync(new SubmitSignUpRequest("DEMO Applicant", "Clerk", users.Office.Id, [RoleCodes.Appraiser], null))).Code
            .ShouldBe("SIGN_UP_REQUEST_PENDING");
        (await service.ListMySignUpRequestsAsync()).Value.Select(r => r.Id).ShouldBe([asked.Value.Id]);

        // The applicant cannot decide it; nor can an unknown user.
        var approve = new ApproveSignUpRequest(users.Office.Id, [RoleCodes.Appraiser], "DEMO");
        (await service.ApproveSignUpAsync(asked.Value.Id, approve)).Code.ShouldBe("CANNOT_DECIDE_OWN_SIGN_UP");
        currentUser.AppUserId = null;
        (await service.ApproveSignUpAsync(asked.Value.Id, approve)).Code.ShouldBe("DECIDING_USER_UNKNOWN");

        // The administrator gives fewer roles than asked: the account is active with them at once.
        currentUser.AppUserId = users.Admin.Id;
        (await service.ApproveSignUpAsync(asked.Value.Id, approve with { Roles = [] })).Code.ShouldBe("VALIDATION_FAILED");
        var approved = await service.ApproveSignUpAsync(asked.Value.Id, approve);
        approved.IsSuccess.ShouldBeTrue(approved.Message);
        approved.Value.Status.ShouldBe(WorkflowStatus.Approved);
        approved.Value.DecidedBy.ShouldBe(users.Admin.Id);
        var user = await db.AppUsers.AsNoTracking().SingleAsync(u => u.Id == users.Applicant.Id);
        (user.Status, user.DisplayName).ShouldBe((AppUserStatus.Active, "DEMO Applicant"));
        var assignment = await db.OfficeAssignments.AsNoTracking().Include(a => a.Roles).ThenInclude(r => r.Role)
            .SingleAsync(a => a.Id == approved.Value.OfficeAssignmentId);
        (assignment.Status, assignment.OfficeId, assignment.CreatedBy, assignment.ApprovedBy).ShouldBe(
            (WorkflowStatus.Approved, (Guid?)users.Office.Id, (Guid?)users.Applicant.Id, (Guid?)users.Admin.Id));
        assignment.EffectiveDate.ShouldBe(scope.ServiceProvider.GetRequiredService<Prime.Application.Common.Interfaces.IClock>().Today);
        assignment.Roles.Select(r => r.Role!.Code).ShouldBe([RoleCodes.Appraiser]);

        // Resolved afresh (the per-request cache still holds the applicant's empty set from before).
        var permissions = scope.ServiceProvider.GetRequiredService<IPermissionService>();
        await permissions.GetAsync();
        currentUser.AppUserId = users.Applicant.Id;
        (await permissions.GetAsync()).Order().ShouldBe(Permissions.DefaultGrants[RoleCodes.Appraiser].Order());

        // Decided once.
        currentUser.AppUserId = users.OtherAdmin.Id;
        (await service.ApproveSignUpAsync(asked.Value.Id, approve)).Code.ShouldBe("SIGN_UP_REQUEST_NOT_OPEN");
        (await service.SubmitSignUpAsync(new SubmitSignUpRequest("x", "y", users.Office.Id, [RoleCodes.Appraiser], null))).Code.ShouldBe("SIGN_UP_NOT_PENDING");
    }

    [Fact]
    public async Task A_rejected_user_stays_pending_and_may_ask_again()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var _ = await db.Database.BeginTransactionAsync();
        var currentUser = scope.ServiceProvider.GetRequiredService<CurrentUserService>();
        var service = scope.ServiceProvider.GetRequiredService<IUserAccountService>();
        var users = await SeedAsync(db);

        currentUser.AppUserId = users.Applicant.Id;
        var asked = await service.SubmitSignUpAsync(new SubmitSignUpRequest("DEMO Applicant", "Clerk", users.Office.Id, [RoleCodes.ViewOnly], null));
        currentUser.AppUserId = users.Admin.Id;
        (await service.RejectSignUpAsync(asked.Value.Id, new DecisionReasonRequest(" "))).Code.ShouldBe("VALIDATION_FAILED");
        var rejected = await service.RejectSignUpAsync(asked.Value.Id, new DecisionReasonRequest("DEMO: not an employee of the office"));
        rejected.Value.Status.ShouldBe(WorkflowStatus.Rejected);
        rejected.Value.DecisionReason.ShouldBe("DEMO: not an employee of the office");
        (await db.AppUsers.AsNoTracking().SingleAsync(u => u.Id == users.Applicant.Id)).Status.ShouldBe(AppUserStatus.Pending);

        currentUser.AppUserId = users.Applicant.Id;
        (await service.SubmitSignUpAsync(new SubmitSignUpRequest("DEMO Applicant", "Clerk", users.Office.Id, [RoleCodes.ViewOnly], "again")))
            .IsSuccess.ShouldBeTrue();
        (await service.ListMySignUpRequestsAsync()).Value.Select(r => r.Status).ShouldBe([WorkflowStatus.PendingReview, WorkflowStatus.Rejected]);
    }

    [Fact]
    public async Task Disabling_a_user_needs_a_second_user_and_takes_effect_on_approval()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var _ = await db.Database.BeginTransactionAsync();
        var currentUser = scope.ServiceProvider.GetRequiredService<CurrentUserService>();
        var service = scope.ServiceProvider.GetRequiredService<IUserAccountService>();
        var users = await SeedAsync(db);
        var target = users.OtherAdmin.Id;

        currentUser.AppUserId = users.Admin.Id;
        var disable = new ProposeUserStatusRequest(AppUserStatus.Inactive, "DEMO: left the office");
        (await service.ProposeStatusChangeAsync(users.Admin.Id, disable)).Code.ShouldBe("CANNOT_CHANGE_OWN_STATUS");
        (await service.ProposeStatusChangeAsync(users.Applicant.Id, disable)).Code.ShouldBe("USER_PENDING");
        (await service.ProposeStatusChangeAsync(target, disable with { NewStatus = AppUserStatus.Active })).Code.ShouldBe("USER_STATUS_UNCHANGED");
        (await service.ProposeStatusChangeAsync(target, disable with { NewStatus = AppUserStatus.Pending })).Code.ShouldBe("VALIDATION_FAILED");
        var proposed = await service.ProposeStatusChangeAsync(target, disable);
        proposed.IsSuccess.ShouldBeTrue(proposed.Message);
        (await service.ProposeStatusChangeAsync(target, disable)).Code.ShouldBe("USER_STATUS_CHANGE_PENDING");

        // The author and the user concerned cannot approve it; until approved nothing changes.
        (await service.ApproveStatusChangeAsync(proposed.Value.Id)).Code.ShouldBe("CANNOT_APPROVE_OWN_USER_STATUS_CHANGE");
        currentUser.AppUserId = target;
        (await service.ApproveStatusChangeAsync(proposed.Value.Id)).Code.ShouldBe("CANNOT_APPROVE_OWN_USER_STATUS_CHANGE");
        (await db.AppUsers.AsNoTracking().SingleAsync(u => u.Id == target)).Status.ShouldBe(AppUserStatus.Active);

        // A third user approves: disabled at once, and it holds no permissions.
        var third = new AppUser { SupabaseUserId = Guid.NewGuid(), DisplayName = "DEMO admin C", Email = $"demo-{Guid.NewGuid():N}@example.invalid" };
        db.AppUsers.Add(third);
        await db.SaveChangesAsync();
        currentUser.AppUserId = third.Id;
        var approved = await service.ApproveStatusChangeAsync(proposed.Value.Id);
        approved.IsSuccess.ShouldBeTrue(approved.Message);
        (approved.Value.Status, approved.Value.CreatedBy, approved.Value.DecidedBy).ShouldBe((WorkflowStatus.Approved, (Guid?)users.Admin.Id, (Guid?)third.Id));
        (await db.AppUsers.AsNoTracking().SingleAsync(u => u.Id == target)).Status.ShouldBe(AppUserStatus.Inactive);
        currentUser.AppUserId = target;
        (await scope.ServiceProvider.GetRequiredService<IPermissionService>().GetAsync()).ShouldBeEmpty();

        // Enabling again goes the same way; a rejection needs a reason and changes nothing.
        currentUser.AppUserId = users.Admin.Id;
        var enable = await service.ProposeStatusChangeAsync(target, new ProposeUserStatusRequest(AppUserStatus.Active, "DEMO: returned"));
        currentUser.AppUserId = third.Id;
        (await service.RejectStatusChangeAsync(enable.Value.Id, new DecisionReasonRequest(null))).Code.ShouldBe("VALIDATION_FAILED");
        (await service.RejectStatusChangeAsync(enable.Value.Id, new DecisionReasonRequest("DEMO: not yet"))).Value.Status.ShouldBe(WorkflowStatus.Rejected);
        (await db.AppUsers.AsNoTracking().SingleAsync(u => u.Id == target)).Status.ShouldBe(AppUserStatus.Inactive);
        (await service.ListStatusChangesAsync(target)).Value.Count.ShouldBe(2);
    }

    [Fact]
    public async Task The_bootstrap_makes_the_first_admin_only_while_there_is_none()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var _ = await db.Database.BeginTransactionAsync();
        var service = scope.ServiceProvider.GetRequiredService<IUserAccountService>();
        var users = await SeedAsync(db);

        (await service.BootstrapFirstAdminAsync(users.Applicant.Email)).Code.ShouldBe("BOOTSTRAP_NOT_NEEDED");

        // A new installation: no SYSTEM_ADMIN in force (simulated inside the rolled-back transaction).
        var yesterday = scope.ServiceProvider.GetRequiredService<Prime.Application.Common.Interfaces.IClock>().Today.AddDays(-1);
        await db.OfficeAssignments.Where(a => a.Status == WorkflowStatus.Approved && a.Roles.Any(r => r.Role!.Code == RoleCodes.SystemAdmin))
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, WorkflowStatus.Rejected).SetProperty(a => a.ApprovedAt, (DateTimeOffset?)null));
        (await service.BootstrapFirstAdminAsync("nobody@example.invalid")).Code.ShouldBe("BOOTSTRAP_USER_NOT_FOUND");
        var made = await service.BootstrapFirstAdminAsync(users.Applicant.Email.ToUpperInvariant());
        made.IsSuccess.ShouldBeTrue(made.Message);
        (await db.AppUsers.AsNoTracking().SingleAsync(u => u.Id == users.Applicant.Id)).Status.ShouldBe(AppUserStatus.Active);
        var assignment = await db.OfficeAssignments.AsNoTracking().Include(a => a.Roles).ThenInclude(r => r.Role)
            .SingleAsync(a => a.AppUserId == users.Applicant.Id);
        (assignment.OfficeId, assignment.Status, assignment.LegalBasis).ShouldBe(((Guid?)null, WorkflowStatus.Approved, UserAccountService.BootstrapLegalBasis));
        assignment.Roles.Select(r => r.Role!.Code).ShouldBe([RoleCodes.SystemAdmin]);
        assignment.EffectiveDate.ShouldBeGreaterThan(yesterday);
        (await service.BootstrapFirstAdminAsync(users.Admin.Email)).Code.ShouldBe("BOOTSTRAP_NOT_NEEDED");
    }

    [Fact]
    public async Task Over_http_a_pending_user_sees_only_their_sign_up_and_a_disabled_user_is_refused()
    {
        var host = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DevAuth:Users:0:Key", "test-applicant");
            builder.UseSetting("DevAuth:Users:0:UserId", ApplicantSubject);
            builder.UseSetting("DevAuth:Users:0:DisplayName", "DEMO test applicant");
            builder.UseSetting("DevAuth:Users:0:Office", "none");
            builder.UseSetting("DevAuth:Users:1:Key", "test-disabled");
            builder.UseSetting("DevAuth:Users:1:UserId", DisabledSubject);
            builder.UseSetting("DevAuth:Users:1:DisplayName", "DEMO test disabled");
            builder.UseSetting("DevAuth:Users:1:Office", "none");
        });
        // Two fixed DEMO users, put back in the state the test needs on every run.
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
            foreach (var (subject, name, status) in new[] { (ApplicantSubject, "DEMO test applicant", AppUserStatus.Pending), (DisabledSubject, "DEMO test disabled", AppUserStatus.Inactive) })
            {
                var id = Guid.Parse(subject);
                var user = await db.AppUsers.FirstOrDefaultAsync(u => u.SupabaseUserId == id);
                if (user is null)
                {
                    db.AppUsers.Add(new AppUser { SupabaseUserId = id, DisplayName = name, Email = string.Empty, Status = status });
                }
                else
                {
                    user.Status = status;
                }
            }
            await db.SaveChangesAsync();
        }
        var client = host.CreateClient();
        HttpRequestMessage As(HttpMethod method, string key, string path) =>
            new(method, path) { Headers = { { DevelopmentAuthenticationHandler.ActAsHeader, key } } };

        var me = await (await client.SendAsync(As(HttpMethod.Get, "test-applicant", "/api/me"))).Content.ReadFromJsonAsync<JsonObject>();
        me!["status"]!.GetValue<string>().ShouldBe("Pending");
        me["permissions"]!.AsArray().ShouldBeEmpty();
        (await client.SendAsync(As(HttpMethod.Get, "test-applicant", "/api/sign-up/options"))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.SendAsync(As(HttpMethod.Get, "test-applicant", "/api/sign-up/mine"))).StatusCode.ShouldBe(HttpStatusCode.OK);
        var refused = await client.SendAsync(As(HttpMethod.Get, "test-applicant", "/api/properties"));
        refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await refused.Content.ReadFromJsonAsync<JsonObject>())!["code"]!.GetValue<string>().ShouldBe("PERMISSION_DENIED");

        var disabled = await client.SendAsync(As(HttpMethod.Get, "test-disabled", "/api/me"));
        disabled.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await disabled.Content.ReadFromJsonAsync<JsonObject>())!["code"]!.GetValue<string>().ShouldBe("USER_DISABLED");
    }

    [Fact]
    public async Task An_assessor_without_a_second_factor_is_refused_but_may_still_ask_who_they_are()
    {
        var client = factory.CreateClient();
        HttpRequestMessage AsChecker(string path, string aal) => new(HttpMethod.Get, path)
        {
            Headers = { { DevelopmentAuthenticationHandler.ActAsHeader, "checker" }, { DevelopmentAuthenticationHandler.AssuranceLevelHeader, aal } },
        };

        var refused = await client.SendAsync(AsChecker("/api/offices", "aal1"));
        refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await refused.Content.ReadFromJsonAsync<JsonObject>())!["code"]!.GetValue<string>().ShouldBe("MFA_REQUIRED");
        var me = await (await client.SendAsync(AsChecker("/api/me", "aal1"))).Content.ReadFromJsonAsync<JsonObject>();
        (me!["mfaRequired"]!.GetValue<bool>(), me["mfaSatisfied"]!.GetValue<bool>()).ShouldBe((true, false));
        me["idleMinutes"]!.GetValue<int>().ShouldBeGreaterThan(0);

        (await client.SendAsync(AsChecker("/api/offices", "aal2"))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Sign_in_and_sign_out_are_audited_as_login_and_logout()
    {
        var client = factory.CreateClient();
        var since = DateTimeOffset.UtcNow.AddSeconds(-5);
        (await client.PostAsJsonAsync("/api/session", new { @event = "SignIn" })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/session", new { @event = "SignOut", reason = "idle" })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/session", new { @event = "Nonsense" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var devUser = await db.AppUsers.SingleAsync(u => u.SupabaseUserId == Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var rows = await db.AuditLogs.AsNoTracking().Where(a => a.UserId == devUser.Id && a.Timestamp >= since && a.TableName == "AppUsers")
            .OrderBy(a => a.Timestamp).ToListAsync();
        rows.Select(a => (a.Action, a.Reason)).ShouldContain((AuditAction.Login, (string?)null));
        rows.Select(a => (a.Action, a.Reason)).ShouldContain((AuditAction.Logout, (string?)"idle"));
    }
}
