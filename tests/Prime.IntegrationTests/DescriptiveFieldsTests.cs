using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Appraisal;
using Prime.Application.Features.Buildings;
using Prime.Application.Features.Descriptions;
using Prime.Application.Features.Forms;
using Prime.Application.Features.Transactions;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// docs/analysis/mrpaao-forms-model.md §10 step 3 — descriptive fields printed
/// on the FAAS and TD, corrected in place with a reason. All values are DEMO.
/// </summary>
public class DescriptiveFieldsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, BillingFlowTests.Seed Seed)
    {
        public IDescriptionService Descriptions => Services.GetRequiredService<IDescriptionService>();
    }

    private async Task<(Ctx Ctx, IAsyncDisposable Transaction)> BeginAsync()
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        await db.TransactionTypes.Where(x => x.Status == WorkflowStatus.Approved)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null));
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, new DateOnly(2026, 1, 1));
        return (new Ctx(db, scope.ServiceProvider, seed), new Scoped(transaction, scope));
    }

    private sealed class Scoped(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    private static UpdatePropertyDescriptionRequest PropertyRequest(Guid? titleTypeId, string reason) => new(
        "DEMO Street", null, "L-9", "B-2", "Psd-DEMO", "T-DEMO-1", titleTypeId, new DateOnly(2001, 5, 4), "TM-DEMO",
        "Lot 10 (DEMO)", "DEMO Road", "Lot 12 (DEMO)", "DEMO River", reason);

    [Fact]
    public async Task PropertyDescription_IsCorrectedWithAReason_AuditedAndOnTheFaas()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var tct = await c.Db.TitleTypes.SingleAsync(x => x.Code == "TCT"); // seeded from the MRPAAO list

        (await c.Descriptions.UpdatePropertyAsync(c.Seed.PropertyId, PropertyRequest(tct.Id, " "))).Code.ShouldBe("VALIDATION_FAILED");
        var updated = await c.Descriptions.UpdatePropertyAsync(c.Seed.PropertyId, PropertyRequest(tct.Id, "DEMO boundaries from the survey plan"));

        updated.IsSuccess.ShouldBeTrue(updated.IsSuccess ? null : updated.Message);
        var audit = await c.Db.AuditLogs.Where(a => a.TableName == "Property" && a.RecordId == c.Seed.PropertyId && a.Action == AuditAction.Update)
            .OrderByDescending(a => a.Timestamp).FirstAsync();
        audit.Reason.ShouldBe("DEMO boundaries from the survey plan");
        audit.NewValue.ShouldNotBeNull().ShouldContain("DEMO River");

        var record = (await c.Services.GetRequiredService<IAppraisalRecordService>().GetAsync(c.Seed.AssessmentId)).Value;
        record.Property.TitleType.ShouldBe("Transfer Certificate of Title");
        record.Property.BoundaryWest.ShouldBe("DEMO River");
        record.Property.LotNumber.ShouldBe("L-9");
    }

    [Fact]
    public async Task BuildingFloorsAndMaterials_FollowTheChecklistRules()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var buildingId = await DemoBuildingAsync(c, totalFloorArea: 100m);
        var roofing = await c.Db.StructuralParts.SingleAsync(x => x.Code == "ROOFING");
        var giSheet = await c.Db.StructuralMaterials.SingleAsync(x => x.Code == "ROOFING-G_I_SHEET");
        var wallWood = await c.Db.StructuralMaterials.SingleAsync(x => x.Code == "EXTERIOR_WALLS-WOOD");

        (await c.Descriptions.AddBuildingFloorAsync(buildingId, new AddBuildingFloorRequest(1, 60m))).IsSuccess.ShouldBeTrue();
        (await c.Descriptions.AddBuildingFloorAsync(buildingId, new AddBuildingFloorRequest(1, 10m))).Code.ShouldBe("BUILDING_FLOOR_DUPLICATE");
        (await c.Descriptions.AddBuildingFloorAsync(buildingId, new AddBuildingFloorRequest(2, 41m))).Code.ShouldBe("BUILDING_FLOORS_EXCEED_FLOOR_AREA");
        (await c.Descriptions.AddBuildingMaterialAsync(buildingId, new AddBuildingMaterialRequest(roofing.Id, giSheet.Id, null, null))).IsSuccess.ShouldBeTrue();
        (await c.Descriptions.AddBuildingMaterialAsync(buildingId, new AddBuildingMaterialRequest(roofing.Id, null, "DEMO Solar tiles", 2))).IsSuccess.ShouldBeTrue();
        (await c.Descriptions.AddBuildingMaterialAsync(buildingId, new AddBuildingMaterialRequest(roofing.Id, wallWood.Id, null, null)))
            .Code.ShouldBe("STRUCTURAL_MATERIAL_NOT_FOUND"); // a wall material is not a roofing material
        (await c.Descriptions.AddBuildingMaterialAsync(buildingId, new AddBuildingMaterialRequest(roofing.Id, giSheet.Id, "both", null)))
            .Code.ShouldBe("VALIDATION_FAILED");

        var building = (await c.Services.GetRequiredService<IBuildingService>().GetByIdAsync(buildingId)).Value;
        building.Floors.ShouldNotBeNull().ShouldHaveSingleItem().Area.ShouldBe(60m);
        building.Materials.ShouldNotBeNull().Select(m => (m.StructuralPartName, m.MaterialName, m.FloorNumber))
            .ShouldBe([("Roofing", "G.I. Sheet", (int?)null), ("Roofing", "DEMO Solar tiles", 2)]);
    }

    [Fact]
    public async Task TransferClearance_IsRecordedOnATransfer_AndPrintedOnItsTd()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var transactions = c.Services.GetRequiredService<ITransactionService>();
        var transfer = (await transactions.CreateTypeAsync(new CreateTransactionTypeRequest(
            "DEMO — not LAM", new DateOnly(2020, 1, 1), null, $"D{Guid.NewGuid():N}"[..8], "DEMO transfer", PropertyTransactionKind.Transfer, 7, null, []))).Value;
        (await transactions.ApproveTypeAsync(transfer.Id)).IsSuccess.ShouldBeTrue();
        var t = (await transactions.OpenAsync(new OpenTransactionRequest(transfer.Id, c.Seed.PropertyId, new DateOnly(2026, 7, 1), "DEMO sale"))).Value;
        var td = await c.Services.GetRequiredService<Application.Features.TaxDeclarations.ITaxDeclarationService>().CreateAsync(new(
            c.Seed.RpuId, $"DEMO-TD-{Guid.NewGuid():N}"[..24], new DateOnly(2026, 7, 1), Taxability.Taxable, c.Seed.ClassificationId,
            c.Seed.TaxDeclaration.ActualUseId, null, 2026, c.Seed.TaxDeclaration.Id, "DEMO", t.Id));
        td.IsSuccess.ShouldBeTrue(td.IsSuccess ? null : td.Message);

        var set = await c.Descriptions.SetTransferTaxClearanceAsync(t.Id, new SetTransferTaxClearanceRequest(
            "CAR-DEMO-1", new DateOnly(2026, 6, 15), "DEMO Seller", "000-000-000", "111-111-111",
            60_000m, "OR-DEMO-1", new DateOnly(2026, 6, 10), 15_000m, "OR-DEMO-2", new DateOnly(2026, 6, 10), null, null, null, null));

        set.IsSuccess.ShouldBeTrue(set.IsSuccess ? null : set.Message);
        (await transactions.GetAsync(t.Id)).Value.TaxClearance.ShouldNotBeNull().CarNumber.ShouldBe("CAR-DEMO-1");
        var form = await c.Services.GetServices<IFormDataProvider>().Single(p => p.SubjectType == FormSubjectType.TaxDeclaration)
            .BuildAsync(td.Value.Id, CancellationToken.None);
        form.ShouldNotBeNull().Data["transferClearance"]!["carNumber"]!.GetValue<string>().ShouldBe("CAR-DEMO-1");
    }

    [Fact]
    public async Task TaxClearance_OnANonTransfer_IsRefused()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var transactions = c.Services.GetRequiredService<ITransactionService>();
        var type = (await transactions.CreateTypeAsync(new CreateTransactionTypeRequest(
            "DEMO — not LAM", new DateOnly(2020, 1, 1), null, $"D{Guid.NewGuid():N}"[..8], "DEMO cancellation", PropertyTransactionKind.Cancellation, 7, null, []))).Value;
        (await transactions.ApproveTypeAsync(type.Id)).IsSuccess.ShouldBeTrue();
        var t = (await transactions.OpenAsync(new OpenTransactionRequest(type.Id, c.Seed.PropertyId, new DateOnly(2026, 7, 1), "DEMO"))).Value;

        (await c.Descriptions.SetTransferTaxClearanceAsync(t.Id, new SetTransferTaxClearanceRequest(
            "CAR", null, null, null, null, null, null, null, null, null, null, null, null, null, null))).Code.ShouldBe("TRANSACTION_NOT_TRANSFER");
    }

    private static async Task<Guid> DemoBuildingAsync(Ctx c, decimal totalFloorArea)
    {
        var rpu = new RealPropertyUnit { PropertyId = c.Seed.PropertyId, RpuNumber = $"RPU-{Guid.NewGuid():N}", RpuType = RpuType.Building, EffectivityDate = new DateOnly(2026, 1, 1) };
        var kind = new BuildingType { Code = $"BT{Guid.NewGuid():N}"[..8], Name = "DEMO_House" };
        var structure = new StructuralType { Code = $"ST{Guid.NewGuid():N}"[..8], Name = "DEMO_Concrete" };
        var condition = new Condition { Code = $"CO{Guid.NewGuid():N}"[..8], Name = "DEMO_Good" };
        c.Db.AddRange(rpu, kind, structure, condition);
        await c.Db.SaveChangesAsync();
        var building = await c.Services.GetRequiredService<IBuildingService>().CreateAsync(new CreateBuildingRequest(
            rpu.Id, kind.Id, structure.Id, c.Seed.TaxDeclaration.ActualUseId, 2, 50m, totalFloorArea, 2020, 2021, condition.Id, null));
        building.IsSuccess.ShouldBeTrue(building.IsSuccess ? null : building.Message);
        return building.Value.Id;
    }
}
