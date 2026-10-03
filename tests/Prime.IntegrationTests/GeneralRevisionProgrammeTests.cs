using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Assessments;
using Prime.Application.Features.Forms;
using Prime.Application.Features.GeneralRevision;
using Prime.Application.Features.Numbering;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Steps L6-6a and L6-6b (docs/analysis/smv-preparation-general-revision.md §4.6): a general revision programme over a
/// scope, its compile and value runs (the runner is called directly, as in GeneralRevisionFlowTests, so no real background
/// job is queued), failures with their reasons, re-runs, suspensions and cancellation; field review; batch submit, approve,
/// reject and post with maker-checker per item, and the Tax Declarations they prepare; notices in batch, their service, the
/// roll gate and the revision's register runs; units taken out, the checklist, completion and its reports (L6-6c). DEMO data.
/// Rolled back.
/// </summary>
public class GeneralRevisionProgrammeTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly DateOnly Effective = new(2027, 1, 1);

    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, CurrentUserService User, AppUser A, AppUser B, BillingFlowTests.Seed Seed, Guid MunicipalityId)
    {
        public IGeneralRevisionProgrammeService Programmes => Services.GetRequiredService<IGeneralRevisionProgrammeService>();
        public GeneralRevisionProgrammeRunner Runner => Services.GetRequiredService<GeneralRevisionProgrammeRunner>();
    }

    private async Task<(Ctx Ctx, IAsyncDisposable Transaction)> BeginAsync()
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            await db.ApprovalChains.Where(x => x.Status == WorkflowStatus.Approved)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null));
            await db.NumberingSchemes.Where(x => x.Status == WorkflowStatus.Approved)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null));
            var user = new AppUser { SupabaseUserId = Guid.NewGuid(), DisplayName = "DEMO User A", Email = $"demo-{Guid.NewGuid():N}@example.invalid" };
            var userB = new AppUser { SupabaseUserId = Guid.NewGuid(), DisplayName = "DEMO User B", Email = $"demo-{Guid.NewGuid():N}@example.invalid" };
            db.AppUsers.AddRange(user, userB);
            await db.SaveChangesAsync();
            var current = scope.ServiceProvider.GetRequiredService<CurrentUserService>();
            current.AppUserId = null;
            var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, new DateOnly(2026, 1, 1));
            current.AppUserId = user.Id;
            var municipalityId = await db.Properties.Where(x => x.Id == seed.PropertyId).Select(x => x.MunicipalityId).SingleAsync();
            return (new Ctx(db, scope.ServiceProvider, current, user, userB, seed, municipalityId), new Scoped(transaction, scope));
        }
        catch
        {
            await transaction.DisposeAsync();
            scope.Dispose();
            throw;
        }
    }

    private sealed class Scoped(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    private static async Task<GeneralRevisionDto> ProgrammeAsync(Ctx c)
    {
        var created = await c.Programmes.CreateAsync(new(2027, Effective, c.Seed.SmvId, [c.MunicipalityId], "DEMO-EO-2027", "DEMO-ORD-GR", "DEMO revision"));
        created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);
        return created.Value;
    }

    /// <summary>A run row as StartRunAsync makes it, without queueing a real job.</summary>
    private static async Task<Guid> RunAsync(Ctx c, Guid programmeId, GeneralRevisionRunMode mode, IReadOnlyList<Guid> itemIds,
        AppUser? startedBy = null, string? reason = null)
    {
        var job = new GeneralRevisionJob
        {
            GeneralRevisionProgrammeId = programmeId, Mode = mode, RevisionYear = 2027, EffectiveDate = Effective, TotalCount = itemIds.Count,
            StartedBy = (startedBy ?? c.A).Id, StartedAt = DateTimeOffset.UtcNow, Reason = reason,
        };
        c.Db.GeneralRevisionJobs.Add(job);
        await c.Db.SaveChangesAsync();
        await c.Runner.RunAsync(job.Id, itemIds, CancellationToken.None);
        return job.Id;
    }

    [Fact]
    public async Task Create_RequiresAnApplicableSmv_AndOneRevisionPerYearAndMunicipality()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        (await c.Programmes.CreateAsync(new(2025, new DateOnly(2025, 1, 1), c.Seed.SmvId, [c.MunicipalityId], null, null, null)))
            .Code.ShouldBe("SMV_NOT_APPLICABLE"); // the SMV takes effect in 2026
        var programme = await ProgrammeAsync(c);
        programme.Status.ShouldBe(GeneralRevisionStatus.Planned);
        programme.Scope.Single().MunicipalityId.ShouldBe(c.MunicipalityId);
        (await c.Programmes.CreateAsync(new(2027, Effective, c.Seed.SmvId, [c.MunicipalityId], null, null, null))).Code.ShouldBe("GENERAL_REVISION_DUPLICATE");
    }

    [Fact]
    public async Task Compile_ThenValue_DraftsTheAssessment_RecordsOldAndNewValues_AndAFailureWithItsReason()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        // A second unit in scope that cannot be valued: a land unit without its land description.
        var bare = new RealPropertyUnit { PropertyId = c.Seed.PropertyId, RpuNumber = $"RPU-{Guid.NewGuid():N}", RpuType = RpuType.Land, EffectivityDate = new DateOnly(2024, 1, 1) };
        c.Db.Add(bare);
        c.Db.Add(new TaxDeclaration
        {
            Rpu = bare, PropertyId = c.Seed.PropertyId, TaxDeclarationNumber = $"TD-{Guid.NewGuid():N}", EffectivityDate = new DateOnly(2024, 1, 1),
            Taxability = Taxability.Taxable, ClassificationId = c.Seed.ClassificationId, ActualUseId = c.Seed.TaxDeclaration.ActualUseId, AssessmentYear = 2024,
            Status = WorkflowStatus.Approved,
        });
        await c.Db.SaveChangesAsync();
        var programme = await ProgrammeAsync(c);

        await RunAsync(c, programme.Id, GeneralRevisionRunMode.Compile, []);
        var items = await c.Db.GeneralRevisionItems.Where(x => x.GeneralRevisionProgrammeId == programme.Id).ToListAsync();
        items.Count.ShouldBe(2);
        var land = items.Single(x => x.RpuId == c.Seed.RpuId);
        (land.PreviousAssessmentId, land.PreviousAssessedValue, land.Status).ShouldBe((c.Seed.AssessmentId, 100_000m, GeneralRevisionItemStatus.Pending));
        await RunAsync(c, programme.Id, GeneralRevisionRunMode.Compile, []); // again: nothing new
        (await c.Db.GeneralRevisionItems.CountAsync(x => x.GeneralRevisionProgrammeId == programme.Id)).ShouldBe(2);

        var jobId = await RunAsync(c, programme.Id, GeneralRevisionRunMode.Value, items.OrderBy(x => x.RpuNumber).Select(x => x.Id).ToList());
        var job = await c.Db.GeneralRevisionJobs.AsNoTracking().SingleAsync(x => x.Id == jobId);
        (job.Status, job.ProcessedCount, job.FailedCount).ShouldBe((JobExecutionStatus.Completed, 2, 1));

        c.Db.ChangeTracker.Clear();
        var valued = await c.Db.GeneralRevisionItems.Include(x => x.Assessment).SingleAsync(x => x.Id == land.Id);
        valued.Status.ShouldBe(GeneralRevisionItemStatus.Assessed);
        valued.NewMarketValue.ShouldBe(500_000m);
        valued.NewAssessedValue.ShouldBe(100_000m);
        var assessment = valued.Assessment.ShouldNotBeNull();
        (assessment.Status, assessment.EffectiveDate, assessment.RevisionReference, assessment.PreviousAssessmentId)
            .ShouldBe((WorkflowStatus.Draft, Effective, (Guid?)jobId, (Guid?)c.Seed.AssessmentId));
        var failed = await c.Db.GeneralRevisionItems.SingleAsync(x => x.RpuId == bare.Id);
        failed.Status.ShouldBe(GeneralRevisionItemStatus.Failed);
        failed.FailureReason.ShouldNotBeNullOrWhiteSpace();

        var dto = (await c.Programmes.GetAsync(programme.Id)).Value;
        dto.Status.ShouldBe(GeneralRevisionStatus.Planned); // runs queued through the service set it in progress; here the rows were made directly
        dto.ItemsByStatus["Assessed"].ShouldBe(1);
        dto.ItemsByStatus["Failed"].ShouldBe(1);
        dto.AssessmentsByStatus["Draft"].ShouldBe(1);
        dto.NewAssessedValue.ShouldBe(100_000m);
        var listed = (await c.Programmes.SearchItemsAsync(programme.Id, new GeneralRevisionItemSearchRequest { Status = GeneralRevisionItemStatus.Assessed })).Value;
        listed.Items.Single().AssessedValueChange.ShouldBe(0m);
        // An item without an assessment shows no Tax Declaration (not one of the TDs that declare no assessment).
        var unassessed = (await c.Programmes.SearchItemsAsync(programme.Id, new GeneralRevisionItemSearchRequest { Status = GeneralRevisionItemStatus.Failed })).Value;
        unassessed.Items.Single().TaxDeclarationId.ShouldBeNull();

        // Valuing the failed item again fails it again, with its reason, without stopping the run.
        var again = await RunAsync(c, programme.Id, GeneralRevisionRunMode.Value, [failed.Id]);
        c.Db.ChangeTracker.Clear();
        var rerun = await c.Db.GeneralRevisionJobs.AsNoTracking().SingleAsync(x => x.Id == again);
        (rerun.Status, rerun.ProcessedCount, rerun.FailedCount, rerun.Remarks).ShouldBe((JobExecutionStatus.Failed, 1, 1, (string?)null));
        (await c.Db.GeneralRevisionItems.AsNoTracking().SingleAsync(x => x.Id == failed.Id)).FailureReason.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ValuingAgain_CancelsTheEarlierDraft_ButAnItemInReviewIsRefused()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var programme = await ProgrammeAsync(c);
        await RunAsync(c, programme.Id, GeneralRevisionRunMode.Compile, []);
        var item = await c.Db.GeneralRevisionItems.SingleAsync(x => x.GeneralRevisionProgrammeId == programme.Id);
        await RunAsync(c, programme.Id, GeneralRevisionRunMode.Value, [item.Id]);
        c.Db.ChangeTracker.Clear();
        var firstDraft = (await c.Db.GeneralRevisionItems.AsNoTracking().SingleAsync(x => x.Id == item.Id)).AssessmentId!.Value;

        await RunAsync(c, programme.Id, GeneralRevisionRunMode.Value, [item.Id]);
        c.Db.ChangeTracker.Clear();
        var again = await c.Db.GeneralRevisionItems.AsNoTracking().SingleAsync(x => x.Id == item.Id);
        again.AssessmentId.ShouldNotBe(firstDraft);
        (await c.Db.Assessments.AsNoTracking().SingleAsync(x => x.Id == firstDraft)).Status.ShouldBe(WorkflowStatus.Cancelled);

        (await c.Services.GetRequiredService<IAssessmentService>().SubmitForReviewAsync(again.AssessmentId!.Value)).IsSuccess.ShouldBeTrue();
        (await c.Programmes.StartRunAsync(programme.Id, new(GeneralRevisionRunMode.Value, [item.Id]))).Code.ShouldBe("GENERAL_REVISION_ITEM_IN_REVIEW");
        (await c.Programmes.CancelAsync(programme.Id, new("DEMO"))).Code.ShouldBe("GENERAL_REVISION_IN_REVIEW");
    }

    [Fact]
    public async Task ACalamitySuspension_BlocksRuns_ForItsPeriod_AndCanBeExtendedAndLifted()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var programme = await ProgrammeAsync(c);
        var today = c.Services.GetRequiredService<IClock>().Today;

        var suspended = (await c.Programmes.SuspendAsync(programme.Id, new(GeneralRevisionSuspensionKind.LocalCalamity, today, "DEMO EO calamity", null))).Value;
        suspended.Suspended.ShouldBeTrue();
        var first = suspended.Suspensions.Single();
        first.UntilDate.ShouldBe(today.AddDays(29)); // GeneralRevision:CalamitySuspensionDays = 30
        (await c.Programmes.StartRunAsync(programme.Id, new(GeneralRevisionRunMode.Compile))).Code.ShouldBe("GENERAL_REVISION_SUSPENDED");

        var extended = (await c.Programmes.SuspendAsync(programme.Id, new(GeneralRevisionSuspensionKind.Extension, today, "DEMO SOF approval", null))).Value;
        extended.Suspensions.Single(s => s.Kind == GeneralRevisionSuspensionKind.Extension)
            .ShouldSatisfyAllConditions(s => s.FromDate.ShouldBe(today.AddDays(30)), s => s.UntilDate.ShouldBe(today.AddDays(59)));

        var lifted = (await c.Programmes.LiftSuspensionAsync(programme.Id, first.Id, new(today))).Value;
        lifted.Suspensions.Single(s => s.Id == first.Id).InForce.ShouldBeFalse();
        lifted.Suspended.ShouldBeFalse(); // the extension starts later
    }

    [Fact]
    public async Task Cancelling_BeforeReview_CancelsTheDrafts()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var programme = await ProgrammeAsync(c);
        await RunAsync(c, programme.Id, GeneralRevisionRunMode.Compile, []);
        var item = await c.Db.GeneralRevisionItems.SingleAsync(x => x.GeneralRevisionProgrammeId == programme.Id);
        await RunAsync(c, programme.Id, GeneralRevisionRunMode.Value, [item.Id]);
        c.Db.ChangeTracker.Clear();
        var draft = (await c.Db.GeneralRevisionItems.AsNoTracking().SingleAsync(x => x.Id == item.Id)).AssessmentId!.Value;

        var cancelled = await c.Programmes.CancelAsync(programme.Id, new("DEMO wrong SMV chosen"));
        cancelled.IsSuccess.ShouldBeTrue(cancelled.IsSuccess ? null : cancelled.Message);
        cancelled.Value.Status.ShouldBe(GeneralRevisionStatus.Cancelled);
        (await c.Db.Assessments.AsNoTracking().SingleAsync(x => x.Id == draft)).Status.ShouldBe(WorkflowStatus.Cancelled);
        (await c.Programmes.StartRunAsync(programme.Id, new(GeneralRevisionRunMode.Compile))).Code.ShouldBe("GENERAL_REVISION_CLOSED");
    }

    /// <summary>A programme with its one unit compiled and valued by user A: a Draft assessment.</summary>
    private static async Task<(GeneralRevisionDto Programme, Guid ItemId, Guid AssessmentId)> AssessedAsync(Ctx c)
    {
        var programme = await ProgrammeAsync(c);
        await RunAsync(c, programme.Id, GeneralRevisionRunMode.Compile, []);
        var item = await c.Db.GeneralRevisionItems.SingleAsync(x => x.GeneralRevisionProgrammeId == programme.Id);
        await RunAsync(c, programme.Id, GeneralRevisionRunMode.Value, [item.Id]);
        c.Db.ChangeTracker.Clear();
        var assessmentId = (await c.Db.GeneralRevisionItems.AsNoTracking().SingleAsync(x => x.Id == item.Id)).AssessmentId!.Value;
        return (programme, item.Id, assessmentId);
    }

    private static async Task<WorkflowStatus> StatusAsync(Ctx c, Guid assessmentId)
    {
        c.Db.ChangeTracker.Clear();
        return (await c.Db.Assessments.AsNoTracking().SingleAsync(x => x.Id == assessmentId)).Status;
    }

    [Fact]
    public async Task BatchRuns_SubmitApprovePost_WithMakerCheckerPerItem_ThenTheTaxDeclarations()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var numbering = c.Services.GetRequiredService<INumberingService>();
        c.User.AppUserId = c.A.Id;
        var tag = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(); // TD numbers are unique across the database
        var scheme = (await numbering.CreateAsync(new CreateNumberingSchemeRequest(
            "DEMO — not an LGU format", new DateOnly(2020, 1, 1), null, NumberedDocumentKind.TaxDeclaration, "DEMO TD",
            $"DEMO-GRT-{tag}-{{YEAR}}-{{SEQ:4}}", null, true))).Value;
        c.User.AppUserId = c.B.Id;
        (await numbering.ApproveAsync(scheme.Id)).IsSuccess.ShouldBeTrue();
        var (programme, itemId, assessmentId) = await AssessedAsync(c);

        (await c.Programmes.StartRunAsync(programme.Id, new(GeneralRevisionRunMode.Approve))).Code.ShouldBe("GENERAL_REVISION_NOTHING_TO_RUN");
        (await c.Programmes.StartRunAsync(programme.Id, new(GeneralRevisionRunMode.Reject))).Code.ShouldBe("VALIDATION_FAILED"); // no reason

        await RunAsync(c, programme.Id, GeneralRevisionRunMode.Submit, [itemId]);
        (await StatusAsync(c, assessmentId)).ShouldBe(WorkflowStatus.PendingReview);

        // A, who made the assessment (the value run acted as A), cannot approve it: an issue, and no approval leaks into later saves.
        var refused = await RunAsync(c, programme.Id, GeneralRevisionRunMode.Approve, [itemId], c.A);
        var job = await c.Db.GeneralRevisionJobs.AsNoTracking().SingleAsync(x => x.Id == refused);
        (job.Status, job.ProcessedCount, job.FailedCount).ShouldBe((JobExecutionStatus.Failed, 1, 1));
        var issue = (await c.Programmes.ListRunIssuesAsync(programme.Id, refused)).Value.Single();
        (issue.ItemId, issue.Code).ShouldBe((itemId, "CANNOT_APPROVE_OWN_ASSESSMENT"));
        (await StatusAsync(c, assessmentId)).ShouldBe(WorkflowStatus.PendingReview);

        await RunAsync(c, programme.Id, GeneralRevisionRunMode.Approve, [itemId], c.B);
        (await StatusAsync(c, assessmentId)).ShouldBe(WorkflowStatus.Approved);
        (await c.Db.Assessments.AsNoTracking().SingleAsync(x => x.Id == assessmentId)).ApprovedBy.ShouldBe(c.B.Id);

        await RunAsync(c, programme.Id, GeneralRevisionRunMode.Post, [itemId], c.B);
        (await StatusAsync(c, assessmentId)).ShouldBe(WorkflowStatus.Posted);
        var item = (await c.Programmes.SearchItemsAsync(programme.Id, new GeneralRevisionItemSearchRequest())).Value.Items.Single();
        item.TaxDeclarationStatus.ShouldBe(WorkflowStatus.Draft);
        item.TaxDeclarationNumber.ShouldBe($"DEMO-GRT-{tag}-2027-0001");

        await RunAsync(c, programme.Id, GeneralRevisionRunMode.SubmitTaxDeclarations, [itemId], c.B);
        var ownTd = await RunAsync(c, programme.Id, GeneralRevisionRunMode.ApproveTaxDeclarations, [itemId], c.B); // B posted, so B made the TD
        (await c.Programmes.ListRunIssuesAsync(programme.Id, ownTd)).Value.Single().Code.ShouldBe("CANNOT_APPROVE_OWN_TAX_DECLARATION");
        await RunAsync(c, programme.Id, GeneralRevisionRunMode.ApproveTaxDeclarations, [itemId], c.A);
        c.Db.ChangeTracker.Clear();
        var declared = (await c.Programmes.SearchItemsAsync(programme.Id, new GeneralRevisionItemSearchRequest { TaxDeclarationStatus = WorkflowStatus.Approved })).Value.Items.Single();
        declared.TaxDeclarationId.ShouldNotBeNull();
        (await c.Db.TaxDeclarations.AsNoTracking().SingleAsync(x => x.Id == c.Seed.TaxDeclaration.Id)).Status.ShouldBe(WorkflowStatus.Cancelled);
    }

    [Fact]
    public async Task ARejectRun_ReturnsTheAssessments_WhichCanThenBeValuedAgain()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var (programme, itemId, assessmentId) = await AssessedAsync(c);
        await RunAsync(c, programme.Id, GeneralRevisionRunMode.Submit, [itemId]);
        await RunAsync(c, programme.Id, GeneralRevisionRunMode.Reject, [itemId], c.B, "DEMO: wrong actual use");
        (await StatusAsync(c, assessmentId)).ShouldBe(WorkflowStatus.Rejected);
        await RunAsync(c, programme.Id, GeneralRevisionRunMode.Value, [itemId]);
        c.Db.ChangeTracker.Clear();
        var again = await c.Db.GeneralRevisionItems.AsNoTracking().SingleAsync(x => x.Id == itemId);
        (again.Status, again.AssessmentId == assessmentId).ShouldBe((GeneralRevisionItemStatus.Assessed, false));
    }

    [Fact]
    public async Task FieldReview_AssignsAndRecordsInspections_AndChangesSendTheItemBackForValuation()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var (programme, itemId, assessmentId) = await AssessedAsync(c);
        (await c.Programmes.AssignInspectionAsync(programme.Id, new([itemId], null, "Route 1"))).Code.ShouldBe("VALIDATION_FAILED");
        (await c.Programmes.AssignInspectionAsync(programme.Id, new([itemId], c.B.Id, "DEMO Route 1"))).Value.ShouldBe(1);
        var awaiting = (await c.Programmes.SearchItemsAsync(programme.Id, new GeneralRevisionItemSearchRequest { Inspection = GeneralRevisionInspectionFilter.Awaiting })).Value;
        awaiting.Items.Single().ShouldSatisfyAllConditions(i => i.InspectorName.ShouldBe("DEMO User B"), i => i.InspectionRoute.ShouldBe("DEMO Route 1"));

        var today = c.Services.GetRequiredService<IClock>().Today;
        (await c.Programmes.RecordInspectionAsync(programme.Id, itemId, new(today.AddDays(1), null, false))).Code.ShouldBe("VALIDATION_FAILED");
        var recorded = await c.Programmes.RecordInspectionAsync(programme.Id, itemId, new(today, "DEMO: new extension seen", true));
        recorded.IsSuccess.ShouldBeTrue(recorded.IsSuccess ? null : recorded.Message);
        (recorded.Value.Status, recorded.Value.InspectedOn, recorded.Value.InspectionFoundChanges).ShouldBe((GeneralRevisionItemStatus.Pending, (DateOnly?)today, (bool?)true));
        (await c.Programmes.SearchItemsAsync(programme.Id, new GeneralRevisionItemSearchRequest { Inspection = GeneralRevisionInspectionFilter.Inspected })).Value.TotalCount.ShouldBe(1);

        // Valued again (its draft gives way), then in review: changes can no longer be recorded without rejecting it first.
        await RunAsync(c, programme.Id, GeneralRevisionRunMode.Value, [itemId]);
        (await StatusAsync(c, assessmentId)).ShouldBe(WorkflowStatus.Cancelled);
        await RunAsync(c, programme.Id, GeneralRevisionRunMode.Submit, [itemId]);
        (await c.Programmes.RecordInspectionAsync(programme.Id, itemId, new(today, null, true))).Code.ShouldBe("GENERAL_REVISION_ITEM_IN_REVIEW");
    }

    [Fact]
    public async Task APostThatPreparesNoTaxDeclaration_IsDone_WithANoteOnTheRun()
    {
        var (c, tx) = await BeginAsync(); // no TD numbering scheme in force
        await using var _ = tx;
        var (programme, itemId, assessmentId) = await AssessedAsync(c);
        await RunAsync(c, programme.Id, GeneralRevisionRunMode.Submit, [itemId]);
        await RunAsync(c, programme.Id, GeneralRevisionRunMode.Approve, [itemId], c.B);
        var post = await RunAsync(c, programme.Id, GeneralRevisionRunMode.Post, [itemId], c.B);
        (await StatusAsync(c, assessmentId)).ShouldBe(WorkflowStatus.Posted);
        var job = await c.Db.GeneralRevisionJobs.AsNoTracking().SingleAsync(x => x.Id == post);
        (job.Status, job.FailedCount).ShouldBe((JobExecutionStatus.Completed, 0));
        var note = (await c.Programmes.ListRunIssuesAsync(programme.Id, post)).Value.Single();
        (note.Code, note.Failed).ShouldBe(("TAX_DECLARATION_NOT_PREPARED", false));
        (await c.Programmes.GetAsync(programme.Id)).Value.Runs.Single(r => r.Id == post).IssueCount.ShouldBe(1);
    }

    /// <summary>A DEMO TD numbering scheme in force (made by A, approved by B), so posting prepares the new TD.</summary>
    private static async Task TdSchemeAsync(Ctx c)
    {
        var numbering = c.Services.GetRequiredService<INumberingService>();
        c.User.AppUserId = c.A.Id;
        var tag = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(); // TD numbers are unique across the database
        var scheme = (await numbering.CreateAsync(new CreateNumberingSchemeRequest(
            "DEMO — not an LGU format", new DateOnly(2020, 1, 1), null, NumberedDocumentKind.TaxDeclaration, "DEMO TD",
            $"DEMO-GRT-{tag}-{{YEAR}}-{{SEQ:4}}", null, true))).Value;
        c.User.AppUserId = c.B.Id;
        (await numbering.ApproveAsync(scheme.Id)).IsSuccess.ShouldBeTrue();
    }

    /// <summary>Submits, approves (B), posts (B) and declares (TD submitted by B, approved by A) the item.</summary>
    private static async Task PostAndDeclareAsync(Ctx c, Guid programmeId, Guid itemId)
    {
        await RunAsync(c, programmeId, GeneralRevisionRunMode.Submit, [itemId]);
        await RunAsync(c, programmeId, GeneralRevisionRunMode.Approve, [itemId], c.B);
        await RunAsync(c, programmeId, GeneralRevisionRunMode.Post, [itemId], c.B);
        await RunAsync(c, programmeId, GeneralRevisionRunMode.SubmitTaxDeclarations, [itemId], c.B);
        await RunAsync(c, programmeId, GeneralRevisionRunMode.ApproveTaxDeclarations, [itemId], c.A);
    }

    private static IGeneralRevisionRecordsService RecordsOf(Ctx c) => c.Services.GetRequiredService<IGeneralRevisionRecordsService>();

    [Fact]
    public async Task Notices_AreGeneratedPerOwner_AndIssued_ThenTheRollWaitsForServiceAndThePeriod_UnlessOverriddenWithAReason()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        // The previous assessment was lower, so the revision raises the value and LGC §223 requires notice.
        await c.Db.Assessments.Where(a => a.Id == c.Seed.AssessmentId).ExecuteUpdateAsync(u => u.SetProperty(a => a.AssessedValue, 80_000m));
        var owner = new Taxpayer { TaxpayerType = TaxpayerType.Individual, LastName = "DEMO Owner", FirstName = "DEMO", Address = "DEMO Street" };
        var sole = new OwnershipType { Code = $"OT{Guid.NewGuid():N}"[..8], Name = "DEMO_Sole" };
        c.Db.AddRange(owner, sole);
        c.Db.PropertyTaxpayers.Add(new PropertyTaxpayer
        {
            PropertyId = c.Seed.PropertyId, Taxpayer = owner, Role = PropertyPartyRole.Owner, OwnershipType = sole, OwnershipPercentage = 100,
            StartDate = new DateOnly(2020, 1, 1), IsCurrent = true,
        });
        await c.Db.SaveChangesAsync();
        await TdSchemeAsync(c);
        var (programme, itemId, assessmentId) = await AssessedAsync(c);
        await PostAndDeclareAsync(c, programme.Id, itemId);
        (await StatusAsync(c, assessmentId)).ShouldBe(WorkflowStatus.Posted);
        var records = RecordsOf(c);

        await RunAsync(c, programme.Id, GeneralRevisionRunMode.GenerateNotices, [itemId], c.B);
        var notice = (await records.SearchNoticesAsync(programme.Id, new GeneralRevisionNoticeSearchRequest())).Value.Items.Single();
        (notice.Status, notice.ItemCount, notice.AddresseeNames.Contains("DEMO Owner")).ShouldBe((NoticeStatus.Draft, 1, true));
        await RunAsync(c, programme.Id, GeneralRevisionRunMode.IssueNotices, [itemId], c.B);
        (await records.SearchNoticesAsync(programme.Id, new GeneralRevisionNoticeSearchRequest { Status = NoticeStatus.Issued })).Value.TotalCount.ShouldBe(1);

        var gate = (await records.RollGatesAsync(programme.Id)).Value.Single();
        (gate.Open, gate.NoticesRequired, gate.NoticesOutstanding, gate.UnitsNotPosted).ShouldBe((false, 1, 1, 0));
        (await records.CreateRegisterRunsAsync(programme.Id, new(RegisterKind.AssessmentRollTaxable))).Code.ShouldBe("ROLL_GATE_CLOSED");
        var overridden = await records.CreateRegisterRunsAsync(programme.Id, new(RegisterKind.AssessmentRollTaxable, OverrideReason: "DEMO: Treasurer's request"));
        overridden.IsSuccess.ShouldBeTrue(overridden.IsSuccess ? null : overridden.Message);
        overridden.Value.Single().RollGateOverrideReason.ShouldBe("DEMO: Treasurer's request");
        (await c.Db.RegisterRuns.AsNoTracking().SingleAsync(x => x.Id == overridden.Value.Single().Id)).Remarks.ShouldContain("DEMO: Treasurer's request");

        var today = c.Services.GetRequiredService<IClock>().Today;
        var served = (await records.RecordNoticeServiceAsync(programme.Id,
            new([notice.Id], NoticeServiceMode.Personal, today, "DEMO acknowledgment 1", null, null))).Value;
        (served.Recorded, served.Failures.Count).ShouldBe((1, 0));
        gate = (await records.RollGatesAsync(programme.Id)).Value.Single();
        (gate.Open, gate.NoticesServed, gate.OpensOn).ShouldBe((false, 1, (DateOnly?)today.AddDays(60))); // GeneralRevision:RollWaitDays = 60

        // After the period the roll runs without an override.
        await c.Db.NoticesOfAssessment.Where(n => n.Id == notice.Id).ExecuteUpdateAsync(u => u.SetProperty(n => n.ReceivedDate, today.AddDays(-60)));
        (await records.RollGatesAsync(programme.Id)).Value.Single().Open.ShouldBeTrue();
        var roll = await records.CreateRegisterRunsAsync(programme.Id, new(RegisterKind.AssessmentRollTaxable, OverrideReason: "not needed"));
        roll.Value.Single().RollGateOverrideReason.ShouldBeNull();
        var orf = (await records.CreateRegisterRunsAsync(programme.Id, new(RegisterKind.OwnershipRecordCard))).Value.Single();
        orf.TaxpayerName.ShouldNotBeNull().ShouldContain("DEMO Owner");
        (await records.ListRegisterRunsAsync(programme.Id)).Value.Count.ShouldBe(3);
    }

    [Fact]
    public async Task AnUnchangedValue_NeedsNoNotice_AndTheGateWaitsOnlyForPosting()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        await TdSchemeAsync(c);
        var (programme, itemId, _) = await AssessedAsync(c);
        var records = RecordsOf(c);
        (await records.RollGatesAsync(programme.Id)).Value.Single().ShouldSatisfyAllConditions(g => g.Open.ShouldBeFalse(), g => g.UnitsNotPosted.ShouldBe(1));
        await RunAsync(c, programme.Id, GeneralRevisionRunMode.Submit, [itemId]);
        await RunAsync(c, programme.Id, GeneralRevisionRunMode.Approve, [itemId], c.B);
        await RunAsync(c, programme.Id, GeneralRevisionRunMode.Post, [itemId], c.B);
        (await records.RollGatesAsync(programme.Id)).Value.Single().TaxDeclarationsNotApproved.ShouldBe(1);
        await RunAsync(c, programme.Id, GeneralRevisionRunMode.SubmitTaxDeclarations, [itemId], c.B);
        await RunAsync(c, programme.Id, GeneralRevisionRunMode.ApproveTaxDeclarations, [itemId], c.A);
        (await c.Programmes.StartRunAsync(programme.Id, new(GeneralRevisionRunMode.GenerateNotices))).Code.ShouldBe("GENERAL_REVISION_NOTHING_TO_RUN");
        var gate = (await records.RollGatesAsync(programme.Id)).Value.Single();
        (gate.NoticesRequired, gate.Open).ShouldBe((0, true));
    }

    private static IGeneralRevisionCompletionService CompletionOf(Ctx c) => c.Services.GetRequiredService<IGeneralRevisionCompletionService>();

    [Fact]
    public async Task AUnitTakenOut_IsNotValued_NorCounted_AndCanBePutBack()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var (programme, itemId, assessmentId) = await AssessedAsync(c);
        var completion = CompletionOf(c);
        (await completion.ExcludeItemAsync(programme.Id, itemId, new(" "))).Code.ShouldBe("VALIDATION_FAILED");
        var excluded = await completion.ExcludeItemAsync(programme.Id, itemId, new("DEMO: retired since compiling"));
        excluded.IsSuccess.ShouldBeTrue(excluded.IsSuccess ? null : excluded.Message);
        (excluded.Value.Status, excluded.Value.ExclusionReason).ShouldBe((GeneralRevisionItemStatus.Excluded, "DEMO: retired since compiling"));
        (await StatusAsync(c, assessmentId)).ShouldBe(WorkflowStatus.Cancelled); // its draft goes with it
        (await c.Programmes.StartRunAsync(programme.Id, new(GeneralRevisionRunMode.Value, [itemId]))).Code.ShouldBe("GENERAL_REVISION_ITEM_EXCLUDED");
        (await RecordsOf(c).RollGatesAsync(programme.Id)).Value.Single().Units.ShouldBe(0);

        var back = (await completion.IncludeItemAsync(programme.Id, itemId)).Value;
        (back.Status, back.ExclusionReason).ShouldBe((GeneralRevisionItemStatus.Pending, (string?)null));
        await RunAsync(c, programme.Id, GeneralRevisionRunMode.Value, [itemId]);
        await RunAsync(c, programme.Id, GeneralRevisionRunMode.Submit, [itemId]);
        c.Db.ChangeTracker.Clear();
        (await completion.ExcludeItemAsync(programme.Id, itemId, new("DEMO"))).Code.ShouldBe("GENERAL_REVISION_ITEM_IN_REVIEW");
    }

    [Fact]
    public async Task Completion_WaitsForTheGatesAndTheManualChecklistSteps_ThenTheReportsAreIssued()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        // The checklist template in force: a manual step and a gate step (DEMO titles), made by A and approved by B.
        await c.Db.GeneralRevisionChecklistStepDefinitions.Where(x => x.Status == WorkflowStatus.Approved)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null));
        var completion = CompletionOf(c);
        c.User.AppUserId = c.A.Id;
        var manual = (await completion.CreateStepDefinitionAsync(new("DEMO — not the LAM's text", new DateOnly(2020, 1, 1), null, "DEMO-01", 1,
            "DEMO office order issued", null, null))).Value;
        var gated = (await completion.CreateStepDefinitionAsync(new("DEMO — not the LAM's text", new DateOnly(2020, 1, 1), null, "DEMO-02", 2,
            "DEMO roll prepared", null, GeneralRevisionGate.AssessmentRollRun))).Value;
        (await completion.ApproveStepDefinitionAsync(manual.Id)).Code.ShouldBe("CANNOT_APPROVE_OWN_CHECKLIST_STEP");
        c.User.AppUserId = c.B.Id;
        (await completion.ApproveStepDefinitionAsync(manual.Id)).IsSuccess.ShouldBeTrue();
        (await completion.ApproveStepDefinitionAsync(gated.Id)).IsSuccess.ShouldBeTrue();

        await TdSchemeAsync(c);
        var (programme, itemId, _) = await AssessedAsync(c);
        var loaded = await completion.LoadChecklistAsync(programme.Id);
        loaded.IsSuccess.ShouldBeTrue(loaded.IsSuccess ? null : loaded.Message);
        loaded.Value.Checklist.Select(s => s.Code).ShouldBe(["DEMO-01", "DEMO-02"]);
        (await completion.LoadChecklistAsync(programme.Id)).Code.ShouldBe("CHECKLIST_ALREADY_LOADED");
        (await completion.CompleteAsync(programme.Id)).Code.ShouldBe("GENERAL_REVISION_NOT_READY");

        await PostAndDeclareAsync(c, programme.Id, itemId); // unchanged value: no notice needed
        var forms = c.Services.GetRequiredService<IFormService>();
        (await forms.IssueAsync(new IssueFormRequest(GeneralRevisionCompletionService.CompletionReportForm, programme.Id))).IsSuccess.ShouldBeFalse();
        (await RecordsOf(c).CreateRegisterRunsAsync(programme.Id, new(RegisterKind.AssessmentRollTaxable))).IsSuccess.ShouldBeTrue();
        var readiness = (await completion.ReadinessAsync(programme.Id)).Value;
        readiness.Checklist.Single(s => s.Code == "DEMO-02").Met.ShouldBeTrue();
        readiness.Blockers.Single().ShouldContain("DEMO-01");
        var gateStep = readiness.Checklist.Single(s => s.Code == "DEMO-02");
        (await completion.CompleteStepAsync(programme.Id, gateStep.Id, new(new DateOnly(2026, 1, 5), null))).Code.ShouldBe("CHECKLIST_STEP_IS_GATE");
        var manualStep = readiness.Checklist.Single(s => s.Code == "DEMO-01");
        (await completion.CompleteStepAsync(programme.Id, manualStep.Id, new(new DateOnly(2026, 1, 5), "DEMO EO 2027-01"))).Value.Blockers.ShouldBeEmpty();

        var completed = await completion.CompleteAsync(programme.Id);
        completed.IsSuccess.ShouldBeTrue(completed.IsSuccess ? null : completed.Message);
        completed.Value.Status.ShouldBe(GeneralRevisionStatus.Completed);
        var report = await forms.IssueAsync(new IssueFormRequest(GeneralRevisionCompletionService.CompletionReportForm, programme.Id));
        report.IsSuccess.ShouldBeTrue(report.IsSuccess ? null : report.Message);
        report.Value.Html.ShouldNotBeNull().ShouldContain("COMPLETION REPORT");
        (await forms.IssueAsync(new IssueFormRequest(GeneralRevisionCompletionService.StatusReportForm, programme.Id))).IsSuccess.ShouldBeTrue();
        (await completion.ReadinessAsync(programme.Id)).Value.Gates.Single(g => g.Gate == GeneralRevisionGate.CompletionReportIssued).Met.ShouldBeTrue();
        (await c.Programmes.StartRunAsync(programme.Id, new(GeneralRevisionRunMode.Compile))).Code.ShouldBe("GENERAL_REVISION_CLOSED");
    }
}
