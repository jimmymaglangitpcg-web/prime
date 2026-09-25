using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Assessments;
using Prime.Application.Features.Forms;
using Prime.Application.Features.Notices;
using Prime.Application.Features.Taxpayers;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// docs/FORMS-REVISION-PLAN.md A6 — Notice of Assessment (LGC §§223, 226).
/// The 30/60-day periods come from appsettings.json (with their citations).
/// Names and references are DEMO test data.
/// </summary>
public class NoticeOfAssessmentTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, BillingFlowTests.Seed Seed, DateOnly Today)
    {
        public INoticeService Notices => Services.GetRequiredService<INoticeService>();
    }

    private async Task<(Ctx Ctx, IAsyncDisposable Transaction)> BeginAsync(bool withOwner = true)
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        await db.NumberingSchemes.Where(x => x.Status == WorkflowStatus.Approved)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null));
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, new DateOnly(2026, 1, 1));
        if (withOwner)
        {
            var owner = new Taxpayer { TaxpayerType = TaxpayerType.Individual, LastName = "DEMO_Owner", FirstName = "Juan", Address = "DEMO Address 1" };
            var type = new OwnershipType { Code = $"OT{Guid.NewGuid():N}"[..8], Name = "DEMO_Sole" };
            db.AddRange(owner, type);
            await db.SaveChangesAsync();
            (await scope.ServiceProvider.GetRequiredService<ITaxpayerService>().AddOwnerAsync(
                new AddPropertyOwnerRequest(seed.PropertyId, owner.Id, type.Id, 100m, new DateOnly(2020, 1, 1)))).IsSuccess.ShouldBeTrue();
        }
        var today = scope.ServiceProvider.GetRequiredService<IClock>().Today;
        return (new Ctx(db, scope.ServiceProvider, seed, today), new Scoped(transaction, scope));
    }

    private sealed class Scoped(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    /// <summary>A second posted assessment of the same RPU, optionally with a different assessed value.</summary>
    private static async Task<Guid> ReassessAsync(Ctx c, decimal? assessedValue)
    {
        var first = await c.Db.Assessments.SingleAsync(x => x.Id == c.Seed.AssessmentId);
        var created = (await c.Services.GetRequiredService<IAssessmentService>().CreateAsync(
            new CreateAssessmentRequest(first.ValuationId, 2027, new DateOnly(2026, 1, 1), first.Id, null, "DEMO reassessment"))).Value;
        var second = await c.Db.Assessments.SingleAsync(x => x.Id == created.Id);
        second.EffectiveDate = new DateOnly(2027, 1, 1);
        second.Status = WorkflowStatus.Posted;
        if (assessedValue is { } av)
        {
            second.AssessedValue = av;
        }
        await c.Db.SaveChangesAsync();
        return second.Id;
    }

    [Fact]
    public async Task FirstAssessment_Notice_IsGeneratedIssuedAndServed_WithAppealDeadline()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;

        var generated = await c.Notices.GenerateAsync(new GenerateNoticeRequest(c.Seed.AssessmentId));
        generated.IsSuccess.ShouldBeTrue(generated.IsSuccess ? null : generated.Message);
        var n = generated.Value;
        n.Reason.ShouldBe(NoticeReason.FirstAssessment);
        n.PreviousAssessedValue.ShouldBeNull();
        n.AssessedValue.ShouldBe(100_000m);
        n.AddresseeNames.ShouldContain("DEMO_Owner");
        n.AddresseeAddress.ShouldBe("DEMO Address 1");
        n.IssuePeriodDays.ShouldBe(30);
        n.IssueDueDate.ShouldBe(c.Today.AddDays(30)); // the DEMO assessment has no approval date: counted from today
        n.Status.ShouldBe(NoticeStatus.Draft);
        (await c.Notices.GenerateAsync(new GenerateNoticeRequest(c.Seed.AssessmentId))).Code.ShouldBe("NOTICE_DUPLICATE");

        (await c.Notices.RecordServiceAsync(n.Id, new RecordNoticeServiceRequest(NoticeServiceMode.Personal, c.Today, "x", "y", null)))
            .Code.ShouldBe("NOTICE_NOT_ISSUED");
        (await c.Notices.IssueAsync(n.Id)).Value.Status.ShouldBe(NoticeStatus.Issued);
        (await c.Notices.RecordServiceAsync(n.Id, new RecordNoticeServiceRequest(NoticeServiceMode.RegisteredMail, c.Today.AddDays(1), "Juan", "RRC-DEMO-1", null)))
            .Code.ShouldBe("NOTICE_RECEIVED_DATE_INVALID"); // not in the future

        var served = (await c.Notices.RecordServiceAsync(n.Id,
            new RecordNoticeServiceRequest(NoticeServiceMode.RegisteredMail, c.Today, "DEMO_Owner, Juan", "RRC-DEMO-1", "Registry return card"))).Value;
        served.Status.ShouldBe(NoticeStatus.Served);
        served.AppealDeadline.ShouldBe(c.Today.AddDays(60));
        (await c.Notices.CancelAsync(n.Id, "late")).Code.ShouldBe("NOTICE_NOT_CANCELLABLE");

        var html = (await c.Services.GetRequiredService<IFormService>().IssueAsync(new IssueFormRequest("NOTICE_OF_ASSESSMENT", n.Id)))
            .Value.Html.ShouldNotBeNull();
        html.ShouldContain("assessed for the first time");
        html.ShouldContain("60 days from the date of your receipt");
        html.ShouldContain("100,000.00");
    }

    [Fact]
    public async Task Reassessment_Notice_StatesIncreaseOrDecrease_AndIsNotRequiredWhenUnchanged()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;

        var unchanged = await ReassessAsync(c, assessedValue: null);
        (await c.Notices.GenerateAsync(new GenerateNoticeRequest(unchanged))).Code.ShouldBe("NOTICE_NOT_REQUIRED");

        var increased = await ReassessAsync(c, assessedValue: 120_000m);
        var notice = (await c.Notices.GenerateAsync(new GenerateNoticeRequest(increased))).Value;
        notice.Reason.ShouldBe(NoticeReason.AssessmentIncreased);
        notice.PreviousAssessedValue.ShouldBe(100_000m);
        notice.AssessedValue.ShouldBe(120_000m);
    }

    [Fact]
    public async Task Refusals_DraftAssessment_NoAddressee_AndDraftNoticeCannotBeIssuedAsForm()
    {
        var (c, tx) = await BeginAsync(withOwner: false);
        await using var _ = tx;

        (await c.Notices.GenerateAsync(new GenerateNoticeRequest(c.Seed.AssessmentId))).Code.ShouldBe("NOTICE_NO_ADDRESSEE");

        var assessment = await c.Db.Assessments.SingleAsync(x => x.Id == c.Seed.AssessmentId);
        assessment.Status = WorkflowStatus.Approved;
        await c.Db.SaveChangesAsync();
        (await c.Notices.GenerateAsync(new GenerateNoticeRequest(c.Seed.AssessmentId))).Code.ShouldBe("ASSESSMENT_NOT_POSTED");
    }

    [Fact]
    public async Task DraftNotice_PreviewsButIsNotIssuedAsForm_AndCanBeCancelled()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var n = (await c.Notices.GenerateAsync(new GenerateNoticeRequest(c.Seed.AssessmentId))).Value;
        var forms = c.Services.GetRequiredService<IFormService>();

        (await forms.PreviewAsync("NOTICE_OF_ASSESSMENT", n.Id)).Value.Html.ShouldContain("DRAFT — NOT ISSUED");
        (await forms.IssueAsync(new IssueFormRequest("NOTICE_OF_ASSESSMENT", n.Id))).Code.ShouldBe("FORM_SUBJECT_NOT_ISSUABLE");

        (await c.Notices.CancelAsync(n.Id, "DEMO: wrong addressee")).Value.Status.ShouldBe(NoticeStatus.Cancelled);
        (await c.Notices.GenerateAsync(new GenerateNoticeRequest(c.Seed.AssessmentId))).IsSuccess.ShouldBeTrue(); // a cancelled notice frees the assessment
    }

    // --- Step 5b (docs/analysis/mrpaao-forms-model.md §14) ---

    [Fact]
    public async Task CombinedNotice_ListsTheOwnersAssessments_AndPrintsTheManualsLayout()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var owner = await c.Db.PropertyTaxpayers.Where(x => x.PropertyId == c.Seed.PropertyId && x.IsCurrent).Select(x => x.TaxpayerId!.Value).SingleAsync();
        var sole = await c.Db.PropertyTaxpayers.Where(x => x.PropertyId == c.Seed.PropertyId).Select(x => x.OwnershipTypeId!.Value).FirstAsync();
        var other = await BillingFlowTests.SeedPostedAssessmentAsync(c.Services, c.Db, new DateOnly(2026, 1, 1));
        (await c.Services.GetRequiredService<ITaxpayerService>().AddOwnerAsync(
            new AddPropertyOwnerRequest(other.PropertyId, owner, sole, 100m, new DateOnly(2020, 1, 1)))).IsSuccess.ShouldBeTrue();

        var candidates = (await c.Notices.CandidatesAsync(owner)).Value;
        candidates.Select(x => x.AssessmentId).ShouldBe([c.Seed.AssessmentId, other.AssessmentId], ignoreOrder: true);

        var notice = await c.Notices.GenerateCombinedAsync(new GenerateCombinedNoticeRequest(owner, [c.Seed.AssessmentId, other.AssessmentId]));

        notice.IsSuccess.ShouldBeTrue(notice.IsSuccess ? null : notice.Message);
        notice.Value.Items.ShouldNotBeNull().Count.ShouldBe(2);
        notice.Value.AssessedValue.ShouldBe(200_000m); // the items' total
        notice.Value.AddresseeNames.ShouldContain("DEMO_Owner");
        notice.Value.AddresseeTaxpayerId.ShouldBe(owner);
        (await c.Notices.ListByPropertyAsync(other.PropertyId)).Value.ShouldContain(n => n.Id == notice.Value.Id); // shows on both properties
        (await c.Notices.CandidatesAsync(owner)).Value.ShouldBeEmpty(); // both now have a notice

        var form = await c.Services.GetRequiredService<IFormService>().PreviewAsync("NOTICE_OF_ASSESSMENT", notice.Value.Id);
        form.IsSuccess.ShouldBeTrue(form.IsSuccess ? null : form.Message);
        form.Value.Authority.ShouldBe(FormAuthority.Mrpaao);
        var html = form.Value.Html;
        html.ShouldContain("MRPAAO 2004, Attachment 10");
        html.ShouldContain(c.Seed.TaxDeclaration.TaxDeclarationNumber);
        html.ShouldContain(other.TaxDeclaration.TaxDeclarationNumber);
        html.ShouldContain("200,000.00");
        html.ShouldContain("Section 223 of RA 7160");
    }

    [Fact]
    public async Task CombinedNotice_RefusesAnAssessmentTheAddresseeDoesNotOwn()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var owner = await c.Db.PropertyTaxpayers.Where(x => x.PropertyId == c.Seed.PropertyId && x.IsCurrent).Select(x => x.TaxpayerId!.Value).SingleAsync();
        var stranger = await BillingFlowTests.SeedPostedAssessmentAsync(c.Services, c.Db, new DateOnly(2026, 1, 1));

        (await c.Notices.GenerateCombinedAsync(new GenerateCombinedNoticeRequest(owner, [c.Seed.AssessmentId, stranger.AssessmentId])))
            .Code.ShouldBe("NOTICE_ADDRESSEE_NOT_OWNER");
    }

    [Fact]
    public async Task UnchangedValue_TakesADescriptiveReason_ButNotADerivedOne()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var unchanged = await ReassessAsync(c, assessedValue: null);

        (await c.Notices.GenerateAsync(new GenerateNoticeRequest(unchanged, NoticeReason.AssessmentIncreased))).Code.ShouldBe("VALIDATION_FAILED");
        var notice = await c.Notices.GenerateAsync(new GenerateNoticeRequest(unchanged, NoticeReason.DeclaredOwnerChanged));

        notice.IsSuccess.ShouldBeTrue(notice.IsSuccess ? null : notice.Message);
        notice.Value.Reason.ShouldBe(NoticeReason.DeclaredOwnerChanged);
        notice.Value.Items.ShouldNotBeNull().ShouldHaveSingleItem().Reason.ShouldBe(NoticeReason.DeclaredOwnerChanged);
        (await c.Notices.GenerateAsync(new GenerateNoticeRequest(unchanged, NoticeReason.OwnerAddressChanged))).Code.ShouldBe("NOTICE_DUPLICATE"); // a draft is open
    }
}

