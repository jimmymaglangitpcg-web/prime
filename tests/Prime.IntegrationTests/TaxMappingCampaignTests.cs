using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Forms;
using Prime.Application.Features.Numbering;
using Prime.Application.Features.Properties;
using Prime.Application.Features.PropertyIdentification;
using Prime.Application.Features.RealPropertyUnits;
using Prime.Application.Features.Registers;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Forms;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Phase 10a-5 (docs/analysis/property-identification.md §3.6; MRPAAO Ch. II §2 A and
/// E.14): the tax mapping campaign's tie-up marks on temporary PINs, the temporary unit
/// PINs (B1, M1), and the ARPN numbering for LGUs without tax maps. Rolled back; the dev
/// database's approved numbering schemes, index numbers and SMVs are set aside inside the
/// transaction, and every value is DEMO data.
/// </summary>
public class TaxMappingCampaignTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, Province Province, Municipality Municipality, Barangay Barangay, TaxMapSection Section)
    {
        public IPinService Pins => Services.GetRequiredService<IPinService>();
        public IPropertyService Properties => Services.GetRequiredService<IPropertyService>();
        public IRealPropertyUnitService Rpus => Services.GetRequiredService<IRealPropertyUnitService>();
        public DateOnly Today => Services.GetRequiredService<IClock>().Today;
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
        var province = new Province { PsgcCode = $"TC-P{tag}", Name = "DEMO_TC Province", PinIndexNumber = "966" };
        var municipality = new Municipality { Province = province, PsgcCode = $"TC-M{tag}", Name = "DEMO_TC Municipality", PinIndexNumber = "03" };
        var barangay = new Barangay { Municipality = municipality, PsgcCode = $"TC-B{tag}", Name = "DEMO_TC Barangay", PinIndexNumber = "0009" };
        var section = new TaxMapSection { Barangay = barangay, IndexNumber = "001" };
        db.AddRange(province, municipality, barangay, section,
            Approved(new NumberingScheme { AppliesTo = NumberedDocumentKind.PropertyIdentificationNumber, Name = "DEMO PIN", Pattern = "{LGUIDX}-{MUNIDX}-{BRGYIDX}-{SECT}-{SEQ:2}" }),
            // The manual's temporary PIN: MM-BBBB-NNNN (Ch. II §2 A.b).
            Approved(new NumberingScheme { AppliesTo = NumberedDocumentKind.TemporaryPin, Name = "DEMO temporary PIN", Pattern = "{MUNIDX}-{BRGYIDX}-{SEQ:4}" }));
        await db.SaveChangesAsync();
        return (new Ctx(db, scope.ServiceProvider, province, municipality, barangay, section), new Disposer(transaction, scope));
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

    private static async Task<(PropertyDto Property, Parcel Parcel)> RegisterAsync(Ctx c)
    {
        var created = await c.Properties.CreateAsync(new CreatePropertyRequest(
            null, c.Province.Id, c.Municipality.Id, c.Barangay.Id, null, "DEMO Street", null, null, null, null, null, null));
        created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);
        var parcel = new Parcel { PropertyId = created.Value.Id, BarangayId = c.Barangay.Id, Area = 500m };
        c.Db.Parcels.Add(parcel);
        await c.Db.SaveChangesAsync();
        return (created.Value, parcel);
    }

    private static Task<Application.Common.Result<PropertyPinDto>> TieUp(Ctx c, Guid propertyId, TieUpStage stage, bool withdraw = false, string? remarks = null) =>
        c.Pins.RecordTieUpAsync(propertyId, new RecordTieUpRequest(stage, withdraw, remarks));

    [Fact]
    public async Task TieUp_OfficeThenField_OnTheTemporaryPin_AndKeptInItsHistory()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var (property, parcel) = await RegisterAsync(c);
        property.PropertyIdentificationNumber.ShouldBe("03-0009-0001");

        (await TieUp(c, property.Id, TieUpStage.Field)).Code.ShouldBe("TIE_UP_OFFICE_FIRST");
        var office = await TieUp(c, property.Id, TieUpStage.Office, remarks: "DEMO: lot 12 on the base map");
        office.IsSuccess.ShouldBeTrue(office.IsSuccess ? null : office.Message);
        office.Value.History.Single().OfficeTieUpAt.ShouldNotBeNull();
        (await TieUp(c, property.Id, TieUpStage.Office)).Code.ShouldBe("TIE_UP_ALREADY_RECORDED");
        (await TieUp(c, property.Id, TieUpStage.Field)).IsSuccess.ShouldBeTrue();
        (await TieUp(c, property.Id, TieUpStage.Office, withdraw: true, remarks: "DEMO")).Code.ShouldBe("TIE_UP_FIELD_CONFIRMED");
        (await TieUp(c, property.Id, TieUpStage.Field, withdraw: true)).Code.ShouldBe("VALIDATION_FAILED"); // a reason is required
        var withdrawn = await TieUp(c, property.Id, TieUpStage.Field, withdraw: true, remarks: "DEMO: owner not met");
        withdrawn.Value.History.Single().FieldConfirmedAt.ShouldBeNull();
        (await c.Db.AuditLogs.AnyAsync(a => a.Reason != null && a.Reason.Contains("Withdrew the field confirmation of temporary PIN 03-0009-0001"))).ShouldBeTrue();
        (await TieUp(c, property.Id, TieUpStage.Field)).IsSuccess.ShouldBeTrue();

        // The permanent PIN keeps the temporary PIN's marks in its history; no more marks after it.
        var placed = await c.Pins.PlaceInSectionAsync(property.Id, new(parcel.Id, c.Section.Id));
        placed.Value.Pin.ShouldBe("966-03-0009-001-01");
        var temporary = placed.Value.History.Single(h => h.Kind == PinKind.Temporary);
        (temporary.OfficeTieUpAt, temporary.FieldConfirmedAt, temporary.TieUpRemarks).ShouldSatisfyAllConditions(
            x => x.OfficeTieUpAt.ShouldNotBeNull(), x => x.FieldConfirmedAt.ShouldNotBeNull(), x => x.TieUpRemarks.ShouldBe("DEMO: owner not met"));
        (await TieUp(c, property.Id, TieUpStage.Office)).Code.ShouldBe("TIE_UP_NEEDS_TEMPORARY_PIN");
    }

    [Fact]
    public async Task UnitsOnATemporaryPin_TakeBAndMPostfixes_UntilThePermanentPin()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var (property, parcel) = await RegisterAsync(c);
        var date = new DateOnly(2024, 1, 1);
        async Task<Guid> Unit(RpuType type, Guid? land = null)
        {
            var r = await c.Rpus.CreateAsync(new CreateRpuRequest(property.Id, $"RPU-{Guid.NewGuid():N}", type, date, null, land));
            r.IsSuccess.ShouldBeTrue(r.IsSuccess ? null : r.Message);
            return r.Value.Id;
        }
        var land = await Unit(RpuType.Land);
        var building1 = await Unit(RpuType.Building, land);
        var machine = await Unit(RpuType.Machinery, land);
        var building2 = await Unit(RpuType.Building, land);

        var units = (await c.Rpus.ListByPropertyAsync(property.Id)).Value.ToDictionary(x => x.Id, x => x.UnitPin);
        units[land].ShouldBe("03-0009-0001");
        (units[building1], units[building2], units[machine]).ShouldBe(("03-0009-0001B1", "03-0009-0001B2", "03-0009-0001M1"));

        (await c.Pins.PlaceInSectionAsync(property.Id, new(parcel.Id, c.Section.Id))).IsSuccess.ShouldBeTrue();
        var after = (await c.Rpus.ListByPropertyAsync(property.Id)).Value.Single(x => x.Id == building1).UnitPin;
        after.ShouldStartWith("966-03-0009-001-01");
        after.ShouldNotContain("B1");
    }

    [Fact]
    public async Task PreRoll_PrintsTheRecordedTieUpMarks()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var (property, _) = await RegisterAsync(c);
        var classification = new Classification { Code = $"C{Guid.NewGuid():N}"[..6], Name = "DEMO_TC Class" };
        var use = new ActualUse { Code = $"U{Guid.NewGuid():N}"[..6], Name = "DEMO_TC Use" };
        var rpu = new RealPropertyUnit { PropertyId = property.Id, RpuNumber = $"RPU-{Guid.NewGuid():N}", RpuType = RpuType.Land, EffectivityDate = new DateOnly(2024, 1, 1) };
        c.Db.AddRange(classification, use, rpu,
            new Land { Rpu = rpu, PropertyId = property.Id, Area = 500m, ClassificationId = classification.Id, ActualUseId = use.Id },
            new TaxDeclaration
            {
                Rpu = rpu, PropertyId = property.Id, TaxDeclarationNumber = $"TD-{Guid.NewGuid():N}", EffectivityDate = new DateOnly(2024, 1, 1),
                Taxability = Taxability.Taxable, ClassificationId = classification.Id, ActualUseId = use.Id, AssessmentYear = 2024, Status = WorkflowStatus.Approved,
            });
        await c.Db.SaveChangesAsync();
        (await TieUp(c, property.Id, TieUpStage.Office, remarks: "DEMO tie-up note")).IsSuccess.ShouldBeTrue();

        var run = await c.Services.GetRequiredService<IRegisterService>().CreateRunAsync(
            new CreateRegisterRunRequest(RegisterKind.PreTaxMapControlRoll, c.Today, c.Barangay.Id, null, null, null, null));
        var html = (await c.Services.GetRequiredService<IFormService>().PreviewAsync("PRE_TMCR", run.Value.Id)).Value.Html;

        html.ShouldContain("<td class=\"tick\">✓</td><td>03-0009-0001</td><td class=\"tick\"></td>");
        html.ShouldContain("DEMO tie-up note");
    }

    [Fact]
    public async Task Arpn_NumbersFaasByBarangay_AndRestartsWithEachGeneralRevision()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        // Only DEMO revisions count here.
        await c.Db.Smvs.Where(x => x.Status == WorkflowStatus.Approved).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Cancelled));
        c.Db.Add(Approved(new NumberingScheme { AppliesTo = NumberedDocumentKind.TaxDeclaration, Name = "DEMO ARPN", Pattern = "{MUNIDX}-{BRGYIDX}-{REV}{SEQ:5}" }));
        var gr2024 = new Smv { OrdinanceNumber = "DEMO-GR-2024", OrdinanceDate = new DateOnly(2024, 1, 1), EffectivityDate = new DateOnly(2024, 1, 1), RevisionYear = 2024, Status = WorkflowStatus.Approved };
        c.Db.Add(gr2024);
        await c.Db.SaveChangesAsync();
        var (property, _) = await RegisterAsync(c);
        var numbering = c.Services.GetRequiredService<INumberingService>();
        async Task<string?> Next(DateOnly asOf) =>
            (await numbering.GenerateIfConfiguredAsync(NumberedDocumentKind.TaxDeclaration,
                await NumberContexts.ForPropertyAsync(c.Db, property.Id, asOf.Year, CancellationToken.None, asOf), asOf)).Value;

        (await Next(new DateOnly(2025, 3, 1))).ShouldBe("03-0009-00001");
        (await Next(new DateOnly(2025, 3, 2))).ShouldBe("03-0009-00002");

        c.Db.Add(new Smv { OrdinanceNumber = "DEMO-GR-2026", OrdinanceDate = new DateOnly(2026, 1, 1), EffectivityDate = new DateOnly(2026, 1, 1), RevisionYear = 2026, Status = WorkflowStatus.Approved });
        await c.Db.SaveChangesAsync();
        (await Next(new DateOnly(2026, 2, 1))).ShouldBe("03-0009-00001"); // a new general revision restarts the series

        // Before any approved revision, {REV} is missing and nothing is numbered.
        gr2024.Status = WorkflowStatus.Cancelled;
        await c.Db.SaveChangesAsync();
        (await numbering.GenerateIfConfiguredAsync(NumberedDocumentKind.TaxDeclaration,
            await NumberContexts.ForPropertyAsync(c.Db, property.Id, 2025, CancellationToken.None, new DateOnly(2025, 6, 1)), new DateOnly(2025, 6, 1)))
            .Code.ShouldBe("NUMBER_CONTEXT_MISSING");
    }
}
