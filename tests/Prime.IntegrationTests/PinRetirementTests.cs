using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Properties;
using Prime.Application.Features.PropertyIdentification;
using Prime.Application.Features.Transactions;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Forms;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Phase 10a-3 (docs/analysis/property-identification.md §3.4; MRPAAO Ch. II §1 D.4):
/// an approved subdivision or consolidation retires the PINs of the properties it ends,
/// and the properties it produces take the next parcel numbers in the section. Rolled
/// back; every index number, pattern and transaction type is DEMO data, and the dev
/// database's approved configuration is cleared inside the transaction.
/// </summary>
public class PinRetirementTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Context(PrimeDbContext Db, IServiceProvider Services, CurrentUserService User, AppUser Maker, AppUser Checker,
        Barangay Barangay, TaxMapSection Section)
    {
        public IPinService Pins => Services.GetRequiredService<IPinService>();
        public IPropertyService Properties => Services.GetRequiredService<IPropertyService>();
        public ITransactionService Tx => Services.GetRequiredService<ITransactionService>();
    }

    private async Task<(Context C, IAsyncDisposable Scope)> BeginAsync()
    {
        var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        await db.ApprovalChains.Where(x => x.Status == WorkflowStatus.Approved)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null));
        await db.NumberingSchemes.Where(x => x.Status == WorkflowStatus.Approved)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null));
        await db.TransactionTypes.Where(x => x.Status == WorkflowStatus.Approved)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null));
        await db.Provinces.ExecuteUpdateAsync(s => s.SetProperty(x => x.PinIndexNumber, (string?)null));
        await db.Municipalities.ExecuteUpdateAsync(s => s.SetProperty(x => x.PinIndexNumber, (string?)null));

        var tag = Guid.NewGuid().ToString("N")[..8];
        var province = new Province { PsgcCode = $"PR-P{tag}", Name = "DEMO_PR Province", PinIndexNumber = "020" };
        var municipality = new Municipality { Province = province, PsgcCode = $"PR-M{tag}", Name = "DEMO_PR Municipality", PinIndexNumber = "15" };
        var barangay = new Barangay { Municipality = municipality, PsgcCode = $"PR-B{tag}", Name = "DEMO_PR Barangay", PinIndexNumber = "0005" };
        var section = new TaxMapSection { Barangay = barangay, IndexNumber = "002" };
        db.AddRange(province, municipality, barangay, section);
        db.Add(Approved(new NumberingScheme
        {
            AppliesTo = NumberedDocumentKind.PropertyIdentificationNumber, Name = "DEMO MRPAAO PIN",
            Pattern = "{LGUIDX}-{MUNIDX}-{BRGYIDX}-{SECT}-{SEQ:2}", AllowManualEntry = true,
        }));
        db.Add(Approved(new NumberingScheme { AppliesTo = NumberedDocumentKind.TemporaryPin, Name = "DEMO temporary PIN", Pattern = "T-{MUNIDX}-{BRGYIDX}-{SEQ:4}" }));
        var users = Enumerable.Range(0, 2).Select(i => new AppUser
        {
            SupabaseUserId = Guid.NewGuid(), DisplayName = $"DEMO User {(char)('A' + i)}", Email = $"demo-{Guid.NewGuid():N}@example.invalid",
        }).ToList();
        db.AppUsers.AddRange(users);
        await db.SaveChangesAsync();

        var user = scope.ServiceProvider.GetRequiredService<CurrentUserService>();
        user.AppUserId = null;
        return (new Context(db, scope.ServiceProvider, user, users[0], users[1], barangay, section), new Disposer(transaction, scope));
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

    /// <summary>Registers a property with one parcel (temporary PIN); placed in <paramref name="section"/> when given.</summary>
    private static async Task<Guid> PropertyAsync(Context c, TaxMapSection? section = null)
    {
        var municipality = await c.Db.Municipalities.AsNoTracking().SingleAsync(x => x.Id == c.Barangay.MunicipalityId);
        var created = await c.Properties.CreateAsync(new CreatePropertyRequest(
            null, municipality.ProvinceId, municipality.Id, c.Barangay.Id, null, "DEMO Street", null, null, null, null, null, null));
        created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);
        var parcel = new Parcel { PropertyId = created.Value.Id, BarangayId = c.Barangay.Id, Area = 500m };
        c.Db.Parcels.Add(parcel);
        await c.Db.SaveChangesAsync();
        if (section is not null)
        {
            var placed = await c.Pins.PlaceInSectionAsync(created.Value.Id, new(parcel.Id, section.Id));
            placed.IsSuccess.ShouldBeTrue(placed.IsSuccess ? null : placed.Message);
        }
        return created.Value.Id;
    }

    private static async Task<Guid> ApprovedTypeAsync(Context c, PropertyTransactionKind kind)
    {
        c.User.AppUserId = c.Maker.Id;
        var created = await c.Tx.CreateTypeAsync(new CreateTransactionTypeRequest(
            "DEMO — not LAM", new DateOnly(2020, 1, 1), null, $"D{Guid.NewGuid():N}"[..8], $"DEMO {kind}", kind, 1, null, []));
        c.User.AppUserId = c.Checker.Id;
        (await c.Tx.ApproveTypeAsync(created.Value.Id)).IsSuccess.ShouldBeTrue();
        return created.Value.Id;
    }

    /// <summary>Opens (as the maker), submits, and returns the transaction id; approval is left to the test.</summary>
    private static async Task<Guid> SubmittedAsync(Context c, PropertyTransactionKind kind, Guid propertyId, TransactionPropertyRole role, params Guid[] related)
    {
        var typeId = await ApprovedTypeAsync(c, kind);
        c.User.AppUserId = c.Maker.Id;
        var opened = await c.Tx.OpenAsync(new OpenTransactionRequest(typeId, propertyId, new DateOnly(2026, 9, 1), $"DEMO {kind}",
            RelatedProperties: related.Select(id => new RelatedPropertyRequest(id, role)).ToList()));
        opened.IsSuccess.ShouldBeTrue(opened.IsSuccess ? null : opened.Message);
        var submitted = await c.Tx.SubmitAsync(opened.Value.Id);
        submitted.IsSuccess.ShouldBeTrue(submitted.IsSuccess ? null : submitted.Message);
        c.User.AppUserId = c.Checker.Id;
        return opened.Value.Id;
    }

    private static async Task<(RecordStatus Property, RecordStatus Parcel, string Pin)> StateAsync(Context c, Guid propertyId)
    {
        c.Db.ChangeTracker.Clear();
        var property = await c.Db.Properties.AsNoTracking().SingleAsync(x => x.Id == propertyId);
        var parcel = await c.Db.Parcels.AsNoTracking().SingleAsync(x => x.PropertyId == propertyId);
        return (property.Status, parcel.Status, property.PropertyIdentificationNumber);
    }

    [Fact]
    public async Task Subdivision_RetiresTheMotherPin_AndTheLotsTakeTheNextNumbersInTheSection()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var mother = await PropertyAsync(c, c.Section);   // 020-15-0005-002-01
        await PropertyAsync(c, c.Section);                // 020-15-0005-002-02, a neighbour
        var lot1 = await PropertyAsync(c);
        var lot2 = await PropertyAsync(c);

        var txId = await SubmittedAsync(c, PropertyTransactionKind.Subdivision, mother, TransactionPropertyRole.Result, lot1, lot2);
        var approved = await c.Tx.ApproveAsync(txId);
        approved.IsSuccess.ShouldBeTrue(approved.IsSuccess ? null : approved.Message);

        (await StateAsync(c, mother)).ShouldBe((RecordStatus.Subdivided, RecordStatus.Subdivided, "020-15-0005-002-01"));
        var motherPin = (await c.Pins.GetAsync(mother)).Value;
        motherPin.Kind.ShouldBeNull(); // no current PIN
        var retired = motherPin.History.Single(h => h.Pin == "020-15-0005-002-01");
        retired.RetiredAt.ShouldNotBeNull();
        retired.PropertyTransactionId.ShouldBe(txId);
        retired.RetirementReason!.ShouldContain("subdivision");

        // The next numbers after the highest in the section, in registration order; the mother's 01 is never given again.
        (await StateAsync(c, lot1)).ShouldBe((RecordStatus.Active, RecordStatus.Active, "020-15-0005-002-03"));
        (await StateAsync(c, lot2)).ShouldBe((RecordStatus.Active, RecordStatus.Active, "020-15-0005-002-04"));
        var lotPin = (await c.Pins.GetAsync(lot1)).Value;
        lotPin.Kind.ShouldBe(PinKind.Permanent);
        lotPin.History.Single(h => h.RetiredAt is null).AssignedByTransactionId.ShouldBe(txId);
        lotPin.History.Single(h => h.Kind == PinKind.Temporary).PropertyTransactionId.ShouldBe(txId);

        // A retired property takes part in no further subdivision or consolidation.
        var other = await PropertyAsync(c);
        c.User.AppUserId = c.Maker.Id;
        var typeId = await ApprovedTypeAsync(c, PropertyTransactionKind.Subdivision);
        c.User.AppUserId = c.Maker.Id;
        (await c.Tx.OpenAsync(new OpenTransactionRequest(typeId, mother, new DateOnly(2026, 9, 2), "DEMO again",
            RelatedProperties: [new(lot1, TransactionPropertyRole.Result), new(other, TransactionPropertyRole.Result)]))).Code.ShouldBe("PROPERTY_NOT_ACTIVE");
    }

    [Fact]
    public async Task Consolidation_RetiresTheSourcePins_AndTheResultTakesTheNextNumber()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var source1 = await PropertyAsync(c, c.Section); // -01
        var source2 = await PropertyAsync(c, c.Section); // -02
        var result = await PropertyAsync(c);

        var txId = await SubmittedAsync(c, PropertyTransactionKind.Consolidation, result, TransactionPropertyRole.Source, source1, source2);
        var approved = await c.Tx.ApproveAsync(txId);
        approved.IsSuccess.ShouldBeTrue(approved.IsSuccess ? null : approved.Message);

        (await StateAsync(c, source1)).ShouldBe((RecordStatus.Consolidated, RecordStatus.Consolidated, "020-15-0005-002-01"));
        (await StateAsync(c, source2)).ShouldBe((RecordStatus.Consolidated, RecordStatus.Consolidated, "020-15-0005-002-02"));
        (await StateAsync(c, result)).ShouldBe((RecordStatus.Active, RecordStatus.Active, "020-15-0005-002-03"));
        (await c.Db.PinAssignments.CountAsync(x => (x.PropertyId == source1 || x.PropertyId == source2) && x.RetiredAt == null)).ShouldBe(0);
    }

    [Fact]
    public async Task Opening_IsRefused_WithoutTwoPropertiesInTheRightRole()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var a = await PropertyAsync(c);
        var b = await PropertyAsync(c);
        var result = await PropertyAsync(c);
        var consolidation = await ApprovedTypeAsync(c, PropertyTransactionKind.Consolidation);
        c.User.AppUserId = c.Maker.Id;

        (await c.Tx.OpenAsync(new OpenTransactionRequest(consolidation, result, new DateOnly(2026, 9, 1), "DEMO one source",
            RelatedProperties: [new(a, TransactionPropertyRole.Source)]))).Code.ShouldBe("TRANSACTION_RELATED_PROPERTIES_INVALID");
        (await c.Tx.OpenAsync(new OpenTransactionRequest(consolidation, result, new DateOnly(2026, 9, 1), "DEMO wrong role",
            RelatedProperties: [new(a, TransactionPropertyRole.Source), new(b, TransactionPropertyRole.Result)]))).Code.ShouldBe("TRANSACTION_RELATED_PROPERTIES_INVALID");
    }

    [Fact]
    public async Task SourcesInTwoSections_NeedTheResultPlacedFirst_AndNothingChangesUntilThen()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var section3 = new TaxMapSection { BarangayId = c.Barangay.Id, IndexNumber = "003" };
        c.Db.Add(section3);
        await c.Db.SaveChangesAsync();
        var source1 = await PropertyAsync(c, c.Section); // 002-01
        var source2 = await PropertyAsync(c, section3);  // 003-01
        var result = await PropertyAsync(c);

        var txId = await SubmittedAsync(c, PropertyTransactionKind.Consolidation, result, TransactionPropertyRole.Source, source1, source2);
        (await c.Tx.ApproveAsync(txId)).Code.ShouldBe("TRANSACTION_PIN_SECTION_AMBIGUOUS");
        (await StateAsync(c, source1)).Property.ShouldBe(RecordStatus.Active);
        (await c.Pins.GetAsync(source1)).Value.Kind.ShouldBe(PinKind.Permanent);

        // The office decides where the consolidated lot goes; approval then keeps that PIN.
        var parcel = await c.Db.Parcels.AsNoTracking().SingleAsync(x => x.PropertyId == result);
        (await c.Pins.PlaceInSectionAsync(result, new(parcel.Id, section3.Id))).Value.Pin.ShouldBe("020-15-0005-003-02");
        var approved = await c.Tx.ApproveAsync(txId);
        approved.IsSuccess.ShouldBeTrue(approved.IsSuccess ? null : approved.Message);
        (await StateAsync(c, result)).Pin.ShouldBe("020-15-0005-003-02");
        (await StateAsync(c, source2)).Property.ShouldBe(RecordStatus.Consolidated);
    }

    [Fact]
    public async Task WithoutPermanentPins_TheSourcesRetire_AndTheResultKeepsItsPin()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var mother = await PropertyAsync(c);
        var lot1 = await PropertyAsync(c);
        var lot2 = await PropertyAsync(c);
        var lotPinBefore = (await StateAsync(c, lot1)).Pin;

        var txId = await SubmittedAsync(c, PropertyTransactionKind.Subdivision, mother, TransactionPropertyRole.Result, lot1, lot2);
        (await c.Tx.ApproveAsync(txId)).IsSuccess.ShouldBeTrue();

        (await StateAsync(c, mother)).Property.ShouldBe(RecordStatus.Subdivided);
        (await c.Pins.GetAsync(mother)).Value.History.Single().PropertyTransactionId.ShouldBe(txId);
        (await StateAsync(c, lot1)).Pin.ShouldBe(lotPinBefore);
        (await c.Pins.GetAsync(lot1)).Value.Kind.ShouldBe(PinKind.Temporary);
    }
}
