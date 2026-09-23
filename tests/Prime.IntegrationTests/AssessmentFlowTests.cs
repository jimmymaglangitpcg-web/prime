using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Assessments;
using Prime.Application.Features.Smv;
using Prime.Application.Features.Valuation;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Phase 6 exit criteria (docs/DEVELOPMENT-ROADMAP.md): an assessment can be
/// created, reviewed, approved by a different user than the creator, and
/// posted. Same single-scope, rolled-back-transaction pattern as
/// ValuationFlowTests/ConstraintTests. These tests never go through a real
/// HTTP request (no `AppUserProvisioningMiddleware` run), so
/// <see cref="ICurrentUserService.AppUserId"/> is never populated
/// automatically — the concrete <see cref="CurrentUserService"/> is
/// resolved directly and its `AppUserId` set explicitly to simulate
/// "acting as user X", which is exactly what the maker-checker check needs
/// to be exercised meaningfully.
/// </summary>
public class AssessmentFlowTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
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

    /// <summary>Seeds a Land, an Approved Smv/SmvSchedule, and an Approved AssessmentLevel bracket covering the resulting market value.</summary>
    private static async Task<Valuation> SeedValuationReadyForAssessmentAsync(IServiceProvider services, PrimeDbContext db, decimal area = 500m, decimal rate = 1000m, decimal assessmentPercentage = 20m)
    {
        var province = new Province { PsgcCode = $"P{Guid.NewGuid():N}"[..10], Name = "Demo Province" };
        var municipality = new Municipality { Province = province, PsgcCode = $"M{Guid.NewGuid():N}"[..10], Name = "Demo Municipality" };
        var barangay = new Barangay { Municipality = municipality, PsgcCode = $"B{Guid.NewGuid():N}"[..10], Name = "Demo Barangay" };
        var classification = new Classification { Code = $"CL{Guid.NewGuid():N}"[..8], Name = "DEMO_Residential" };
        var actualUse = new ActualUse { Code = $"AU{Guid.NewGuid():N}"[..8], Name = "DEMO_Residential Use" };
        var propertyType = new PropertyType { Code = "LAND", Name = "DEMO_Land" };
        db.AddRange(province, municipality, barangay, classification, actualUse, propertyType);

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

        var land = new Land { Rpu = rpu, Property = property, Area = area, Classification = classification, ActualUse = actualUse };
        db.Lands.Add(land);
        await db.SaveChangesAsync();

        var smvService = services.GetRequiredService<ISmvService>();
        var smv = (await smvService.CreateSmvAsync(new CreateSmvRequest(
            $"ORD-{Guid.NewGuid():N}", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 1), 2026, "DEMO_SMV for AssessmentFlowTests"))).Value;
        await smvService.ApproveSmvAsync(smv.Id);
        var schedule = (await smvService.CreateScheduleAsync(smv.Id, new CreateSmvScheduleRequest(
            classification.Id, actualUse.Id, propertyType.Id, null, "per sqm", rate, null, null, new DateOnly(2026, 1, 1)))).Value;
        await smvService.ApproveScheduleAsync(schedule.Id);

        var marketValue = area * rate;
        var assessmentLevelService = services.GetRequiredService<Application.Features.AssessmentLevels.IAssessmentLevelService>();
        var assessmentLevel = (await assessmentLevelService.CreateAsync(new Application.Features.AssessmentLevels.CreateAssessmentLevelRequest(
            $"ORD-{Guid.NewGuid():N}", new DateOnly(2026, 1, 1), classification.Id, actualUse.Id, propertyType.Id,
            0m, marketValue * 2, assessmentPercentage, new DateOnly(2026, 1, 1)))).Value;
        await assessmentLevelService.ApproveAsync(assessmentLevel.Id);

        var valuationService = services.GetRequiredService<IValuationService>();
        var valuationResult = await valuationService.ComputeForLandAsync(land.Id);
        valuationResult.IsSuccess.ShouldBeTrue(valuationResult.IsSuccess ? null : valuationResult.Message);

        var valuation = await db.Valuations.SingleAsync(x => x.Id == valuationResult.Value.Id);
        return valuation;
    }

    [Fact]
    public async Task CreateAssessment_FromValuation_ComputesAssessedValueAndFreezesPercentage()
    {
        var (db, services, transaction) = await BeginTestScopeAsync(factory);
        await using var _ = transaction;

        var valuation = await SeedValuationReadyForAssessmentAsync(services, db, area: 500m, rate: 1000m, assessmentPercentage: 20m);

        var assessmentService = services.GetRequiredService<IAssessmentService>();
        var result = await assessmentService.CreateAsync(new CreateAssessmentRequest(
            valuation.Id, 2026, new DateOnly(2026, 1, 1), null, null, "Initial assessment"));

        result.IsSuccess.ShouldBeTrue(result.IsSuccess ? null : result.Message);
        result.Value.MarketValue.ShouldBe(500_000m);
        result.Value.AssessmentPercentage.ShouldBe(20m);
        result.Value.AssessedValue.ShouldBe(100_000m); // 500,000 * 20%
        result.Value.Status.ShouldBe(WorkflowStatus.Draft);
    }

    [Fact]
    public async Task ApproveAssessment_RejectsWhenApproverIsCreator_SucceedsWithDifferentUser()
    {
        var (db, services, transaction) = await BeginTestScopeAsync(factory);
        await using var _ = transaction;

        var valuation = await SeedValuationReadyForAssessmentAsync(services, db);
        var currentUser = services.GetRequiredService<CurrentUserService>();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        currentUser.AppUserId = userA;
        var assessmentService = services.GetRequiredService<IAssessmentService>();
        var created = (await assessmentService.CreateAsync(new CreateAssessmentRequest(
            valuation.Id, 2026, new DateOnly(2026, 1, 1), null, null, null))).Value;
        (await assessmentService.SubmitForReviewAsync(created.Id)).IsSuccess.ShouldBeTrue();

        // Same user who created it tries to approve — must be rejected.
        var selfApprove = await assessmentService.ApproveAsync(created.Id);
        selfApprove.IsSuccess.ShouldBeFalse();
        selfApprove.Code.ShouldBe("CANNOT_APPROVE_OWN_ASSESSMENT");

        // A different acting user approves successfully.
        currentUser.AppUserId = userB;
        var approve = await assessmentService.ApproveAsync(created.Id);
        approve.IsSuccess.ShouldBeTrue(approve.IsSuccess ? null : approve.Message);
        approve.Value.Status.ShouldBe(WorkflowStatus.Approved);
        approve.Value.ApprovedBy.ShouldBe(userB);

        var post = await assessmentService.PostAsync(created.Id);
        post.IsSuccess.ShouldBeTrue(post.IsSuccess ? null : post.Message);
        post.Value.Status.ShouldBe(WorkflowStatus.Posted);
    }

    [Fact]
    public async Task Reassessment_ChainsToPreviousAssessment()
    {
        var (db, services, transaction) = await BeginTestScopeAsync(factory);
        await using var _ = transaction;

        var valuation = await SeedValuationReadyForAssessmentAsync(services, db);
        var assessmentService = services.GetRequiredService<IAssessmentService>();

        var first = (await assessmentService.CreateAsync(new CreateAssessmentRequest(
            valuation.Id, 2026, new DateOnly(2026, 1, 1), null, null, "First"))).Value;

        var second = await assessmentService.CreateAsync(new CreateAssessmentRequest(
            valuation.Id, 2026, new DateOnly(2026, 6, 1), first.Id, null, "Reassessment after correction"));

        second.IsSuccess.ShouldBeTrue(second.IsSuccess ? null : second.Message);
        second.Value.PreviousAssessmentId.ShouldBe(first.Id);

        var history = await assessmentService.ListByRpuAsync(valuation.RpuId, null);
        history.Value.Count.ShouldBe(2);
    }
}
