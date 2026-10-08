using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Gis;
using Prime.Application.Features.Gis.ReferenceLayers;
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
/// Step L2-3 (docs/analysis/identification-numbering.md §4.3; exit criterion 3): a territorial change moves the
/// properties of a barangay to another municipality's barangay, retiring and re-assigning every PIN with its history;
/// barangay-line parts; disputed areas hatched on the tax map. DEMO index numbers, patterns and shapes. The background
/// scheduler is replaced by a recorder, and the job's runner is called directly inside the test transaction.
/// </summary>
public class TerritorialChangeTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed class RecordingScheduler : IBackgroundJobScheduler
    {
        public int Enqueued { get; private set; }
        public void Enqueue<T>(Expression<Func<T, Task>> methodCall) => Enqueued++;
    }

    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, Province Province, Barangay Source, Barangay Target, Barangay Neighbour,
        TaxMapSection Section)
    {
        public ITerritorialChangeService Changes => Services.GetRequiredService<ITerritorialChangeService>();
        public IPinService Pins => Services.GetRequiredService<IPinService>();
        public IPropertyService Properties => Services.GetRequiredService<IPropertyService>();
    }

    /// <summary>Town 15 with barangays 0005 (section 002) and 0006, and town 16 with barangay 0001; LAM PIN and temporary PIN schemes.</summary>
    private async Task<(Ctx C, IAsyncDisposable Scope)> BeginAsync()
    {
        var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped<IBackgroundJobScheduler, RecordingScheduler>()));
        var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        await db.NumberingSchemes.Where(x => x.Status == WorkflowStatus.Approved)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null));
        await db.Provinces.ExecuteUpdateAsync(s => s.SetProperty(x => x.PinIndexNumber, (string?)null));
        await db.Municipalities.ExecuteUpdateAsync(s => s.SetProperty(x => x.PinIndexNumber, (string?)null));
        var tag = Guid.NewGuid().ToString("N")[..8];
        var province = new Province { PsgcCode = $"TC-P{tag}", Name = "DEMO_TC Province", PinIndexNumber = "020" };
        var townA = new Municipality { Province = province, PsgcCode = $"TC-A{tag}", Name = "DEMO_TC Town A", PinIndexNumber = "15" };
        var townB = new Municipality { Province = province, PsgcCode = $"TC-B{tag}", Name = "DEMO_TC Town B", PinIndexNumber = "16" };
        var source = new Barangay { Municipality = townA, PsgcCode = $"TC-S{tag}", Name = "DEMO_TC Source", PinIndexNumber = "0005" };
        var neighbour = new Barangay { Municipality = townA, PsgcCode = $"TC-N{tag}", Name = "DEMO_TC Neighbour", PinIndexNumber = "0006" };
        var target = new Barangay { Municipality = townB, PsgcCode = $"TC-T{tag}", Name = "DEMO_TC Target", PinIndexNumber = "0001" };
        var section = new TaxMapSection { Barangay = source, IndexNumber = "002" };
        db.AddRange(province, townA, townB, source, neighbour, target, section,
            Approved(new NumberingScheme { AppliesTo = NumberedDocumentKind.PropertyIdentificationNumber, Name = "DEMO LAM PIN", Pattern = "{LGUIDX}-{MUNIDX}-{BRGYIDX}-{SECT}-{SEQ:3}" }),
            Approved(new NumberingScheme { AppliesTo = NumberedDocumentKind.TemporaryPin, Name = "DEMO temporary PIN", Pattern = "T-{MUNIDX}-{BRGYIDX}-{SEQ:4}" }));
        await db.SaveChangesAsync();
        return (new Ctx(db, scope.ServiceProvider, province, source, target, neighbour, section), new Disposer(transaction, scope));
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

    private static readonly GeometryFactory Geometry = new(new PrecisionModel(), 4326);

    private static MultiPolygon Square(double x, double y, double size) => Geometry.CreateMultiPolygon([Geometry.CreatePolygon([
        new Coordinate(x, y), new Coordinate(x + size, y), new Coordinate(x + size, y + size), new Coordinate(x, y + size), new Coordinate(x, y)])]);

    /// <summary>A property in <paramref name="barangay"/>; placed in the section (permanent PIN) unless <paramref name="place"/> is false.</summary>
    private static async Task<(Guid Id, Parcel Parcel)> PropertyAsync(Ctx c, Barangay barangay, bool place = true, MultiPolygon? shape = null)
    {
        var created = await c.Properties.CreateAsync(new CreatePropertyRequest(null, c.Province.Id, barangay.MunicipalityId, barangay.Id, null, "DEMO Street",
            null, null, null, null, null, null));
        created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);
        var parcel = new Parcel { PropertyId = created.Value.Id, BarangayId = barangay.Id, Area = 500m, Geometry = shape };
        c.Db.Parcels.Add(parcel);
        await c.Db.SaveChangesAsync();
        if (place)
        {
            (await c.Pins.PlaceInSectionAsync(created.Value.Id, new(parcel.Id, c.Section.Id))).IsSuccess.ShouldBeTrue();
        }
        return (created.Value.Id, parcel);
    }

    [Fact]
    public async Task ATransferredBarangay_GetsNewPins_KeepingSectionAndParcelNumbers_WithHistory()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var (first, _) = await PropertyAsync(c, c.Source);
        await PropertyAsync(c, c.Source);
        await PropertyAsync(c, c.Source, place: false);                               // a temporary PIN only
        var (elsewhere, _) = await PropertyAsync(c, c.Neighbour, place: false);       // not moved
        var building = (await c.Services.GetRequiredService<IRealPropertyUnitService>().CreateAsync(new CreateRpuRequest(
            first, $"RPU-{Guid.NewGuid():N}", RpuType.Building, new DateOnly(2026, 1, 1), null))).Value;
        var elsewherePin = (await c.Db.Properties.SingleAsync(x => x.Id == elsewhere)).PropertyIdentificationNumber;

        var draft = await c.Changes.CreateAsync(new CreateTerritorialChangeRequest(TerritorialChangeKind.TransferredTerritory,
            "DEMO RA 0000 transferring the barangay — not a real law", new DateOnly(2026, 6, 1), TerritorialChangePinMode.KeepParcelNumbers,
            [new(c.Source.Id, c.Target.Id)]));
        draft.IsSuccess.ShouldBeTrue(draft.IsSuccess ? null : draft.Message);
        var approved = await TestSeed.AsCheckerAsync(c.Services, () => c.Changes.ApproveAsync(draft.Value.Id));
        approved.IsSuccess.ShouldBeTrue(approved.IsSuccess ? null : approved.Message);
        approved.Value.TotalCount.ShouldBe(3);
        ((RecordingScheduler)c.Services.GetRequiredService<IBackgroundJobScheduler>()).Enqueued.ShouldBe(1);

        await c.Services.GetRequiredService<TerritorialChangeJobRunner>().RunAsync(draft.Value.Id, CancellationToken.None);

        var done = (await c.Changes.GetAsync(draft.Value.Id)).Value;
        (done.RunStatus, done.ProcessedCount, done.FailedCount).ShouldBe((JobExecutionStatus.Completed, 3, 0));
        done.Items.Select(i => (i.OldPin, i.NewPin)).OrderBy(x => x.OldPin).ShouldBe([
            ("020-15-0005-002-001", "020-16-0001-002-001"), ("020-15-0005-002-002", "020-16-0001-002-002"), ("T-15-0005-0003", "T-16-0001-0001"),
        ]);
        var moved = await c.Db.Properties.SingleAsync(x => x.Id == first);
        (moved.BarangayId, moved.MunicipalityId).ShouldBe((c.Target.Id, c.Target.MunicipalityId));
        var history = await c.Db.PinAssignments.Where(a => a.PropertyId == first).OrderBy(a => a.AssignedAt).ToListAsync();
        history.First(a => a.Pin == "020-15-0005-002-001").RetirementReason.ShouldStartWith("Territorial change (TransferredTerritory)");
        history.Single(a => a.RetiredAt == null).Section!.BarangayId.ShouldBe(c.Target.Id); // section 002 created in the receiving barangay
        (await c.Db.Parcels.Where(p => p.PropertyId == first).Select(p => p.BarangayId).SingleAsync()).ShouldBe(c.Target.Id);
        (await c.Services.GetRequiredService<IRealPropertyUnitService>().GetByIdAsync(building.Id)).Value.UnitPin.ShouldBe("020-16-0001-002-001-1001");
        (await c.Db.Properties.SingleAsync(x => x.Id == elsewhere)).PropertyIdentificationNumber.ShouldBe(elsewherePin);
        (await c.Changes.ResumeAsync(draft.Value.Id)).Code.ShouldBe("TERRITORIAL_CHANGE_NOT_RESUMABLE"); // nothing left to move
    }

    [Fact]
    public async Task TemporaryPinMode_AndTheChecks()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var (first, _) = await PropertyAsync(c, c.Source);
        var orphan = new Barangay { MunicipalityId = c.Target.MunicipalityId, PsgcCode = $"TC-O{Guid.NewGuid():N}"[..10], Name = "DEMO_TC No index" };
        c.Db.Add(orphan);
        await c.Db.SaveChangesAsync();
        (await c.Changes.CreateAsync(new CreateTerritorialChangeRequest(TerritorialChangeKind.CreatedLgu, "DEMO", new DateOnly(2026, 6, 1),
            TerritorialChangePinMode.TemporaryPins, [new(c.Source.Id, orphan.Id)]))).Code.ShouldBe("TARGET_INDEX_MISSING");
        (await c.Changes.CreateAsync(new CreateTerritorialChangeRequest(TerritorialChangeKind.CreatedLgu, " ", new DateOnly(2026, 6, 1),
            TerritorialChangePinMode.TemporaryPins, [new(c.Source.Id, c.Target.Id)]))).Code.ShouldBe("VALIDATION_FAILED");

        var job = (await c.Changes.CreateAsync(new CreateTerritorialChangeRequest(TerritorialChangeKind.CreatedLgu, "DEMO RA 0001 creating a town — not a real law",
            new DateOnly(2026, 6, 1), TerritorialChangePinMode.TemporaryPins, [new(c.Source.Id, c.Target.Id)]))).Value;
        (await TestSeed.AsCheckerAsync(c.Services, () => c.Changes.ApproveAsync(job.Id))).IsSuccess.ShouldBeTrue();
        await c.Services.GetRequiredService<TerritorialChangeJobRunner>().RunAsync(job.Id, CancellationToken.None);

        (await c.Db.Properties.SingleAsync(x => x.Id == first)).PropertyIdentificationNumber.ShouldBe("T-16-0001-0001"); // until re-tax-mapping
        (await c.Db.Parcels.SingleAsync(p => p.PropertyId == first)).SectionId.ShouldBeNull();
    }

    [Fact]
    public async Task BarangayParts_AreRecordedForTheLargerPartsBarangay()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var (property, _) = await PropertyAsync(c, c.Source, place: false);
        var parts = c.Services.GetRequiredService<IBarangayPartService>();

        var set = await parts.SetAsync(property, new([new(c.Source.Id, 300m, 70m), new(c.Neighbour.Id, 200m, 30m)], "DEMO survey plan"));
        set.IsSuccess.ShouldBeTrue(set.IsSuccess ? null : set.Message);
        set.Value.Select(p => (p.BarangayName, p.Area, p.AssessedValueShare)).ShouldBe([("DEMO_TC Source", 300m, 70m), ("DEMO_TC Neighbour", 200m, 30m)]);
        (await parts.SetAsync(property, new([new(c.Source.Id, 100m, 50m), new(c.Neighbour.Id, 400m, 50m)], "DEMO"))).Code.ShouldBe("BARANGAY_PART_NOT_LARGEST");
        (await parts.SetAsync(property, new([new(c.Source.Id, 300m, 70m), new(c.Target.Id, 200m, 30m)], "DEMO"))).Code.ShouldBe("BARANGAY_PART_INVALID");
        (await parts.SetAsync(property, new([new(c.Source.Id, 300m, 70m), new(c.Neighbour.Id, 200m, 20m)], "DEMO"))).Code.ShouldBe("VALIDATION_FAILED");
        (await parts.SetAsync(property, new([], "DEMO: the line was re-surveyed"))).Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task ADisputedArea_IsDrawnOnTheTaxMap_WithThePinsItTouches()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var (inside, _) = await PropertyAsync(c, c.Source, shape: Square(125.0, 7.0, 0.001));
        await PropertyAsync(c, c.Source, shape: Square(125.01, 7.01, 0.001));
        var layers = c.Services.GetRequiredService<IReferenceLayerService>();
        JsonElement Collection(object feature) => JsonSerializer.SerializeToElement(new { type = "FeatureCollection", features = new[] { feature } });
        var square = new[] { new[] { new[] { 124.999, 6.999 }, new[] { 125.02, 6.999 }, new[] { 125.02, 7.02 }, new[] { 124.999, 7.02 }, new[] { 124.999, 6.999 } } };
        (await layers.ImportAsync(ReferenceLayer.Sections, new ImportReferenceLayerRequest(new DateOnly(2026, 1, 1), "DEMO shapes", null, Collection(new
        {
            type = "Feature", properties = new { psgcCode = c.Source.PsgcCode, section = "002" }, geometry = new { type = "Polygon", coordinates = square },
        })), dryRun: false)).Value.Committed.ShouldBeTrue();
        var disputed = new[] { new[] { new[] { 124.9995, 6.9995 }, new[] { 125.0005, 6.9995 }, new[] { 125.0005, 7.0005 }, new[] { 124.9995, 7.0005 }, new[] { 124.9995, 6.9995 } } };
        var imported = await layers.ImportAsync(ReferenceLayer.DisputedAreas, new ImportReferenceLayerRequest(new DateOnly(2026, 1, 1), "DEMO shapes", "DEMO case 1", Collection(new
        {
            type = "Feature", properties = new { code = "DEMO-DISPUTE-1", name = "DEMO boundary dispute" }, geometry = new { type = "Polygon", coordinates = disputed },
        })), dryRun: false);
        imported.Value.Committed.ShouldBeTrue(string.Join("; ", imported.Value.Errors.Select(e => e.Message)));

        var sheet = await c.Services.GetRequiredService<ITaxMapSheetService>().TaxMapAsync(c.Section.Id, new DateOnly(2026, 6, 1));

        var dispute = sheet.Value.Features.Single(f => f.Properties.Role == "disputed");
        dispute.Properties.Label.ShouldBe("DEMO-DISPUTE-1");
        var insidePin = (await c.Db.Properties.SingleAsync(x => x.Id == inside)).PropertyIdentificationNumber;
        dispute.Properties.Name.ShouldBe($"DEMO boundary dispute — PIN {insidePin}"); // only the parcel it touches
    }
}
