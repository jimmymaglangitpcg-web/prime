using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Forms;
using Prime.Application.Features.Gis;
using Prime.Application.Features.Gis.ReferenceLayers;
using Prime.Application.Features.Registers;
using Prime.Domain.Common;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Phase 10a-4 (docs/analysis/property-identification.md §3.5, §3.7; MRPAAO Ch. II §2):
/// the post-TMCR per tax map section, the pre-TMCR, the section boundary layer and the
/// index map sheets. Rolled back; every index number, PIN and shape is DEMO data.
/// </summary>
public class TaxMapRollsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Town(Province Province, Municipality Municipality, Barangay Barangay, TaxMapSection Section, TaxMapSection OtherSection, Classification Class, ActualUse Use);

    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, Town Town, string Tag, double Lon, double Lat)
    {
        public IRegisterService Registers => Services.GetRequiredService<IRegisterService>();
        public IFormService Forms => Services.GetRequiredService<IFormService>();
        public IReferenceLayerService Layers => Services.GetRequiredService<IReferenceLayerService>();
        public ITaxMapSheetService Sheets => Services.GetRequiredService<ITaxMapSheetService>();
        public DateOnly Today => Services.GetRequiredService<IClock>().Today;
    }

    private async Task<(Ctx C, IAsyncDisposable Scope)> BeginAsync()
    {
        var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        await TestSeed.UseReferenceFormsAsync(db); // these tests assert the reference layouts
        // Index numbers share number spaces with the dev database's DEMO ones; clear them inside the transaction.
        await db.Provinces.ExecuteUpdateAsync(s => s.SetProperty(x => x.PinIndexNumber, (string?)null));
        await db.Municipalities.ExecuteUpdateAsync(s => s.SetProperty(x => x.PinIndexNumber, (string?)null));

        var tag = Guid.NewGuid().ToString("N")[..8];
        var province = new Province { PsgcCode = $"TR-P{tag}", Name = "DEMO_TR Province", PinIndexNumber = "977" };
        var municipality = new Municipality { Province = province, PsgcCode = $"TR-M{tag}", Name = "DEMO_TR Municipality", PinIndexNumber = "04" };
        var barangay = new Barangay { Municipality = municipality, PsgcCode = $"TR-B{tag}", Name = "DEMO_TR Barangay", PinIndexNumber = "0007" };
        var section = new TaxMapSection { Barangay = barangay, IndexNumber = "003" };
        var other = new TaxMapSection { Barangay = barangay, IndexNumber = "004" };
        var classification = new Classification { Code = $"R{tag}"[..6], Name = "DEMO_TR Residential" };
        var use = new ActualUse { Code = $"U{tag}"[..6], Name = "DEMO_TR Residential use" };
        db.AddRange(province, municipality, barangay, section, other, classification, use);
        await db.SaveChangesAsync();
        var town = new Town(province, municipality, barangay, section, other, classification, use);
        return (new Ctx(db, scope.ServiceProvider, town, tag, 121.0 + Random.Shared.NextDouble(), 14.0 + Random.Shared.NextDouble()),
            new Disposer(transaction, scope));
    }

    private sealed class Disposer(IAsyncDisposable transaction, IAsyncDisposable scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await scope.DisposeAsync();
        }
    }

    private static readonly DateTimeOffset Mapped = new DateTimeOffset(2025, 1, 10, 0, 0, 0, TimeSpan.FromHours(8)).ToUniversalTime();

    /// <summary>A property with its parcel, an optional approved land TD, and its PIN history.</summary>
    private static async Task<PropertyEntity> PropertyAsync(Ctx c, string pin, TaxMapSection? section, int? parcelNumber, string? tdNumber,
        string? temporaryPin = null, DateTimeOffset? retiredAt = null, Polygon? shape = null)
    {
        var t = c.Town;
        var property = new PropertyEntity
        {
            PropertyIdentificationNumber = pin, ProvinceId = t.Province.Id, MunicipalityId = t.Municipality.Id, BarangayId = t.Barangay.Id,
            SurveyNumber = $"PSD-{parcelNumber}", LotNumber = $"{parcelNumber}", TitleNumber = $"T-{parcelNumber}",
        };
        var parcel = new Parcel
        {
            Property = property, BarangayId = t.Barangay.Id, SectionId = section?.Id, ParcelNumber = parcelNumber, Area = 400m,
            Geometry = shape is null ? null : new MultiPolygon([shape]) { SRID = SpatialReference.StorageSrid },
        };
        c.Db.AddRange(property, parcel);
        if (temporaryPin is not null)
        {
            c.Db.Add(new PinAssignment
            {
                Property = property, Pin = temporaryPin, Kind = PinKind.Temporary, BarangayId = t.Barangay.Id, AssignedAt = Mapped.AddDays(-5),
                RetiredAt = section is null ? null : Mapped, RetirementReason = section is null ? null : "Superseded",
            });
        }
        if (section is not null)
        {
            c.Db.Add(new PinAssignment
            {
                Property = property, Pin = pin, Kind = PinKind.Permanent, Parcel = parcel, BarangayId = t.Barangay.Id, SectionId = section.Id,
                ParcelNumber = parcelNumber, AssignedAt = Mapped, RetiredAt = retiredAt, RetirementReason = retiredAt is null ? null : "Retired by the DEMO subdivision",
            });
        }
        if (tdNumber is not null)
        {
            var rpu = new RealPropertyUnit { Property = property, RpuNumber = $"RPU-{Guid.NewGuid():N}", RpuType = RpuType.Land, EffectivityDate = new DateOnly(2024, 1, 1) };
            c.Db.AddRange(rpu,
                new Land { Rpu = rpu, Property = property, Area = 450m, AreaUnit = "sqm", ClassificationId = t.Class.Id, ActualUseId = t.Use.Id },
                new TaxDeclaration
                {
                    Rpu = rpu, Property = property, TaxDeclarationNumber = tdNumber, EffectivityDate = new DateOnly(2024, 1, 1),
                    Taxability = Taxability.Taxable, ClassificationId = t.Class.Id, ActualUseId = t.Use.Id, AssessmentYear = 2024, Status = WorkflowStatus.Approved,
                });
        }
        await c.Db.SaveChangesAsync();
        return property;
    }

    private static async Task<RegisterRunDto> RunAsync(Ctx c, RegisterKind kind, DateOnly asOf, Guid? sectionId = null)
    {
        var run = await c.Registers.CreateRunAsync(new CreateRegisterRunRequest(kind, asOf, sectionId is null ? c.Town.Barangay.Id : null, null, null, null, "DEMO run", sectionId));
        run.IsSuccess.ShouldBeTrue(run.IsSuccess ? null : run.Message);
        return run.Value;
    }

    private static async Task<string> PreviewAsync(Ctx c, RegisterRunDto run)
    {
        var preview = await c.Forms.PreviewAsync(run.FormCode, run.Id);
        preview.IsSuccess.ShouldBeTrue(preview.IsSuccess ? null : preview.Message);
        return preview.Value.Html;
    }

    private Polygon Square(Ctx c, double dx, double dy, double size) => new GeometryFactory(new PrecisionModel(), SpatialReference.StorageSrid).CreatePolygon(
    [
        new(c.Lon + dx, c.Lat + dy), new(c.Lon + dx + size, c.Lat + dy), new(c.Lon + dx + size, c.Lat + dy + size),
        new(c.Lon + dx, c.Lat + dy + size), new(c.Lon + dx, c.Lat + dy),
    ]);

    [Fact]
    public async Task SectionRoll_ListsTheSectionInPinOrder_WithIndexNumbers_AndNotesRetiredPins()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var p = $"D{c.Tag}-0007";
        await PropertyAsync(c, $"{p}-003-02", c.Town.Section, 2, $"TD-{c.Tag}-2");
        await PropertyAsync(c, $"{p}-003-01", c.Town.Section, 1, null);
        await PropertyAsync(c, $"{p}-003-03", c.Town.Section, 3, $"TD-{c.Tag}-3", retiredAt: new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.FromHours(8)).ToUniversalTime());
        await PropertyAsync(c, $"{p}-004-01", c.Town.OtherSection, 1, $"TD-{c.Tag}-4");

        var run = await RunAsync(c, RegisterKind.TaxMapControlRoll, c.Today, c.Town.Section.Id);
        run.BarangayId.ShouldBe(c.Town.Barangay.Id);
        (run.SectionId, run.SectionIndexNumber, run.FormCode).ShouldBe((c.Town.Section.Id, "003", "TMCR"));

        var html = await PreviewAsync(c, run);
        html.ShouldContain("Index No. <b>977</b>");
        html.ShouldContain("Index No. <b>04</b>");
        html.ShouldContain("Index No. <b>0007</b>");
        html.ShouldContain("Section Index No. <b>003</b>");
        html.IndexOf("No land FAAS in force", StringComparison.Ordinal).ShouldBeLessThan(html.IndexOf($"TD-{c.Tag}-2", StringComparison.Ordinal)); // 01 before 02
        html.ShouldContain($"PIN {p}-003-03 — retired");
        html.ShouldContain("PIN retired 2026-06-01: Retired by the DEMO subdivision");
        html.ShouldNotContain($"TD-{c.Tag}-3");
        html.ShouldNotContain($"TD-{c.Tag}-4"); // another section

        // Before the retirement, lot 03 is an ordinary entry.
        var earlier = await PreviewAsync(c, await RunAsync(c, RegisterKind.TaxMapControlRoll, new DateOnly(2026, 5, 31), c.Town.Section.Id));
        earlier.ShouldContain($"TD-{c.Tag}-3");
        earlier.ShouldNotContain("— retired");
        // Before the section was mapped, it is empty.
        (await PreviewAsync(c, await RunAsync(c, RegisterKind.TaxMapControlRoll, new DateOnly(2025, 1, 9), c.Town.Section.Id)))
            .ShouldContain("No parcel is listed in this section");
    }

    [Fact]
    public async Task SectionRuns_AreValidated()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var other = new Barangay { MunicipalityId = c.Town.Municipality.Id, PsgcCode = $"TR-X{c.Tag}", Name = "DEMO_TR Other" };
        c.Db.Add(other);
        await c.Db.SaveChangesAsync();

        (await c.Registers.CreateRunAsync(new(RegisterKind.RecordOfAssessment, c.Today, c.Town.Barangay.Id, c.Town.Class.Id, null, null, null, c.Town.Section.Id)))
            .Code.ShouldBe("VALIDATION_FAILED");
        (await c.Registers.CreateRunAsync(new(RegisterKind.TaxMapControlRoll, c.Today, other.Id, null, null, null, null, c.Town.Section.Id)))
            .Code.ShouldBe("VALIDATION_FAILED");
        (await c.Registers.CreateRunAsync(new(RegisterKind.TaxMapControlRoll, c.Today, null, null, null, null, null, Guid.NewGuid())))
            .Code.ShouldBe("TAX_MAP_SECTION_NOT_FOUND");
        (await c.Registers.CreateRunAsync(new(RegisterKind.PreTaxMapControlRoll, c.Today, null, null, null, null, null)))
            .Code.ShouldBe("VALIDATION_FAILED");
    }

    [Fact]
    public async Task BarangayRoll_WithoutASection_IsInPinOrder()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        await PropertyAsync(c, $"D{c.Tag}-0007-004-01", c.Town.OtherSection, 1, $"TD-{c.Tag}-B");
        await PropertyAsync(c, $"D{c.Tag}-0007-003-01", c.Town.Section, 1, $"TD-{c.Tag}-A");

        var html = await PreviewAsync(c, await RunAsync(c, RegisterKind.TaxMapControlRoll, c.Today));

        html.ShouldContain("(whole barangay)");
        html.IndexOf($"TD-{c.Tag}-A", StringComparison.Ordinal).ShouldBeLessThan(html.IndexOf($"TD-{c.Tag}-B", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PreRoll_ListsLandFaasInTemporaryPinOrder_WithFinalPinsAndAreas()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        // About 11 m × 11 m at this latitude: a measured area well above zero.
        await PropertyAsync(c, $"D{c.Tag}-0007-003-01", c.Town.Section, 1, $"TD-{c.Tag}-2", temporaryPin: $"T{c.Tag}-0002", shape: Square(c, 0, 0, 0.0001));
        await PropertyAsync(c, $"T{c.Tag}-0001", null, null, $"TD-{c.Tag}-1", temporaryPin: $"T{c.Tag}-0001");
        await PropertyAsync(c, $"R{c.Tag}-typed", null, null, $"TD-{c.Tag}-9");
        await PropertyAsync(c, $"T{c.Tag}-0003", null, null, null, temporaryPin: $"T{c.Tag}-0003"); // no FAAS: not listed

        var run = await RunAsync(c, RegisterKind.PreTaxMapControlRoll, c.Today);
        run.FormCode.ShouldBe("PRE_TMCR");
        var html = await PreviewAsync(c, run);

        html.ShouldContain("PRE TAX MAPPING CONTROL ROLL");
        html.ShouldContain("Barangay No. <b>0007</b>");
        var first = html.IndexOf($"TD-{c.Tag}-1", StringComparison.Ordinal);
        var second = html.IndexOf($"TD-{c.Tag}-2", StringComparison.Ordinal);
        var untagged = html.IndexOf($"TD-{c.Tag}-9", StringComparison.Ordinal);
        first.ShouldBeGreaterThan(0);
        first.ShouldBeLessThan(second);
        second.ShouldBeLessThan(untagged); // land without a temporary PIN comes last
        html.ShouldContain($"D{c.Tag}-0007-003-01"); // the final PIN
        html.ShouldContain("450 sqm"); // declared
        html.ShouldNotContain($"T{c.Tag}-0003");
        html.ShouldContain("Land FAAS listed: <b>3</b>");

        var parcelId = await c.Db.Parcels.Where(x => x.SectionId == c.Town.Section.Id).Select(x => x.Id).SingleAsync();
        var measured = (await c.Services.GetRequiredService<IGeometryMeasurementService>().GetParcelAreasAsync([parcelId]))[parcelId];
        measured.ShouldBeGreaterThan(50m);
        html.ShouldContain(Math.Round(measured, 2).ToString("#,##0.####", CultureInfo.InvariantCulture));
    }

    private static JsonElement Collection(params string[] features) =>
        JsonDocument.Parse($$"""{"type":"FeatureCollection","features":[{{string.Join(",", features)}}]}""").RootElement.Clone();

    private static string SectionFeature(string? psgc, string? section, Polygon shape)
    {
        var props = string.Join(",", new[] { psgc is null ? null : $"\"psgcCode\":\"{psgc}\"", section is null ? null : $"\"section\":\"{section}\"" }.Where(x => x is not null));
        var ring = string.Join(",", shape.ExteriorRing.Coordinates.Select(p => FormattableString.Invariant($"[{p.X},{p.Y}]")));
        return $$$"""{"type":"Feature","properties":{{{{props}}}},"geometry":{"type":"Polygon","coordinates":[[{{{ring}}}]]}}""";
    }

    [Fact]
    public async Task SectionLayer_ImportsBySectionKey_AndDrawsTheSheets()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var psgc = c.Town.Barangay.PsgcCode;
        var request = (JsonElement fc) => new ImportReferenceLayerRequest(new DateOnly(2025, 1, 1), "DEMO tax map sections", null, fc);

        var bad = await c.Layers.ImportAsync(ReferenceLayer.Sections, request(Collection(
            SectionFeature(psgc, "009", Square(c, 0, 0, 0.001)), SectionFeature(psgc, null, Square(c, 0, 0, 0.001)))), dryRun: true);
        bad.Value.Errors.Select(e => e.Code).ShouldBe(["KEY_REQUIRED", "TAX_MAP_SECTION_NOT_FOUND"], ignoreOrder: true);

        var imported = await c.Layers.ImportAsync(ReferenceLayer.Sections, request(Collection(
            SectionFeature(psgc, "003", Square(c, 0, 0, 0.001)), SectionFeature(psgc, "004", Square(c, 0.001, 0, 0.001)))), dryRun: false);
        imported.Value.Errors.ShouldBeEmpty();
        (imported.Value.Committed, imported.Value.NewFeatures).ShouldBe((true, 2));

        var bbox = FormattableString.Invariant($"{c.Lon - 0.01},{c.Lat - 0.01},{c.Lon + 0.01},{c.Lat + 0.01}");
        var layer = await c.Layers.GetFeaturesAsync(ReferenceLayer.Sections, bbox, c.Today, null);
        layer.Value.Features.Select(f => (f.Properties.Key, f.Properties.Name)).ShouldBe(
            [($"{psgc}/003", "Section 003"), ($"{psgc}/004", "Section 004")], ignoreOrder: true);

        // The tax map of section 003: its boundary, the four index numbers.
        var sheet = (await c.Sheets.TaxMapAsync(c.Town.Section.Id, c.Today)).Value;
        sheet.Heading.Select(h => (h.Label, h.IndexNumber)).ShouldBe([("Province", "977"), ("Municipality", "04"), ("Barangay", "0007"), ("Section", "003")]);
        sheet.Extent.ShouldNotBeNull();
        sheet.Extent![0].ShouldBe(c.Lon, 1e-9);
        sheet.Features.Single().Properties.Role.ShouldBe("area");
        sheet.Missing.ShouldBeEmpty();
        (await c.Sheets.TaxMapAsync(c.Town.Section.Id, new DateOnly(2024, 12, 31))).Value.Missing.ShouldHaveSingleItem();

        // The section index map: both sections labelled; the barangay itself has no boundary yet.
        var index = (await c.Sheets.SectionIndexAsync(c.Town.Barangay.Id, c.Today)).Value;
        index.Features.Select(f => f.Properties.Label).ShouldBe(["003", "004"]);
        index.Missing.ShouldContain(m => m.Contains("barangay has no boundary"));

        // The barangay index map: the barangay is numbered, but has no boundary to draw.
        var barangays = (await c.Sheets.BarangayIndexAsync(c.Town.Municipality.Id, null, c.Today)).Value;
        barangays.Heading.Select(h => h.IndexNumber).ShouldBe(["977", "04"]);
        barangays.Extent.ShouldBeNull();
        barangays.Missing.ShouldContain(m => m.Contains("DEMO_TR Barangay"));

        (await c.Sheets.TaxMapAsync(Guid.NewGuid(), null)).Code.ShouldBe("TAX_MAP_SECTION_NOT_FOUND");
    }
}
