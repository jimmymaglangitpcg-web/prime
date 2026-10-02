using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Descriptions;
using Prime.Application.Features.MachineryUnits;
using Prime.Application.Features.Offices;
using Prime.Application.Features.Parcels;
using Prime.Application.Features.Taxpayers;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step L5-1 (docs/analysis/records-and-forms.md §4.1): the data the LAM forms print that PRIME did not keep —
/// owner's sex, cadastral numbers, machinery acquisition documents, the office's Sanggunian and the signatory's REA
/// licence. Rolled-back-transaction pattern; every value is DEMO data.
/// </summary>
public class LamRecordsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, Province Province, Municipality Town, Barangay Barangay, PropertyEntity Property);

    private async Task<(Ctx Ctx, IAsyncDisposable Transaction)> BeginAsync()
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        var province = new Province { PsgcCode = $"P{Guid.NewGuid():N}"[..10], Name = "DEMO Province" };
        var town = new Municipality { Province = province, PsgcCode = $"M{Guid.NewGuid():N}"[..10], Name = "DEMO Town" };
        var barangay = new Barangay { Municipality = town, PsgcCode = $"B{Guid.NewGuid():N}"[..10], Name = "DEMO Barangay" };
        var property = new PropertyEntity { PropertyIdentificationNumber = $"PIN-{Guid.NewGuid():N}", Province = province, Municipality = town, Barangay = barangay };
        db.AddRange(province, town, barangay, property);
        await db.SaveChangesAsync();
        return (new Ctx(db, scope.ServiceProvider, province, town, barangay, property), new Scoped(transaction, scope));
    }

    private sealed class Scoped(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    [Fact]
    public async Task OwnersSex_IsKeptForIndividualsOnly()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var taxpayers = c.Services.GetRequiredService<ITaxpayerService>();

        var person = await taxpayers.CreateAsync(new CreateTaxpayerRequest(TaxpayerType.Individual, "DEMO_Owner", "Ana", null, null, null, null, null,
            null, null, null, null, null, Sex.Female));
        person.IsSuccess.ShouldBeTrue(person.IsSuccess ? null : person.Message);
        person.Value.Sex.ShouldBe(Sex.Female);

        (await taxpayers.CreateAsync(new CreateTaxpayerRequest(TaxpayerType.Corporation, null, null, null, null, "DEMO Corp", null, null,
            null, null, null, null, null, Sex.Male))).Code.ShouldBe("VALIDATION_FAILED");

        var corporation = (await taxpayers.CreateAsync(new CreateTaxpayerRequest(TaxpayerType.Corporation, null, null, null, null, "DEMO Corp", null, null,
            null, null, null, null, null))).Value;
        (await taxpayers.UpdateDetailsAsync(corporation.Id, new UpdateTaxpayerDetailsRequest(null, null, null, null, Sex.Male, "DEMO correction")))
            .Code.ShouldBe("VALIDATION_FAILED");
    }

    [Fact]
    public async Task CadastralNumber_IsOnTheProperty_AndAParcelMayOverrideIt()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var descriptions = c.Services.GetRequiredService<IDescriptionService>();
        var updated = await descriptions.UpdatePropertyAsync(c.Property.Id, new UpdatePropertyDescriptionRequest(
            null, null, "1", null, null, null, null, null, null, null, null, null, null, "DEMO survey data", " DEMO Cad-001 "));
        updated.IsSuccess.ShouldBeTrue(updated.IsSuccess ? null : updated.Message);
        (await c.Db.Properties.AsNoTracking().SingleAsync(x => x.Id == c.Property.Id)).CadastralNumber.ShouldBe("DEMO Cad-001");

        var parcel = await c.Services.GetRequiredService<IParcelService>().CreateAsync(new CreateParcelRequest(
            c.Property.Id, c.Barangay.Id, null, null, 100m, null, "1-A", null, "DEMO Cad-002"));
        parcel.IsSuccess.ShouldBeTrue(parcel.IsSuccess ? null : parcel.Message);
        parcel.Value.CadastralNumber.ShouldBe("DEMO Cad-002");
    }

    [Fact]
    public async Task MachineryAcquisitionDocuments_AreRecordedWithAReason()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var type = new MachineryType { Code = $"MT{Guid.NewGuid():N}"[..8], Name = "DEMO Generator" };
        var rpu = new RealPropertyUnit { PropertyId = c.Property.Id, RpuNumber = $"RPU-{Guid.NewGuid():N}", RpuType = RpuType.Machinery, EffectivityDate = new DateOnly(2026, 1, 1) };
        c.Db.AddRange(type, rpu);
        var machine = new Machinery { Rpu = rpu, PropertyId = c.Property.Id, MachineryType = type, AcquisitionCost = 1_000m };
        c.Db.Add(machine);
        await c.Db.SaveChangesAsync();

        var request = new UpdateMachineryDescriptionRequest("DEMO generator", null, null, null, null, null, null, null, null, "DEMO documents",
            "DEMO-ENG-1", new DateOnly(2025, 3, 1), "DEMO-IMP-1", new DateOnly(2025, 1, 15), "DEMO Supplier", "DEMO Supplier Address",
            "DEMO-OR-1", new DateOnly(2025, 2, 1));
        var descriptions = c.Services.GetRequiredService<IDescriptionService>();
        (await descriptions.UpdateMachineryAsync(machine.Id, request with { Reason = " " })).Code.ShouldBe("VALIDATION_FAILED");
        var result = await descriptions.UpdateMachineryAsync(machine.Id, request);
        result.IsSuccess.ShouldBeTrue(result.IsSuccess ? null : result.Message);

        var documents = (await c.Services.GetRequiredService<IMachineryService>().GetByIdAsync(machine.Id)).Value.Documents.ShouldNotBeNull();
        documents.ShouldBe(new MachineryDocumentsDto("DEMO-ENG-1", new DateOnly(2025, 3, 1), "DEMO-IMP-1", new DateOnly(2025, 1, 15),
            "DEMO Supplier", "DEMO Supplier Address", "DEMO-OR-1", new DateOnly(2025, 2, 1)));
    }

    [Fact]
    public async Task OfficeSanggunian_IsKeptPerOffice()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var offices = c.Services.GetRequiredService<IOfficeService>();
        var code = $"DEMO-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var created = await offices.CreateAsync(new CreateOfficeRequest(code, "DEMO Municipal Assessor's Office", OfficeKind.Municipal, null, null, null,
            null, "DEMO Sangguniang Bayan"));
        created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);
        created.Value.SanggunianName.ShouldBe("DEMO Sangguniang Bayan");

        var updated = await offices.UpdateAsync(created.Value.Id, new UpdateOfficeRequest(created.Value.Name, null, null, null, RecordStatus.Active,
            null, "  "));
        updated.Value.SanggunianName.ShouldBeNull();
    }

    [Fact]
    public async Task ReaLicence_IsSetWithItsValidity_AndAReason()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var user = new AppUser { SupabaseUserId = Guid.NewGuid(), DisplayName = "DEMO Appraiser", Email = $"demo-{Guid.NewGuid():N}@example.invalid" };
        c.Db.AppUsers.Add(user);
        await c.Db.SaveChangesAsync();
        var offices = c.Services.GetRequiredService<IOfficeService>();
        var validUntil = new DateOnly(2027, 12, 31);

        (await offices.UpdateUserLicenceAsync(user.Id, new UpdateUserLicenceRequest("DEMO-REA-1", null, "DEMO"))).Code.ShouldBe("VALIDATION_FAILED");
        (await offices.UpdateUserLicenceAsync(user.Id, new UpdateUserLicenceRequest("DEMO-REA-1", validUntil, ""))).Code.ShouldBe("VALIDATION_FAILED");
        (await offices.UpdateUserLicenceAsync(Guid.NewGuid(), new UpdateUserLicenceRequest(null, null, "DEMO"))).Code.ShouldBe("USER_NOT_FOUND");

        var set = await offices.UpdateUserLicenceAsync(user.Id, new UpdateUserLicenceRequest(" DEMO-REA-1 ", validUntil, "DEMO licence"));
        set.IsSuccess.ShouldBeTrue(set.IsSuccess ? null : set.Message);
        set.Value.ReaLicenceNumber.ShouldBe("DEMO-REA-1");
        set.Value.ReaLicenceValidUntil.ShouldBe(validUntil);
        (await offices.ListUsersAsync()).Value.Single(u => u.Id == user.Id).ReaLicenceNumber.ShouldBe("DEMO-REA-1");

        var cleared = (await offices.UpdateUserLicenceAsync(user.Id, new UpdateUserLicenceRequest(null, null, "DEMO expired, not renewed"))).Value;
        cleared.ReaLicenceNumber.ShouldBeNull();
        cleared.ReaLicenceValidUntil.ShouldBeNull();
    }
}
