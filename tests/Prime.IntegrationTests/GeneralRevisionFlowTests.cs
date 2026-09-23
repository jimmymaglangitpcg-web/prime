using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.GeneralRevision;
using Prime.Application.Features.Smv;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Phase 6 exit criteria: a general revision job can run against a batch of
/// demo properties without blocking a browser request; history is
/// preserved. Calls <see cref="GeneralRevisionJobRunner.RunAsync"/> directly
/// rather than through the real Hangfire enqueue/worker pipeline — that
/// pipeline is a well-tested third-party concern, not something this
/// codebase needs to re-verify (see the class's own doc comment).
/// </summary>
public class GeneralRevisionFlowTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static async Task<(PrimeDbContext Db, IServiceProvider Services, IAsyncDisposable Transaction)> BeginTestScopeAsync(WebApplicationFactory<Program> factory)
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        return (db, scope.ServiceProvider, new ScopedTransaction(transaction, scope));
    }

    private sealed class ScopedTransaction(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    private static async Task<Guid> SeedLandRpuAsync(IServiceProvider services, PrimeDbContext db, Guid classificationId, Guid actualUseId, decimal area)
    {
        var province = new Province { PsgcCode = $"P{Guid.NewGuid():N}"[..10], Name = "Demo Province" };
        var municipality = new Municipality { Province = province, PsgcCode = $"M{Guid.NewGuid():N}"[..10], Name = "Demo Municipality" };
        var barangay = new Barangay { Municipality = municipality, PsgcCode = $"B{Guid.NewGuid():N}"[..10], Name = "Demo Barangay" };
        db.AddRange(province, municipality, barangay);

        var property = new PropertyEntity
        {
            PropertyIdentificationNumber = $"PIN-{Guid.NewGuid():N}",
            Province = province,
            Municipality = municipality,
            Barangay = barangay,
        };
        db.Properties.Add(property);

        var rpu = new RealPropertyUnit
        {
            Property = property,
            RpuNumber = $"RPU-{Guid.NewGuid():N}",
            RpuType = RpuType.Land,
            EffectivityDate = DateOnly.FromDateTime(DateTime.UtcNow),
        };
        db.RealPropertyUnits.Add(rpu);
        await db.SaveChangesAsync();

        var land = new Land { Rpu = rpu, Property = property, Area = area, ClassificationId = classificationId, ActualUseId = actualUseId };
        db.Lands.Add(land);
        await db.SaveChangesAsync();

        return rpu.Id;
    }

    [Fact]
    public async Task RunAsync_ProcessesBatchOfRpus_ProducesDraftAssessmentsTaggedWithJob()
    {
        var (db, services, transaction) = await BeginTestScopeAsync(factory);
        await using var _ = transaction;

        var classification = new Classification { Code = $"CL{Guid.NewGuid():N}"[..8], Name = "DEMO_Residential" };
        var actualUse = new ActualUse { Code = $"AU{Guid.NewGuid():N}"[..8], Name = "DEMO_Residential Use" };
        var propertyType = new PropertyType { Code = "LAND", Name = "DEMO_Land" };
        db.AddRange(classification, actualUse, propertyType);
        await db.SaveChangesAsync();

        var smvService = services.GetRequiredService<ISmvService>();
        var smv = (await smvService.CreateSmvAsync(new CreateSmvRequest(
            $"ORD-{Guid.NewGuid():N}", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 1), 2026, "DEMO_SMV for GeneralRevisionFlowTests"))).Value;
        await smvService.ApproveSmvAsync(smv.Id);
        var schedule = (await smvService.CreateScheduleAsync(smv.Id, new CreateSmvScheduleRequest(
            classification.Id, actualUse.Id, propertyType.Id, null, "per sqm", 1000m, null, null, new DateOnly(2026, 1, 1)))).Value;
        await smvService.ApproveScheduleAsync(schedule.Id);

        var assessmentLevelService = services.GetRequiredService<Application.Features.AssessmentLevels.IAssessmentLevelService>();
        var assessmentLevel = (await assessmentLevelService.CreateAsync(new Application.Features.AssessmentLevels.CreateAssessmentLevelRequest(
            $"ORD-{Guid.NewGuid():N}", new DateOnly(2026, 1, 1), classification.Id, actualUse.Id, propertyType.Id,
            0m, null, 20m, new DateOnly(2026, 1, 1)))).Value;
        await assessmentLevelService.ApproveAsync(assessmentLevel.Id);

        var rpuId1 = await SeedLandRpuAsync(services, db, classification.Id, actualUse.Id, area: 100m);
        var rpuId2 = await SeedLandRpuAsync(services, db, classification.Id, actualUse.Id, area: 200m);

        var startedBy = Guid.NewGuid();

        // The job row is created directly here rather than through
        // IGeneralRevisionService.StartAsync, which would enqueue via the
        // *real* Hangfire client — a write outside this test's transaction
        // that a live Hangfire server could pick up concurrently. Calling
        // GeneralRevisionJobRunner.RunAsync directly (below) is exactly the
        // "not through the real Hangfire pipeline" approach the class doc
        // comment describes; constructing its input the same way keeps the
        // whole test inside the rolled-back transaction.
        var job = new GeneralRevisionJob { RevisionYear = 2026, TotalCount = 2, StartedBy = startedBy, StartedAt = DateTimeOffset.UtcNow };
        db.GeneralRevisionJobs.Add(job);
        await db.SaveChangesAsync();

        var runner = services.GetRequiredService<GeneralRevisionJobRunner>();
        await runner.RunAsync(job.Id, [rpuId1, rpuId2], 2026, CancellationToken.None);

        db.ChangeTracker.Clear();
        job = await db.GeneralRevisionJobs.SingleAsync(x => x.Id == job.Id);
        job.Status.ShouldBe(JobExecutionStatus.Completed);
        job.ProcessedCount.ShouldBe(2);
        job.FailedCount.ShouldBe(0);

        var assessments = await db.Assessments.Where(x => x.RevisionReference == job.Id).ToListAsync();
        assessments.Count.ShouldBe(2);
        assessments.ShouldAllBe(a => a.Status == WorkflowStatus.Draft);
        assessments.ShouldAllBe(a => a.AssessedValue > 0);
    }
}
