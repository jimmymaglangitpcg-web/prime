using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Forms;
using Prime.Application.Features.MarketData;
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
/// Step L6-1 (docs/analysis/smv-preparation-general-revision.md §4.1): market transactions with review, the CSV
/// import, the abstracts of building permits and machinery registrations with their links, and the report rows.
/// DEMO data. Rolled back.
/// </summary>
public class MarketDataTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, CurrentUserService User, AppUser A, BillingFlowTests.Seed Seed,
        Guid MunicipalityId, string MunicipalityCode, Guid BarangayId, SubClassification SubClass)
    {
        public IMarketTransactionService Transactions => Services.GetRequiredService<IMarketTransactionService>();
        public IMarketDataAbstractService Abstracts => Services.GetRequiredService<IMarketDataAbstractService>();
        public IMarketDataImportService Import => Services.GetRequiredService<IMarketDataImportService>();
        public IMarketDataReportService Reports => Services.GetRequiredService<IMarketDataReportService>();
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
        var user = new AppUser { SupabaseUserId = Guid.NewGuid(), DisplayName = "DEMO User A", Email = $"demo-{Guid.NewGuid():N}@example.invalid" };
        db.AppUsers.Add(user);
        var subClass = new SubClassification { Code = $"SC{Guid.NewGuid():N}"[..8], Name = "DEMO R-1" };
        db.Add(subClass);
        await db.SaveChangesAsync();
        var current = scope.ServiceProvider.GetRequiredService<CurrentUserService>();
        current.AppUserId = null;
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, new DateOnly(2026, 1, 1));
        current.AppUserId = user.Id;
        var place = await db.Properties.Where(x => x.Id == seed.PropertyId)
            .Select(x => new { x.MunicipalityId, x.Municipality!.PsgcCode, x.BarangayId }).SingleAsync();
        return (new Ctx(db, scope.ServiceProvider, current, user, seed, place.MunicipalityId, place.PsgcCode, place.BarangayId, subClass), new Scoped(transaction, scope));
    }

    private sealed class Scoped(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    private static SaveMarketTransactionRequest Sale(Ctx c, decimal consideration, decimal? area, DateOnly? date = null, bool building = false,
        decimal? landPart = null, Guid? classification = null, string? document = null) => new(
        MarketDataSource.RegistryAbstract, null, date ?? new DateOnly(2026, 3, 1), document ?? $"DEMO-DOC-{Guid.NewGuid():N}"[..20], null,
        "DEMO Grantor", "DEMO Grantee", null, c.MunicipalityId, c.BarangayId, "DEMO Street", null, null, null, "L-1", null, null,
        true, building, classification, c.SubClass.Id, null, null, null, area, AreaMeasure.SquareMetre, building ? 50m : null, consideration, landPart, null);

    [Fact]
    public async Task Sale_UnitPrice_Review_AndAnEditReturnsItToUnreviewed()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;

        var created = await c.Transactions.CreateAsync(Sale(c, 900_000m, 300m));
        created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);
        created.Value.LandUnitPrice.ShouldBe(3000m);
        created.Value.Review.ShouldBe(MarketDataReview.Unreviewed);

        // Accepting needs the classification; excluding needs a reason.
        (await c.Transactions.ReviewAsync(created.Value.Id, new(MarketDataReview.Accepted, null, null, null))).Code.ShouldBe("MARKET_DATA_NO_CLASSIFICATION");
        (await c.Transactions.ReviewAsync(created.Value.Id, new(MarketDataReview.Excluded, " ", null, null))).Code.ShouldBe("VALIDATION_FAILED");

        var edited = await c.Transactions.UpdateAsync(created.Value.Id, Sale(c, 900_000m, 300m, classification: c.Seed.ClassificationId));
        edited.IsSuccess.ShouldBeTrue();
        var accepted = await c.Transactions.ReviewAsync(created.Value.Id, new(MarketDataReview.Accepted, null, new DateOnly(2026, 3, 10), "DEMO field check"));
        accepted.IsSuccess.ShouldBeTrue(accepted.IsSuccess ? null : accepted.Message);
        accepted.Value.Review.ShouldBe(MarketDataReview.Accepted);
        accepted.Value.FieldValidatedOn.ShouldBe(new DateOnly(2026, 3, 10));

        // Any change: what was validated is no longer what is recorded.
        var changed = await c.Transactions.UpdateAsync(created.Value.Id, Sale(c, 950_000m, 300m, classification: c.Seed.ClassificationId));
        changed.Value.Review.ShouldBe(MarketDataReview.Unreviewed);
        changed.Value.LandUnitPrice.ShouldBe(3166.67m);

        var cancelled = await c.Transactions.CancelAsync(created.Value.Id, new("DEMO entered twice"));
        cancelled.Value.CancelledAt.ShouldNotBeNull();
        (await c.Transactions.UpdateAsync(created.Value.Id, Sale(c, 1m, 1m))).Code.ShouldBe("MARKET_DATA_CANCELLED");
        (await c.Transactions.SearchAsync(new MarketTransactionSearchRequest { MunicipalityId = c.MunicipalityId })).Value.Items
            .ShouldNotContain(x => x.Id == created.Value.Id);
    }

    [Fact]
    public async Task LandAndBuilding_WithoutTheLandPart_CannotBeAccepted()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var t = (await c.Transactions.CreateAsync(Sale(c, 2_000_000m, 200m, building: true, classification: c.Seed.ClassificationId))).Value;
        t.LandUnitPrice.ShouldBeNull();
        (await c.Transactions.ReviewAsync(t.Id, new(MarketDataReview.Accepted, null, null, null))).Code.ShouldBe("MARKET_DATA_NO_UNIT_PRICE");

        var split = await c.Transactions.UpdateAsync(t.Id, Sale(c, 2_000_000m, 200m, building: true, landPart: 1_200_000m, classification: c.Seed.ClassificationId));
        split.Value.LandUnitPrice.ShouldBe(6000m);
        split.Value.BuildingUnitPrice.ShouldBe(16000m);
        (await c.Transactions.CreateAsync(Sale(c, 100m, 10m, landPart: 50m))).Code.ShouldBe("VALIDATION_FAILED"); // land part without a building
    }

    [Fact]
    public async Task OutsideTheJurisdiction_IsRefused_AndHidden()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var t = (await c.Transactions.CreateAsync(Sale(c, 500_000m, 100m))).Value;

        c.Services.GetRequiredService<JurisdictionState>().Restrict([Guid.NewGuid()]);

        (await c.Transactions.CreateAsync(Sale(c, 500_000m, 100m))).Code.ShouldBe("JURISDICTION_FORBIDDEN");
        (await c.Transactions.GetAsync(t.Id)).Code.ShouldBe("MARKET_TRANSACTION_NOT_FOUND");
    }

    [Fact]
    public async Task Import_PreviewShowsEachRowsErrors_ABadFileImportsNothing_AGoodFileImportsUnreviewed()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var classCode = await c.Db.Classifications.Where(x => x.Id == c.Seed.ClassificationId).Select(x => x.Code).SingleAsync();
        var bad = Encoding.UTF8.GetBytes(
            "transaction_date,municipality,consideration,land_area,classification,sub_class,document_reference\n"
            + $"2026-02-01,{c.MunicipalityCode},600000,200,{classCode},{c.SubClass.Code},DEMO-IMP-1\n"
            + $"2026-13-01,{c.MunicipalityCode},abc,200,NOPE,,DEMO-IMP-2\n");

        var preview = await c.Import.PreviewAsync(bad);
        preview.IsSuccess.ShouldBeTrue(preview.IsSuccess ? null : preview.Message);
        preview.Value.RowCount.ShouldBe(2);
        preview.Value.ValidCount.ShouldBe(1);
        var errors = preview.Value.Rows.Single(r => !r.Valid).Errors;
        errors.ShouldContain(e => e.StartsWith("transaction_date"));
        errors.ShouldContain(e => e.StartsWith("consideration"));
        errors.ShouldContain(e => e.StartsWith("classification"));
        (await c.Import.ImportAsync(bad, preview.Value.Fingerprint)).Code.ShouldBe("IMPORT_HAS_ERRORS");
        (await c.Db.MarketTransactions.CountAsync(x => x.DocumentReference == "DEMO-IMP-1")).ShouldBe(0);

        var good = Encoding.UTF8.GetBytes(
            "transaction_date,municipality,consideration,land_area,land_area_unit,classification,sub_class,document_reference,conveys\n"
            + $"2026-02-01,{c.MunicipalityCode},600000,200,sqm,{classCode},{c.SubClass.Code},DEMO-IMP-1,land\n"
            + $"2026-02-03,{c.MunicipalityCode},\"1,500,000\",1.5,ha,{classCode},,DEMO-IMP-3,\n");
        var goodPreview = (await c.Import.PreviewAsync(good)).Value;
        goodPreview.ValidCount.ShouldBe(2);
        (await c.Import.ImportAsync(good, "0000")).Code.ShouldBe("IMPORT_FILE_CHANGED");
        var imported = await c.Import.ImportAsync(good, goodPreview.Fingerprint);
        imported.IsSuccess.ShouldBeTrue(imported.IsSuccess ? null : imported.Message);
        imported.Value.ImportedCount.ShouldBe(2);
        var rows = await c.Db.MarketTransactions.Where(x => x.ImportBatch == imported.Value.Batch).OrderBy(x => x.TransactionDate).ToListAsync();
        rows.Select(r => (r.LandUnitPrice, r.LandAreaUnit, r.Review)).ShouldBe([
            (3000m, AreaMeasure.SquareMetre, MarketDataReview.Unreviewed), (1_000_000m, AreaMeasure.Hectare, MarketDataReview.Unreviewed)]);

        // The same file again: every row is already recorded.
        (await c.Import.PreviewAsync(good)).Value.ValidCount.ShouldBe(0);
    }

    [Fact]
    public async Task Import_UnknownOrMissingColumns_RefuseTheFile()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        (await c.Import.PreviewAsync(Encoding.UTF8.GetBytes("transaction_date,municipality\n2026-01-01,X\n"))).Code.ShouldBe("IMPORT_FILE_INVALID");
        (await c.Import.PreviewAsync(Encoding.UTF8.GetBytes("transaction_date,municipality,consideration,price\n2026-01-01,X,1,2\n"))).Code.ShouldBe("IMPORT_FILE_INVALID");
    }

    private static SaveBuildingPermitRequest Permit(Ctx c, string number, BuildingPermitScope scope = BuildingPermitScope.NewConstruction) => new(
        number, new DateOnly(2026, 2, 1), null, null, "DEMO Permittee", null, null, c.MunicipalityId, c.BarangayId, null, "DEMO Street",
        scope, null, null, 2, 120m, 1_800_000m, null, new DateOnly(2026, 2, 5), null);

    [Fact]
    public async Task Permits_AreUniquePerMunicipality_SuggestTheBuildingWithTheirNumber_AndLeaveTheLeadsListWhenLinked()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var number = $"BP-{Guid.NewGuid():N}"[..16];
        var permit = await c.Abstracts.CreatePermitAsync(Permit(c, number));
        permit.IsSuccess.ShouldBeTrue(permit.IsSuccess ? null : permit.Message);
        (await c.Abstracts.CreatePermitAsync(Permit(c, number))).Code.ShouldBe("BUILDING_PERMIT_DUPLICATE");
        var demolition = (await c.Abstracts.CreatePermitAsync(Permit(c, number + "D", BuildingPermitScope.Demolition))).Value;

        // A building declared with the same permit number is suggested, not linked.
        var buildingType = new BuildingType { Code = $"BT{Guid.NewGuid():N}"[..8], Name = "DEMO kind" };
        var structuralType = new StructuralType { Code = $"ST{Guid.NewGuid():N}"[..8], Name = "DEMO type" };
        var condition = new Condition { Code = $"CO{Guid.NewGuid():N}"[..8], Name = "DEMO good" };
        var useId = await c.Db.Lands.Where(x => x.RpuId == c.Seed.RpuId).Select(x => x.ActualUseId).SingleAsync();
        var rpu = new RealPropertyUnit { PropertyId = c.Seed.PropertyId, RpuNumber = $"RPU-{Guid.NewGuid():N}", RpuType = RpuType.Building, EffectivityDate = new DateOnly(2026, 1, 1) };
        var building = new Building
        {
            Rpu = rpu, PropertyId = c.Seed.PropertyId, BuildingType = buildingType, StructuralType = structuralType, ActualUseId = useId,
            Condition = condition, FloorArea = 120m, TotalFloorArea = 120m, BuildingPermitNumber = number,
        };
        c.Db.AddRange(buildingType, structuralType, condition, rpu, building);
        await c.Db.SaveChangesAsync();

        var leads = (await c.Abstracts.SearchPermitsAsync(new AbstractSearchRequest { MunicipalityId = c.MunicipalityId, UnlinkedOnly = true })).Value.Items;
        leads.Single(x => x.Id == permit.Value.Id).SuggestedBuildingId.ShouldBe(building.Id);
        leads.ShouldNotContain(x => x.Id == demolition.Id); // nothing to declare

        var linked = await c.Abstracts.LinkPermitAsync(permit.Value.Id, new(building.Id));
        linked.Value.BuildingId.ShouldBe(building.Id);
        linked.Value.BuildingPropertyId.ShouldBe(c.Seed.PropertyId);
        (await c.Abstracts.SearchPermitsAsync(new AbstractSearchRequest { MunicipalityId = c.MunicipalityId, UnlinkedOnly = true })).Value.Items
            .ShouldNotContain(x => x.Id == permit.Value.Id);
        (await c.Abstracts.LinkPermitAsync(permit.Value.Id, new(Guid.NewGuid()))).Code.ShouldBe("BUILDING_NOT_FOUND");
    }

    [Fact]
    public async Task MachineryRegistrations_AreUniquePerMunicipality_AndCanBeCancelled()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var number = $"CR-{Guid.NewGuid():N}"[..16];
        SaveMachineryRegistrationRequest Reg() => new(number, new DateOnly(2026, 1, 10), "DEMO Owner", null, null, c.MunicipalityId, null, "DEMO plant",
            null, "DEMO rice mill", "DEMO brand", 2025, null, 750_000m, "New", new DateOnly(2026, 1, 5), null, null);
        var reg = await c.Abstracts.CreateRegistrationAsync(Reg());
        reg.IsSuccess.ShouldBeTrue(reg.IsSuccess ? null : reg.Message);
        (await c.Abstracts.CreateRegistrationAsync(Reg())).Code.ShouldBe("MACHINERY_REGISTRATION_DUPLICATE");
        (await c.Abstracts.CancelRegistrationAsync(reg.Value.Id, new("DEMO wrong municipality"))).Value.CancelledAt.ShouldNotBeNull();
        (await c.Abstracts.CreateRegistrationAsync(Reg())).IsSuccess.ShouldBeTrue(); // the cancelled one no longer counts
    }

    [Fact]
    public async Task SalesReport_LowestMedianHighest_OfAcceptedSalesOnly_AndTheAbstractListsEveryRecord()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        foreach (var (price, accept) in new[] { (300_000m, true), (500_000m, true), (400_000m, true), (100_000m, false) })
        {
            var t = (await c.Transactions.CreateAsync(Sale(c, price, 100m, classification: c.Seed.ClassificationId))).Value;
            if (accept)
            {
                (await c.Transactions.ReviewAsync(t.Id, new(MarketDataReview.Accepted, null, null, null))).IsSuccess.ShouldBeTrue();
            }
        }
        var provider = c.Services.GetServices<IFormDataProvider>().Single(p => p.SubjectType == FormSubjectType.MarketDataReport);

        var sales = (await c.Reports.CreateAsync(new(MarketDataReportKind.SalesReport, c.MunicipalityId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), null))).Value;
        sales.FormCode.ShouldBe("MARKET_SALES_REPORT");
        var data = (await provider.BuildAsync(sales.Id, CancellationToken.None)).ShouldNotBeNull().Data;
        var group = data["data"]!["groups"]!.AsArray().Single()!;
        group["count"]!.GetValue<int>().ShouldBe(3);
        group["lowest"]!.GetValue<decimal>().ShouldBe(3000m);
        group["median"]!.GetValue<decimal>().ShouldBe(4000m);
        group["highest"]!.GetValue<decimal>().ShouldBe(5000m);

        var abstractRun = (await c.Reports.CreateAsync(new(MarketDataReportKind.TransactionsAbstract, c.MunicipalityId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), null))).Value;
        var abstractData = (await provider.BuildAsync(abstractRun.Id, CancellationToken.None)).ShouldNotBeNull().Data;
        abstractData["data"]!["transactions"]!.AsArray().Count.ShouldBe(4);
        (await c.Reports.CreateAsync(new(MarketDataReportKind.SalesReport, c.MunicipalityId, new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1), null)))
            .Code.ShouldBe("VALIDATION_FAILED");
    }
}
