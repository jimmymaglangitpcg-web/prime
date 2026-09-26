using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Properties;
using Prime.Application.Features.PropertyIdentification;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Forms;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Phase 10a-2 (docs/analysis/property-identification.md §3.3–§3.4; MRPAAO Ch. II §1):
/// temporary PINs at registration, the permanent PIN when a parcel is placed in a
/// tax map section, PIN history, and index numbers locked once used. Rolled back;
/// every index number and pattern is DEMO data, and the dev database's approved
/// numbering schemes and index numbers are cleared inside the transaction.
/// </summary>
public class PinAssignmentTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string PinPattern = "{LGUIDX}-{MUNIDX}-{BRGYIDX}-{SECT}-{SEQ:2}";

    private sealed record Place(Province Province, Municipality Municipality, Barangay Barangay, TaxMapSection Section);

    private sealed record Context(PrimeDbContext Db, IServiceProvider Services, Place Town)
    {
        public IPinService Pins => Services.GetRequiredService<IPinService>();
        public IPropertyService Properties => Services.GetRequiredService<IPropertyService>();
    }

    private async Task<(Context C, IAsyncDisposable Scope)> BeginAsync(bool pinScheme = true, bool temporaryScheme = true, bool manualEntry = true)
    {
        var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        await db.NumberingSchemes.Where(x => x.Status == WorkflowStatus.Approved)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null));
        await db.Provinces.ExecuteUpdateAsync(s => s.SetProperty(x => x.PinIndexNumber, (string?)null));
        await db.Municipalities.ExecuteUpdateAsync(s => s.SetProperty(x => x.PinIndexNumber, (string?)null));

        var town = await PlaceAsync(db, "020", "15", null, "0005", "002");
        if (pinScheme)
        {
            db.Add(Approved(new NumberingScheme { AppliesTo = NumberedDocumentKind.PropertyIdentificationNumber, Name = "DEMO MRPAAO PIN", Pattern = PinPattern, AllowManualEntry = manualEntry }));
        }
        if (temporaryScheme)
        {
            db.Add(Approved(new NumberingScheme { AppliesTo = NumberedDocumentKind.TemporaryPin, Name = "DEMO temporary PIN", Pattern = "T-{MUNIDX}-{BRGYIDX}-{SEQ:4}" }));
        }
        await db.SaveChangesAsync();
        return (new Context(db, scope.ServiceProvider, town), new Disposer(transaction, scope));
    }

    private sealed class Disposer(IAsyncDisposable transaction, IAsyncDisposable scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await scope.DisposeAsync();
        }
    }

    private static NumberingScheme Approved(NumberingScheme scheme)
    {
        scheme.LegalBasis = "DEMO — MRPAAO Ch. II layout, not an LGU source";
        scheme.EffectiveDate = new DateOnly(2020, 1, 1);
        scheme.Status = WorkflowStatus.Approved;
        scheme.ApprovedAt = DateTimeOffset.UtcNow;
        return scheme;
    }

    /// <summary>A DEMO place: a province (or a city with its own number and a district), a barangay and a section.</summary>
    private static async Task<Place> PlaceAsync(PrimeDbContext db, string lguIndex, string munOrDistrict, string? cityIndex, string barangayIndex, string sectionIndex)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var province = new Province { PsgcCode = $"PA-P{tag}", Name = "DEMO_PA Province", PinIndexNumber = cityIndex is null ? lguIndex : null };
        var municipality = new Municipality
        {
            Province = province, PsgcCode = $"PA-M{tag}", Name = "DEMO_PA LGU", IsCity = cityIndex is not null,
            PinIndexNumber = cityIndex ?? munOrDistrict,
        };
        var district = cityIndex is null ? null : new CityDistrict { Municipality = municipality, IndexNumber = munOrDistrict, Name = "DEMO_PA District" };
        var barangay = new Barangay { Municipality = municipality, CityDistrict = district, PsgcCode = $"PA-B{tag}", Name = "DEMO_PA Barangay", PinIndexNumber = barangayIndex };
        var section = new TaxMapSection { Barangay = barangay, IndexNumber = sectionIndex };
        db.AddRange(province, municipality, barangay, section);
        if (district is not null)
        {
            db.Add(district);
        }
        await db.SaveChangesAsync();
        return new Place(province, municipality, barangay, section);
    }

    private static async Task<(PropertyDto Property, Parcel Parcel)> RegisterAsync(Context c, Place place, string? typedPin = null)
    {
        var created = await c.Properties.CreateAsync(new CreatePropertyRequest(
            typedPin, place.Province.Id, place.Municipality.Id, place.Barangay.Id, null, "DEMO Street", null, null, null, null, null, null));
        created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);
        var parcel = new Parcel { PropertyId = created.Value.Id, BarangayId = place.Barangay.Id, Area = 500m };
        c.Db.Parcels.Add(parcel);
        await c.Db.SaveChangesAsync();
        return (created.Value, parcel);
    }

    [Fact]
    public async Task Registration_GivesATemporaryPin_AndThePermanentPinComesFromTheSection()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;

        var (first, firstParcel) = await RegisterAsync(c, c.Town);
        first.PropertyIdentificationNumber.ShouldBe("T-15-0005-0001");

        var placed = await c.Pins.PlaceInSectionAsync(first.Id, new(firstParcel.Id, c.Town.Section.Id));

        placed.IsSuccess.ShouldBeTrue(placed.IsSuccess ? null : placed.Message);
        var pin = placed.Value;
        pin.Pin.ShouldBe("020-15-0005-002-01");
        pin.Kind.ShouldBe(PinKind.Permanent);
        (pin.LguIndex, pin.MunicipalityIndex, pin.BarangayIndex, pin.SectionIndex, pin.ParcelNumber).ShouldBe(("020", "15", "0005", "002", 1));
        pin.History.Select(h => (h.Pin, h.Kind, h.RetiredAt is null)).ShouldBe([("T-15-0005-0001", PinKind.Temporary, false), ("020-15-0005-002-01", PinKind.Permanent, true)]);
        (await c.Db.Parcels.SingleAsync(x => x.Id == firstParcel.Id)).ParcelNumber.ShouldBe(1);
        (await c.Db.Properties.SingleAsync(x => x.Id == first.Id)).PropertyIdentificationNumber.ShouldBe("020-15-0005-002-01");

        var (second, secondParcel) = await RegisterAsync(c, c.Town);
        (await c.Pins.PlaceInSectionAsync(second.Id, new(secondParcel.Id, c.Town.Section.Id))).Value.Pin.ShouldBe("020-15-0005-002-02");
        (await c.Pins.PlaceInSectionAsync(first.Id, new(firstParcel.Id, c.Town.Section.Id))).Code.ShouldBe("PIN_ALREADY_PERMANENT");
    }

    [Fact]
    public async Task CityDistrict_GivesTheCityAndDistrictNumbers()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var city = await PlaceAsync(c.Db, "", "06", "132", "0012", "001");
        var (property, parcel) = await RegisterAsync(c, city);

        (await c.Pins.PlaceInSectionAsync(property.Id, new(parcel.Id, city.Section.Id))).Value.Pin.ShouldBe("132-06-0012-001-01");
    }

    [Fact]
    public async Task MigratedParcelNumber_IsKept_AndNeverGeneratedAgain_UntilTheSectionIsFull()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;

        var (migrated, migratedParcel) = await RegisterAsync(c, c.Town, typedPin: "DEMO-OLD-PIN-1");
        migrated.PropertyIdentificationNumber.ShouldBe("DEMO-OLD-PIN-1");
        (await c.Pins.PlaceInSectionAsync(migrated.Id, new(migratedParcel.Id, c.Town.Section.Id, ParcelNumber: 35))).Value.Pin.ShouldBe("020-15-0005-002-35");

        var (next, nextParcel) = await RegisterAsync(c, c.Town);
        (await c.Pins.PlaceInSectionAsync(next.Id, new(nextParcel.Id, c.Town.Section.Id))).Value.Pin.ShouldBe("020-15-0005-002-36");

        var (last, lastParcel) = await RegisterAsync(c, c.Town);
        (await c.Pins.PlaceInSectionAsync(last.Id, new(lastParcel.Id, c.Town.Section.Id, ParcelNumber: 99))).Value.Pin.ShouldBe("020-15-0005-002-99");
        var (overflow, overflowParcel) = await RegisterAsync(c, c.Town);
        (await c.Pins.PlaceInSectionAsync(overflow.Id, new(overflowParcel.Id, c.Town.Section.Id))).Code.ShouldBe("TAX_MAP_SECTION_FULL");

        // A parcel number already given cannot be typed again.
        (await c.Pins.PlaceInSectionAsync(overflow.Id, new(overflowParcel.Id, c.Town.Section.Id, ParcelNumber: 35))).Code.ShouldBe("PIN_DUPLICATE");
        (await c.Pins.GetAsync(migrated.Id)).Value.History.First().Pin.ShouldBe("DEMO-OLD-PIN-1");
    }

    [Fact]
    public async Task Placement_IsRefused_ForTheWrongOrRetiredSection_OrMissingNumbers()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var (property, parcel) = await RegisterAsync(c, c.Town);
        var elsewhere = await PlaceAsync(c.Db, "021", "16", null, "0001", "001");

        (await c.Pins.PlaceInSectionAsync(property.Id, new(parcel.Id, elsewhere.Section.Id))).Code.ShouldBe("VALIDATION_FAILED");
        (await c.Pins.PlaceInSectionAsync(property.Id, new(Guid.NewGuid(), c.Town.Section.Id))).Code.ShouldBe("PARCEL_NOT_FOUND");

        var retired = new TaxMapSection { BarangayId = c.Town.Barangay.Id, IndexNumber = "009", RetiredOn = new DateOnly(2026, 1, 1), RetirementReason = "DEMO" };
        c.Db.Add(retired);
        await c.Db.SaveChangesAsync();
        (await c.Pins.PlaceInSectionAsync(property.Id, new(parcel.Id, retired.Id))).Code.ShouldBe("TAX_MAP_SECTION_RETIRED");

        await c.Db.Barangays.Where(x => x.Id == c.Town.Barangay.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.PinIndexNumber, (string?)null));
        c.Db.ChangeTracker.Clear();
        (await c.Pins.PlaceInSectionAsync(property.Id, new(parcel.Id, c.Town.Section.Id))).Code.ShouldBe("NUMBER_CONTEXT_MISSING");
    }

    [Fact]
    public async Task WithoutASectionedScheme_PlacementIsRefused_AndRegistrationWithoutAnyPinNeedsATypedOne()
    {
        var (c, scope) = await BeginAsync(pinScheme: false);
        await using var _ = scope;
        var (property, parcel) = await RegisterAsync(c, c.Town, typedPin: "DEMO-TYPED-1");

        (await c.Pins.PlaceInSectionAsync(property.Id, new(parcel.Id, c.Town.Section.Id))).Code.ShouldBe("PIN_SCHEME_NOT_SECTIONED");
        (await c.Properties.CreateAsync(new CreatePropertyRequest(null, c.Town.Province.Id, c.Town.Municipality.Id, c.Town.Barangay.Id,
            null, null, null, null, null, null, null, null))).Code.ShouldBe("NUMBER_REQUIRED");
    }

    [Fact]
    public async Task SectionedSchemeWithoutTemporaryPins_NeedsATypedPin()
    {
        var (c, scope) = await BeginAsync(temporaryScheme: false);
        await using var _ = scope;

        (await c.Properties.CreateAsync(new CreatePropertyRequest(null, c.Town.Province.Id, c.Town.Municipality.Id, c.Town.Barangay.Id,
            null, null, null, null, null, null, null, null))).Code.ShouldBe("NUMBER_REQUIRED");
        (await RegisterAsync(c, c.Town, typedPin: "DEMO-TYPED-2")).Property.PropertyIdentificationNumber.ShouldBe("DEMO-TYPED-2");
    }

    [Fact]
    public async Task IndexNumbers_AreLocked_OncePermanentPinsUseThem()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var ids = c.Services.GetRequiredService<IPropertyIdentificationService>();
        var (property, parcel) = await RegisterAsync(c, c.Town);
        (await ids.SetBarangayIndexAsync(c.Town.Barangay.Id, new("0006", null, "DEMO before any PIN"))).IsSuccess.ShouldBeTrue();
        (await ids.SetBarangayIndexAsync(c.Town.Barangay.Id, new("0005", null, "DEMO back"))).IsSuccess.ShouldBeTrue();

        (await c.Pins.PlaceInSectionAsync(property.Id, new(parcel.Id, c.Town.Section.Id))).IsSuccess.ShouldBeTrue();

        (await ids.SetBarangayIndexAsync(c.Town.Barangay.Id, new("0006", null, "DEMO"))).Code.ShouldBe("PIN_INDEX_LOCKED");
        (await ids.SetMunicipalityIndexAsync(c.Town.Municipality.Id, new("16", "DEMO"))).Code.ShouldBe("PIN_INDEX_LOCKED");
        (await ids.SetProvinceIndexAsync(c.Town.Province.Id, new("021", "DEMO"))).Code.ShouldBe("PIN_INDEX_LOCKED");
        // A registered PIN is never given to another property.
        (await c.Properties.CreateAsync(new CreatePropertyRequest("T-15-0005-0001", c.Town.Province.Id, c.Town.Municipality.Id, c.Town.Barangay.Id,
            null, null, null, null, null, null, null, null))).Code.ShouldBe("PROPERTY_PIN_DUPLICATE");
    }
}
