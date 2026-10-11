using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.LevyRates;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Phase 11 step R4a (docs/analysis/reporting.md §10, Q5, Q18): levy rates as effective-dated configuration under
/// maker-checker, per municipality or province-wide, optionally per classification. Every rate here is DEMO data, not an
/// ordinance's. Rolled back.
/// </summary>
public class LevyRateTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly DateOnly Jan1 = new(2026, 1, 1);

    private sealed record Ctx(IServiceProvider Services, CurrentUserService User, AppUser Maker, AppUser Checker, Guid MunicipalityId, Guid ClassId)
    {
        public ILevyRateService Rates => Services.GetRequiredService<ILevyRateService>();
    }

    private async Task<(Ctx C, IAsyncDisposable Scope)> BeginAsync()
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        var users = Enumerable.Range(0, 2).Select(i => new AppUser
        {
            SupabaseUserId = Guid.NewGuid(), DisplayName = $"DEMO Levy User {(char)('A' + i)}", Email = $"demo-{Guid.NewGuid():N}@example.invalid",
        }).ToList();
        var province = await db.Provinces.Select(p => p.Id).FirstAsync();
        var municipality = new Municipality { ProvinceId = province, PsgcCode = $"M{Guid.NewGuid():N}"[..10], Name = "DEMO Levy Municipality" };
        var cls = new Classification { Code = $"CL{Guid.NewGuid():N}"[..8], Name = "DEMO_Levy class" };
        db.AppUsers.AddRange(users);
        db.AddRange(municipality, cls);
        await db.SaveChangesAsync();
        var user = scope.ServiceProvider.GetRequiredService<CurrentUserService>();
        return (new Ctx(scope.ServiceProvider, user, users[0], users[1], municipality.Id, cls.Id), new Scoped(transaction, scope));
    }

    private sealed class Scoped(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    private static async Task<LevyRateDto> RateAsync(Ctx c, LevyKind kind, decimal percent, Guid? municipalityId, Guid? classId = null, DateOnly? effective = null)
    {
        c.User.AppUserId = c.Maker.Id;
        var created = await c.Rates.CreateAsync(new CreateLevyRateRequest(kind, municipalityId, classId, percent, "DEMO ordinance — not an LGU's",
            effective ?? Jan1, null, null));
        created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);
        created.Value.Status.ShouldBe(WorkflowStatus.Draft);
        c.User.AppUserId = c.Checker.Id;
        var approved = await c.Rates.ApproveAsync(created.Value.Id);
        approved.IsSuccess.ShouldBeTrue(approved.IsSuccess ? null : approved.Message);
        return approved.Value;
    }

    [Fact]
    public async Task ARate_IsApprovedByASecondUser_AndOnlyThenInForce()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        c.User.AppUserId = c.Maker.Id;
        var draft = await c.Rates.CreateAsync(new CreateLevyRateRequest(LevyKind.Basic, c.MunicipalityId, null, 1.25m, "DEMO ordinance", Jan1, null, null));
        draft.IsSuccess.ShouldBeTrue(draft.IsSuccess ? null : draft.Message);

        (await c.Rates.InForceAsync(Jan1)).Find(LevyKind.Basic, c.MunicipalityId, null).ShouldBeNull();
        (await c.Rates.ApproveAsync(draft.Value.Id)).Code.ShouldBe("CANNOT_APPROVE_OWN_LEVY_RATE");
        c.User.AppUserId = c.Checker.Id;
        (await c.Rates.ApproveAsync(draft.Value.Id)).IsSuccess.ShouldBeTrue();

        var found = (await c.Rates.InForceAsync(Jan1)).Find(LevyKind.Basic, c.MunicipalityId, null).ShouldNotBeNull();
        found.RatePercent.ShouldBe(1.25m);
        (await c.Rates.InForceAsync(Jan1.AddDays(-1))).Find(LevyKind.Basic, c.MunicipalityId, null).ShouldBeNull();
    }

    [Fact]
    public async Task ANewRateForTheSameKeys_TakesOverFromItsDate_AndTheOldOneStaysInHistory()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var old = await RateAsync(c, LevyKind.SpecialEducationFund, 1m, c.MunicipalityId);
        await RateAsync(c, LevyKind.SpecialEducationFund, 0.75m, c.MunicipalityId, effective: new DateOnly(2027, 1, 1));

        (await c.Rates.InForceAsync(new DateOnly(2026, 12, 31))).Find(LevyKind.SpecialEducationFund, c.MunicipalityId, null)!.RatePercent.ShouldBe(1m);
        (await c.Rates.InForceAsync(new DateOnly(2027, 1, 1))).Find(LevyKind.SpecialEducationFund, c.MunicipalityId, null)!.RatePercent.ShouldBe(0.75m);
        var history = (await c.Rates.ListAsync(inForceOnly: false)).Value.Where(r => r.Code == old.Code).ToList();
        history.Count.ShouldBe(2);
        history.Single(r => r.Id == old.Id).EndDate.ShouldBe(new DateOnly(2026, 12, 31));
    }

    [Fact]
    public async Task TheMostSpecificRate_Applies_MunicipalityAndClassFirst_ThenTheProvince()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var province = await RateAsync(c, LevyKind.IdleLand, 5m, null);
        await RateAsync(c, LevyKind.IdleLand, 4m, c.MunicipalityId);
        await RateAsync(c, LevyKind.IdleLand, 3m, c.MunicipalityId, c.ClassId);

        var table = await c.Rates.InForceAsync(Jan1);
        table.Find(LevyKind.IdleLand, c.MunicipalityId, c.ClassId)!.RatePercent.ShouldBe(3m);
        table.Find(LevyKind.IdleLand, c.MunicipalityId, Guid.NewGuid())!.RatePercent.ShouldBe(4m);
        table.Find(LevyKind.IdleLand, Guid.NewGuid(), c.ClassId)!.Id.ShouldBe(province.Id);
        table.Find(LevyKind.Basic, c.MunicipalityId, c.ClassId).ShouldBeNull();
    }

    [Fact]
    public async Task AMunicipalOffice_CannotSetAProvinceWideRate_OrAnotherMunicipalitys()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        c.Services.GetRequiredService<JurisdictionState>().Restrict([Guid.NewGuid()]);
        c.User.AppUserId = c.Maker.Id;

        (await c.Rates.CreateAsync(new CreateLevyRateRequest(LevyKind.Basic, null, null, 1m, "DEMO", Jan1, null, null))).Code.ShouldBe("JURISDICTION_FORBIDDEN");
        (await c.Rates.CreateAsync(new CreateLevyRateRequest(LevyKind.Basic, c.MunicipalityId, null, 1m, "DEMO", Jan1, null, null))).Code
            .ShouldBe("JURISDICTION_FORBIDDEN");
        (await c.Rates.CreateAsync(new CreateLevyRateRequest(LevyKind.Basic, c.MunicipalityId, null, 0m, "DEMO", Jan1, null, null))).Code
            .ShouldBe("VALIDATION_FAILED");
    }
}
