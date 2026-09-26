using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.PropertyIdentification;
using Prime.Domain.Entities.Reference;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Phase 10a-1 (docs/analysis/property-identification.md §3.1–§3.2; MRPAAO Ch. II §1):
/// index numbers, city districts and tax map sections. Rolled back; every
/// place and number is DEMO data, and index numbers already in the dev
/// database are cleared inside the transaction.
/// </summary>
public class PropertyIdentificationTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Context(PrimeDbContext Db, IPropertyIdentificationService Service, Province Province, Municipality Town, Municipality City,
        Barangay[] Barangays);

    private async Task<(Context C, IAsyncDisposable Scope)> BeginAsync()
    {
        var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        await db.Provinces.ExecuteUpdateAsync(s => s.SetProperty(x => x.PinIndexNumber, (string?)null));
        await db.Municipalities.ExecuteUpdateAsync(s => s.SetProperty(x => x.PinIndexNumber, (string?)null));

        var tag = Guid.NewGuid().ToString("N")[..8];
        var province = new Province { PsgcCode = $"PI-P{tag}", Name = "DEMO_PI Province" };
        var town = new Municipality { Province = province, PsgcCode = $"PI-M{tag}", Name = "DEMO_PI Town" };
        var city = new Municipality { Province = province, PsgcCode = $"PI-C{tag}", Name = "DEMO_PI City", IsCity = true };
        var barangays = Enumerable.Range(1, 3).Select(i => new Barangay { Municipality = town, PsgcCode = $"PI-B{i}{tag}", Name = $"DEMO_PI Barangay {i}" })
            .Append(new Barangay { Municipality = city, PsgcCode = $"PI-CB{tag}", Name = "DEMO_PI City Barangay" }).ToArray();
        db.AddRange(province, town, city);
        db.AddRange(barangays);
        await db.SaveChangesAsync();

        var context = new Context(db, scope.ServiceProvider.GetRequiredService<IPropertyIdentificationService>(), province, town, city, barangays);
        return (context, new Disposer(transaction, scope));
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
    public async Task LguNumbers_ShareOneSpace_AndAChangeNeedsAReason()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var s = c.Service;

        (await s.SetProvinceIndexAsync(c.Province.Id, new("20", null))).Code.ShouldBe("VALIDATION_FAILED");
        (await s.SetProvinceIndexAsync(c.Province.Id, new("020", null))).Value.PinIndexNumber.ShouldBe("020");
        // A city's own 3-digit number may not repeat a province's.
        (await s.SetMunicipalityIndexAsync(c.City.Id, new("020", null))).Code.ShouldBe("PIN_INDEX_DUPLICATE");
        (await s.SetMunicipalityIndexAsync(c.City.Id, new("132", null))).Value.PinIndexNumber.ShouldBe("132");
        // A municipality's 2-digit number is unique within its province.
        (await s.SetMunicipalityIndexAsync(c.Town.Id, new("15", null))).Value.PinIndexNumber.ShouldBe("15");
        var other = new Municipality { ProvinceId = c.Province.Id, PsgcCode = $"PI-O{Guid.NewGuid():N}"[..14], Name = "DEMO_PI Other" };
        c.Db.Add(other);
        await c.Db.SaveChangesAsync();
        (await s.SetMunicipalityIndexAsync(other.Id, new("15", null))).Code.ShouldBe("PIN_INDEX_DUPLICATE");

        (await s.SetProvinceIndexAsync(c.Province.Id, new("021", null))).Code.ShouldBe("VALIDATION_FAILED");
        (await s.SetProvinceIndexAsync(c.Province.Id, new("021", "DEMO corrected per BLGF list"))).Value.PinIndexNumber.ShouldBe("021");
    }

    [Fact]
    public async Task Districts_TakeTheNextNumber_AndBarangayNumbersAreUniquePerDistrict()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var s = c.Service;

        var first = (await s.CreateDistrictAsync(c.City.Id, new(null, "DEMO District I"))).Value;
        var second = (await s.CreateDistrictAsync(c.City.Id, new(null, "DEMO District II"))).Value;
        first.IndexNumber.ShouldBe("01");
        second.IndexNumber.ShouldBe("02");
        (await s.CreateDistrictAsync(c.City.Id, new("02", "DEMO again"))).Code.ShouldBe("PIN_INDEX_DUPLICATE");
        (await s.CreateDistrictAsync(c.City.Id, new("2", "DEMO bad"))).Code.ShouldBe("VALIDATION_FAILED");

        var cityBarangay = c.Barangays[3];
        (await s.SetBarangayIndexAsync(cityBarangay.Id, new("0001", first.Id, null))).Value.CityDistrictIndexNumber.ShouldBe("01");
        // A district of another city or municipality is refused.
        (await s.SetBarangayIndexAsync(c.Barangays[0].Id, new("0001", first.Id, null))).Code.ShouldBe("CITY_DISTRICT_NOT_FOUND");

        // In a municipality without districts the number is unique in the municipality (NULL district included).
        (await s.SetBarangayIndexAsync(c.Barangays[0].Id, new("0001", null, null))).IsSuccess.ShouldBeTrue();
        (await s.SetBarangayIndexAsync(c.Barangays[1].Id, new("0001", null, null))).Code.ShouldBe("PIN_INDEX_DUPLICATE");
        (await s.SetBarangayIndexAsync(c.Barangays[1].Id, new("001", null, null))).Code.ShouldBe("VALIDATION_FAILED");

        var listed = (await s.ListBarangaysAsync(c.Town.Id)).Value;
        listed.First().PinIndexNumber.ShouldBe("0001");
        (await s.ListMunicipalitiesAsync(c.Province.Id)).Value.Single(m => m.Id == c.City.Id).DistrictCount.ShouldBe(2);
    }

    [Fact]
    public async Task DividedBarangay_IsRetired_AndTheNewOnesTakeTheNextNumbers()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var s = c.Service;
        // As in the manual's illustration: the mother is 0012 and the highest number is 0021.
        await s.SetBarangayIndexAsync(c.Barangays[0].Id, new("0012", null, null));
        await s.SetBarangayIndexAsync(c.Barangays[1].Id, new("0021", null, null));
        var tag = Guid.NewGuid().ToString("N")[..8];

        var split = await s.SplitBarangayAsync(c.Barangays[0].Id, new("DEMO Ord. creating four barangays", new DateOnly(2026, 1, 1),
            [new("DEMO East", $"PI-E{tag}"), new("DEMO North", $"PI-N{tag}"), new("DEMO South", $"PI-S{tag}"), new("DEMO West", $"PI-W{tag}")]));

        split.IsSuccess.ShouldBeTrue(split.IsSuccess ? null : split.Message);
        split.Value.Select(b => (b.PinIndexNumber, b.RetiredOn is not null))
            .ShouldBe([("0012", true), ("0022", false), ("0023", false), ("0024", false), ("0025", false)]);
        split.Value.Where(b => b.RetiredOn is null).ShouldAllBe(b => b.SplitFromBarangayId == c.Barangays[0].Id);

        (await s.SetBarangayIndexAsync(c.Barangays[0].Id, new("0099", null, "DEMO"))).Code.ShouldBe("BARANGAY_RETIRED");
        (await s.SplitBarangayAsync(c.Barangays[0].Id, new("DEMO", new DateOnly(2026, 1, 1), [new("a", $"PI-a{tag}"), new("b", $"PI-b{tag}")])))
            .Code.ShouldBe("BARANGAY_RETIRED");
        (await s.CreateSectionAsync(c.Barangays[0].Id, new(null, null, null))).Code.ShouldBe("BARANGAY_RETIRED");
        // The retired number is never given again.
        (await s.SetBarangayIndexAsync(c.Barangays[2].Id, new("0012", null, null))).Code.ShouldBe("PIN_INDEX_DUPLICATE");
    }

    [Fact]
    public async Task Sections_TakeTheNextNumber_AndRetiredNumbersAreNeverReused()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var s = c.Service;
        var barangay = c.Barangays[0].Id;

        var first = (await s.CreateSectionAsync(barangay, new(null, "DEMO upper-left", null))).Value;
        var second = (await s.CreateSectionAsync(barangay, new(null, null, null))).Value;
        first.IndexNumber.ShouldBe("001");
        second.IndexNumber.ShouldBe("002");

        (await s.RetireSectionAsync(second.Id, new(" ", new DateOnly(2026, 1, 1)))).Code.ShouldBe("VALIDATION_FAILED");
        (await s.RetireSectionAsync(second.Id, new("DEMO tax map revision", new DateOnly(2026, 1, 1)))).Value.RetiredOn.ShouldNotBeNull();
        (await s.RetireSectionAsync(second.Id, new("again", new DateOnly(2026, 1, 1)))).Code.ShouldBe("TAX_MAP_SECTION_RETIRED");

        (await s.CreateSectionAsync(barangay, new("002", null, null))).Code.ShouldBe("PIN_INDEX_DUPLICATE");
        var crowded = (await s.CreateSectionAsync(barangay, new(null, "DEMO subdivision drawn apart", first.Id))).Value;
        crowded.IndexNumber.ShouldBe("003");
        crowded.SplitFromSectionId.ShouldBe(first.Id);
        (await s.CreateSectionAsync(barangay, new(null, null, Guid.NewGuid()))).Code.ShouldBe("TAX_MAP_SECTION_NOT_FOUND");
        (await s.ListSectionsAsync(barangay)).Value.Select(x => x.IndexNumber).ShouldBe(["001", "002", "003"]);
    }
}
