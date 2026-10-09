using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Prime.Application.Common.Security;
using Prime.Application.Features.Audit;
using Prime.Application.Features.Registers;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Entities.Offices;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Prime.WebApi.Authentication;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Phase 12 step P12-3 (docs/analysis/workflow-security.md §4.3): status changes are audited as the action they are,
/// plain child rows are audited with their parent, a register run and a browser print are audited as EXPORT, the
/// viewer is gated by <c>audit.view</c> and shows a record's history with its children, and the database refuses to
/// change or delete an audit row. DEMO data; service tests rolled back. The HTTP tests add DEMO audit rows and a DEMO
/// register run to the development database (audit rows cannot be removed).
/// </summary>
public class AuditTrailTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task A_status_change_is_audited_as_its_action_and_a_child_row_with_its_parent()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var _ = await db.Database.BeginTransactionAsync();
        var role = await db.Roles.FirstAsync(r => r.Code == RoleCodes.ViewOnly);
        var user = new AppUser { SupabaseUserId = Guid.NewGuid(), DisplayName = "DEMO audited user", Email = $"demo-{Guid.NewGuid():N}@example.invalid" };
        db.AppUsers.Add(user);
        var assignment = new OfficeAssignment
        {
            AppUserId = user.Id, EffectiveDate = new DateOnly(2020, 1, 1), LegalBasis = "DEMO", Status = WorkflowStatus.PendingReview,
        };
        db.OfficeAssignments.Add(assignment);
        await db.SaveChangesAsync();

        // A child row, plain Entity: audited with its parent's table and id.
        var child = new OfficeAssignmentRole { OfficeAssignmentId = assignment.Id, RoleId = role.Id };
        db.OfficeAssignmentRoles.Add(child);
        await db.SaveChangesAsync();
        var childRow = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.RecordId == child.Id);
        (childRow.Action, childRow.TableName, childRow.ParentTableName, childRow.ParentRecordId)
            .ShouldBe((AuditAction.Create, "OfficeAssignmentRoles", "OfficeAssignments", (Guid?)assignment.Id));

        // Approval and cancellation are named; a status that is no decision stays UPDATE.
        async Task<AuditAction> SetStatus(WorkflowStatus status)
        {
            assignment.Status = status;
            assignment.ApprovedAt = status == WorkflowStatus.Approved ? DateTimeOffset.UtcNow : null; // CK_..._Approval
            await db.SaveChangesAsync();
            return (await db.AuditLogs.AsNoTracking().Where(a => a.RecordId == assignment.Id).OrderByDescending(a => a.Timestamp).FirstAsync()).Action;
        }
        (await SetStatus(WorkflowStatus.Submitted)).ShouldBe(AuditAction.Update);
        (await SetStatus(WorkflowStatus.Approved)).ShouldBe(AuditAction.Approve);
        // Values are readable: an enum by its name, not its number.
        var approveRow = await db.AuditLogs.AsNoTracking().Where(a => a.RecordId == assignment.Id && a.Action == AuditAction.Approve).SingleAsync();
        (JsonNode.Parse(approveRow.OldValue!)!["Status"]!.GetValue<string>(), JsonNode.Parse(approveRow.NewValue!)!["Status"]!.GetValue<string>())
            .ShouldBe(("Submitted", "Approved"));
        (await SetStatus(WorkflowStatus.Cancelled)).ShouldBe(AuditAction.Cancel);
        assignment.LegalBasis = "DEMO amended";
        await db.SaveChangesAsync();
        (await db.AuditLogs.AsNoTracking().Where(a => a.RecordId == assignment.Id).OrderByDescending(a => a.Timestamp).FirstAsync()).Action
            .ShouldBe(AuditAction.Update);

        // Removing the child is a DELETE row under the parent; the record's history includes it.
        db.OfficeAssignmentRoles.Remove(child);
        await db.SaveChangesAsync();
        var history = await scope.ServiceProvider.GetRequiredService<IAuditTrailService>()
            .ListAsync(new AuditLogQuery { RecordId = assignment.Id, IncludeChildren = true, PageSize = 100 });
        history.IsSuccess.ShouldBeTrue(history.Message);
        history.Value.Items.Where(a => a.RecordId == child.Id).Select(a => a.Action).OrderBy(a => a).ShouldBe([AuditAction.Create, AuditAction.Delete]);
        history.Value.Items.Count(a => a.RecordId == assignment.Id).ShouldBe(5);
        (await scope.ServiceProvider.GetRequiredService<IAuditTrailService>().ListAsync(new AuditLogQuery { RecordId = assignment.Id }))
            .Value.Items.ShouldAllBe(a => a.RecordId == assignment.Id);
    }

    [Fact]
    public async Task The_database_refuses_to_change_or_delete_an_audit_row()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        foreach (var sql in new[]
                 {
                     """UPDATE "AuditLogs" SET "Reason" = 'tampered' WHERE "Id" = (SELECT "Id" FROM "AuditLogs" LIMIT 1)""",
                     """DELETE FROM "AuditLogs" WHERE "Id" = (SELECT "Id" FROM "AuditLogs" LIMIT 1)""",
                     """TRUNCATE "AuditLogs" """,
                 })
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var error = await Should.ThrowAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql));
            error.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
            error.MessageText.ShouldContain("AuditLogs is append-only");
        }
    }

    [Fact]
    public async Task A_register_run_and_a_print_are_exports_and_only_audit_view_reads_the_trail()
    {
        var client = factory.CreateClient();
        Guid barangayId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            barangayId = await scope.ServiceProvider.GetRequiredService<PrimeDbContext>().Barangays.Select(b => b.Id).FirstAsync();
        }

        // A register run made by the server: CREATE by the interceptor, EXPORT by the action.
        var run = await client.PostAsJsonAsync("/api/registers",
            new CreateRegisterRunRequest(RegisterKind.TaxMapControlRoll, DateOnly.FromDateTime(DateTime.Today), barangayId, null, null, null, "DEMO P12-3 test"));
        run.StatusCode.ShouldBe(HttpStatusCode.OK, await run.Content.ReadAsStringAsync());
        var runId = (await run.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<Guid>();
        var runRows = await client.GetFromJsonAsync<JsonObject>($"/api/audit-logs?recordId={runId}");
        runRows!["items"]!.AsArray().Select(r => r!["action"]!.GetValue<string>()).OrderBy(a => a).ShouldBe(["Create", "Export"]);

        // A print made in the browser, reported by the page.
        var id = Guid.NewGuid();
        (await client.PostAsJsonAsync("/api/audit/exports", new RecordExportRequest("IssuedForms", id, "DEMO print", "Print"))).StatusCode
            .ShouldBe(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/audit/exports", new RecordExportRequest("IssuedForms", id, "DEMO print", "Fax"))).StatusCode
            .ShouldBe(HttpStatusCode.BadRequest);
        var printed = await client.GetFromJsonAsync<JsonObject>($"/api/audit-logs?recordId={id}&action=Export");
        var row = printed!["items"]!.AsArray().ShouldHaveSingleItem()!;
        (row["tableName"]!.GetValue<string>(), row["module"]!.GetValue<string>()).ShouldBe(("IssuedForms", AuditTrailService.ExportModule));
        row["userName"]!.GetValue<string>().ShouldNotBeNullOrEmpty();
        JsonNode.Parse(row["newValue"]!.GetValue<string>())!["Format"]!.GetValue<string>().ShouldBe("Print");

        // Only holders of audit.view read the trail; anyone signed in may report a print.
        var asAppraiser = new HttpRequestMessage(HttpMethod.Get, "/api/audit-logs");
        asAppraiser.Headers.Add(DevelopmentAuthenticationHandler.ActAsHeader, "mun-appraiser");
        var refused = await client.SendAsync(asAppraiser);
        refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await refused.Content.ReadFromJsonAsync<JsonObject>())!["message"]!.GetValue<string>().ShouldContain(Permissions.AuditView);
        (await client.GetFromJsonAsync<List<string>>("/api/audit-logs/tables"))!.ShouldContain("RegisterRuns");
    }

    /// <summary>A broad filter is counted only up to the cap and says there are more (production-hardening.md §9, H4); rolled back.</summary>
    [Fact]
    public async Task A_broad_list_counts_up_to_the_cap_and_says_there_are_more()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var _ = await db.Database.BeginTransactionAsync();
        var module = $"DEMO-{Guid.NewGuid():N}"[..20];
        async Task AddRows(int count) => await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "AuditLogs" ("Id", "Module", "TableName", "RecordId", "Action", "Timestamp")
            SELECT gen_random_uuid(), {module}, 'DEMO', gen_random_uuid(), 'Create', now() - make_interval(secs => g) FROM generate_series(1, {count}) g
            """);
        var audit = scope.ServiceProvider.GetRequiredService<IAuditTrailService>();

        await AddRows(Prime.Application.Common.CappedCount.Limit);
        var exact = (await audit.ListAsync(new AuditLogQuery { Module = module, PageSize = 50 })).Value;
        (exact.TotalCount, exact.TotalIsLowerBound).ShouldBe((Prime.Application.Common.CappedCount.Limit, false));

        await AddRows(1);
        var capped = (await audit.ListAsync(new AuditLogQuery { Module = module, PageSize = 50 })).Value;
        (capped.TotalCount, capped.TotalIsLowerBound, capped.Items.Count).ShouldBe((Prime.Application.Common.CappedCount.Limit, true, 50));
        // A page past the cap is served as the last countable one.
        var deep = (await audit.ListAsync(new AuditLogQuery { Module = module, PageSize = 50, Page = 1_000 })).Value;
        deep.Page.ShouldBe(Prime.Application.Common.CappedCount.Limit / 50 + 1);
    }
}
