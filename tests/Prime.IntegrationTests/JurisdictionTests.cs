using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Common;
using Prime.Application.Features.Parcels;
using Prime.Application.Features.Properties;
using Prime.Application.Features.RealPropertyUnits;
using Prime.Application.Features.TaxDeclarations;
using Prime.Application.Features.Taxpayers;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step LP-2 (docs/analysis/province-wide-operation.md §3.3): a municipal
/// user reads and writes only in the municipalities their office covers. The
/// sweep checks the model (every property-rooted entity is filtered), the rows
/// (nothing outside the jurisdiction is returned) and the services (reads say
/// "not found", writes are refused, province-wide uniqueness still holds, and
/// taxpayers follow Q5). Everything is rolled back.
/// </summary>
public class JurisdictionTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    /// <summary>Property-rooted entities deliberately left unfiltered, with the reason.</summary>
    private static readonly Dictionary<string, string> Unfiltered = new()
    {
        ["TaxBill"] = "frozen treasury (CLAUDE.md §0)",
        ["PaymentAllocation"] = "frozen treasury (CLAUDE.md §0)",
        ["PropertyTransactionProperty"] = "a transaction's related properties; reached through its filtered transaction",
        ["TerritorialChangeItem"] = "a provincial territorial change spanning municipalities; read through its job (identification-numbering.md §4.3)",
    };

    private sealed record World(
        IServiceProvider Services, PrimeDbContext Db, JurisdictionState Jurisdiction, Province Province,
        Municipality A, Municipality B, Barangay BarangayA, Barangay BarangayB,
        PropertyEntity PropertyA, PropertyEntity PropertyB, RealPropertyUnit RpuB, Parcel ParcelB, TaxDeclaration TdB,
        Taxpayer OwnerA, Taxpayer OwnerB, Taxpayer Unlinked);

    private async Task<(World W, IAsyncDisposable Scope)> BeginAsync()
    {
        var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        var tag = Random.Shared.Next(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);

        var province = new Province { PsgcCode = $"91{tag}", Name = $"DEMO LP2 Province {tag}" };
        var a = new Municipality { Province = province, PsgcCode = $"91{tag[..6]}01", Name = $"DEMO LP2 Town A {tag}" };
        var b = new Municipality { Province = province, PsgcCode = $"91{tag[..6]}02", Name = $"DEMO LP2 Town B {tag}" };
        var barangayA = new Barangay { Municipality = a, PsgcCode = $"91{tag[..6]}011", Name = "DEMO LP2 Brgy A" };
        var barangayB = new Barangay { Municipality = b, PsgcCode = $"91{tag[..6]}021", Name = "DEMO LP2 Brgy B" };
        db.Barangays.AddRange(barangayA, barangayB);

        PropertyEntity Property(Municipality m, Barangay brgy, string suffix) => new()
        {
            PropertyIdentificationNumber = $"DEMO-LP2-{tag}-{suffix}", Province = province, Municipality = m, Barangay = brgy, Status = RecordStatus.Active,
        };
        var propertyA = Property(a, barangayA, "A");
        var propertyB = Property(b, barangayB, "B");
        var rpuB = new RealPropertyUnit { Property = propertyB, RpuNumber = $"DEMO-LP2-RPU-{tag}", RpuType = RpuType.Land, EffectivityDate = new DateOnly(2026, 1, 1) };
        var parcelB = new Parcel { Property = propertyB, BarangayId = barangayB.Id, Area = 100m };
        var tdB = new TaxDeclaration
        {
            Rpu = rpuB, Property = propertyB, TaxDeclarationNumber = $"DEMO-LP2-TD-{tag}", EffectivityDate = new DateOnly(2026, 1, 1), AssessmentYear = 2026,
            ClassificationId = await db.Classifications.Select(x => x.Id).FirstAsync(), ActualUseId = await db.ActualUses.Select(x => x.Id).FirstAsync(),
        };
        Taxpayer Person(string last, string tin) => new() { TaxpayerType = TaxpayerType.Individual, LastName = last, FirstName = "DEMO", Tin = tin, Address = "DEMO address", Status = RecordStatus.Active };
        var ownerA = Person($"DemoLpTwoAlpha{tag}", $"A{tag}");
        var ownerB = Person($"DemoLpTwoBravo{tag}", $"B{tag}");
        var unlinked = Person($"DemoLpTwoCharlie{tag}", $"C{tag}");
        db.AddRange(propertyA, propertyB, rpuB, parcelB, tdB, ownerA, ownerB, unlinked);
        var ownershipType = await db.OwnershipTypes.Select(x => (Guid?)x.Id).FirstOrDefaultAsync()
            ?? db.OwnershipTypes.Add(new OwnershipType { Code = $"DEMO-LP2-{tag}", Name = "DEMO ownership" }).Entity.Id;
        PropertyTaxpayer Owns(PropertyEntity p, Taxpayer t) => new()
        {
            Property = p, Taxpayer = t, Role = PropertyPartyRole.Owner, OwnershipTypeId = ownershipType, OwnershipPercentage = 100m,
            StartDate = new DateOnly(2026, 1, 1), IsCurrent = true,
        };
        db.PropertyTaxpayers.AddRange(Owns(propertyA, ownerA), Owns(propertyB, ownerB));
        await db.SaveChangesAsync();

        var jurisdiction = scope.ServiceProvider.GetRequiredService<JurisdictionState>();
        return (new World(scope.ServiceProvider, db, jurisdiction, province, a, b, barangayA, barangayB, propertyA, propertyB, rpuB, parcelB, tdB,
            ownerA, ownerB, unlinked), new Disposer(transaction, scope));
    }

    private sealed class Disposer(IAsyncDisposable transaction, IAsyncDisposable scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await scope.DisposeAsync();
        }
    }

    [Fact]
    public void EveryPropertyRootedEntity_HasAJurisdictionFilter()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var missing = db.Model.GetEntityTypes()
            .Where(t => t.FindProperty("PropertyId") is { IsNullable: false } && !t.GetDeclaredQueryFilters().Any())
            .Select(t => t.ClrType.Name)
            .Where(name => !Unfiltered.ContainsKey(name))
            .ToList();
        missing.ShouldBeEmpty("Each entity carrying a property must be filtered by jurisdiction, or listed in Unfiltered with its reason.");
        db.Model.FindEntityType(typeof(PropertyEntity))!.GetDeclaredQueryFilters().ShouldNotBeEmpty();
        db.Model.FindEntityType(typeof(Prime.Domain.Entities.SwornStatements.SwornStatement))!.GetDeclaredQueryFilters().ShouldNotBeEmpty();
        db.Model.FindEntityType(typeof(Prime.Domain.Entities.Registers.RegisterRun))!.GetDeclaredQueryFilters().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ARestrictedRequest_ReadsNoRowOutsideItsMunicipalities()
    {
        var (w, scope) = await BeginAsync();
        await using var _ = scope;
        (await w.Db.Properties.CountAsync()).ShouldBeGreaterThan(1);

        // A jurisdiction of one empty municipality: every filtered table must come back empty.
        var empty = new Municipality { Province = w.Province, PsgcCode = $"{w.A.PsgcCode[..8]}99", Name = "DEMO LP2 Empty" };
        w.Db.Municipalities.Add(empty);
        await w.Db.SaveChangesAsync();
        w.Jurisdiction.Restrict([empty.Id]);

        var db = w.Db;
        var counts = new Dictionary<string, int>
        {
            ["Properties"] = await db.Properties.CountAsync(),
            ["RealPropertyUnits"] = await db.RealPropertyUnits.CountAsync(),
            ["Parcels"] = await db.Parcels.CountAsync(),
            ["Lands"] = await db.Lands.CountAsync(),
            ["Buildings"] = await db.Buildings.CountAsync(),
            ["MachineryUnits"] = await db.MachineryUnits.CountAsync(),
            ["PropertyTaxpayers"] = await db.PropertyTaxpayers.CountAsync(),
            ["PinAssignments"] = await db.PinAssignments.CountAsync(),
            ["Valuations"] = await db.Valuations.CountAsync(),
            ["Assessments"] = await db.Assessments.CountAsync(),
            ["TaxDeclarations"] = await db.TaxDeclarations.CountAsync(),
            ["NoticesOfAssessment"] = await db.NoticesOfAssessment.CountAsync(),
            ["NoticeOfAssessmentItems"] = await db.NoticeOfAssessmentItems.CountAsync(),
            ["PropertyTransactions"] = await db.PropertyTransactions.CountAsync(),
            ["SwornStatements"] = await db.SwornStatements.CountAsync(),
            ["RegisterRuns (by barangay)"] = await db.RegisterRuns.CountAsync(x => x.BarangayId != null),
        };
        counts.Where(c => c.Value != 0).Select(c => c.Key).ShouldBeEmpty();
        // Uniqueness checks still see the province.
        (await db.Properties.IgnoreQueryFilters().AnyAsync(p => p.Id == w.PropertyB.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task AMunicipalUser_CannotReadOrWriteAnotherMunicipality_ButUniquenessIsProvinceWide()
    {
        var (w, scope) = await BeginAsync();
        await using var _ = scope;
        w.Jurisdiction.Restrict([w.A.Id]);
        var properties = w.Services.GetRequiredService<IPropertyService>();
        var rpus = w.Services.GetRequiredService<IRealPropertyUnitService>();
        var parcels = w.Services.GetRequiredService<IParcelService>();
        var tds = w.Services.GetRequiredService<ITaxDeclarationService>();
        var taxpayers = w.Services.GetRequiredService<ITaxpayerService>();

        // Reads: the other municipality's records do not exist for this user.
        (await properties.GetProfileAsync(w.PropertyB.Id)).Code.ShouldBe("PROPERTY_NOT_FOUND");
        (await properties.GetProfileAsync(w.PropertyA.Id)).IsSuccess.ShouldBeTrue();
        var search = (await properties.SearchAsync(new PropertySearchRequest { SearchTerm = w.PropertyA.PropertyIdentificationNumber[..^2], PageSize = 100 })).Value;
        search.Items.Select(p => p.Id).ShouldBe([w.PropertyA.Id]);
        (await rpus.GetByIdAsync(w.RpuB.Id)).Code.ShouldBe("RPU_NOT_FOUND");
        (await parcels.GetByIdAsync(w.ParcelB.Id)).Code.ShouldBe("PARCEL_NOT_FOUND");
        (await tds.GetByIdAsync(w.TdB.Id)).Code.ShouldBe("TAX_DECLARATION_NOT_FOUND");
        (await taxpayers.GetOwnershipHistoryAsync(w.PropertyB.Id)).Code.ShouldBe("PROPERTY_NOT_FOUND");

        // Writes: nothing is created in, or under, the other municipality.
        (await properties.CreateAsync(new CreatePropertyRequest(null, w.Province.Id, w.B.Id, w.BarangayB.Id, null, null, null, null, null, null, null, null)))
            .Code.ShouldBe("JURISDICTION_FORBIDDEN");
        (await rpus.CreateAsync(new CreateRpuRequest(w.PropertyB.Id, "DEMO-LP2-NEW", RpuType.Land, new DateOnly(2026, 1, 1), null))).Code.ShouldBe("PROPERTY_NOT_FOUND");
        (await taxpayers.AddOwnerAsync(new AddPropertyOwnerRequest(w.PropertyB.Id, w.OwnerA.Id, null, 0m, new DateOnly(2026, 1, 1), PropertyPartyRole.Administrator)))
            .Code.ShouldBe("PROPERTY_NOT_FOUND");

        // Uniqueness is province-wide: the other municipality's RPU number is still taken.
        (await rpus.CreateAsync(new CreateRpuRequest(w.PropertyA.Id, w.RpuB.RpuNumber, RpuType.Land, new DateOnly(2026, 1, 1), null))).Code.ShouldBe("RPU_NUMBER_DUPLICATE");
    }

    [Fact]
    public async Task Taxpayers_AreProvinceWide_WithFullDetailsOnlyInTheJurisdiction()
    {
        var (w, scope) = await BeginAsync();
        await using var _ = scope;
        // A user who may see personal data, so only the jurisdiction limits what is shown (masking: PersonalDataTests).
        var encoder = await TestSeed.UserWithRoleAsync(w.Db, RoleCodes.AssessmentEncoder);
        await w.Db.SaveChangesAsync();
        w.Services.GetRequiredService<Prime.Infrastructure.Identity.CurrentUserService>().AppUserId = encoder.Id;
        w.Jurisdiction.Restrict([w.A.Id]);
        var taxpayers = w.Services.GetRequiredService<ITaxpayerService>();

        var own = (await taxpayers.GetByIdAsync(w.OwnerA.Id)).Value;
        (own.Limited, own.Address).ShouldBe((false, "DEMO address"));
        var fresh = (await taxpayers.GetByIdAsync(w.Unlinked.Id)).Value;
        (fresh.Limited, fresh.Tin).ShouldBe((true, w.Unlinked.Tin)); // not a party in the jurisdiction: name and TIN, enough to link
        var other = (await taxpayers.GetByIdAsync(w.OwnerB.Id)).Value;
        (other.Limited, other.Address, other.Tin).ShouldBe((true, (string?)null, w.OwnerB.Tin));

        // A partial name finds nothing outside the jurisdiction; the exact TIN finds the owner, name and TIN only.
        var partial = (await taxpayers.SearchAsync(new TaxpayerSearchRequest { SearchTerm = "DemoLpTwoBravo", PageSize = 50 })).Value;
        partial.Items.ShouldNotContain(t => t.Id == w.OwnerB.Id);
        var exact = (await taxpayers.SearchAsync(new TaxpayerSearchRequest { SearchTerm = w.OwnerB.Tin, PageSize = 50 })).Value;
        var found = exact.Items.Single(t => t.Id == w.OwnerB.Id);
        (found.Limited, found.Address).ShouldBe((true, (string?)null));
        var mine = (await taxpayers.SearchAsync(new TaxpayerSearchRequest { SearchTerm = "DemoLpTwoAlpha", PageSize = 50 })).Value;
        mine.Items.Single(t => t.Id == w.OwnerA.Id).Limited.ShouldBeFalse();
    }

    [Fact]
    public async Task TheRequestPipeline_RestrictsTheDemoMunicipalAppraiser()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Prime-Dev-Act-As", "mun-appraiser");
        var me = await client.GetFromJsonAsync<JsonElement>("/api/me");
        var allowed = me.GetProperty("municipalityIds").EnumerateArray().Select(x => x.GetGuid()).ToHashSet();
        allowed.ShouldNotBeEmpty();

        var page = await client.GetFromJsonAsync<JsonElement>("/api/properties?pageSize=200");
        page.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("municipalityId").GetGuid()).ShouldAllBe(id => allowed.Contains(id));

        // The usual (province-wide) user sees at least as much.
        var admin = factory.CreateClient();
        var all = await admin.GetFromJsonAsync<JsonElement>("/api/properties?pageSize=1");
        all.GetProperty("totalCount").GetInt32().ShouldBeGreaterThanOrEqualTo(page.GetProperty("totalCount").GetInt32());
    }
}
