using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Properties;
using Prime.Application.Features.PropertyIdentification;
using Prime.Application.Features.RealPropertyUnits;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Forms;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step L2-2 (docs/analysis/identification-numbering.md §4.2; exit criterion 2): the LAM's unit PINs (Book II
/// pp.38–39) — building, leasing property and its units by floor, machinery on a lot and in a unit, mineral right,
/// structures over water with parcel 000 — composed from DEMO index numbers (020, 15, 0005, section 002) and a DEMO
/// LAM-format PIN scheme with a 3-digit parcel. The dev database's approved schemes and index numbers are cleared
/// inside the transaction.
/// </summary>
public class StructuredUnitPinTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, Province Province, Municipality Town, Barangay Barangay, TaxMapSection Section)
    {
        public IPinService Pins => Services.GetRequiredService<IPinService>();
        public IPropertyService Properties => Services.GetRequiredService<IPropertyService>();
        public IRealPropertyUnitService Rpus => Services.GetRequiredService<IRealPropertyUnitService>();
    }

    private async Task<(Ctx C, IAsyncDisposable Scope)> BeginAsync()
    {
        var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        await db.NumberingSchemes.Where(x => x.Status == WorkflowStatus.Approved)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null));
        await db.Provinces.ExecuteUpdateAsync(s => s.SetProperty(x => x.PinIndexNumber, (string?)null));
        await db.Municipalities.ExecuteUpdateAsync(s => s.SetProperty(x => x.PinIndexNumber, (string?)null));
        var tag = Guid.NewGuid().ToString("N")[..8];
        var province = new Province { PsgcCode = $"SU-P{tag}", Name = "DEMO_SU Province", PinIndexNumber = "020" };
        var town = new Municipality { Province = province, PsgcCode = $"SU-M{tag}", Name = "DEMO_SU Town", PinIndexNumber = "15" };
        var barangay = new Barangay { Municipality = town, PsgcCode = $"SU-B{tag}", Name = "DEMO_SU Barangay", PinIndexNumber = "0005" };
        var section = new TaxMapSection { Barangay = barangay, IndexNumber = "002" };
        db.AddRange(province, town, barangay, section,
            Approved(new NumberingScheme { AppliesTo = NumberedDocumentKind.PropertyIdentificationNumber, Name = "DEMO LAM PIN", Pattern = "{LGUIDX}-{MUNIDX}-{BRGYIDX}-{SECT}-{SEQ:3}" }),
            Approved(new NumberingScheme { AppliesTo = NumberedDocumentKind.TemporaryPin, Name = "DEMO temporary PIN", Pattern = "T-{MUNIDX}-{BRGYIDX}-{SEQ:4}" }));
        await db.SaveChangesAsync();
        return (new Ctx(db, scope.ServiceProvider, province, town, barangay, section), new Disposer(transaction, scope));
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
        scheme.LegalBasis = "DEMO — LAM Book II layout, not an LGU source";
        scheme.EffectiveDate = new DateOnly(2020, 1, 1);
        scheme.Status = WorkflowStatus.Approved;
        scheme.ApprovedAt = DateTimeOffset.UtcNow;
        return scheme;
    }

    private static async Task<PropertyDto> RegisterAsync(Ctx c)
    {
        var created = await c.Properties.CreateAsync(new CreatePropertyRequest(
            null, c.Province.Id, c.Town.Id, c.Barangay.Id, null, "DEMO Street", null, null, null, null, null, null));
        created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);
        return created.Value;
    }

    private static async Task<RpuDto> UnitAsync(Ctx c, Guid propertyId, RpuType type, Guid? host = null, bool leasing = false, string? floorPrefix = null,
        int? floor = null, int? unit = null)
    {
        var created = await c.Rpus.CreateAsync(new CreateRpuRequest(propertyId, $"RPU-{Guid.NewGuid():N}", type, new DateOnly(2026, 1, 1), null,
            HostRpuId: host, IsLeasingProperty: leasing, FloorPrefix: floorPrefix, FloorNumber: floor, UnitNumber: unit));
        created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);
        return created.Value;
    }

    [Fact]
    public async Task EveryLamUnitPinExample_IsComposed()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var property = await RegisterAsync(c);
        var parcel = new Parcel { PropertyId = property.Id, BarangayId = c.Barangay.Id, Area = 500m };
        c.Db.Parcels.Add(parcel);
        await c.Db.SaveChangesAsync();
        (await c.Pins.PlaceInSectionAsync(property.Id, new(parcel.Id, c.Section.Id))).Value.Pin.ShouldBe("020-15-0005-002-001"); // 3-digit parcel

        await UnitAsync(c, property.Id, RpuType.Land);
        (await UnitAsync(c, property.Id, RpuType.Building)).UnitPin.ShouldBe("020-15-0005-002-001-1001");
        var condo = await UnitAsync(c, property.Id, RpuType.Building, leasing: true);
        condo.UnitPin.ShouldBe("020-15-0005-002-001-(3001)");
        var unit = await UnitAsync(c, property.Id, RpuType.Building, condo.Id, floor: 1);
        unit.UnitPin.ShouldBe("020-15-0005-002-001-(3001)F01-001");
        (await UnitAsync(c, property.Id, RpuType.Building, condo.Id, floor: 1)).UnitPin.ShouldBe("020-15-0005-002-001-(3001)F01-002"); // next on the floor
        (await UnitAsync(c, property.Id, RpuType.Building, condo.Id, floorPrefix: "B", floor: 1)).UnitPin.ShouldBe("020-15-0005-002-001-(3001)B01-001");
        (await UnitAsync(c, property.Id, RpuType.Machinery, unit.Id)).UnitPin.ShouldBe("020-15-0005-002-001-(3001)F01-2001"); // machinery in the unit
        (await UnitAsync(c, property.Id, RpuType.Machinery)).UnitPin.ShouldBe("020-15-0005-002-001-2002");                    // machinery on the lot
        (await UnitAsync(c, property.Id, RpuType.MineralRight)).UnitPin.ShouldBe("020-15-0005-002-001-4001");

        // A unit's number is unique on its floor; a floor prefix is one of the configured ones; a unit belongs to a leasing property.
        (await c.Rpus.CreateAsync(new CreateRpuRequest(property.Id, $"RPU-{Guid.NewGuid():N}", RpuType.Building, new DateOnly(2026, 1, 1), null,
            HostRpuId: condo.Id, FloorNumber: 1, UnitNumber: 1))).Code.ShouldBe("UNIT_NUMBER_DUPLICATE");
        (await c.Rpus.CreateAsync(new CreateRpuRequest(property.Id, $"RPU-{Guid.NewGuid():N}", RpuType.Building, new DateOnly(2026, 1, 1), null,
            HostRpuId: condo.Id, FloorPrefix: "X", FloorNumber: 1))).Code.ShouldBe("VALIDATION_FAILED");
        var plain = (await c.Rpus.ListByPropertyAsync(property.Id)).Value.First(r => r.UnitPin.EndsWith("-1001"));
        (await c.Rpus.CreateAsync(new CreateRpuRequest(property.Id, $"RPU-{Guid.NewGuid():N}", RpuType.Building, new DateOnly(2026, 1, 1), null,
            HostRpuId: plain.Id, FloorNumber: 1))).Code.ShouldBe("RPU_HOST_LINK_INVALID");
    }

    [Fact]
    public async Task StructuresOverWater_TakeParcel000_OnePropertyPerSection()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var wharf = await RegisterAsync(c);

        var placed = await c.Pins.PlaceInSectionAsync(wharf.Id, new(Guid.Empty, c.Section.Id, OverWater: true));

        placed.IsSuccess.ShouldBeTrue(placed.IsSuccess ? null : placed.Message);
        placed.Value.Pin.ShouldBe("020-15-0005-002-000");
        (await c.Db.Properties.SingleAsync(x => x.Id == wharf.Id)).IsOverWater.ShouldBeTrue();
        (await UnitAsync(c, wharf.Id, RpuType.Building)).UnitPin.ShouldBe("020-15-0005-002-000-1001");
        (await UnitAsync(c, wharf.Id, RpuType.Building)).UnitPin.ShouldBe("020-15-0005-002-000-1002");

        // A second structure over water in the section is a unit of that property, not a property of its own.
        var other = await RegisterAsync(c);
        (await c.Pins.PlaceInSectionAsync(other.Id, new(Guid.Empty, c.Section.Id, OverWater: true))).Code.ShouldBe("PIN_OVER_WATER_EXISTS");
        // A property on land is not placed over water.
        await UnitAsync(c, other.Id, RpuType.Land);
        var otherSection = new TaxMapSection { BarangayId = c.Barangay.Id, IndexNumber = "003" };
        c.Db.Add(otherSection);
        await c.Db.SaveChangesAsync();
        (await c.Pins.PlaceInSectionAsync(other.Id, new(Guid.Empty, otherSection.Id, OverWater: true))).Code.ShouldBe("PIN_OVER_WATER_HAS_LAND");
    }

    [Fact]
    public void TheParenthesisConvention_IsASetting()
    {
        const string pin = "020-15-0005-002-005";
        UnitPin.Compose(pin, new UnitPinParts(1001), true, UnitPinParentheses.LeasingProperty).ShouldBe("020-15-0005-002-005-1001");
        UnitPin.Compose(pin, new UnitPinParts(1001), true, UnitPinParentheses.Parcel).ShouldBe("020-15-0005-002-(005)-1001"); // MRPAAO p.42
        UnitPin.Compose(pin, new UnitPinParts(1001), false, UnitPinParentheses.Parcel).ShouldBe("020-15-0005-002-005-1001");
        UnitPin.Compose(pin, new UnitPinParts(null, 3001, "F", 1, 1), false, UnitPinParentheses.LeasingProperty).ShouldBe("020-15-0005-002-005-(3001)F01-001");
        UnitPin.Compose(pin, new UnitPinParts(null, 3001, "F", 1, 1), false, UnitPinParentheses.Parcel).ShouldBe("020-15-0005-002-005-3001F01-001");
        UnitPin.Compose(pin, new UnitPinParts(null), false, UnitPinParentheses.LeasingProperty).ShouldBe(pin); // land
        UnitPin.Compose("T-15-0005-0001", new UnitPinParts(1001), false, UnitPinParentheses.LeasingProperty, "B1").ShouldBe("T-15-0005-0001B1");
    }
}
