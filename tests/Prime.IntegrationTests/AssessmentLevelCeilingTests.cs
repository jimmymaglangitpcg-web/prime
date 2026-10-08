using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.AssessmentLevels;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Entities.Reference;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step L3-2 (docs/analysis/assessment-listing-exemptions.md §4.2, Q6): statutory maximum assessment levels as
/// approved configuration; a level above the ceiling in force for its keys is refused at creation and approval.
/// Every value here is DEMO data, not a statutory maximum. Rolled back.
/// </summary>
public class AssessmentLevelCeilingTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly DateOnly Jan1 = new(2026, 1, 1);

    private sealed record Ctx(IServiceProvider Services, CurrentUserService User, AppUser Maker, AppUser Checker,
        Guid LandTypeId, Guid ClassId, Guid OtherClassId, Guid UseId)
    {
        public IAssessmentLevelCeilingService Ceilings => Services.GetRequiredService<IAssessmentLevelCeilingService>();
        public IAssessmentLevelService Levels => Services.GetRequiredService<IAssessmentLevelService>();
    }

    private async Task<(Ctx C, IAsyncDisposable Scope)> BeginAsync()
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        var users = Enumerable.Range(0, 2).Select(i => new AppUser
        {
            SupabaseUserId = Guid.NewGuid(), DisplayName = $"DEMO User {(char)('A' + i)}", Email = $"demo-{Guid.NewGuid():N}@example.invalid",
        }).ToList();
        var cls = new Classification { Code = $"CL{Guid.NewGuid():N}"[..8], Name = "DEMO_Ceiling class" };
        var other = new Classification { Code = $"CL{Guid.NewGuid():N}"[..8], Name = "DEMO_Other class" };
        var use = new ActualUse { Code = $"AU{Guid.NewGuid():N}"[..8], Name = "DEMO_Ceiling use" };
        db.AppUsers.AddRange(users);
        db.AddRange(cls, other, use);
        await db.SaveChangesAsync();
        var user = scope.ServiceProvider.GetRequiredService<CurrentUserService>();
        user.AppUserId = null;
        var land = await TestSeed.LandPropertyTypeAsync(db);
        return (new Ctx(scope.ServiceProvider, user, users[0], users[1], land.Id, cls.Id, other.Id, use.Id), new Scoped(transaction, scope));
    }

    private sealed class Scoped(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    private static async Task<AssessmentLevelCeilingDto> CeilingAsync(Ctx c, decimal max, decimal lower = 0m, decimal? upper = null, Guid? classId = null,
        DateOnly? effective = null)
    {
        c.User.AppUserId = c.Maker.Id;
        var created = await c.Ceilings.CreateAsync(new CreateAssessmentLevelCeilingRequest("DEMO basis — cites no law", effective ?? Jan1, null,
            $"DEMO-CEIL-{Guid.NewGuid():N}"[..20], null, c.LandTypeId, classId ?? c.ClassId, null, lower, upper, max));
        created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);
        (await c.Ceilings.ApproveAsync(created.Value.Id)).Code.ShouldBe("CANNOT_APPROVE_OWN_ASSESSMENT_LEVEL_CEILING");
        c.User.AppUserId = null;
        (await c.Ceilings.ApproveAsync(created.Value.Id)).Code.ShouldBe("APPROVING_USER_UNKNOWN");
        c.User.AppUserId = c.Checker.Id;
        var approved = await c.Ceilings.ApproveAsync(created.Value.Id);
        approved.IsSuccess.ShouldBeTrue(approved.IsSuccess ? null : approved.Message);
        return approved.Value;
    }

    /// <summary>A draft level made by the maker; the checker is left acting, so the test can approve it.</summary>
    private static async Task<Application.Common.Result<AssessmentLevelDto>> LevelAsync(Ctx c, decimal percent, decimal lower = 0m, decimal? upper = null,
        Guid? classId = null, DateOnly? effective = null)
    {
        c.User.AppUserId = c.Maker.Id;
        var created = await c.Levels.CreateAsync(new CreateAssessmentLevelRequest($"DEMO-ORD-{Guid.NewGuid():N}"[..20], null, classId ?? c.ClassId, c.UseId,
            c.LandTypeId, lower, upper, percent, effective ?? new DateOnly(2026, 7, 1)));
        c.User.AppUserId = c.Checker.Id;
        return created;
    }

    [Fact]
    public async Task ALevelAboveTheCeilingInForce_IsRefused_WithItsLegalBasis()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var ceiling = await CeilingAsync(c, 20m);

        var above = await LevelAsync(c, 25m);
        above.Code.ShouldBe("ASSESSMENT_LEVEL_ABOVE_CEILING");
        above.Message!.ShouldContain("above the maximum of 20%");
        above.Message.ShouldContain($"DEMO basis — cites no law (ceiling {ceiling.Code}");

        // A level taking effect before the ceiling, another classification: not covered.
        (await LevelAsync(c, 25m, effective: new DateOnly(2025, 6, 1))).IsSuccess.ShouldBeTrue();
        (await LevelAsync(c, 25m, classId: c.OtherClassId)).IsSuccess.ShouldBeTrue();
        (await LevelAsync(c, 20m)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task OnlyTheBracketsThatOverlap_AreChecked()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        await CeilingAsync(c, 10m, lower: 0m, upper: 100_000m);

        (await LevelAsync(c, 30m, lower: 100_000m)).IsSuccess.ShouldBeTrue(); // over 100,000: outside the ceiling's bracket
        (await LevelAsync(c, 30m, lower: 50_000m, upper: 200_000m)).Code.ShouldBe("ASSESSMENT_LEVEL_ABOVE_CEILING");
    }

    [Fact]
    public async Task ACeilingApprovedAfterTheDraft_BlocksItsApproval_AndWarnsOfApprovedLevelsAboveIt()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var approvedLevel = await LevelAsync(c, 30m, lower: 0m, upper: 100_000m);
        (await c.Levels.ApproveAsync(approvedLevel.Value.Id)).IsSuccess.ShouldBeTrue();
        var draft = await LevelAsync(c, 30m, lower: 100_000m);

        var ceiling = await CeilingAsync(c, 20m);

        ceiling.Warning!.ShouldStartWith("1 approved assessment level(s) in force from");
        ceiling.Warning.ShouldContain(approvedLevel.Value.OrdinanceNumber);
        (await c.Levels.ApproveAsync(draft.Value.Id)).Code.ShouldBe("ASSESSMENT_LEVEL_ABOVE_CEILING");
        (await c.Ceilings.ListAsync(inForceOnly: true)).Value.ShouldContain(x => x.Id == ceiling.Id);
    }
}
