using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Offices;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step LP-1 (docs/analysis/province-wide-operation.md §3.1–§3.2): offices,
/// jurisdictions and office assignments, with maker-checker, and the office
/// scope a request sees. Everything but the DEMO seeding is rolled back.
/// </summary>
public class OfficeTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(IServiceProvider Services, PrimeDbContext Db, CurrentUserService User, AppUser Maker, AppUser Checker, AppUser Staff)
    {
        public IOfficeService Offices => Services.GetRequiredService<IOfficeService>();
        public IOfficeContext Scope => Services.GetRequiredService<IOfficeContext>();
    }

    private async Task<(Ctx C, IAsyncDisposable Scope)> BeginAsync()
    {
        var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        var users = new[] { "DEMO Maker", "DEMO Checker", "DEMO Staff" }.Select(n => new AppUser
        {
            SupabaseUserId = Guid.NewGuid(), DisplayName = n, Email = $"demo-{Guid.NewGuid():N}@example.invalid",
        }).ToList();
        db.AppUsers.AddRange(users);
        await db.SaveChangesAsync();
        var user = scope.ServiceProvider.GetRequiredService<CurrentUserService>();
        user.AppUserId = users[0].Id;
        return (new Ctx(scope.ServiceProvider, db, user, users[0], users[1], users[2]), new Disposer(transaction, scope));
    }

    private sealed class Disposer(IAsyncDisposable transaction, IAsyncDisposable scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await scope.DisposeAsync();
        }
    }

    private static async Task<Municipality> MunicipalityAsync(PrimeDbContext db, string name)
    {
        var tag = Random.Shared.Next(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var province = new Province { PsgcCode = $"93{tag}", Name = $"DEMO LP Province {tag}" };
        var municipality = new Municipality { Province = province, PsgcCode = $"93{tag[..6]}01", Name = $"DEMO LP {name} {tag}" };
        db.Municipalities.Add(municipality);
        await db.SaveChangesAsync();
        return municipality;
    }

    private static string Code() => $"T-{Guid.NewGuid():N}"[..14].ToUpperInvariant();

    [Fact]
    public async Task Offices_HaveUniqueCodes_AndTheProvinceHasOneProvincialOffice()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;

        var code = Code();
        var office = (await c.Offices.CreateAsync(new(code, "DEMO LP Office", OfficeKind.Municipal, "DEMO Municipal Assessor", null, null))).Value;
        (office.Code, office.Kind, office.Status).ShouldBe((code, OfficeKind.Municipal, RecordStatus.Active));
        (await c.Offices.CreateAsync(new(code, "Again", OfficeKind.Municipal, null, null, null))).Code.ShouldBe("OFFICE_CODE_DUPLICATE");
        (await c.Offices.CreateAsync(new("lower case", "Bad", OfficeKind.Municipal, null, null, null))).Code.ShouldBe("VALIDATION_FAILED");

        // The DEMO seeder (or a real set-up) already created the provincial office.
        (await c.Db.Offices.CountAsync(o => o.Kind == OfficeKind.Provincial)).ShouldBe(1);
        (await c.Offices.CreateAsync(new(Code(), "Second province", OfficeKind.Provincial, null, null, null))).Code.ShouldBe("OFFICE_PROVINCIAL_DUPLICATE");

        var updated = (await c.Offices.UpdateAsync(office.Id, new("DEMO LP Office renamed", null, "DEMO address", null, RecordStatus.Active))).Value;
        (updated.Name, updated.Code, updated.Address).ShouldBe(("DEMO LP Office renamed", code, "DEMO address"));
    }

    [Fact]
    public async Task Jurisdiction_NeedsASecondUser_AndANewOfficeForAMunicipalityEndsThePrevious()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var town = await MunicipalityAsync(c.Db, "Town");
        var first = (await c.Offices.CreateAsync(new(Code(), "DEMO LP First", OfficeKind.Municipal, null, null, null))).Value;
        var second = (await c.Offices.CreateAsync(new(Code(), "DEMO LP Second", OfficeKind.Municipal, null, null, null))).Value;
        var provincial = await c.Db.Offices.SingleAsync(o => o.Kind == OfficeKind.Provincial);

        (await c.Offices.CreateJurisdictionAsync(new(provincial.Id, town.Id, new DateOnly(2026, 1, 1), "DEMO", null))).Code.ShouldBe("OFFICE_NOT_MUNICIPAL");

        var draft = (await c.Offices.CreateJurisdictionAsync(new(first.Id, town.Id, new DateOnly(2026, 1, 1), "DEMO order 1", null))).Value;
        draft.Status.ShouldBe(WorkflowStatus.Draft);
        (await c.Offices.ApproveJurisdictionAsync(draft.Id)).Code.ShouldBe("CANNOT_APPROVE_OWN_OFFICE_JURISDICTION");
        c.User.AppUserId = c.Checker.Id;
        (await c.Offices.ApproveJurisdictionAsync(draft.Id)).Value.Status.ShouldBe(WorkflowStatus.Approved);

        c.User.AppUserId = c.Maker.Id;
        var move = (await c.Offices.CreateJurisdictionAsync(new(second.Id, town.Id, new DateOnly(2026, 7, 1), "DEMO order 2", null))).Value;
        c.User.AppUserId = c.Checker.Id;
        (await c.Offices.ApproveJurisdictionAsync(move.Id)).IsSuccess.ShouldBeTrue();

        var history = (await c.Offices.ListJurisdictionsAsync(null, town.Id)).Value;
        history.Select(h => (h.OfficeId, h.EffectiveDate, h.EndDate)).ShouldBe([
            (second.Id, new DateOnly(2026, 7, 1), (DateOnly?)null),
            (first.Id, new DateOnly(2026, 1, 1), (DateOnly?)new DateOnly(2026, 6, 30)),
        ]);

        // The first office no longer covers anything today, so it may be deactivated; the second may not.
        (await c.Offices.UpdateAsync(second.Id, new("DEMO LP Second", null, null, null, RecordStatus.Inactive))).Code.ShouldBe("OFFICE_IN_USE");
        (await c.Offices.UpdateAsync(first.Id, new("DEMO LP First", null, null, null, RecordStatus.Inactive))).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Assignment_GivesTheUserAnOfficeScope_AndOnlySomeRolesAreProvinceWide()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var town = await MunicipalityAsync(c.Db, "Scope");
        var office = (await c.Offices.CreateAsync(new(Code(), "DEMO LP Scope Office", OfficeKind.Municipal, null, null, null))).Value;
        var jurisdiction = (await c.Offices.CreateJurisdictionAsync(new(office.Id, town.Id, new DateOnly(2026, 1, 1), "DEMO", null))).Value;
        c.User.AppUserId = c.Checker.Id;
        await c.Offices.ApproveJurisdictionAsync(jurisdiction.Id);

        c.User.AppUserId = c.Maker.Id;
        (await c.Offices.CreateAssignmentAsync(new(c.Staff.Id, null, [RoleCodes.Assessor], new DateOnly(2026, 1, 1), "DEMO", null)))
            .Code.ShouldBe("ASSIGNMENT_OFFICE_REQUIRED");
        (await c.Offices.CreateAssignmentAsync(new(c.Staff.Id, office.Id, ["NO_SUCH_ROLE"], new DateOnly(2026, 1, 1), "DEMO", null)))
            .Code.ShouldBe("ROLE_NOT_FOUND");
        var assignment = (await c.Offices.CreateAssignmentAsync(new(c.Staff.Id, office.Id, [RoleCodes.Appraiser, RoleCodes.AssessmentEncoder],
            new DateOnly(2026, 1, 1), "DEMO designation", null))).Value;

        // Before approval the user has no office and sees nothing.
        c.User.AppUserId = c.Staff.Id;
        var before = await OfficeContext.LoadAsync(c.Db, c.Staff.Id, new DateOnly(2026, 9, 29), default);
        (before.Assigned, before.ProvinceWide, before.MunicipalityIds!.Count).ShouldBe((false, false, 0));
        (await c.Offices.ApproveAssignmentAsync(assignment.Id)).Code.ShouldBe("CANNOT_APPROVE_OWN_OFFICE_ASSIGNMENT");
        c.User.AppUserId = c.Maker.Id;
        (await c.Offices.ApproveAssignmentAsync(assignment.Id)).Code.ShouldBe("CANNOT_APPROVE_OWN_OFFICE_ASSIGNMENT");
        c.User.AppUserId = c.Checker.Id;
        (await c.Offices.ApproveAssignmentAsync(assignment.Id)).Value.Status.ShouldBe(WorkflowStatus.Approved);

        c.User.AppUserId = c.Staff.Id;
        var me = (await c.Offices.GetCurrentAsync()).Value;
        (me.OfficeId, me.OfficeKind, me.Assigned, me.ProvinceWide).ShouldBe((office.Id, OfficeKind.Municipal, true, false));
        me.Roles.ShouldBe([RoleCodes.Appraiser, RoleCodes.AssessmentEncoder]);
        me.MunicipalityIds.ShouldBe([town.Id]);

        // Ending the assignment removes the scope from the day after.
        c.User.AppUserId = c.Maker.Id;
        (await c.Offices.EndAssignmentAsync(assignment.Id, new(new DateOnly(2026, 6, 30), "DEMO transfer"))).Value.EndDate.ShouldBe(new DateOnly(2026, 6, 30));
        (await OfficeContext.LoadAsync(c.Db, c.Staff.Id, new DateOnly(2026, 9, 29), default)).Assigned.ShouldBeFalse();
        (await OfficeContext.LoadAsync(c.Db, c.Staff.Id, new DateOnly(2026, 6, 30), default)).OfficeId.ShouldBe(office.Id);

        // A province-wide auditor sees the whole province.
        var auditor = (await c.Offices.CreateAssignmentAsync(new(c.Staff.Id, null, [RoleCodes.Auditor], new DateOnly(2026, 7, 1), "DEMO", null))).Value;
        c.User.AppUserId = c.Checker.Id;
        await c.Offices.ApproveAssignmentAsync(auditor.Id);
        var wide = await OfficeContext.LoadAsync(c.Db, c.Staff.Id, new DateOnly(2026, 9, 29), default);
        (wide.Assigned, wide.ProvinceWide, wide.OfficeId).ShouldBe((true, true, (Guid?)null));
    }

    [Fact]
    public async Task DevUsers_ActAsTheirDemoOffice_ThroughTheHeader()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Prime-Dev-Act-As", "mun-appraiser");
        var me = await client.GetFromJsonAsync<JsonElement>("/api/me");
        me.GetProperty("officeKind").GetString().ShouldBe("Municipal");
        me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ShouldBe([RoleCodes.Appraiser]);
        me.GetProperty("municipalityIds").GetArrayLength().ShouldBeGreaterThan(0);

        var admin = factory.CreateClient();
        var adminMe = await admin.GetFromJsonAsync<JsonElement>("/api/me");
        adminMe.GetProperty("provinceWide").GetBoolean().ShouldBeTrue();

        var devUsers = await admin.GetFromJsonAsync<JsonElement>("/api/dev/users");
        devUsers.EnumerateArray().Select(u => u.GetProperty("key").GetString()).ShouldContain("mun-assessor");
    }
}
