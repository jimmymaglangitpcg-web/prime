using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Common.Security;
using Prime.Application.Features.Security;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Entities.Offices;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Prime.WebApi.Authentication;
using Prime.WebApi.Authorization;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Phase 12 step P12-1 (docs/analysis/workflow-security.md §4.1): every API action declares a permission, each role holds
/// the provisional default grants, the API refuses what a user's roles do not allow, and the matrix changes only through
/// a second user's approval. DEMO users only; rolled back.
/// </summary>
public class PermissionTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public void Every_api_action_declares_a_known_permission()
    {
        var actions = factory.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>().ToList();
        actions.Count.ShouldBeGreaterThan(300);
        var known = Permissions.All.Select(p => p.Code).ToHashSet();
        var problems = new List<string>();
        foreach (var action in actions)
        {
            var name = $"{action.ControllerName}.{action.ActionName}";
            var declared = action.EndpointMetadata.OfType<RequirePermissionAttribute>().Select(a => a.Permission).ToList();
            var signedIn = action.EndpointMetadata.OfType<SignedInOnlyAttribute>().Any();
            if (declared.Contains(Permissions.Undeclared) || (declared.Count == 0 && !signedIn))
            {
                problems.Add($"{name}: no permission declared");
            }
            problems.AddRange(declared.Where(p => p != Permissions.Undeclared && !known.Contains(p)).Select(p => $"{name}: unknown permission {p}"));
        }
        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task Each_role_holds_its_provisional_default_grants()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var _ = await db.Database.BeginTransactionAsync();
        var currentUser = scope.ServiceProvider.GetRequiredService<CurrentUserService>();
        var provincial = await db.Offices.FirstAsync(o => o.Kind == OfficeKind.Provincial);
        foreach (var role in await db.Roles.ToListAsync())
        {
            var user = new AppUser { SupabaseUserId = Guid.NewGuid(), DisplayName = $"DEMO {role.Code}", Email = $"demo-{Guid.NewGuid():N}@example.invalid" };
            db.AppUsers.Add(user);
            db.OfficeAssignments.Add(new OfficeAssignment
            {
                AppUserId = user.Id, OfficeId = provincial.Id, EffectiveDate = new DateOnly(2020, 1, 1), LegalBasis = "DEMO", Status = WorkflowStatus.Approved,
                ApprovedAt = DateTimeOffset.UtcNow, Roles = [new OfficeAssignmentRole { RoleId = role.Id }],
            });
            await db.SaveChangesAsync();
            currentUser.AppUserId = user.Id;
            var held = await scope.ServiceProvider.GetRequiredService<IPermissionService>().GetAsync();
            held.Order().ShouldBe(Permissions.DefaultGrants[role.Code].Order(), $"role {role.Code}");
        }

        // Spot checks of the separation the matrix gives (Q2).
        Permissions.DefaultGrants[RoleCodes.ViewOnly].ShouldNotContain(Permissions.PropertyEdit);
        Permissions.DefaultGrants[RoleCodes.SystemAdmin].ShouldNotContain(Permissions.AssessmentApprove);
        Permissions.DefaultGrants[RoleCodes.AssessmentEncoder].ShouldContain(Permissions.TdPrepare);
        Permissions.DefaultGrants[RoleCodes.AssessmentEncoder].ShouldNotContain(Permissions.TdApprove);
        Permissions.DefaultGrants[RoleCodes.AssessmentReviewer].ShouldNotContain(Permissions.AssessmentPrepare);
        Permissions.DefaultGrants.Values.ShouldAllBe(grants => !grants.Contains(Permissions.TreasuryLegacy));

        // A user without an office assignment holds nothing.
        var nobody = new AppUser { SupabaseUserId = Guid.NewGuid(), DisplayName = "DEMO unassigned", Email = $"demo-{Guid.NewGuid():N}@example.invalid" };
        db.AppUsers.Add(nobody);
        await db.SaveChangesAsync();
        currentUser.AppUserId = nobody.Id;
        (await scope.ServiceProvider.GetRequiredService<IPermissionService>().GetAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_api_refuses_what_the_users_roles_do_not_allow()
    {
        var client = factory.CreateClient();
        HttpRequestMessage Approve(string? actAs)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/assessments/{Guid.NewGuid()}/approve");
            if (actAs is not null)
            {
                request.Headers.Add(DevelopmentAuthenticationHandler.ActAsHeader, actAs);
            }
            return request;
        }

        // A DEMO appraiser may not approve an assessment: refused before the service, in the §63 shape.
        var refused = await client.SendAsync(Approve("mun-appraiser"));
        refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var error = await refused.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonObject>();
        (error!["code"]!.GetValue<string>(), error["message"]!.GetValue<string>()).ShouldBe(("PERMISSION_DENIED",
            "You do not have the permission this action needs: assessment.approve."));

        // The usual dev user prepares but does not approve; the checker (an assessor) passes the gate and reaches the service.
        (await client.SendAsync(Approve(null))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.SendAsync(Approve("checker"))).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Who am I needs no permission and lists the user's.
        var me = await client.GetFromJsonAsync<System.Text.Json.Nodes.JsonObject>("/api/me");
        me!["permissions"]!.AsArray().Select(p => p!.GetValue<string>()).ShouldContain(Permissions.PropertyEdit);
    }

    [Fact]
    public async Task The_matrix_changes_only_when_a_second_user_approves()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var _ = await db.Database.BeginTransactionAsync();
        var currentUser = scope.ServiceProvider.GetRequiredService<CurrentUserService>();
        var users = Enumerable.Range(0, 2).Select(i => new AppUser
        {
            SupabaseUserId = Guid.NewGuid(), DisplayName = $"DEMO admin {i}", Email = $"demo-{Guid.NewGuid():N}@example.invalid",
        }).ToList();
        db.AppUsers.AddRange(users);
        await db.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<IRolePermissionService>();
        var viewOnly = Permissions.DefaultGrants[RoleCodes.ViewOnly];

        currentUser.AppUserId = users[0].Id;
        (await service.ProposeAsync(new ProposeRolePermissionsRequest(RoleCodes.ViewOnly, viewOnly, "DEMO"))).Code.ShouldBe("ROLE_PERMISSION_UNCHANGED");
        (await service.ProposeAsync(new ProposeRolePermissionsRequest(RoleCodes.ViewOnly, [.. viewOnly, "no.such"], "DEMO"))).Code.ShouldBe("PERMISSION_NOT_FOUND");
        (await service.ProposeAsync(new ProposeRolePermissionsRequest(RoleCodes.ViewOnly, viewOnly, " "))).Code.ShouldBe("VALIDATION_FAILED");

        // VIEW_ONLY gains audit.view and loses market.view.
        var proposed = await service.ProposeAsync(new ProposeRolePermissionsRequest(RoleCodes.ViewOnly,
            [.. viewOnly.Where(p => p != Permissions.MarketView), Permissions.AuditView], "DEMO change"));
        proposed.IsSuccess.ShouldBeTrue(proposed.Message);
        proposed.Value.Grant.ShouldBe([Permissions.AuditView]);
        proposed.Value.Revoke.ShouldBe([Permissions.MarketView]);
        (await service.ProposeAsync(new ProposeRolePermissionsRequest(RoleCodes.ViewOnly, viewOnly.Take(2).ToList(), "DEMO"))).Code
            .ShouldBe("ROLE_PERMISSION_CHANGE_PENDING");

        // Nothing changes while it waits; its author cannot approve it.
        async Task<List<string>> Grants() => await db.RolePermissions.AsNoTracking().Where(rp => rp.Role!.Code == RoleCodes.ViewOnly)
            .Select(rp => rp.Permission!.Code).OrderBy(c => c).ToListAsync();
        (await Grants()).ShouldBe(viewOnly.Order().ToList());
        (await service.ApproveAsync(proposed.Value.Id)).Code.ShouldBe("CANNOT_APPROVE_OWN_ROLE_PERMISSION_CHANGE");

        currentUser.AppUserId = users[1].Id;
        (await service.RejectAsync(proposed.Value.Id, new DecideRolePermissionChangeRequest(null))).Code.ShouldBe("VALIDATION_FAILED");
        var approved = await service.ApproveAsync(proposed.Value.Id);
        approved.IsSuccess.ShouldBeTrue(approved.Message);
        (approved.Value.Status, approved.Value.DecidedBy).ShouldBe((WorkflowStatus.Approved, (Guid?)users[1].Id));
        (await Grants()).ShouldBe(viewOnly.Where(p => p != Permissions.MarketView).Append(Permissions.AuditView).Order().ToList());
        (await service.ApproveAsync(proposed.Value.Id)).Code.ShouldBe("ROLE_PERMISSION_CHANGE_DECIDED");

        // The seeder never overrides an approved change: restarting it leaves the grants as they are.
        await new PermissionCatalogSeeder(scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PermissionCatalogSeeder>.Instance).StartAsync(CancellationToken.None);
        (await Grants()).ShouldContain(Permissions.AuditView);
    }
}
