using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Assessments;
using Prime.Application.Features.Numbering;
using Prime.Application.Features.Properties;
using Prime.Application.Features.RealPropertyUnits;
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
/// docs/analysis/mrpaao-forms-model.md §6 — step 1: the FAAS as a TD with the
/// assessment it declares, unit PIN postscripts, unit links and per-unit
/// parties. Every number format and name is DEMO test data.
/// </summary>
public class FaasIdentityTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, CurrentUserService User, AppUser A, AppUser B, BillingFlowTests.Seed Seed)
    {
        public ITaxDeclarationService Tds => Services.GetRequiredService<ITaxDeclarationService>();
        public IRealPropertyUnitService Rpus => Services.GetRequiredService<IRealPropertyUnitService>();
        public ITaxpayerService Taxpayers => Services.GetRequiredService<ITaxpayerService>();
        public ITransactionService Tx => Services.GetRequiredService<ITransactionService>();
    }

    private async Task<(Ctx Ctx, IAsyncDisposable Transaction)> BeginAsync(bool post = true)
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
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, new DateOnly(2026, 1, 1), post: post);
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

    private static async Task ApproveTdSchemeAsync(Ctx c)
    {
        var numbering = c.Services.GetRequiredService<INumberingService>();
        c.User.AppUserId = c.A.Id;
        var scheme = (await numbering.CreateAsync(new CreateNumberingSchemeRequest(
            "DEMO — not an LGU format", new DateOnly(2020, 1, 1), null, NumberedDocumentKind.TaxDeclaration, "DEMO TD",
            "DEMO-TD-{YEAR}-{SEQ:4}", null, true))).Value;
        c.User.AppUserId = c.B.Id;
        (await numbering.ApproveAsync(scheme.Id)).IsSuccess.ShouldBeTrue();
        c.User.AppUserId = null;
    }

    private static CreateTaxDeclarationRequest Td(Ctx c, Guid rpuId, Guid? previous, Guid? assessmentId = null) =>
        new(rpuId, $"DEMO-TD-{Guid.NewGuid():N}"[..24], new DateOnly(2026, 7, 1), Taxability.Taxable, c.Seed.ClassificationId,
            c.Seed.TaxDeclaration.ActualUseId, null, 2026, previous, "DEMO", null, assessmentId);

    private static CreateRpuRequest Unit(Ctx c, RpuType type, Guid? land = null, Guid? host = null) =>
        new(c.Seed.PropertyId, $"RPU-{Guid.NewGuid():N}", type, new DateOnly(2026, 1, 1), null, land, host);

    [Fact]
    public async Task NewTd_DeclaresTheAssessmentInForce_AndItsNumberIsTheFaasNumber()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;

        var td = await c.Tds.CreateAsync(Td(c, c.Seed.RpuId, c.Seed.TaxDeclaration.Id));

        td.IsSuccess.ShouldBeTrue(td.IsSuccess ? null : td.Message);
        td.Value.AssessmentId.ShouldBe(c.Seed.AssessmentId);
        td.Value.FaasNumber.ShouldBe(td.Value.TaxDeclarationNumber); // FAAS = ARP = TD (default)
        (await c.Tds.GetByIdAsync(c.Seed.TaxDeclaration.Id)).Value.FaasNumber.ShouldBeNull(); // a legacy TD declares no assessment
    }

    [Fact]
    public async Task NewTd_NamingAnotherUnitsAssessment_IsRefused()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var building = (await c.Rpus.CreateAsync(Unit(c, RpuType.Building, c.Seed.RpuId))).Value;

        var td = await c.Tds.CreateAsync(Td(c, building.Id, null, c.Seed.AssessmentId));

        td.Code.ShouldBe("TAX_DECLARATION_ASSESSMENT_INVALID");
    }

    [Fact]
    public async Task Posting_PreparesADraftTdDeclaringTheAssessment()
    {
        var (c, tx) = await BeginAsync(post: false);
        await using var _ = tx;
        await ApproveTdSchemeAsync(c);
        var assessment = await c.Db.Assessments.SingleAsync(x => x.Id == c.Seed.AssessmentId);
        assessment.Status = WorkflowStatus.Approved;
        assessment.ApprovedAt = DateTimeOffset.UtcNow;
        await c.Db.SaveChangesAsync();

        var posted = await c.Services.GetRequiredService<IAssessmentService>().PostAsync(c.Seed.AssessmentId);

        posted.IsSuccess.ShouldBeTrue(posted.IsSuccess ? null : posted.Message);
        posted.Value.FaasNumber.ShouldBeNull(); // the FAAS number is the TD's, not the assessment's
        var prepared = (await c.Tds.ListByRpuAsync(c.Seed.RpuId)).Value.Single(t => t.Id != c.Seed.TaxDeclaration.Id);
        prepared.Status.ShouldBe(WorkflowStatus.Draft);
        prepared.AssessmentId.ShouldBe(c.Seed.AssessmentId);
        prepared.PreviousTaxDeclarationId.ShouldBe(c.Seed.TaxDeclaration.Id);
        prepared.RevisionNumber.ShouldBe(c.Seed.TaxDeclaration.RevisionNumber + 1);
        prepared.ClassificationId.ShouldBe(c.Seed.ClassificationId);
        prepared.EffectivityDate.ShouldBe(new DateOnly(2026, 1, 1));
        prepared.TaxDeclarationNumber.ShouldStartWith("DEMO-TD-2026-");
        prepared.FaasNumber.ShouldBe(prepared.TaxDeclarationNumber);
    }

    [Fact]
    public async Task Posting_WithNoTdNumberingScheme_PostsAndPreparesNothing()
    {
        var (c, tx) = await BeginAsync(post: false);
        await using var _ = tx;
        var assessment = await c.Db.Assessments.SingleAsync(x => x.Id == c.Seed.AssessmentId);
        assessment.Status = WorkflowStatus.Approved;
        assessment.ApprovedAt = DateTimeOffset.UtcNow;
        await c.Db.SaveChangesAsync();

        (await c.Services.GetRequiredService<IAssessmentService>().PostAsync(c.Seed.AssessmentId)).Value.Status.ShouldBe(WorkflowStatus.Posted);

        (await c.Tds.ListByRpuAsync(c.Seed.RpuId)).Value.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Units_GetPinPostscriptsInTheirSeries_AndLinksAreChecked()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;

        var first = (await c.Rpus.CreateAsync(Unit(c, RpuType.Building, c.Seed.RpuId))).Value;
        var second = (await c.Rpus.CreateAsync(Unit(c, RpuType.Building, c.Seed.RpuId))).Value;
        var machine = await c.Rpus.CreateAsync(Unit(c, RpuType.Machinery, c.Seed.RpuId, first.Id));
        var land = (await c.Rpus.GetByIdAsync(c.Seed.RpuId)).Value;

        first.PinSuffix.ShouldBe(1001);
        second.PinSuffix.ShouldBe(1002);
        machine.IsSuccess.ShouldBeTrue(machine.IsSuccess ? null : machine.Message);
        machine.Value.PinSuffix.ShouldBe(2001);
        machine.Value.HostRpuId.ShouldBe(first.Id);
        machine.Value.LandRpuId.ShouldBe(c.Seed.RpuId);
        land.PinSuffix.ShouldBeNull();
        var pin = await c.Db.Properties.Where(p => p.Id == c.Seed.PropertyId).Select(p => p.PropertyIdentificationNumber).SingleAsync();
        first.UnitPin.ShouldBe($"{pin}-1001");
        land.UnitPin.ShouldBe(pin);

        (await c.Rpus.CreateAsync(Unit(c, RpuType.Machinery, host: c.Seed.RpuId))).Code.ShouldBe("RPU_HOST_LINK_INVALID"); // host is land
        (await c.Rpus.CreateAsync(Unit(c, RpuType.Building, first.Id))).Code.ShouldBe("RPU_LAND_LINK_INVALID"); // "land" is a building
        (await c.Rpus.CreateAsync(Unit(c, RpuType.Building, host: first.Id))).Code.ShouldBe("RPU_HOST_LINK_INVALID"); // only machinery is hosted
    }

    [Fact]
    public async Task UnitOwners_HaveTheirOwnShares_ParenthesiseTheParcel_AndTransferWithoutTheLand()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var landOwner = new Taxpayer { TaxpayerType = TaxpayerType.Individual, LastName = "DEMO_LandOwner", FirstName = "DEMO" };
        var builder = new Taxpayer { TaxpayerType = TaxpayerType.Individual, LastName = "DEMO_BuildingOwner", FirstName = "DEMO" };
        var buyer = new Taxpayer { TaxpayerType = TaxpayerType.Individual, LastName = "DEMO_BuildingBuyer", FirstName = "DEMO" };
        var sole = new OwnershipType { Code = $"OT{Guid.NewGuid():N}"[..8], Name = "DEMO_Sole" };
        c.Db.AddRange(landOwner, builder, buyer, sole);
        await c.Db.SaveChangesAsync();
        var building = (await c.Rpus.CreateAsync(Unit(c, RpuType.Building, c.Seed.RpuId))).Value;

        (await c.Taxpayers.AddOwnerAsync(new AddPropertyOwnerRequest(c.Seed.PropertyId, landOwner.Id, sole.Id, 100m, new DateOnly(2020, 1, 1))))
            .IsSuccess.ShouldBeTrue();
        // 100% of the building on top of 100% of the property: shares count per scope.
        var unitOwner = await c.Taxpayers.AddOwnerAsync(
            new AddPropertyOwnerRequest(c.Seed.PropertyId, builder.Id, sole.Id, 100m, new DateOnly(2020, 1, 1), RpuId: building.Id));
        unitOwner.IsSuccess.ShouldBeTrue(unitOwner.IsSuccess ? null : unitOwner.Message);
        unitOwner.Value.RpuId.ShouldBe(building.Id);
        (await c.Taxpayers.AddOwnerAsync(new AddPropertyOwnerRequest(c.Seed.PropertyId, buyer.Id, sole.Id, 10m, new DateOnly(2020, 1, 1), RpuId: building.Id)))
            .Code.ShouldBe("OWNERSHIP_PERCENTAGE_EXCEEDS_100");
        (await c.Taxpayers.AddOwnerAsync(new AddPropertyOwnerRequest(c.Seed.PropertyId, buyer.Id, sole.Id, 10m, new DateOnly(2020, 1, 1), RpuId: c.Seed.RpuId)))
            .Code.ShouldBe("PROPERTY_PARTY_UNIT_INVALID"); // the land's parties are the property's

        var reloaded = (await c.Rpus.GetByIdAsync(building.Id)).Value;
        reloaded.OwnedSeparately.ShouldBeTrue();
        reloaded.UnitPin.ShouldContain("-(");
        reloaded.UnitPin.ShouldEndWith(")-1001");
        async Task<string[]> NamesAsync(Guid rpuId) => (await PropertyParties.ProjectAsync(
                await PropertyParties.ScopeAsync(c.Db, c.Seed.PropertyId, rpuId, x => x.IsCurrent, CancellationToken.None), CancellationToken.None))
            .Select(o => o.TaxpayerDisplayName).ToArray();
        (await NamesAsync(building.Id)).ShouldHaveSingleItem().ShouldContain("DEMO_BuildingOwner");
        (await NamesAsync(c.Seed.RpuId)).ShouldHaveSingleItem().ShouldContain("DEMO_LandOwner");

        // A transfer of the building alone: the land owner stays.
        c.User.AppUserId = c.A.Id;
        var type = (await c.Tx.CreateTypeAsync(new CreateTransactionTypeRequest(
            "DEMO — not LAM", new DateOnly(2020, 1, 1), null, $"D{Guid.NewGuid():N}"[..8], "DEMO transfer", PropertyTransactionKind.Transfer, 7, null, []))).Value;
        c.User.AppUserId = c.B.Id;
        (await c.Tx.ApproveTypeAsync(type.Id)).IsSuccess.ShouldBeTrue();
        c.User.AppUserId = c.A.Id;
        (await c.Tx.OpenAsync(new OpenTransactionRequest(type.Id, c.Seed.PropertyId, new DateOnly(2026, 7, 1), "DEMO land unit",
            [new NewPartyRequest(PropertyPartyRole.Owner, buyer.Id, sole.Id, 100m)], TransferRpuId: c.Seed.RpuId))).Code.ShouldBe("TRANSACTION_UNIT_INVALID");
        var opened = await c.Tx.OpenAsync(new OpenTransactionRequest(type.Id, c.Seed.PropertyId, new DateOnly(2026, 7, 1), "DEMO building sale",
            [new NewPartyRequest(PropertyPartyRole.Owner, buyer.Id, sole.Id, 100m)], TransferRpuId: building.Id));
        opened.IsSuccess.ShouldBeTrue(opened.IsSuccess ? null : opened.Message);
        opened.Value.TransferRpuId.ShouldBe(building.Id);
        (await c.Tx.SubmitAsync(opened.Value.Id)).IsSuccess.ShouldBeTrue();
        c.User.AppUserId = c.B.Id;
        var approved = await c.Tx.ApproveAsync(opened.Value.Id);
        approved.IsSuccess.ShouldBeTrue(approved.IsSuccess ? null : approved.Message);

        (await NamesAsync(building.Id)).ShouldHaveSingleItem().ShouldContain("DEMO_BuildingBuyer");
        (await NamesAsync(c.Seed.RpuId)).ShouldHaveSingleItem().ShouldContain("DEMO_LandOwner");
        var sold = await c.Db.PropertyTaxpayers.SingleAsync(x => x.TaxpayerId == builder.Id);
        sold.IsCurrent.ShouldBeFalse();
        sold.EndDate.ShouldBe(new DateOnly(2026, 6, 30));
    }
}
