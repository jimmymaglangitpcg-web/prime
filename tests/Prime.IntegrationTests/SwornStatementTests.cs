using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Forms;
using Prime.Application.Features.SwornStatements;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// docs/analysis/mrpaao-forms-model.md §16 step 7 — the owner's sworn
/// statement of market value (MRPAAO Att. 11). DEMO data.
/// </summary>
public class SwornStatementTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, BillingFlowTests.Seed Seed, Guid MunicipalityId, Guid BuildingRpuId, DateOnly Today)
    {
        public ISwornStatementService Statements => Services.GetRequiredService<ISwornStatementService>();
    }

    /// <summary>The seeded land RPU with its approved TD, and a building RPU on the same property with no TD yet.</summary>
    private async Task<(Ctx Ctx, IAsyncDisposable Transaction)> BeginAsync()
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, new DateOnly(2026, 1, 1));
        var municipalityId = await db.Properties.Where(p => p.Id == seed.PropertyId).Select(p => p.MunicipalityId).SingleAsync();
        var building = new RealPropertyUnit
        {
            PropertyId = seed.PropertyId, RpuNumber = $"RPU-B-{Guid.NewGuid():N}", RpuType = RpuType.Building, EffectivityDate = new DateOnly(2026, 1, 1),
        };
        db.RealPropertyUnits.Add(building);
        await db.SaveChangesAsync();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().Today;
        return (new Ctx(db, scope.ServiceProvider, seed, municipalityId, building.Id, today), new Scoped(transaction, scope));
    }

    private sealed class Scoped(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    private static SaveSwornStatementRequest Header(Ctx c, DeclarantCapacity capacity = DeclarantCapacity.Owner, string? owners = null,
        bool sworn = true, bool thumbmarked = false, Guid? supersedes = null, Guid? municipalityId = null) =>
        new("DEMO_Declarant, Juan", null, "Filipino", "Married", "DEMO Address 11", "000-000-000", capacity, owners, municipalityId ?? c.MunicipalityId,
            SwornStatementFilingBasis.Section202, c.Today, "DEMO City", thumbmarked, null, null,
            sworn ? c.Today : null, "DEMO City", sworn ? "DEMO Notary" : null, null, "DEMO ID 123", c.Today.AddYears(-1), "DEMO City",
            sworn ? c.Today : null, supersedes, null);

    private static AddSwornStatementItemRequest LandItem(Guid tdId) =>
        new(SwornStatementItemKind.Land, tdId, null, null, 650_000m, LotNumber: "4", BlockNumber: "2", Area: 500m, AreaUnit: "sqm");

    private static AddSwornStatementItemRequest NewBuilding() =>
        new(SwornStatementItemKind.Building, null, null, "DEMO Street", 1_200_000m, FloorArea: 120m, Storeys: 2, Description: "DEMO two-storey house", YearCompleted: 2025);

    private static async Task<SwornStatementDto> DraftAsync(Ctx c, SaveSwornStatementRequest? header = null)
    {
        var created = await c.Statements.CreateAsync(header ?? Header(c));
        created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);
        return created.Value;
    }

    [Fact]
    public async Task Statement_DeclaresAnExistingTdAndANewBuilding_AndFiles()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var draft = await DraftAsync(c);

        var withLand = await c.Statements.AddItemAsync(draft.Id, LandItem(c.Seed.TaxDeclaration.Id));
        withLand.IsSuccess.ShouldBeTrue(withLand.IsSuccess ? null : withLand.Message);
        var land = withLand.Value.Items.Single();
        land.TdNumber.ShouldBe(c.Seed.TaxDeclaration.TaxDeclarationNumber);
        land.PropertyId.ShouldBe(c.Seed.PropertyId);
        land.RpuId.ShouldBe(c.Seed.RpuId);
        land.IsNew.ShouldBeFalse();
        land.FloorArea.ShouldBeNull(); // only the land's columns are kept

        var withBuilding = (await c.Statements.AddItemAsync(draft.Id, NewBuilding())).Value;
        withBuilding.Items.Count.ShouldBe(2);
        withBuilding.Items[1].IsNew.ShouldBeTrue();
        withBuilding.Items[1].Sequence.ShouldBe(2);
        withBuilding.TotalDeclaredValue.ShouldBe(1_850_000m);

        var number = $"DEMO-SS-{Guid.NewGuid():N}"[..20];
        var filed = await c.Statements.FileAsync(draft.Id, new FileSwornStatementRequest(number));

        filed.IsSuccess.ShouldBeTrue(filed.IsSuccess ? null : filed.Message);
        filed.Value.Status.ShouldBe(SwornStatementStatus.Filed);
        filed.Value.Number.ShouldBe(number);
        filed.Value.FiledAt.ShouldNotBeNull();
        (await c.Statements.ForPropertyAsync(c.Seed.PropertyId)).Value.Select(s => s.Id).ShouldContain(draft.Id);
        (await c.Statements.AddItemAsync(draft.Id, NewBuilding())).Code.ShouldBe("SWORN_STATEMENT_NOT_DRAFT");
        (await c.Statements.UpdateAsync(draft.Id, Header(c))).Code.ShouldBe("SWORN_STATEMENT_NOT_DRAFT");
    }

    [Fact]
    public async Task Filing_RequiresTheJuratItemsAndWitnessesWhenThumbmarked()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;

        var empty = await DraftAsync(c, Header(c, sworn: false));
        var incomplete = await c.Statements.FileAsync(empty.Id, new FileSwornStatementRequest(null));
        incomplete.Code.ShouldBe("SWORN_STATEMENT_INCOMPLETE");
        incomplete.Message!.ShouldContain("at least one property");
        incomplete.Message.ShouldContain("the jurat");
        incomplete.Message.ShouldContain("the date received");

        var thumb = await DraftAsync(c, Header(c, thumbmarked: true));
        await c.Statements.AddItemAsync(thumb.Id, NewBuilding());
        (await c.Statements.FileAsync(thumb.Id, new FileSwornStatementRequest(null))).Message!.ShouldContain("two witnesses");

        (await c.Statements.CreateAsync(Header(c, DeclarantCapacity.AuthorizedRepresentative))).Code.ShouldBe("VALIDATION_FAILED");
        (await c.Statements.CreateAsync(Header(c, DeclarantCapacity.AuthorizedRepresentative, owners: "DEMO_Owner, Maria"))).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Items_MustMatchTheUnitKind_BeInForce_AndInTheStatementsLgu()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var draft = await DraftAsync(c);
        var td = c.Seed.TaxDeclaration;

        (await c.Statements.AddItemAsync(draft.Id, NewBuilding() with { TaxDeclarationId = td.Id })).Code.ShouldBe("SWORN_STATEMENT_KIND_MISMATCH");
        (await c.Statements.AddItemAsync(draft.Id, LandItem(td.Id) with { ExistingTdNumber = "X-1" })).Code.ShouldBe("VALIDATION_FAILED");
        (await c.Statements.AddItemAsync(draft.Id, LandItem(td.Id) with { Area = null })).Code.ShouldBe("VALIDATION_FAILED");

        // Another city/municipality (Att. 11, Note 1).
        var otherMunicipality = new Municipality { ProvinceId = await c.Db.Municipalities.Where(m => m.Id == c.MunicipalityId).Select(m => m.ProvinceId).SingleAsync(),
            PsgcCode = $"M{Guid.NewGuid():N}"[..10], Name = "DEMO Other Municipality" };
        c.Db.Municipalities.Add(otherMunicipality);
        await c.Db.SaveChangesAsync();
        var elsewhere = await DraftAsync(c, Header(c, municipalityId: otherMunicipality.Id));
        (await c.Statements.AddItemAsync(elsewhere.Id, LandItem(td.Id))).Code.ShouldBe("SWORN_STATEMENT_OTHER_LGU");

        // A TD that is not in force.
        await c.Db.TaxDeclarations.Where(x => x.Id == td.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Draft));
        (await c.Statements.AddItemAsync(draft.Id, LandItem(td.Id))).Code.ShouldBe("TAX_DECLARATION_NOT_IN_FORCE");
    }

    [Fact]
    public async Task Correction_SupersedesTheFiledStatement_AndCancellingItRestoresIt()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var first = await DraftAsync(c);
        await c.Statements.AddItemAsync(first.Id, NewBuilding());
        (await c.Statements.FileAsync(first.Id, new FileSwornStatementRequest(null))).IsSuccess.ShouldBeTrue();

        var correction = await DraftAsync(c, Header(c, supersedes: first.Id));
        await c.Statements.AddItemAsync(correction.Id, NewBuilding() with { DeclaredMarketValue = 1_000_000m });
        (await c.Statements.CreateAsync(Header(c, supersedes: first.Id))).Code.ShouldBe("SWORN_STATEMENT_ALREADY_CORRECTED");
        (await c.Statements.FileAsync(correction.Id, new FileSwornStatementRequest(null))).IsSuccess.ShouldBeTrue();

        var old = (await c.Statements.GetAsync(first.Id)).Value;
        old.Status.ShouldBe(SwornStatementStatus.Superseded);
        old.SupersededById.ShouldBe(correction.Id);
        (await c.Statements.CancelAsync(first.Id, new CancelSwornStatementRequest("DEMO"))).Code.ShouldBe("SWORN_STATEMENT_NOT_CANCELLABLE");

        var cancelled = await c.Statements.CancelAsync(correction.Id, new CancelSwornStatementRequest("DEMO wrong owner"));
        cancelled.Value.Status.ShouldBe(SwornStatementStatus.Cancelled);
        cancelled.Value.CancellationReason.ShouldBe("DEMO wrong owner");
        (await c.Statements.GetAsync(first.Id)).Value.Status.ShouldBe(SwornStatementStatus.Filed);
    }

    [Fact]
    public async Task NewItem_IsLinkedToItsUnitOnceRegistered_AndFoundBySearch()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var draft = await DraftAsync(c);
        await c.Statements.AddItemAsync(draft.Id, LandItem(c.Seed.TaxDeclaration.Id));
        var building = (await c.Statements.AddItemAsync(draft.Id, NewBuilding())).Value.Items.Single(i => i.IsNew);
        await c.Statements.FileAsync(draft.Id, new FileSwornStatementRequest(null));

        (await c.Statements.LinkItemAsync(draft.Id, building.Id, new LinkSwornStatementItemRequest(c.Seed.RpuId))).Code.ShouldBe("SWORN_STATEMENT_KIND_MISMATCH");
        var linked = await c.Statements.LinkItemAsync(draft.Id, building.Id, new LinkSwornStatementItemRequest(c.BuildingRpuId));
        linked.IsSuccess.ShouldBeTrue(linked.IsSuccess ? null : linked.Message);
        var item = linked.Value.Items.Single(i => i.Id == building.Id);
        item.RpuId.ShouldBe(c.BuildingRpuId);
        item.PropertyId.ShouldBe(c.Seed.PropertyId);
        (await c.Statements.LinkItemAsync(draft.Id, building.Id, new LinkSwornStatementItemRequest(c.BuildingRpuId))).Code.ShouldBe("SWORN_STATEMENT_ITEM_LINKED");

        var byTd = await c.Statements.SearchAsync(new SwornStatementSearchRequest { TdNumber = c.Seed.TaxDeclaration.TaxDeclarationNumber });
        byTd.Value.Items.Select(x => x.Id).ShouldBe([draft.Id]);
        var byName = await c.Statements.SearchAsync(new SwornStatementSearchRequest { Declarant = "demo_declarant", MunicipalityId = c.MunicipalityId });
        byName.Value.Items.Single().ItemCount.ShouldBe(2);
    }

    [Fact]
    public async Task Form_PreviewsADraftForSigning_AndIssuesOnceFiled()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var forms = c.Services.GetRequiredService<IFormService>();
        var draft = await DraftAsync(c);
        await c.Statements.AddItemAsync(draft.Id, LandItem(c.Seed.TaxDeclaration.Id));
        await c.Statements.AddItemAsync(draft.Id, NewBuilding());

        var preview = await forms.PreviewAsync("SWORN_STATEMENT", draft.Id);
        preview.IsSuccess.ShouldBeTrue(preview.IsSuccess ? null : preview.Message);
        preview.Value.Authority.ShouldBe(FormAuthority.Mrpaao);
        var html = preview.Value.Html;
        html.ShouldContain("SWORN STATEMENT OF THE TRUE CURRENT AND FAIR MARKET VALUE OF REAL PROPERTIES");
        html.ShouldContain("DRAFT — FOR SIGNING; NOT YET FILED");
        html.ShouldContain(c.Seed.TaxDeclaration.TaxDeclarationNumber);
        html.ShouldContain(">NEW<");
        html.ShouldContain("650,000.00");
        html.ShouldContain("1,850,000.00");
        html.ShouldContain("DEMO_Declarant, Juan");
        (await forms.IssueAsync(new IssueFormRequest("SWORN_STATEMENT", draft.Id))).Code.ShouldBe("FORM_SUBJECT_NOT_ISSUABLE");

        var number = $"DEMO-SS-{Guid.NewGuid():N}"[..20];
        await c.Statements.FileAsync(draft.Id, new FileSwornStatementRequest(number));
        var issued = await forms.IssueAsync(new IssueFormRequest("SWORN_STATEMENT", draft.Id));
        issued.IsSuccess.ShouldBeTrue(issued.IsSuccess ? null : issued.Message);
        issued.Value.DocumentNumber.ShouldBe(number);
        issued.Value.Html.ShouldNotBeNull().ShouldNotContain("NOT YET FILED");
    }
}
