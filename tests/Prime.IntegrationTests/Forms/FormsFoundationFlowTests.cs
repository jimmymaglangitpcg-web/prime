using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Approvals;
using Prime.Application.Features.Assessments;
using Prime.Application.Features.Billing.Bills;
using Prime.Application.Features.Forms;
using Prime.Application.Features.Numbering;
using Prime.Application.Features.TaxDeclarations;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests.Forms;

/// <summary>
/// Forms Foundation (docs/FORMS-REVISION-PLAN.md §5 A1–A3, §10 done criteria):
/// numbering schemes, form versions with frozen issued snapshots, and
/// approval chains. Rolled-back-transaction pattern; every pattern, label and
/// template here is DEMO test data, not LAM content.
/// </summary>
public class FormsFoundationFlowTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    // The LGU's local date, as IClock computes it (Lgu:TimeZone in appsettings.json).
    private static readonly DateOnly Today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila")).DateTime);

    private sealed record Scope(PrimeDbContext Db, IServiceProvider Services, CurrentUserService User, AppUser A, AppUser B, AppUser C, AppUser D);

    private async Task<(Scope Scope, IAsyncDisposable Transaction)> BeginAsync()
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        await BillingFlowTests.RetireExistingRulesAsync(db);
        await db.NumberingSchemes.Where(x => x.Status == WorkflowStatus.Approved)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null));
        await db.ApprovalChains.Where(x => x.Status == WorkflowStatus.Approved)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null));

        var users = Enumerable.Range(0, 4).Select(i => new AppUser
        {
            SupabaseUserId = Guid.NewGuid(), DisplayName = $"DEMO Signer {(char)('A' + i)}", Email = $"demo-{Guid.NewGuid():N}@example.invalid",
        }).ToList();
        db.AppUsers.AddRange(users);
        await db.SaveChangesAsync();

        var s = new Scope(db, scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<CurrentUserService>(), users[0], users[1], users[2], users[3]);
        return (s, new ScopedTransaction(transaction, scope));
    }

    private sealed class ScopedTransaction(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    private static async Task<NumberingSchemeDto> ApprovedSchemeAsync(Scope s, NumberedDocumentKind kind, string pattern, bool allowManual = false)
    {
        var numbering = s.Services.GetRequiredService<INumberingService>();
        s.User.AppUserId = s.A.Id;
        var created = await numbering.CreateAsync(new CreateNumberingSchemeRequest("DEMO — not an LGU format", Today, null, kind, "DEMO scheme", pattern, null, allowManual));
        created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);

        (await numbering.ApproveAsync(created.Value.Id)).Code.ShouldBe("CANNOT_APPROVE_OWN_NUMBERING_SCHEME");
        s.User.AppUserId = s.B.Id;
        var approved = await numbering.ApproveAsync(created.Value.Id);
        approved.IsSuccess.ShouldBeTrue(approved.IsSuccess ? null : approved.Message);
        return approved.Value;
    }

    private static CreateTaxDeclarationRequest TdRequest(BillingFlowTests.Seed seed, string? number, string? remarks = null) =>
        new(seed.RpuId, number, new DateOnly(2026, 1, 1), Taxability.Taxable, seed.ClassificationId,
            seed.TaxDeclaration.ActualUseId, null, 2026, null, remarks);

    // --- A1 numbering ---

    [Fact]
    public async Task TdScheme_GeneratesSequentialNumbers_AndRefusesManualEntry()
    {
        var (s, tx) = await BeginAsync();
        await using var _ = tx;
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(s.Services, s.Db, new DateOnly(2026, 1, 1));
        await ApprovedSchemeAsync(s, NumberedDocumentKind.TaxDeclaration, "DEMO-TD-{YEAR}-{SEQ:4}");
        var tds = s.Services.GetRequiredService<ITaxDeclarationService>();

        (await tds.CreateAsync(TdRequest(seed, null))).Value.TaxDeclarationNumber.ShouldBe("DEMO-TD-2026-0001");
        (await tds.CreateAsync(TdRequest(seed, null))).Value.TaxDeclarationNumber.ShouldBe("DEMO-TD-2026-0002");
        (await tds.CreateAsync(TdRequest(seed, "TYPED-1"))).Code.ShouldBe("NUMBER_MANUAL_ENTRY_NOT_ALLOWED");
    }

    [Fact]
    public async Task NoScheme_NumberMustBeTyped_AsBefore()
    {
        var (s, tx) = await BeginAsync();
        await using var _ = tx;
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(s.Services, s.Db, new DateOnly(2026, 1, 1));
        var tds = s.Services.GetRequiredService<ITaxDeclarationService>();

        (await tds.CreateAsync(TdRequest(seed, null))).Code.ShouldBe("NUMBER_REQUIRED");
        (await tds.CreateAsync(TdRequest(seed, $"TYPED-{Guid.NewGuid():N}"[..20]))).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task PostingBill_AssignsBillNumberFromScheme()
    {
        var (s, tx) = await BeginAsync();
        await using var _ = tx;
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(s.Services, s.Db, new DateOnly(2026, 1, 1));
        await BillingFlowTests.SeedRulesAsync(s.Db, new DateOnly(2026, 1, 1));
        await ApprovedSchemeAsync(s, NumberedDocumentKind.TaxBill, "DEMO-BILL-{YEAR}-{SEQ:3}");
        var bills = s.Services.GetRequiredService<IBillService>();

        var bill = (await bills.GenerateAsync(new GenerateBillRequest(seed.RpuId, 2026, new DateOnly(2026, 1, 15)))).Value;
        bill.BillNumber.ShouldBeNull();
        var posted = await bills.PostAsync(bill.Id);

        posted.IsSuccess.ShouldBeTrue(posted.IsSuccess ? null : posted.Message);
        posted.Value.BillNumber.ShouldBe("DEMO-BILL-2026-001");
    }

    // --- A2 forms ---

    [Fact]
    public async Task IssuingTd_FreezesSnapshot_IsIdempotent_UntilCancelled()
    {
        var (s, tx) = await BeginAsync();
        await using var _ = tx;
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(s.Services, s.Db, new DateOnly(2026, 1, 1));
        var forms = s.Services.GetRequiredService<IFormService>();
        var request = new IssueFormRequest("TAX_DECLARATION", seed.TaxDeclaration.Id);

        var first = await forms.IssueAsync(request);
        first.IsSuccess.ShouldBeTrue(first.IsSuccess ? null : first.Message);
        first.Value.Authority.ShouldBe(FormAuthority.PrimeProvisional);
        first.Value.DocumentNumber.ShouldBe(seed.TaxDeclaration.TaxDeclarationNumber);
        var firstHtml = first.Value.Html.ShouldNotBeNull();
        firstHtml.ShouldContain(seed.TaxDeclaration.TaxDeclarationNumber);
        firstHtml.ShouldContain("PROVISIONAL — NOT AN OFFICIAL FORM");
        firstHtml.ShouldContain("100,000.00"); // assessed value from the posted assessment

        var td = await s.Db.TaxDeclarations.SingleAsync(x => x.Id == seed.TaxDeclaration.Id);
        td.Remarks = "DEMO remark added after issue";
        await s.Db.SaveChangesAsync();

        var again = (await forms.IssueAsync(request)).Value;
        again.Id.ShouldBe(first.Value.Id);
        again.RenderedHtmlSha256.ShouldBe(first.Value.RenderedHtmlSha256);
        again.Html!.ShouldNotContain("DEMO remark added after issue");

        (await forms.CancelIssuedAsync(first.Value.Id, "DEMO: data corrected")).Value.Status.ShouldBe(WorkflowStatus.Cancelled);
        var reissued = (await forms.IssueAsync(request)).Value;
        reissued.Id.ShouldNotBe(first.Value.Id);
        reissued.Html!.ShouldContain("DEMO remark added after issue");
        (await forms.GetIssuedAsync(first.Value.Id)).Value.RenderedHtmlSha256.ShouldBe(first.Value.RenderedHtmlSha256);
    }

    [Fact]
    public async Task NewFormVersion_ReplacesProvisionalWithoutCodeChange_EarlierIssuesUnchanged()
    {
        var (s, tx) = await BeginAsync();
        await using var _ = tx;
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(s.Services, s.Db, new DateOnly(2026, 1, 1));
        var forms = s.Services.GetRequiredService<IFormService>();
        var request = new IssueFormRequest("TAX_DECLARATION", seed.TaxDeclaration.Id);
        var provisional = (await forms.IssueAsync(request)).Value;

        s.User.AppUserId = s.A.Id;
        var v2 = (await forms.CreateDefinitionAsync(new CreateFormDefinitionRequest(
            "DEMO fixture standing in for a LAM form", Today, null, "TAX_DECLARATION", "DEMO Tax Declaration v2",
            FormSubjectType.TaxDeclaration, FormAuthority.Lam, "DEMO — not a LAM reference",
            "<p>DEMO FIXTURE TD {{ td.number }} AV {{ assessment.assessedValue | money }}</p>"))).Value;
        s.User.AppUserId = s.B.Id;
        (await forms.ApproveDefinitionAsync(v2.Id)).IsSuccess.ShouldBeTrue();

        var issued = (await forms.IssueAsync(request)).Value;
        issued.FormVersion.ShouldBe(v2.Version);
        var issuedHtml = issued.Html.ShouldNotBeNull();
        issuedHtml.ShouldContain($"DEMO FIXTURE TD {seed.TaxDeclaration.TaxDeclarationNumber} AV 100,000.00");
        issuedHtml.ShouldNotContain("NOT AN OFFICIAL FORM");

        var old = (await forms.GetIssuedAsync(provisional.Id)).Value;
        old.FormVersion.ShouldBe(provisional.FormVersion);
        old.Html!.ShouldContain("PROVISIONAL — NOT AN OFFICIAL FORM");
    }

    [Fact]
    public async Task DraftBill_CanBePreviewed_ButNotIssued()
    {
        var (s, tx) = await BeginAsync();
        await using var _ = tx;
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(s.Services, s.Db, new DateOnly(2026, 1, 1));
        await BillingFlowTests.SeedRulesAsync(s.Db, new DateOnly(2026, 1, 1));
        var bill = (await s.Services.GetRequiredService<IBillService>().GenerateAsync(new GenerateBillRequest(seed.RpuId, 2026, new DateOnly(2026, 1, 15)))).Value;
        var forms = s.Services.GetRequiredService<IFormService>();

        var preview = await forms.PreviewAsync("TAX_BILL", bill.Id);
        preview.IsSuccess.ShouldBeTrue(preview.IsSuccess ? null : preview.Message);
        preview.Value.IssueBlocker.ShouldNotBeNull();
        preview.Value.Html.ShouldContain("1,800.00");

        (await forms.IssueAsync(new IssueFormRequest("TAX_BILL", bill.Id))).Code.ShouldBe("FORM_SUBJECT_NOT_ISSUABLE");
        (await s.Db.IssuedForms.CountAsync(x => x.SubjectId == bill.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task InvalidTemplate_IsRejectedAtCreation()
    {
        var (s, tx) = await BeginAsync();
        await using var _ = tx;
        var result = await s.Services.GetRequiredService<IFormService>().CreateDefinitionAsync(new CreateFormDefinitionRequest(
            "DEMO", Today, null, "DEMO_BROKEN", "DEMO", FormSubjectType.TaxBill, FormAuthority.Other, null, "{% if x %}never closed"));

        result.Code.ShouldBe("VALIDATION_FAILED");
    }

    // --- A3 approval chains ---

    [Fact]
    public async Task ApprovalChain_RequiresEachStepByADifferentPerson_AndSignaturesReachTheForm()
    {
        var (s, tx) = await BeginAsync();
        await using var _ = tx;
        var chains = s.Services.GetRequiredService<IApprovalChainService>();
        s.User.AppUserId = s.A.Id;
        var chain = (await chains.CreateAsync(new CreateApprovalChainRequest("DEMO — not the LAM chain", Today, null, ApprovalSubjectType.Assessment,
            "DEMO three-step chain", [
                new ApprovalStepRequest(1, "APPRAISED_BY", "DEMO Appraised by", "DEMO Appraiser"),
                new ApprovalStepRequest(2, "RECOMMENDED_BY", "DEMO Recommending approval", null),
                new ApprovalStepRequest(3, "APPROVED_BY", "DEMO Approved by", "DEMO Assessor"),
            ]))).Value;
        s.User.AppUserId = s.B.Id;
        (await chains.ApproveAsync(chain.Id)).IsSuccess.ShouldBeTrue();

        s.User.AppUserId = null; // the seed approves its own DEMO SMV/levels, so it must not act as A
        var seeded = await BillingFlowTests.SeedPostedAssessmentAsync(s.Services, s.Db, new DateOnly(2026, 1, 1), post: false);
        var valuationId = (await s.Db.Assessments.SingleAsync(x => x.Id == seeded.AssessmentId)).ValuationId;
        var assessments = s.Services.GetRequiredService<IAssessmentService>();
        s.User.AppUserId = s.A.Id; // A creates the assessment under review
        var created = (await assessments.CreateAsync(new CreateAssessmentRequest(valuationId, 2026, new DateOnly(2026, 1, 1), null, null, "DEMO"))).Value;
        var seed = seeded with { AssessmentId = created.Id };
        (await assessments.SubmitForReviewAsync(seed.AssessmentId)).IsSuccess.ShouldBeTrue();

        (await assessments.ApproveAsync(seed.AssessmentId)).Code.ShouldBe("CANNOT_SIGN_OWN_RECORD");
        s.User.AppUserId = s.B.Id;
        (await assessments.ApproveAsync(seed.AssessmentId)).Value.Status.ShouldBe(WorkflowStatus.PendingReview);
        (await assessments.ApproveAsync(seed.AssessmentId)).Code.ShouldBe("APPROVAL_STEP_SAME_SIGNER");
        s.User.AppUserId = s.C.Id;
        (await assessments.ApproveAsync(seed.AssessmentId)).Value.Status.ShouldBe(WorkflowStatus.PendingReview);
        s.User.AppUserId = s.D.Id;
        var approved = (await assessments.ApproveAsync(seed.AssessmentId)).Value;
        approved.Status.ShouldBe(WorkflowStatus.Approved);
        approved.ApprovedBy.ShouldBe(s.D.Id);
        (await assessments.ApproveAsync(seed.AssessmentId)).Code.ShouldBe("ASSESSMENT_NOT_PENDING_REVIEW");

        var records = (await chains.ListRecordsAsync(ApprovalSubjectType.Assessment, seed.AssessmentId)).Value;
        records.Select(r => (r.StepCode, r.SignatoryName)).ShouldBe([
            ("APPRAISED_BY", "DEMO Signer B"), ("RECOMMENDED_BY", "DEMO Signer C"), ("APPROVED_BY", "DEMO Signer D")]);

        (await assessments.PostAsync(seed.AssessmentId)).IsSuccess.ShouldBeTrue();
        var html = (await s.Services.GetRequiredService<IFormService>().IssueAsync(new IssueFormRequest("TAX_DECLARATION", seed.TaxDeclaration.Id))).Value.Html!;
        html.ShouldContain("DEMO Recommending approval");
        html.ShouldContain("DEMO Signer C");
        html.ShouldContain("DEMO Assessor");
    }
}
