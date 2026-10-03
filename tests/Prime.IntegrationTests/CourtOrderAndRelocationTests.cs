using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.TaxDeclarations;
using Prime.Application.Features.Transactions;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step L3-3 (docs/analysis/assessment-listing-exemptions.md §4.3): court orders that cancel and restore declarations,
/// machinery relocated to another property, and the code a TD shows when no codes are ranked (Q9). Every code and
/// requirement is DEMO data. Rolled back.
/// </summary>
public class CourtOrderAndRelocationTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
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

    private static readonly TransactionRequirementRequest Order = new(1, "DEMO-ORDER", "DEMO certified copy of the court order", true, null);

    private static async Task<TransactionTypeDto> ApprovedTypeAsync(Ctx c, PropertyTransactionKind kind, params TransactionRequirementRequest[] requirements)
    {
        c.User.AppUserId = c.A.Id;
        var created = await c.Tx.CreateTypeAsync(new CreateTransactionTypeRequest(
            "DEMO — not LAM", new DateOnly(2020, 1, 1), null, $"D{Guid.NewGuid():N}"[..8], $"DEMO {kind}", kind, null, null, requirements));
        created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);
        c.User.AppUserId = c.B.Id;
        var approved = await c.Tx.ApproveTypeAsync(created.Value.Id);
        approved.IsSuccess.ShouldBeTrue(approved.IsSuccess ? null : approved.Message);
        c.User.AppUserId = null;
        return approved.Value;
    }

    /// <summary>Satisfies the requirements and submits as A, approves as B.</summary>
    private static async Task<PropertyTransactionDto> CompleteAsync(Ctx c, PropertyTransactionDto tx)
    {
        c.User.AppUserId = c.A.Id;
        foreach (var r in tx.Requirements)
        {
            (await c.Tx.SatisfyRequirementAsync(tx.Id, r.Id, new SatisfyRequirementRequest("DEMO Civil Case 001", null))).IsSuccess.ShouldBeTrue();
        }
        var submitted = await c.Tx.SubmitAsync(tx.Id);
        submitted.IsSuccess.ShouldBeTrue(submitted.IsSuccess ? null : submitted.Message);
        c.User.AppUserId = c.B.Id;
        var approved = await c.Tx.ApproveAsync(tx.Id);
        approved.IsSuccess.ShouldBeTrue(approved.IsSuccess ? null : approved.Message);
        c.User.AppUserId = null;
        return approved.Value;
    }

    private static CreateTaxDeclarationRequest Td(Ctx c, Guid rpuId, Guid? previous, Guid? transactionId, Guid? restores = null) =>
        new(rpuId, $"DEMO-TD-{Guid.NewGuid():N}"[..24], new DateOnly(2026, 7, 1), Taxability.Taxable, c.Seed.ClassificationId,
            c.Seed.TaxDeclaration.ActualUseId, null, 2027, previous, "DEMO", transactionId, RestoresTaxDeclarationId: restores);

    [Fact]
    public async Task ACourtOrderType_RequiresTheOrder()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var created = await c.Tx.CreateTypeAsync(new CreateTransactionTypeRequest("DEMO", new DateOnly(2020, 1, 1), null, "DCO", "DEMO court order",
            PropertyTransactionKind.CourtOrder, null, null, []));
        created.Code.ShouldBe("VALIDATION_FAILED");
        created.Message!.ShouldContain("court order");
    }

    [Fact]
    public async Task ACourtOrder_CancelsADeclaration_AndAnotherRestoresItByANewTd()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var type = await ApprovedTypeAsync(c, PropertyTransactionKind.CourtOrder, Order);
        var original = c.Seed.TaxDeclaration.Id;

        c.User.AppUserId = c.A.Id;
        var cancel = await c.Tx.OpenAsync(new OpenTransactionRequest(type.Id, c.Seed.PropertyId, new DateOnly(2026, 7, 1), "DEMO order to cancel",
            CancelTaxDeclarationIds: [original]));
        await CompleteAsync(c, cancel.Value);
        (await c.Db.TaxDeclarations.AsNoTracking().SingleAsync(x => x.Id == original)).Status.ShouldBe(WorkflowStatus.Cancelled);

        // Only a court order restores; the new TD names the cancelled one, which stays cancelled.
        (await c.Tds.CreateAsync(Td(c, c.Seed.RpuId, null, null, restores: original))).Code.ShouldBe("TAX_DECLARATION_RESTORE_NOT_ALLOWED");
        c.User.AppUserId = c.A.Id;
        var restore = await c.Tx.OpenAsync(new OpenTransactionRequest(type.Id, c.Seed.PropertyId, new DateOnly(2026, 8, 1), "DEMO order to restore"));
        var restoring = await c.Tds.CreateAsync(Td(c, c.Seed.RpuId, null, restore.Value.Id, restores: original));
        restoring.IsSuccess.ShouldBeTrue(restoring.IsSuccess ? null : restoring.Message);
        (restoring.Value.RevisionNumber, restoring.Value.RestoresTaxDeclarationId).ShouldBe((2, (Guid?)original));
        await CompleteAsync(c, restore.Value);

        (await c.Db.TaxDeclarations.AsNoTracking().SingleAsync(x => x.Id == restoring.Value.Id)).Status.ShouldBe(WorkflowStatus.Approved);
        (await c.Db.TaxDeclarations.AsNoTracking().SingleAsync(x => x.Id == original)).Status.ShouldBe(WorkflowStatus.Cancelled);
    }

    [Fact]
    public async Task ARelocatedMachine_ContinuesOnTheReceivingProperty_AndItsOldUnitAndTdEnd()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var type = await ApprovedTypeAsync(c, PropertyTransactionKind.MachineryRelocation);
        var source = await c.Db.Properties.AsNoTracking().SingleAsync(x => x.Id == c.Seed.PropertyId);
        var receiving = new PropertyEntity
        {
            PropertyIdentificationNumber = $"PIN-{Guid.NewGuid():N}", ProvinceId = source.ProvinceId, MunicipalityId = source.MunicipalityId, BarangayId = source.BarangayId,
        };
        var machine = new RealPropertyUnit { PropertyId = source.Id, RpuNumber = $"RPU-M-{Guid.NewGuid():N}", RpuType = RpuType.Machinery, EffectivityDate = new DateOnly(2024, 1, 1) };
        c.Db.AddRange(receiving, machine);
        await c.Db.SaveChangesAsync();
        var oldTd = new TaxDeclaration
        {
            RpuId = machine.Id, PropertyId = source.Id, TaxDeclarationNumber = $"TD-M-{Guid.NewGuid():N}", EffectivityDate = new DateOnly(2024, 1, 1),
            ClassificationId = c.Seed.ClassificationId, ActualUseId = c.Seed.TaxDeclaration.ActualUseId, AssessmentYear = 2024, Status = WorkflowStatus.Approved,
        };
        c.Db.Add(oldTd);
        await c.Db.SaveChangesAsync();

        c.User.AppUserId = c.A.Id;
        (await c.Tx.OpenAsync(new OpenTransactionRequest(type.Id, receiving.Id, new DateOnly(2026, 7, 1), "DEMO machine moved"))).Code.ShouldBe("RELOCATED_UNIT_REQUIRED");
        (await c.Tx.OpenAsync(new OpenTransactionRequest(type.Id, source.Id, new DateOnly(2026, 7, 1), "DEMO", RelocatedRpuId: machine.Id))).Code.ShouldBe("RELOCATED_UNIT_INVALID");
        var opened = await c.Tx.OpenAsync(new OpenTransactionRequest(type.Id, receiving.Id, new DateOnly(2026, 7, 1), "DEMO machine moved", RelocatedRpuId: machine.Id));
        opened.IsSuccess.ShouldBeTrue(opened.IsSuccess ? null : opened.Message);
        opened.Value.RelatedProperties.ShouldHaveSingleItem().ShouldBe(new TransactionPropertyDto(source.Id, source.PropertyIdentificationNumber, TransactionPropertyRole.Source));
        (await c.Tx.SubmitAsync(opened.Value.Id)).Code.ShouldBe("TRANSACTION_EMPTY");

        // The receiving unit continues the moved one; its TD replaces the moved unit's TD across properties.
        var arrived = new RealPropertyUnit
        {
            PropertyId = receiving.Id, RpuNumber = $"RPU-M-{Guid.NewGuid():N}", RpuType = RpuType.Machinery, EffectivityDate = new DateOnly(2026, 7, 1), PreviousRpuId = machine.Id,
        };
        c.Db.Add(arrived);
        await c.Db.SaveChangesAsync();
        (await c.Tds.CreateAsync(Td(c, arrived.Id, oldTd.Id, null))).Code.ShouldBe("PREVIOUS_TAX_DECLARATION_OTHER_RPU");
        var newTd = await c.Tds.CreateAsync(Td(c, arrived.Id, oldTd.Id, opened.Value.Id));
        newTd.IsSuccess.ShouldBeTrue(newTd.IsSuccess ? null : newTd.Message);
        await CompleteAsync(c, opened.Value);

        var cancelled = await c.Db.TaxDeclarations.AsNoTracking().SingleAsync(x => x.Id == oldTd.Id);
        (cancelled.Status, cancelled.SupersededByTaxDeclarationId).ShouldBe((WorkflowStatus.Cancelled, (Guid?)newTd.Value.Id));
        var moved = await c.Db.RealPropertyUnits.AsNoTracking().SingleAsync(x => x.Id == machine.Id);
        (moved.Status, moved.EndDate).ShouldBe((RecordStatus.Superseded, (DateOnly?)new DateOnly(2026, 6, 30)));
        (await c.Db.TaxDeclarations.AsNoTracking().SingleAsync(x => x.Id == newTd.Value.Id)).Status.ShouldBe(WorkflowStatus.Approved);
    }

    [Fact]
    public void WithNoRanks_TheProducingTransactionsCodeWins_AndARankWinsWhereConfigured()
    {
        // Candidates in order of precedence: the transaction, a named code, the assessment's code (Q9).
        TransactionCodes.Highest([("TR", null), ("RA", null)]).ShouldBe(("TR", (int?)null));
        TransactionCodes.Highest([(null, null), ("RA", null)]).ShouldBe(("RA", (int?)null));
        TransactionCodes.Highest([("TR", null), ("GR", 5)]).ShouldBe(("GR", (int?)5));
    }
}
