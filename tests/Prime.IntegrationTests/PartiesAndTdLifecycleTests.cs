using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Approvals;
using Prime.Application.Features.Forms;
using Prime.Application.Features.Properties;
using Prime.Application.Features.TaxDeclarations;
using Prime.Application.Features.Taxpayers;
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
/// docs/FORMS-REVISION-PLAN.md §5 A4: statutory party capacities (LGC
/// §§204–205), the Tax Declaration lifecycle with "cancels / cancelled by",
/// and TD annotations. Rolled-back-transaction pattern; names and texts are
/// DEMO test data.
/// </summary>
public class PartiesAndTdLifecycleTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, CurrentUserService User, AppUser A, AppUser B, BillingFlowTests.Seed Seed);

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
        return (new Ctx(db, scope.ServiceProvider, user, users[0], users[1], seed), new Scoped(transaction, scope));
    }

    private sealed class Scoped(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    private static async Task<Taxpayer> TaxpayerAsync(PrimeDbContext db, string last)
    {
        var t = new Taxpayer { TaxpayerType = TaxpayerType.Individual, LastName = last, FirstName = "DEMO" };
        db.Taxpayers.Add(t);
        await db.SaveChangesAsync();
        return t;
    }

    private static async Task<OwnershipType> OwnershipTypeAsync(PrimeDbContext db)
    {
        var o = new OwnershipType { Code = $"OT{Guid.NewGuid():N}"[..8], Name = "DEMO_Sole" };
        db.OwnershipTypes.Add(o);
        await db.SaveChangesAsync();
        return o;
    }

    // --- Parties (LGC §§204–205) ---

    [Fact]
    public async Task UnknownOwner_IsDeclaredWithoutTaxpayer_AndEndedWhenOwnerIdentified()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var parties = c.Services.GetRequiredService<ITaxpayerService>();
        var propertyId = c.Seed.PropertyId;

        var unknown = await parties.AddOwnerAsync(new AddPropertyOwnerRequest(propertyId, null, null, 0m, new DateOnly(2025, 1, 1), PropertyPartyRole.UnknownOwner));
        unknown.IsSuccess.ShouldBeTrue(unknown.IsSuccess ? null : unknown.Message);
        unknown.Value.TaxpayerDisplayName.ShouldBe(PropertyParties.UnknownOwnerName);
        (await parties.AddOwnerAsync(new AddPropertyOwnerRequest(propertyId, null, null, 0m, new DateOnly(2025, 2, 1), PropertyPartyRole.UnknownOwner)))
            .Code.ShouldBe("UNKNOWN_OWNER_ALREADY_DECLARED");

        var owner = await TaxpayerAsync(c.Db, "DEMO_Identified");
        var type = await OwnershipTypeAsync(c.Db);
        (await parties.AddOwnerAsync(new AddPropertyOwnerRequest(propertyId, owner.Id, type.Id, 100m, new DateOnly(2026, 3, 1)))).IsSuccess.ShouldBeTrue();

        var history = (await parties.GetOwnershipHistoryAsync(propertyId)).Value;
        var ended = history.Single(h => h.Role == PropertyPartyRole.UnknownOwner);
        ended.IsCurrent.ShouldBeFalse();
        ended.EndDate.ShouldBe(new DateOnly(2026, 2, 28));
        ended.EndReason.ShouldBe("Owner identified");
        history.Single(h => h.IsCurrent).TaxpayerDisplayName.ShouldContain("DEMO_Identified");

        (await parties.AddOwnerAsync(new AddPropertyOwnerRequest(propertyId, null, null, 0m, new DateOnly(2026, 4, 1), PropertyPartyRole.UnknownOwner)))
            .Code.ShouldBe("PROPERTY_HAS_KNOWN_OWNER");
    }

    [Fact]
    public async Task RoleRules_AreValidated()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var parties = c.Services.GetRequiredService<ITaxpayerService>();
        var person = await TaxpayerAsync(c.Db, "DEMO_Person");

        (await parties.AddOwnerAsync(new AddPropertyOwnerRequest(c.Seed.PropertyId, person.Id, null, 0m, new DateOnly(2026, 1, 1), PropertyPartyRole.UnknownOwner)))
            .Code.ShouldBe("VALIDATION_FAILED"); // an unknown owner has no taxpayer
        (await parties.AddOwnerAsync(new AddPropertyOwnerRequest(c.Seed.PropertyId, person.Id, null, 50m, new DateOnly(2026, 1, 1))))
            .Code.ShouldBe("VALIDATION_FAILED"); // an owner needs an ownership type
    }

    [Fact]
    public async Task Administrator_HasNoOwnershipType_AndDoesNotCountTowardOwnership_AndCanBeEnded()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var parties = c.Services.GetRequiredService<ITaxpayerService>();
        var owner = await TaxpayerAsync(c.Db, "DEMO_Estate");
        var admin = await TaxpayerAsync(c.Db, "DEMO_Administrator");
        var type = await OwnershipTypeAsync(c.Db);

        (await parties.AddOwnerAsync(new AddPropertyOwnerRequest(c.Seed.PropertyId, owner.Id, type.Id, 100m, new DateOnly(2026, 1, 1)))).IsSuccess.ShouldBeTrue();
        var added = await parties.AddOwnerAsync(new AddPropertyOwnerRequest(c.Seed.PropertyId, admin.Id, null, 0m, new DateOnly(2026, 1, 1), PropertyPartyRole.Administrator));
        added.IsSuccess.ShouldBeTrue(added.IsSuccess ? null : added.Message);
        added.Value.OwnershipTypeName.ShouldBeNull();

        var ended = await parties.EndPartyAsync(added.Value.PropertyTaxpayerId, new EndPropertyPartyRequest(new DateOnly(2026, 6, 30), "DEMO: estate settled"));
        ended.Value.IsCurrent.ShouldBeFalse();
        ended.Value.EndReason.ShouldBe("DEMO: estate settled");
        (await parties.EndPartyAsync(added.Value.PropertyTaxpayerId, new EndPropertyPartyRequest(new DateOnly(2026, 7, 1), "again")))
            .Code.ShouldBe("PROPERTY_PARTY_ALREADY_ENDED");
    }

    // --- Tax Declaration lifecycle ---

    private CreateTaxDeclarationRequest Td(Ctx c, Guid? previous) =>
        new(c.Seed.RpuId, $"DEMO-TD-{Guid.NewGuid():N}"[..24], new DateOnly(2027, 1, 1), Taxability.Taxable, c.Seed.ClassificationId,
            c.Seed.TaxDeclaration.ActualUseId, null, 2027, previous, "DEMO revision");

    [Fact]
    public async Task ApprovingReplacementTd_CancelsThePreviousOne_AndBothFormsSaySo()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var tds = c.Services.GetRequiredService<ITaxDeclarationService>();
        var current = c.Seed.TaxDeclaration; // Approved by the seed

        c.User.AppUserId = c.A.Id;
        var orphan = (await tds.CreateAsync(Td(c, previous: null))).Value;
        (await tds.SubmitForReviewAsync(orphan.Id)).IsSuccess.ShouldBeTrue();
        (await tds.ApproveAsync(orphan.Id)).Code.ShouldBe("CANNOT_APPROVE_OWN_TAX_DECLARATION");
        c.User.AppUserId = c.B.Id;
        (await tds.ApproveAsync(orphan.Id)).Code.ShouldBe("TAX_DECLARATION_CURRENT_EXISTS");

        c.User.AppUserId = c.A.Id;
        var replacement = (await tds.CreateAsync(Td(c, previous: current.Id))).Value;
        await tds.SubmitForReviewAsync(replacement.Id);
        c.User.AppUserId = c.B.Id;
        var approved = await tds.ApproveAsync(replacement.Id);

        approved.IsSuccess.ShouldBeTrue(approved.IsSuccess ? null : approved.Message);
        approved.Value.Status.ShouldBe(WorkflowStatus.Approved);
        approved.Value.ApprovedBy.ShouldBe(c.B.Id);
        var old = (await tds.GetByIdAsync(current.Id)).Value;
        old.Status.ShouldBe(WorkflowStatus.Cancelled);
        old.SupersededByTaxDeclarationId.ShouldBe(replacement.Id);
        old.CancellationReason.ShouldBe($"Cancelled by TD No. {replacement.TaxDeclarationNumber}.");

        var forms = c.Services.GetRequiredService<IFormService>();
        var oldForm = (await forms.IssueAsync(new IssueFormRequest("TAX_DECLARATION", current.Id))).Value.Html.ShouldNotBeNull();
        oldForm.ShouldContain($"CANCELLED BY TD No. {replacement.TaxDeclarationNumber}");
        var newForm = (await forms.IssueAsync(new IssueFormRequest("TAX_DECLARATION", replacement.Id))).Value.Html.ShouldNotBeNull();
        newForm.ShouldContain($"Cancels TD No.</th><td>{current.TaxDeclarationNumber}");

        (await tds.CreateAsync(Td(c, previous: current.Id))).Code.ShouldBe("PREVIOUS_TAX_DECLARATION_CANCELLED");
    }

    [Fact]
    public async Task Reject_AndOutrightCancel()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var tds = c.Services.GetRequiredService<ITaxDeclarationService>();

        c.User.AppUserId = c.A.Id;
        var draft = (await tds.CreateAsync(Td(c, c.Seed.TaxDeclaration.Id))).Value;
        await tds.SubmitForReviewAsync(draft.Id);
        c.User.AppUserId = c.B.Id;
        var rejected = (await tds.RejectAsync(draft.Id, "DEMO: wrong classification")).Value;
        rejected.Status.ShouldBe(WorkflowStatus.Rejected);
        rejected.CancellationReason.ShouldBe("DEMO: wrong classification");
        (await tds.GetByIdAsync(c.Seed.TaxDeclaration.Id)).Value.Status.ShouldBe(WorkflowStatus.Approved); // untouched

        (await tds.CancelAsync(draft.Id, "x")).Code.ShouldBe("TAX_DECLARATION_NOT_APPROVED");
        var cancelled = (await tds.CancelAsync(c.Seed.TaxDeclaration.Id, "DEMO: duplicate declaration")).Value;
        cancelled.Status.ShouldBe(WorkflowStatus.Cancelled);
        cancelled.CancelledAt.ShouldNotBeNull();
        cancelled.SupersededByTaxDeclarationId.ShouldBeNull();
    }

    [Fact]
    public async Task TdApproval_FollowsAConfiguredChain()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var chains = c.Services.GetRequiredService<IApprovalChainService>();
        c.User.AppUserId = c.A.Id;
        var chain = (await chains.CreateAsync(new CreateApprovalChainRequest("DEMO", new DateOnly(2020, 1, 1), null, ApprovalSubjectType.TaxDeclaration,
            "DEMO TD chain", [new ApprovalStepRequest(1, "RECOMMENDED_BY", "DEMO Recommended", null), new ApprovalStepRequest(2, "APPROVED_BY", "DEMO Approved", null)]))).Value;
        c.User.AppUserId = c.B.Id;
        (await chains.ApproveAsync(chain.Id)).IsSuccess.ShouldBeTrue();

        var tds = c.Services.GetRequiredService<ITaxDeclarationService>();
        c.User.AppUserId = null;
        var td = (await tds.CreateAsync(Td(c, c.Seed.TaxDeclaration.Id))).Value;
        await tds.SubmitForReviewAsync(td.Id);

        c.User.AppUserId = c.A.Id;
        (await tds.ApproveAsync(td.Id)).Value.Status.ShouldBe(WorkflowStatus.PendingReview);
        (await tds.GetByIdAsync(c.Seed.TaxDeclaration.Id)).Value.Status.ShouldBe(WorkflowStatus.Approved); // not yet replaced
        c.User.AppUserId = c.B.Id;
        (await tds.ApproveAsync(td.Id)).Value.Status.ShouldBe(WorkflowStatus.Approved);
        (await tds.GetByIdAsync(c.Seed.TaxDeclaration.Id)).Value.Status.ShouldBe(WorkflowStatus.Cancelled);
    }

    // --- Annotations ---

    [Fact]
    public async Task Annotation_IsAddedAndLifted_NeverDeleted_AndPrinted()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var levy = new AnnotationType { Code = $"AN{Guid.NewGuid():N}"[..8], Name = "DEMO Levy" };
        c.Db.AnnotationTypes.Add(levy);
        await c.Db.SaveChangesAsync();
        var tds = c.Services.GetRequiredService<ITaxDeclarationService>();
        var tdId = c.Seed.TaxDeclaration.Id;

        var added = await tds.AddAnnotationAsync(tdId, new AddTaxDeclarationAnnotationRequest(levy.Id, "DEMO warrant of levy", "WL-DEMO-1", new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 2)));
        added.IsSuccess.ShouldBeTrue(added.IsSuccess ? null : added.Message);
        (await tds.GetByIdAsync(tdId)).Value.ActiveAnnotationCount.ShouldBe(1);

        var lifted = (await tds.LiftAnnotationAsync(added.Value.Id, new LiftTaxDeclarationAnnotationRequest("DEMO: taxes paid", "OR-DEMO-9"))).Value;
        lifted.LiftedAt.ShouldNotBeNull();
        (await tds.LiftAnnotationAsync(added.Value.Id, new LiftTaxDeclarationAnnotationRequest("again", null))).Code.ShouldBe("ANNOTATION_ALREADY_LIFTED");
        (await tds.ListAnnotationsAsync(tdId)).Value.Count.ShouldBe(1);
        (await tds.GetByIdAsync(tdId)).Value.ActiveAnnotationCount.ShouldBe(0);

        var html = (await c.Services.GetRequiredService<IFormService>().IssueAsync(new IssueFormRequest("TAX_DECLARATION", tdId))).Value.Html.ShouldNotBeNull();
        html.ShouldContain("DEMO warrant of levy");
        html.ShouldContain("lifted");

        await tds.CancelAsync(tdId, "DEMO");
        (await tds.AddAnnotationAsync(tdId, new AddTaxDeclarationAnnotationRequest(levy.Id, "late", null, null, new DateOnly(2026, 6, 1))))
            .Code.ShouldBe("TAX_DECLARATION_NOT_ANNOTATABLE");
    }
}
