using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Numbering;
using Prime.Application.Features.TaxDeclarations;
using Prime.Application.Features.Taxpayers;
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
/// docs/FORMS-REVISION-PLAN.md A5 — property transactions (CLAUDE.md §34–§37).
/// Every code, label and requirement is DEMO test data, not the LAM's.
/// </summary>
public class PropertyTransactionTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, CurrentUserService User, AppUser A, AppUser B, BillingFlowTests.Seed Seed)
    {
        public ITransactionService Tx => Services.GetRequiredService<ITransactionService>();
        public ITaxDeclarationService Tds => Services.GetRequiredService<ITaxDeclarationService>();
    }

    private async Task<(Ctx Ctx, IAsyncDisposable Transaction)> BeginAsync()
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        // Retire approved configuration left in the shared dev DB (rolled back with the test).
        await db.ApprovalChains.Where(x => x.Status == WorkflowStatus.Approved)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null));
        await db.NumberingSchemes.Where(x => x.Status == WorkflowStatus.Approved)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null));
        await db.TransactionTypes.Where(x => x.Status == WorkflowStatus.Approved)
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

    private static async Task<TransactionTypeDto> ApprovedTypeAsync(Ctx c, PropertyTransactionKind kind, params TransactionRequirementRequest[] requirements)
    {
        c.User.AppUserId = c.A.Id;
        var created = await c.Tx.CreateTypeAsync(new CreateTransactionTypeRequest(
            "DEMO — not LAM", new DateOnly(2020, 1, 1), null, $"D{Guid.NewGuid():N}"[..8], $"DEMO {kind}", kind, 1, null, requirements));
        created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);
        (await c.Tx.ApproveTypeAsync(created.Value.Id)).Code.ShouldBe("CANNOT_APPROVE_OWN_TRANSACTION_TYPE");
        c.User.AppUserId = c.B.Id;
        var approved = await c.Tx.ApproveTypeAsync(created.Value.Id);
        approved.IsSuccess.ShouldBeTrue(approved.IsSuccess ? null : approved.Message);
        c.User.AppUserId = null;
        return approved.Value;
    }

    private static async Task<(Taxpayer Buyer, OwnershipType Type)> PartiesAsync(Ctx c)
    {
        var seller = new Taxpayer { TaxpayerType = TaxpayerType.Individual, LastName = "DEMO_Seller", FirstName = "DEMO" };
        var buyer = new Taxpayer { TaxpayerType = TaxpayerType.Individual, LastName = "DEMO_Buyer", FirstName = "DEMO" };
        var type = new OwnershipType { Code = $"OT{Guid.NewGuid():N}"[..8], Name = "DEMO_Sole" };
        c.Db.AddRange(seller, buyer, type);
        await c.Db.SaveChangesAsync();
        (await c.Services.GetRequiredService<ITaxpayerService>().AddOwnerAsync(
            new AddPropertyOwnerRequest(c.Seed.PropertyId, seller.Id, type.Id, 100m, new DateOnly(2020, 1, 1)))).IsSuccess.ShouldBeTrue();
        return (buyer, type);
    }

    private static CreateTaxDeclarationRequest Td(Ctx c, Guid? previous, Guid? transactionId) =>
        new(c.Seed.RpuId, $"DEMO-TD-{Guid.NewGuid():N}"[..24], new DateOnly(2026, 7, 1), Taxability.Taxable, c.Seed.ClassificationId,
            c.Seed.TaxDeclaration.ActualUseId, null, 2027, previous, "DEMO", transactionId);

    [Fact]
    public async Task Transfer_EndsOldOwner_StartsNewOwner_AndReplacesTheTd_InOneApproval()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var type = await ApprovedTypeAsync(c, PropertyTransactionKind.Transfer,
            new TransactionRequirementRequest(1, "TRANSFER_TAX", "DEMO proof of transfer tax payment", true, "LGC §135(b)"),
            new TransactionRequirementRequest(2, "PHOTO", "DEMO optional photo", false, null));
        var (buyer, ownershipType) = await PartiesAsync(c);

        c.User.AppUserId = c.A.Id;
        var opened = await c.Tx.OpenAsync(new OpenTransactionRequest(type.Id, c.Seed.PropertyId, new DateOnly(2026, 7, 1), "DEMO sale",
            [new NewPartyRequest(PropertyPartyRole.Owner, buyer.Id, ownershipType.Id, 100m)]));
        opened.IsSuccess.ShouldBeTrue(opened.IsSuccess ? null : opened.Message);
        var t = opened.Value;
        t.Requirements.Count.ShouldBe(2);

        var newTd = (await c.Tds.CreateAsync(Td(c, c.Seed.TaxDeclaration.Id, t.Id))).Value;
        newTd.PropertyTransactionId.ShouldBe(t.Id);
        (await c.Tds.SubmitForReviewAsync(newTd.Id)).Code.ShouldBe("TAX_DECLARATION_IN_TRANSACTION");

        (await c.Tx.SubmitAsync(t.Id)).Code.ShouldBe("TRANSACTION_REQUIREMENTS_UNMET"); // only the mandatory one blocks
        var requirement = t.Requirements.Single(r => r.Code == "TRANSFER_TAX");
        (await c.Tx.SatisfyRequirementAsync(t.Id, requirement.Id, new SatisfyRequirementRequest("OR-DEMO-555", null))).IsSuccess.ShouldBeTrue();
        (await c.Tx.SubmitAsync(t.Id)).Value.Status.ShouldBe(WorkflowStatus.PendingReview);
        (await c.Tds.GetByIdAsync(newTd.Id)).Value.Status.ShouldBe(WorkflowStatus.PendingReview);

        (await c.Tx.ApproveAsync(t.Id)).Code.ShouldBe("CANNOT_APPROVE_OWN_PROPERTY_TRANSACTION");
        c.User.AppUserId = c.B.Id;
        var approved = await c.Tx.ApproveAsync(t.Id);
        approved.IsSuccess.ShouldBeTrue(approved.IsSuccess ? null : approved.Message);
        approved.Value.Status.ShouldBe(WorkflowStatus.Approved);

        // TDs: the new one is current; the one it replaces is cancelled by it.
        (await c.Tds.GetByIdAsync(newTd.Id)).Value.Status.ShouldBe(WorkflowStatus.Approved);
        var old = (await c.Tds.GetByIdAsync(c.Seed.TaxDeclaration.Id)).Value;
        old.Status.ShouldBe(WorkflowStatus.Cancelled);
        old.SupersededByTaxDeclarationId.ShouldBe(newTd.Id);

        // Ownership: history preserved, both ends pointing at the transaction (CLAUDE.md §35).
        var links = await c.Db.PropertyTaxpayers.Include(x => x.Taxpayer).Where(x => x.PropertyId == c.Seed.PropertyId).ToListAsync();
        var sold = links.Single(x => x.Taxpayer!.LastName == "DEMO_Seller");
        sold.IsCurrent.ShouldBeFalse();
        sold.EndDate.ShouldBe(new DateOnly(2026, 6, 30));
        sold.EndedByTransactionId.ShouldBe(t.Id);
        var bought = links.Single(x => x.Taxpayer!.LastName == "DEMO_Buyer");
        bought.IsCurrent.ShouldBeTrue();
        bought.StartDate.ShouldBe(new DateOnly(2026, 7, 1));
        bought.StartedByTransactionId.ShouldBe(t.Id);
    }

    [Fact]
    public async Task Transfer_WithSharesNotTotalling100_CannotBeSubmitted()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var type = await ApprovedTypeAsync(c, PropertyTransactionKind.Transfer);
        var (buyer, ownershipType) = await PartiesAsync(c);
        var t = (await c.Tx.OpenAsync(new OpenTransactionRequest(type.Id, c.Seed.PropertyId, new DateOnly(2026, 7, 1), "DEMO partial",
            [new NewPartyRequest(PropertyPartyRole.Owner, buyer.Id, ownershipType.Id, 60m)]))).Value;

        (await c.Tx.SubmitAsync(t.Id)).Code.ShouldBe("TRANSFER_PARTIES_INVALID");
    }

    [Fact]
    public async Task Transfer_EffectiveOnOrBeforeTheCurrentOwnersStart_IsRefusedAtSubmit()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var type = await ApprovedTypeAsync(c, PropertyTransactionKind.Transfer);
        var (buyer, ownershipType) = await PartiesAsync(c); // the seller's ownership starts 2020-01-01
        var t = (await c.Tx.OpenAsync(new OpenTransactionRequest(type.Id, c.Seed.PropertyId, new DateOnly(2020, 1, 1), "DEMO same-day",
            [new NewPartyRequest(PropertyPartyRole.Owner, buyer.Id, ownershipType.Id, 100m)]))).Value;

        (await c.Tx.SubmitAsync(t.Id)).Code.ShouldBe("TRANSFER_EFFECTIVE_DATE_CONFLICT");
    }

    [Fact]
    public async Task Cancellation_CancelsTheListedTd_WithNoSuccessor()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var type = await ApprovedTypeAsync(c, PropertyTransactionKind.Cancellation);
        c.User.AppUserId = c.A.Id;
        var t = (await c.Tx.OpenAsync(new OpenTransactionRequest(type.Id, c.Seed.PropertyId, new DateOnly(2026, 8, 1), "DEMO duplicate declaration",
            CancelTaxDeclarationIds: [c.Seed.TaxDeclaration.Id]))).Value;
        (await c.Tx.SubmitAsync(t.Id)).IsSuccess.ShouldBeTrue();
        c.User.AppUserId = c.B.Id;
        (await c.Tx.ApproveAsync(t.Id)).IsSuccess.ShouldBeTrue();

        var td = (await c.Tds.GetByIdAsync(c.Seed.TaxDeclaration.Id)).Value;
        td.Status.ShouldBe(WorkflowStatus.Cancelled);
        td.SupersededByTaxDeclarationId.ShouldBeNull();
        td.CancellationReason.ShouldNotBeNull().ShouldContain("Cancelled by transaction");
    }

    [Fact]
    public async Task Rejecting_RejectsItsTds_AndLeavesEverythingElseUntouched()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var type = await ApprovedTypeAsync(c, PropertyTransactionKind.Reassessment);
        c.User.AppUserId = c.A.Id;
        var t = (await c.Tx.OpenAsync(new OpenTransactionRequest(type.Id, c.Seed.PropertyId, new DateOnly(2026, 7, 1), "DEMO reassessment"))).Value;
        (await c.Tx.SubmitAsync(t.Id)).Code.ShouldBe("TRANSACTION_EMPTY");
        var newTd = (await c.Tds.CreateAsync(Td(c, c.Seed.TaxDeclaration.Id, t.Id))).Value;
        (await c.Tx.SubmitAsync(t.Id)).IsSuccess.ShouldBeTrue();

        c.User.AppUserId = c.B.Id;
        var rejected = (await c.Tx.RejectAsync(t.Id, "DEMO: valuation not supported")).Value;
        rejected.Status.ShouldBe(WorkflowStatus.Rejected);
        (await c.Tds.GetByIdAsync(newTd.Id)).Value.Status.ShouldBe(WorkflowStatus.Rejected);
        (await c.Tds.GetByIdAsync(c.Seed.TaxDeclaration.Id)).Value.Status.ShouldBe(WorkflowStatus.Approved);
        (await c.Tx.WithdrawAsync(t.Id, "late")).Code.ShouldBe("PROPERTY_TRANSACTION_INVALID_STATE");
    }

    [Fact]
    public async Task Open_RefusesUnapprovedType_PartiesOnNonTransfer_AndNonCurrentTdToCancel()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        c.User.AppUserId = c.A.Id;
        var draftType = (await c.Tx.CreateTypeAsync(new CreateTransactionTypeRequest("DEMO", new DateOnly(2020, 1, 1), null, "DRAFTX",
            "DEMO draft", PropertyTransactionKind.Correction, null, null, []))).Value;
        (await c.Tx.OpenAsync(new OpenTransactionRequest(draftType.Id, c.Seed.PropertyId, new DateOnly(2026, 1, 1), "x")))
            .Code.ShouldBe("TRANSACTION_TYPE_NOT_IN_FORCE");

        var correction = await ApprovedTypeAsync(c, PropertyTransactionKind.Correction);
        var (buyer, ownershipType) = await PartiesAsync(c);
        (await c.Tx.OpenAsync(new OpenTransactionRequest(correction.Id, c.Seed.PropertyId, new DateOnly(2026, 1, 1), "x",
            [new NewPartyRequest(PropertyPartyRole.Owner, buyer.Id, ownershipType.Id, 100m)]))).Code.ShouldBe("TRANSACTION_PARTIES_NOT_ALLOWED");

        var draftTd = (await c.Tds.CreateAsync(Td(c, c.Seed.TaxDeclaration.Id, null))).Value;
        (await c.Tx.OpenAsync(new OpenTransactionRequest(correction.Id, c.Seed.PropertyId, new DateOnly(2026, 1, 1), "x",
            CancelTaxDeclarationIds: [draftTd.Id]))).Code.ShouldBe("TRANSACTION_TD_NOT_CANCELLABLE");
    }

    [Fact]
    public async Task TransactionNumber_ComesFromItsScheme()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var numbering = c.Services.GetRequiredService<INumberingService>();
        c.User.AppUserId = c.A.Id;
        var scheme = (await numbering.CreateAsync(new CreateNumberingSchemeRequest("DEMO", new DateOnly(2020, 1, 1), null,
            NumberedDocumentKind.PropertyTransaction, "DEMO tx numbers", "DEMO-TX-{YEAR}-{SEQ:3}", null, false))).Value;
        c.User.AppUserId = c.B.Id;
        (await numbering.ApproveAsync(scheme.Id)).IsSuccess.ShouldBeTrue();
        var type = await ApprovedTypeAsync(c, PropertyTransactionKind.Correction);

        var t = (await c.Tx.OpenAsync(new OpenTransactionRequest(type.Id, c.Seed.PropertyId, new DateOnly(2026, 5, 1), "DEMO correction"))).Value;
        t.TransactionNumber.ShouldBe("DEMO-TX-2026-001");
    }
}
