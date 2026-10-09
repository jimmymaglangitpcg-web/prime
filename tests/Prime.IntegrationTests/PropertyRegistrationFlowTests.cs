using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Parcels;
using Prime.Application.Features.Properties;
using Prime.Application.Features.RealPropertyUnits;
using Prime.Application.Features.TaxDeclarations;
using Prime.Application.Features.Taxpayers;
using Prime.Domain.Entities.Audit;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Exercises the exact flow CLAUDE.md/docs/DEVELOPMENT-ROADMAP.md Phase 4
/// names as the exit criteria: create-property → create-taxpayer →
/// create-parcel → create-RPU → create-TD, through real HTTP requests
/// against the real WebApi host (authenticated via the Development bypass
/// — the same code path a real Supabase JWT would take once past
/// authentication), with real database writes, and verifies AuditLog rows
/// were produced for each step. This test commits real data (unlike
/// ConstraintTests' rolled-back transactions) because it spans multiple
/// independent HTTP requests, each with its own DbContext scope. Its rows
/// are not deleted afterwards: properties, TDs and ownership are history
/// tables that the database refuses to delete (HistoryDeleteGuards;
/// docs/analysis/production-hardening.md §4.3). They sit under one fixed
/// TEST_ province and reference set, reused on every run, and are
/// identifiable by the run's testId.
/// </summary>
public class PropertyRegistrationFlowTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    // Must match Program.cs's AddJsonOptions — the API serializes enums as
    // strings (found necessary by actually driving the frontend against
    // this API; see docs/DEVELOPMENT-ROADMAP.md Phase 4 frontend notes).
    // A plain HttpClient's default ReadFromJsonAsync doesn't know that,
    // same as any real API consumer wouldn't without configuring for it.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task FullRegistrationFlow_CreatesRecordsAndAuditLogs()
    {
        var client = factory.CreateClient();
        var testId = Guid.NewGuid().ToString("N")[..8];

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();

        var (province, municipality, barangay, classification, actualUse, ownershipType) = await TestReferenceAsync(db);

        {
            // 1. Create Property
            var createPropertyResponse = await client.PostAsJsonAsync("/api/properties", new CreatePropertyRequest(
                PropertyIdentificationNumber: $"TEST-PIN-{testId}",
                ProvinceId: province.Id,
                MunicipalityId: municipality.Id,
                BarangayId: barangay.Id,
                ZoneId: null,
                Street: "123 Test Street",
                Sitio: null,
                LotNumber: "L-1",
                BlockNumber: null,
                SurveyNumber: null,
                TitleNumber: null,
                TaxMapNumber: null));
            createPropertyResponse.StatusCode.ShouldBe(HttpStatusCode.Created, await createPropertyResponse.Content.ReadAsStringAsync());
            var property = await createPropertyResponse.Content.ReadFromJsonAsync<PropertyDto>(JsonOptions);
            property.ShouldNotBeNull();

            // 2. Create Taxpayer
            var createTaxpayerResponse = await client.PostAsJsonAsync("/api/taxpayers", new CreateTaxpayerRequest(
                TaxpayerType: TaxpayerType.Individual,
                LastName: "DelaCruz",
                FirstName: "Juan",
                MiddleName: null,
                Suffix: null,
                CorporateName: null,
                Tin: $"TIN-{testId}",
                Address: null,
                BarangayId: barangay.Id,
                MunicipalityId: municipality.Id,
                ProvinceId: province.Id,
                ContactNumber: null,
                Email: null));
            createTaxpayerResponse.StatusCode.ShouldBe(HttpStatusCode.Created, await createTaxpayerResponse.Content.ReadAsStringAsync());
            var taxpayer = await createTaxpayerResponse.Content.ReadFromJsonAsync<TaxpayerDto>(JsonOptions);
            taxpayer.ShouldNotBeNull();
            taxpayer.DisplayName.ShouldBe("DelaCruz, Juan");

            // 2b. Register ownership
            var addOwnerResponse = await client.PostAsJsonAsync(
                $"/api/properties/{property.Id}/owners",
                new TaxpayersController_AddOwnerBody(taxpayer.Id, ownershipType.Id, 100m, DateOnly.FromDateTime(DateTime.UtcNow)));
            addOwnerResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await addOwnerResponse.Content.ReadAsStringAsync());

            // 3. Create Parcel
            var createParcelResponse = await client.PostAsJsonAsync("/api/parcels", new CreateParcelRequest(
                PropertyId: property.Id,
                BarangayId: barangay.Id,
                ZoneId: null,
                GeometryWkt: "POLYGON((121.0 14.5, 121.001 14.5, 121.001 14.501, 121.0 14.501, 121.0 14.5))",
                Area: 500m,
                SurveyNumber: null,
                LotNumber: "L-1",
                BlockNumber: null));
            createParcelResponse.StatusCode.ShouldBe(HttpStatusCode.Created, await createParcelResponse.Content.ReadAsStringAsync());
            var parcel = await createParcelResponse.Content.ReadFromJsonAsync<ParcelDto>(JsonOptions);
            parcel.ShouldNotBeNull();

            // 4. Create RPU
            var createRpuResponse = await client.PostAsJsonAsync("/api/rpus", new CreateRpuRequest(
                PropertyId: property.Id,
                RpuNumber: $"TEST-RPU-{testId}",
                RpuType: RpuType.Land,
                EffectivityDate: DateOnly.FromDateTime(DateTime.UtcNow),
                PreviousRpuId: null));
            createRpuResponse.StatusCode.ShouldBe(HttpStatusCode.Created, await createRpuResponse.Content.ReadAsStringAsync());
            var rpu = await createRpuResponse.Content.ReadFromJsonAsync<RpuDto>(JsonOptions);
            rpu.ShouldNotBeNull();

            // 5. Create Tax Declaration
            var createTdResponse = await client.PostAsJsonAsync("/api/tax-declarations", new CreateTaxDeclarationRequest(
                RpuId: rpu.Id,
                TaxDeclarationNumber: $"TEST-TD-{testId}",
                EffectivityDate: DateOnly.FromDateTime(DateTime.UtcNow),
                Taxability: Taxability.Taxable,
                ClassificationId: classification.Id,
                ActualUseId: actualUse.Id,
                SubClassificationId: null,
                AssessmentYear: 2026,
                PreviousTaxDeclarationId: null,
                Remarks: null));
            createTdResponse.StatusCode.ShouldBe(HttpStatusCode.Created, await createTdResponse.Content.ReadAsStringAsync());
            var taxDeclaration = await createTdResponse.Content.ReadFromJsonAsync<TaxDeclarationDto>(JsonOptions);
            taxDeclaration.ShouldNotBeNull();

            // 6. A building on the land, owned apart from it (docs/analysis/mrpaao-forms-model.md §6.2–6.4).
            // The same taxpayer as the property owner: at the property's scope this would be a duplicate.
            var buildingResponse = await client.PostAsJsonAsync("/api/rpus", new CreateRpuRequest(
                property.Id, $"TEST-BLDG-{testId}", RpuType.Building, DateOnly.FromDateTime(DateTime.UtcNow), null, LandRpuId: rpu.Id));
            buildingResponse.StatusCode.ShouldBe(HttpStatusCode.Created, await buildingResponse.Content.ReadAsStringAsync());
            var building = (await buildingResponse.Content.ReadFromJsonAsync<RpuDto>(JsonOptions)).ShouldNotBeNull();
            building.PinSuffix.ShouldBe(1001);
            building.LandRpuId.ShouldBe(rpu.Id);
            var unitOwnerResponse = await client.PostAsJsonAsync($"/api/properties/{property.Id}/owners",
                new TaxpayersController_AddOwnerBody(taxpayer.Id, ownershipType.Id, 100m, DateOnly.FromDateTime(DateTime.UtcNow), RpuId: building.Id));
            unitOwnerResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await unitOwnerResponse.Content.ReadAsStringAsync());
            (await unitOwnerResponse.Content.ReadFromJsonAsync<PropertyOwnerDto>(JsonOptions)).ShouldNotBeNull().RpuId.ShouldBe(building.Id);
            var units = await client.GetFromJsonAsync<List<RpuDto>>($"/api/properties/{property.Id}/rpus", JsonOptions);
            // LAM convention (identification-numbering.md Q6): no parentheses for a building owned apart from the land.
            units.ShouldNotBeNull().Single(u => u.Id == building.Id).UnitPin.ShouldBe($"TEST-PIN-{testId}-1001");

            // Verify: Property Profile shows everything
            var profileResponse = await client.GetAsync($"/api/properties/{property.Id}");
            profileResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            var profile = await profileResponse.Content.ReadFromJsonAsync<PropertyProfileDto>(JsonOptions);
            profile.ShouldNotBeNull();
            profile.Owners.ShouldContain(o => o.TaxpayerId == taxpayer.Id);
            profile.Parcels.ShouldContain(p => p.Id == parcel.Id);
            profile.Rpus.ShouldContain(r => r.Id == rpu.Id);
            profile.TaxDeclarations.ShouldContain(t => t.Id == taxDeclaration.Id);

            // Verify: audit log entries exist for each created record
            // (CLAUDE.md §48; Phase 4 exit criteria).
            using var verifyScope = factory.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PrimeDbContext>();

            // Table names as EF Core actually generated them: "Property"
            // and "RealPropertyUnit" have explicit singular ToTable()
            // overrides; the rest use EF's default pluralized convention.
            await AssertAuditLogExists(verifyDb, "Property", property.Id, AuditAction.Create);
            await AssertAuditLogExists(verifyDb, "Taxpayers", taxpayer.Id, AuditAction.Create);
            await AssertAuditLogExists(verifyDb, "Parcels", parcel.Id, AuditAction.Create);
            await AssertAuditLogExists(verifyDb, "RealPropertyUnit", rpu.Id, AuditAction.Create);
            await AssertAuditLogExists(verifyDb, "TaxDeclarations", taxDeclaration.Id, AuditAction.Create);

            var propertyAuditLog = await verifyDb.AuditLogs
                .Where(a => a.TableName == "Property" && a.RecordId == property.Id)
                .SingleAsync();
            propertyAuditLog.UserId.ShouldNotBeNull();
            propertyAuditLog.NewValue.ShouldNotBeNull();
            propertyAuditLog.NewValue.ShouldContain(property.PropertyIdentificationNumber);
        }
    }

    private static async Task AssertAuditLogExists(PrimeDbContext db, string tableName, Guid recordId, AuditAction action)
    {
        var exists = await db.AuditLogs.AnyAsync(a => a.TableName == tableName && a.RecordId == recordId && a.Action == action);
        exists.ShouldBeTrue($"Expected an AuditLog row for {tableName}/{recordId}/{action}.");
    }

    // One TEST_ reference set shared by every run: the committed rows of a run hang off it and cannot be deleted.
    internal static async Task<(Province, Municipality, Barangay, Classification, ActualUse, OwnershipType)> TestReferenceAsync(PrimeDbContext db)
    {
        const string key = "REGFLOW";
        var province = await db.Provinces.FirstOrDefaultAsync(x => x.PsgcCode == $"TESTP{key}")
            ?? db.Provinces.Add(new Province { PsgcCode = $"TESTP{key}", Name = "TEST_Province_RegistrationFlow" }).Entity;
        var municipality = await db.Municipalities.FirstOrDefaultAsync(x => x.PsgcCode == $"TESTM{key}")
            ?? db.Municipalities.Add(new Municipality { Province = province, PsgcCode = $"TESTM{key}", Name = "TEST_Municipality_RegistrationFlow" }).Entity;
        var barangay = await db.Barangays.FirstOrDefaultAsync(x => x.PsgcCode == $"TESTB{key}")
            ?? db.Barangays.Add(new Barangay { Municipality = municipality, PsgcCode = $"TESTB{key}", Name = "TEST_Barangay_RegistrationFlow" }).Entity;
        var classification = await db.Classifications.FirstOrDefaultAsync(x => x.Code == $"TESTCL{key}")
            ?? db.Classifications.Add(new Classification { Code = $"TESTCL{key}", Name = "TEST_Classification_RegistrationFlow" }).Entity;
        var actualUse = await db.ActualUses.FirstOrDefaultAsync(x => x.Code == $"TESTAU{key}")
            ?? db.ActualUses.Add(new ActualUse { Code = $"TESTAU{key}", Name = "TEST_ActualUse_RegistrationFlow" }).Entity;
        var ownershipType = await db.OwnershipTypes.FirstOrDefaultAsync(x => x.Code == $"TESTOT{key}")
            ?? db.OwnershipTypes.Add(new OwnershipType { Code = $"TESTOT{key}", Name = "TEST_OwnershipType_RegistrationFlow" }).Entity;
        await db.SaveChangesAsync();
        return (province, municipality, barangay, classification, actualUse, ownershipType);
    }

    // Mirrors TaxpayersController.AddOwnerBody's shape for the client-side POST body.
    private sealed record TaxpayersController_AddOwnerBody(Guid TaxpayerId, Guid OwnershipTypeId, decimal OwnershipPercentage, DateOnly StartDate,
        PropertyPartyRole? Role = null, Guid? RpuId = null);
}
