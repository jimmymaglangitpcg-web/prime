using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.GeneralRevision;
using Prime.Application.Features.Smv;
using Prime.Application.Features.Valuation;
using Prime.Domain.Entities;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step L1-1 (docs/analysis/valuation-foundation.md §4.1): a unit is valued as of a date under
/// the rules in force then (CLAUDE.md §75 "a past date uses the rules then in force"), and a
/// general revision values as of its effectivity, under the new SMV even before it takes
/// effect. Two DEMO SMVs: 1,000/sqm from 2026, 1,500/sqm from the 2027 revision. Rolled back.
/// </summary>
public class ValuationDateTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly DateOnly Revision2027 = new(2027, 1, 1);

    private async Task<(IServiceProvider Services, PrimeDbContext Db, BillingFlowTests.Seed Seed, IAsyncDisposable Scope)> BeginAsync()
    {
        var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        // 500 sqm at the 2026 SMV's 1,000/sqm, level 20 % (open-ended), posted effective 2026-01-01.
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, new DateOnly(2026, 1, 1));
        var schedule = await db.SmvSchedules.AsNoTracking().SingleAsync(x => x.SmvId == seed.SmvId);

        var smv = scope.ServiceProvider.GetRequiredService<ISmvService>();
        var revision = (await smv.CreateSmvAsync(new CreateSmvRequest(
            $"DEMO-GR-{Guid.NewGuid():N}"[..20], new DateOnly(2026, 9, 1), null, Revision2027, 2027, "DEMO 2027 revision"))).Value;
        (await smv.ApproveSmvAsync(revision.Id)).IsSuccess.ShouldBeTrue();
        var rate = await smv.CreateScheduleAsync(revision.Id, new CreateSmvScheduleRequest(
            schedule.ClassificationId, schedule.ActualUseId, schedule.PropertyTypeId, null, "per sqm", 1_500m, null, null, Revision2027));
        rate.IsSuccess.ShouldBeTrue(rate.IsSuccess ? null : rate.Message);
        (await smv.ApproveScheduleAsync(rate.Value.Id)).IsSuccess.ShouldBeTrue();
        return (scope.ServiceProvider, db, seed, new Disposer(transaction, scope));
    }

    private sealed class Disposer(IAsyncDisposable transaction, IAsyncDisposable scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await scope.DisposeAsync();
        }
    }

    [Fact]
    public async Task A_unit_is_valued_under_the_rules_in_force_on_the_valuation_date()
    {
        var (services, _, seed, scope) = await BeginAsync();
        await using var _ = scope;
        var valuations = services.GetRequiredService<IValuationService>();

        var beforeResult = await valuations.ComputeForRpuAsync(seed.RpuId, asOf: new DateOnly(2026, 12, 31));
        beforeResult.IsSuccess.ShouldBeTrue(beforeResult.Message);
        var before = beforeResult.Value;
        (before.ComputedMarketValue, before.EffectiveDate, before.SmvId).ShouldBe((500_000m, new DateOnly(2026, 12, 31), (Guid?)seed.SmvId));

        var after = (await valuations.ComputeForRpuAsync(seed.RpuId, asOf: Revision2027)).Value;
        (after.ComputedMarketValue, after.EffectiveDate).ShouldBe((750_000m, Revision2027));
        after.SmvId.ShouldNotBe(seed.SmvId);

        // Before any SMV: nothing to value with, and the message names the date.
        var none = await valuations.ComputeForRpuAsync(seed.RpuId, asOf: new DateOnly(2025, 12, 31));
        none.Code.ShouldBe("SMV_SCHEDULE_NOT_FOUND");
        none.Message!.ShouldContain("2025-12-31");

        // Without a date: today, as before.
        var today = services.GetRequiredService<IClock>().Today;
        (await valuations.ComputeForRpuAsync(seed.RpuId)).Value.EffectiveDate.ShouldBe(today);
    }

    [Fact]
    public async Task A_general_revision_values_and_assesses_as_of_its_effectivity()
    {
        var (services, db, seed, scope) = await BeginAsync();
        await using var _ = scope;
        services.GetRequiredService<IClock>().Today.ShouldBeLessThan(Revision2027); // run before the new SMV takes effect

        var job = new GeneralRevisionJob { RevisionYear = 2027, EffectiveDate = Revision2027, TotalCount = 1, StartedAt = DateTimeOffset.UtcNow };
        db.GeneralRevisionJobs.Add(job);
        await db.SaveChangesAsync();
        await services.GetRequiredService<GeneralRevisionJobRunner>().RunAsync(job.Id, [seed.RpuId], 2027, CancellationToken.None);

        (await db.GeneralRevisionJobs.AsNoTracking().SingleAsync(x => x.Id == job.Id)).FailedCount.ShouldBe(0);
        var revised = await db.Assessments.AsNoTracking().Include(x => x.Valuation).SingleAsync(x => x.RevisionReference == job.Id);
        (revised.EffectiveDate, revised.MarketValue, revised.AssessedValue).ShouldBe((Revision2027, 750_000m, 150_000m));
        revised.Valuation!.EffectiveDate.ShouldBe(Revision2027);
        revised.PreviousAssessmentId.ShouldBe(seed.AssessmentId);
    }
}
