using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Forms;
using Prime.Application.Features.Notices;
using Prime.Application.Features.TaxDeclarations;
using Prime.Application.Features.Transactions;
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
/// Step L3-4 (docs/analysis/assessment-listing-exemptions.md §4.4): a pending court claim blocks cancellation (Q12);
/// Notices of Cancellation for a cancellation on the assessor's own motion and for a previous owner's cancelled
/// assessment (Q11); discovery summonses (Q10). DEMO data. Rolled back.
/// </summary>
public class CancellationAndDiscoveryTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, CurrentUserService User, AppUser A, AppUser B, BillingFlowTests.Seed Seed, DateOnly Today)
    {
        public ITransactionService Tx => Services.GetRequiredService<ITransactionService>();
        public ITaxDeclarationService Tds => Services.GetRequiredService<ITaxDeclarationService>();
        public INoticeOfCancellationService Notices => Services.GetRequiredService<INoticeOfCancellationService>();
        public IDiscoverySummonsService Discovery => Services.GetRequiredService<IDiscoverySummonsService>();
        public IFormService Forms => Services.GetRequiredService<IFormService>();
    }

    private async Task<(Ctx Ctx, IAsyncDisposable Transaction)> BeginAsync()
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        await db.ApprovalChains.Where(x => x.Status == WorkflowStatus.Approved)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null));
        await db.NumberingSchemes.Where(x => x.Status == WorkflowStatus.Approved)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null));
        var users = Enumerable.Range(0, 2).Select(i => new AppUser
        {
            SupabaseUserId = Guid.NewGuid(), DisplayName = $"DEMO User {(char)('A' + i)}", Email = $"demo-{Guid.NewGuid():N}@example.invalid",
        }).ToList();
        db.AppUsers.AddRange(users);
        await db.SaveChangesAsync();
        var user = scope.ServiceProvider.GetRequiredService<CurrentUserService>();
        user.AppUserId = null;
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, new DateOnly(2026, 1, 1));
        var today = scope.ServiceProvider.GetRequiredService<IClock>().Today;
        return (new Ctx(db, scope.ServiceProvider, user, users[0], users[1], seed, today), new Scoped(transaction, scope));
    }

    private sealed class Scoped(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    private static async Task<TransactionTypeDto> ApprovedTypeAsync(Ctx c, PropertyTransactionKind kind, bool motuProprio = false)
    {
        c.User.AppUserId = c.A.Id;
        var created = await c.Tx.CreateTypeAsync(new CreateTransactionTypeRequest("DEMO — not LAM", new DateOnly(2020, 1, 1), null,
            $"D{Guid.NewGuid():N}"[..8], $"DEMO {kind}", kind, null, null, [], CancelsMotuProprio: motuProprio));
        created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);
        c.User.AppUserId = c.B.Id;
        var approved = await TestSeed.AsCheckerAsync(c.Services, () => c.Tx.ApproveTypeAsync(created.Value.Id));
        approved.IsSuccess.ShouldBeTrue(approved.IsSuccess ? null : approved.Message);
        c.User.AppUserId = null;
        return approved.Value;
    }

    private static async Task<Taxpayer> PartyAsync(Ctx c, PropertyPartyRole role, string name, string address, DateOnly start, bool current = true)
    {
        var taxpayer = new Taxpayer { TaxpayerType = TaxpayerType.Individual, LastName = name, FirstName = "DEMO", Address = address };
        c.Db.Add(taxpayer);
        OwnershipType? type = null;
        if (role == PropertyPartyRole.Owner)
        {
            type = new OwnershipType { Code = $"OT{Guid.NewGuid():N}"[..8], Name = "DEMO_Sole" };
            c.Db.Add(type);
        }
        c.Db.PropertyTaxpayers.Add(new PropertyTaxpayer
        {
            PropertyId = c.Seed.PropertyId, Taxpayer = taxpayer, Role = role, OwnershipType = type, OwnershipPercentage = role == PropertyPartyRole.Owner ? 100 : 0,
            StartDate = start, IsCurrent = current, EndDate = current ? null : new DateOnly(2025, 12, 31),
        });
        await c.Db.SaveChangesAsync();
        return taxpayer;
    }

    [Fact]
    public async Task APendingCourtClaim_BlocksEveryCancellation_UntilLifted()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var claim = new AnnotationType { Code = $"AN{Guid.NewGuid():N}"[..8], Name = "DEMO Adverse claim pending in court", BlocksCancellation = true };
        c.Db.Add(claim);
        await c.Db.SaveChangesAsync();
        var td = c.Seed.TaxDeclaration.Id;
        var annotation = await c.Tds.AddAnnotationAsync(td, new AddTaxDeclarationAnnotationRequest(claim.Id, "DEMO Civil Case 002", "CC-002", null, new DateOnly(2026, 1, 1)));
        annotation.IsSuccess.ShouldBeTrue(annotation.IsSuccess ? null : annotation.Message);

        var direct = await c.Tds.RequestCancellationAsync(td, "DEMO");
        direct.Code.ShouldBe(CancellationGuard.Code);
        direct.Message!.ShouldContain("DEMO Adverse claim pending in court");
        var type = await ApprovedTypeAsync(c, PropertyTransactionKind.Cancellation);
        (await c.Tx.OpenAsync(new OpenTransactionRequest(type.Id, c.Seed.PropertyId, c.Today, "DEMO", CancelTaxDeclarationIds: [td]))).Code.ShouldBe(CancellationGuard.Code);
        // A TD replacing it cannot be approved either.
        var replacing = await c.Tds.CreateAsync(new CreateTaxDeclarationRequest(c.Seed.RpuId, $"DEMO-TD-{Guid.NewGuid():N}"[..24], new DateOnly(2027, 1, 1),
            Taxability.Taxable, c.Seed.ClassificationId, c.Seed.TaxDeclaration.ActualUseId, null, 2027, td, "DEMO"));
        (await c.Tds.SubmitForReviewAsync(replacing.Value.Id)).IsSuccess.ShouldBeTrue();
        (await TestSeed.AsCheckerAsync(c.Services, () => c.Tds.ApproveAsync(replacing.Value.Id))).Code.ShouldBe(CancellationGuard.Code);

        (await c.Tds.LiftAnnotationAsync(annotation.Value.Id, new LiftTaxDeclarationAnnotationRequest("DEMO resolved by the court", "CC-002 decision"))).IsSuccess.ShouldBeTrue();
        (await TestSeed.AsCheckerAsync(c.Services, () => c.Tds.ApproveAsync(replacing.Value.Id))).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task AMotuProprioCancellation_GivesANoticePerAddress_IssuedAndServed()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        await PartyAsync(c, PropertyPartyRole.Owner, "DEMO_Declarant", "DEMO Address 1", new DateOnly(2020, 1, 1));
        await PartyAsync(c, PropertyPartyRole.LegalInterestHolder, "DEMO_Mortgagee", "DEMO Address 2", new DateOnly(2021, 1, 1));
        var type = await ApprovedTypeAsync(c, PropertyTransactionKind.Cancellation, motuProprio: true);

        c.User.AppUserId = c.A.Id;
        var opened = await c.Tx.OpenAsync(new OpenTransactionRequest(type.Id, c.Seed.PropertyId, c.Today, "DEMO gross illegality of the assessment",
            CancelTaxDeclarationIds: [c.Seed.TaxDeclaration.Id]));
        (await c.Tx.SubmitAsync(opened.Value.Id)).IsSuccess.ShouldBeTrue();
        c.User.AppUserId = c.B.Id;
        (await TestSeed.AsCheckerAsync(c.Services, () => c.Tx.ApproveAsync(opened.Value.Id))).IsSuccess.ShouldBeTrue();
        c.User.AppUserId = null;

        var notices = (await c.Notices.ListByPropertyAsync(c.Seed.PropertyId)).Value;
        notices.Count.ShouldBe(2);
        notices.ShouldAllBe(n => n.Ground == CancellationNoticeGround.MotuProprio && n.Status == NoticeStatus.Draft
            && n.TaxDeclarationNumber == c.Seed.TaxDeclaration.TaxDeclarationNumber && n.Reason.Contains("gross illegality"));
        notices.Select(n => n.AddresseeAddress).Order().ShouldBe(["DEMO Address 1", "DEMO Address 2"]);
        notices.Single(n => n.AddresseeAddress == "DEMO Address 2").AddresseeNames.ShouldContain("(Person with legal interest)");

        var preview = await c.Forms.PreviewAsync("NOTICE_OF_CANCELLATION", notices[0].Id);
        preview.IsSuccess.ShouldBeTrue(preview.IsSuccess ? null : preview.Message);
        preview.Value.Html.ShouldContain("NOTICE OF CANCELLATION");
        preview.Value.Html.ShouldContain(c.Seed.TaxDeclaration.TaxDeclarationNumber);

        var issued = await c.Notices.IssueAsync(notices[0].Id);
        issued.Value.Status.ShouldBe(NoticeStatus.Issued);
        var served = await c.Notices.RecordServiceAsync(notices[0].Id, new RecordNoticeServiceRequest(NoticeServiceMode.Personal, c.Today, "DEMO_Declarant", "DEMO receiving copy", null));
        served.Value.Status.ShouldBe(NoticeStatus.Served);
    }

    [Fact]
    public async Task AReassessmentReplacingAPreviousOwnersDeclaration_NotifiesThatOwnerOnly()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        await PartyAsync(c, PropertyPartyRole.Owner, "DEMO_Former", "DEMO Old Address", new DateOnly(2020, 1, 1), current: false);
        await PartyAsync(c, PropertyPartyRole.Owner, "DEMO_Present", "DEMO New Address", new DateOnly(2026, 1, 1));
        var old = await c.Db.TaxDeclarations.SingleAsync(x => x.Id == c.Seed.TaxDeclaration.Id);
        old.AssessmentId = c.Seed.AssessmentId;
        var seedAssessment = await c.Db.Assessments.AsNoTracking().Include(x => x.Lines).SingleAsync(x => x.Id == c.Seed.AssessmentId);
        var reassessment = new Assessment
        {
            RpuId = seedAssessment.RpuId, PropertyId = seedAssessment.PropertyId, ValuationId = seedAssessment.ValuationId, AssessmentYear = 2027,
            MarketValue = seedAssessment.MarketValue, AssessedValue = seedAssessment.AssessedValue, Status = WorkflowStatus.Posted,
            EffectiveDate = new DateOnly(2027, 1, 1), PreviousAssessmentId = seedAssessment.Id,
            Lines = seedAssessment.Lines.Select(l => new AssessmentLine
            {
                Sequence = l.Sequence, ClassificationId = l.ClassificationId, ActualUseId = l.ActualUseId, PropertyTypeId = l.PropertyTypeId,
                MarketValue = l.MarketValue, AssessmentLevelId = l.AssessmentLevelId, AssessmentPercentage = l.AssessmentPercentage, AssessedValue = l.AssessedValue,
            }).ToList(),
        };
        c.Db.Add(reassessment);
        await c.Db.SaveChangesAsync();

        var td = await c.Tds.CreateAsync(new CreateTaxDeclarationRequest(c.Seed.RpuId, $"DEMO-TD-{Guid.NewGuid():N}"[..24], new DateOnly(2027, 1, 1),
            Taxability.Taxable, c.Seed.ClassificationId, c.Seed.TaxDeclaration.ActualUseId, null, 2027, old.Id, "DEMO", AssessmentId: reassessment.Id));
        td.IsSuccess.ShouldBeTrue(td.IsSuccess ? null : td.Message);
        (await c.Tds.SubmitForReviewAsync(td.Value.Id)).IsSuccess.ShouldBeTrue();
        var approved = await TestSeed.AsCheckerAsync(c.Services, () => c.Tds.ApproveAsync(td.Value.Id));
        approved.IsSuccess.ShouldBeTrue(approved.IsSuccess ? null : approved.Message);

        var notice = (await c.Notices.ListByPropertyAsync(c.Seed.PropertyId)).Value.ShouldHaveSingleItem();
        (notice.Ground, notice.AddresseeAddress, notice.ReplacedByTaxDeclarationId).ShouldBe((CancellationNoticeGround.PreviousOwner, "DEMO Old Address", (Guid?)td.Value.Id));
        notice.AddresseeNames.ShouldContain("DEMO_FORMER", Case.Insensitive);
    }

    [Fact]
    public async Task Discovery_TwoSummonses_ThenTheVerification()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var type = await ApprovedTypeAsync(c, PropertyTransactionKind.NewDiscovery);
        var other = await ApprovedTypeAsync(c, PropertyTransactionKind.Cancellation);
        var t = c.Today;
        var opened = await c.Tx.OpenAsync(new OpenTransactionRequest(type.Id, c.Seed.PropertyId, t, "DEMO undeclared building found"));
        var wrong = await c.Tx.OpenAsync(new OpenTransactionRequest(other.Id, c.Seed.PropertyId, t, "DEMO"));
        (await c.Discovery.IssueAsync(wrong.Value.Id, new IssueSummonsRequest("DEMO Owner", null, null, null))).Code.ShouldBe("SUMMONS_NOT_ALLOWED");

        var first = (await c.Discovery.IssueAsync(opened.Value.Id, new IssueSummonsRequest("DEMO Owner", "DEMO Address", null, t.AddDays(-60)))).Value.Summonses.Single();
        (await c.Discovery.IssueAsync(opened.Value.Id, new IssueSummonsRequest("DEMO Owner", null, null, null))).Code.ShouldBe("SUMMONS_NOT_ALLOWED");
        (await c.Discovery.RecordOutcomeAsync(first.Id, new SummonsOutcomeRequest(SummonsOutcome.NotComplied, t, null))).Code.ShouldBe("SUMMONS_NOT_SERVED");
        var served = (await c.Discovery.RecordServiceAsync(first.Id, new RecordSummonsServiceRequest(NoticeServiceMode.Personal, t.AddDays(-58), "DEMO Owner", "DEMO receiving copy", null))).Value;
        served.Summonses[0].DueDate.ShouldBe(t.AddDays(-58 + 15));
        (await c.Discovery.RecordOutcomeAsync(first.Id, new SummonsOutcomeRequest(SummonsOutcome.NotComplied, t.AddDays(-45), null))).Code.ShouldBe("SUMMONS_PERIOD_RUNNING");
        (await c.Discovery.RecordOutcomeAsync(first.Id, new SummonsOutcomeRequest(SummonsOutcome.NotComplied, t.AddDays(-40), "DEMO no reply"))).IsSuccess.ShouldBeTrue();
        (await c.Discovery.RecordVerificationAsync(opened.Value.Id, new VerificationRequest("DEMO"))).Code.ShouldBe("VERIFICATION_NOT_DUE");

        var second = (await c.Discovery.IssueAsync(opened.Value.Id, new IssueSummonsRequest("DEMO Owner", "DEMO Address", null, t.AddDays(-39)))).Value.Summonses[1];
        second.Sequence.ShouldBe(2);
        await c.Discovery.RecordServiceAsync(second.Id, new RecordSummonsServiceRequest(NoticeServiceMode.RegisteredMail, t.AddDays(-35), "DEMO Owner", "DEMO return card", null));
        (await c.Discovery.RecordOutcomeAsync(second.Id, new SummonsOutcomeRequest(SummonsOutcome.NotComplied, t.AddDays(-19), null))).IsSuccess.ShouldBeTrue();
        (await c.Discovery.IssueAsync(opened.Value.Id, new IssueSummonsRequest("DEMO Owner", null, null, null))).Code.ShouldBe("SUMMONS_NOT_ALLOWED");

        var verified = (await c.Discovery.RecordVerificationAsync(opened.Value.Id, new VerificationRequest("DEMO Registry of Deeds and DEMO BIR records checked"))).Value;
        (verified.InterAgencyVerification, verified.CanIssue).ShouldBe(("DEMO Registry of Deeds and DEMO BIR records checked", false));
        verified.NextStep!.ShouldContain("declares the property");

        var preview = await c.Forms.PreviewAsync("DISCOVERY_SUMMONS", second.Id);
        preview.IsSuccess.ShouldBeTrue(preview.IsSuccess ? null : preview.Message);
        preview.Value.Html.ShouldContain("SUMMONS (SECOND)");
        preview.Value.Html.ShouldContain("within <b>15</b> days");
    }
}
