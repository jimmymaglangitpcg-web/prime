using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Forms;
using Prime.Application.Features.Notices;
using Prime.Application.Features.Registers;
using Prime.Application.Features.RealPropertyUnits;
using Prime.Application.Features.Taxpayers;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step L5-2 (docs/analysis/records-and-forms.md §4.2): the <c>lam</c> object the FAAS, TD, NOA and register form data
/// carry for the LAM versions of the forms, beside the unchanged MRPAAO fields. Seeded DEMO land unit: TD effective
/// 2024-01-01 declaring a posted assessment of MV 500,000 / AV 100,000.
/// </summary>
public class LamFormDataTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, BillingFlowTests.Seed Seed)
    {
        public async Task<JsonObject> DataAsync(FormSubjectType type, Guid id) =>
            (await Services.GetServices<IFormDataProvider>().Single(p => p.SubjectType == type).BuildAsync(id, CancellationToken.None))
            .ShouldNotBeNull().Data;
    }

    private async Task<(Ctx Ctx, IAsyncDisposable Transaction)> BeginAsync()
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, new DateOnly(2026, 1, 1));
        var td = await db.TaxDeclarations.SingleAsync(x => x.Id == seed.TaxDeclaration.Id);
        td.AssessmentId = seed.AssessmentId;
        var property = await db.Properties.SingleAsync(x => x.Id == seed.PropertyId);
        property.TitleNumber = null;
        property.CadastralNumber = "DEMO Cad-L52";
        var owner = new Taxpayer
        {
            TaxpayerType = TaxpayerType.Individual, LastName = "DEMO_Owner", FirstName = "Ana", Address = "DEMO Address 1",
            Tin = "DEMO-TIN-L52", Email = "demo-owner@example.invalid", Sex = Sex.Female,
        };
        var type = new OwnershipType { Code = $"OT{Guid.NewGuid():N}"[..8], Name = "DEMO_Sole" };
        db.AddRange(owner, type);
        await db.SaveChangesAsync();
        (await scope.ServiceProvider.GetRequiredService<ITaxpayerService>().AddOwnerAsync(
            new AddPropertyOwnerRequest(seed.PropertyId, owner.Id, type.Id, 100m, new DateOnly(2020, 1, 1)))).IsSuccess.ShouldBeTrue();
        return (new Ctx(db, scope.ServiceProvider, seed), new Scoped(transaction, scope));
    }

    private sealed class Scoped(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    [Fact]
    public async Task TaxDeclaration_Lam_CarriesKindRegistrationWordsOrdinancesAndRows()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;

        var data = await c.DataAsync(FormSubjectType.TaxDeclaration, c.Seed.TaxDeclaration.Id);

        var lam = data["lam"]!.AsObject();
        lam["kind"]!.GetValue<string>().ShouldBe("Land");
        lam["registrationType"]!.GetValue<string>().ShouldBe("Untitled");
        lam["cadastralNumber"]!.GetValue<string>().ShouldBe("DEMO Cad-L52");
        lam["assessedValueInWords"]!.GetValue<string>().ShouldBe("ONE HUNDRED THOUSAND PESOS ONLY");
        lam["backTaxPeriod"].ShouldBeNull();
        var levelOrdinances = await c.Db.AssessmentLines.Where(l => l.AssessmentId == c.Seed.AssessmentId)
            .Select(l => l.AssessmentLevel!.OrdinanceNumber).Distinct().ToListAsync();
        lam["ordinances"]!.AsArray().Select(o => o!["number"]!.GetValue<string>()).ShouldBe(levelOrdinances);
        var row = lam["rows"]!.AsArray().Single()!;
        row["area"]!.GetValue<decimal>().ShouldBe(500m);
        var demoOwner = lam["owners"]!.AsArray().Single(o => o!["tin"]?.GetValue<string>() == "DEMO-TIN-L52")!;
        demoOwner["email"]!.GetValue<string>().ShouldBe("demo-owner@example.invalid");
        demoOwner["sex"].ShouldBeNull(); // Forms:PrintOwnerSex is off by default
        // The MRPAAO fields are untouched.
        data["mrpaao"]!["kind"]!["type"]!.GetValue<string>().ShouldBe("Land");
    }

    [Fact]
    public async Task OwnersSex_IsCarriedOnlyWhenTheOfficeEnablesIt()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var provider = new TaxDeclarationFormDataProvider(c.Services.GetRequiredService<IApplicationDbContext>(),
            c.Services.GetRequiredService<IOptions<UnitPinOptions>>(), Options.Create(new FormsOptions { PrintOwnerSex = true }));

        var data = (await provider.BuildAsync(c.Seed.TaxDeclaration.Id, CancellationToken.None)).ShouldNotBeNull().Data;

        data["lam"]!["owners"]!.AsArray().Single(o => o!["tin"]?.GetValue<string>() == "DEMO-TIN-L52")!["sex"]!.GetValue<string>().ShouldBe("Female");
    }

    [Fact]
    public async Task Faas_Lam_CarriesEncoderRegistrationAndParties()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;

        var lam = (await c.DataAsync(FormSubjectType.Faas, c.Seed.TaxDeclaration.Id))["lam"]!.AsObject();

        lam["registrationType"]!.GetValue<string>().ShouldBe("Untitled");
        lam["cadastralNumber"]!.GetValue<string>().ShouldBe("DEMO Cad-L52");
        lam["supersededMarketValue"].ShouldBeNull(); // first assessment of the unit
        lam["backTaxPeriod"].ShouldBeNull();
        lam["encodedBy"]!["date"].ShouldNotBeNull();
        lam["owners"]!.AsArray().ShouldContain(o => o!["email"]!.GetValue<string>() == "demo-owner@example.invalid");
        lam["machines"]!.AsArray().ShouldBeEmpty();
        lam.ContainsKey("titleTypeCode").ShouldBeTrue(); // L5-6: the LAM title-type boxes
    }

    [Fact]
    public async Task Registers_Lam_TmcrPreviousPinAndValue_RoaTaxableSplit()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var clock = c.Services.GetRequiredService<IClock>();
        var td = await c.Db.TaxDeclarations.SingleAsync(x => x.Id == c.Seed.TaxDeclaration.Id);
        td.Status = WorkflowStatus.Approved;
        td.ApprovedAt = DateTimeOffset.UtcNow.AddDays(-1);
        var property = await c.Db.Properties.SingleAsync(x => x.Id == c.Seed.PropertyId);
        c.Db.PinAssignments.Add(new PinAssignment
        {
            PropertyId = property.Id, Pin = "DEMO-OLD-PIN-L52", Kind = PinKind.Temporary,
            AssignedAt = DateTimeOffset.UtcNow.AddDays(-30), RetiredAt = DateTimeOffset.UtcNow.AddDays(-2), RetirementReason = "DEMO renumbering",
        });
        await c.Db.SaveChangesAsync();
        var registers = c.Services.GetRequiredService<IRegisterService>();
        var today = clock.Today;

        var tmcr = await registers.CreateRunAsync(new CreateRegisterRunRequest(RegisterKind.TaxMapControlRoll, today, property.BarangayId, null, null, null, null));
        tmcr.IsSuccess.ShouldBeTrue(tmcr.IsSuccess ? null : tmcr.Message);
        var tmcrRow = (await c.DataAsync(FormSubjectType.Register, tmcr.Value.Id))["rows"]!.AsArray().Single()!;
        tmcrRow["lam"]!["previousPin"]!.GetValue<string>().ShouldBe("DEMO-OLD-PIN-L52");
        tmcrRow["lam"]!["cadastralNumber"]!.GetValue<string>().ShouldBe("DEMO Cad-L52");
        tmcrRow["lam"]!["marketValue"]!.GetValue<decimal>().ShouldBe(500_000m);

        var roa = await registers.CreateRunAsync(new CreateRegisterRunRequest(RegisterKind.RecordOfAssessment, today, property.BarangayId,
            c.Seed.ClassificationId, null, today.AddDays(-7), null));
        roa.IsSuccess.ShouldBeTrue(roa.IsSuccess ? null : roa.Message);
        var roaLam = (await c.DataAsync(FormSubjectType.Register, roa.Value.Id))["rows"]!.AsArray().Single()!["lam"]!;
        roaLam["marketValueTaxable"]!.GetValue<decimal>().ShouldBe(500_000m);
        roaLam["assessedValueTaxable"]!.GetValue<decimal>().ShouldBe(100_000m);
        roaLam["marketValueExempt"].ShouldBeNull();
        roaLam["sectionParcel"].ShouldBeNull(); // no permanent PIN in a tax map section

        var roll = await registers.CreateRunAsync(new CreateRegisterRunRequest(RegisterKind.AssessmentRollTaxable, today, property.BarangayId, null, null, null, null));
        var rollLam = (await c.DataAsync(FormSubjectType.Register, roll.Value.Id))["rows"]!.AsArray().Single()!["lam"]!.AsObject();
        rollLam.ContainsKey("administrator").ShouldBeTrue();
        // L5-6: the LAM roll's section, parcel and use code.
        rollLam["actualUseCode"]!.GetValue<string>().ShouldBe(await c.Db.ActualUses.Where(x => x.Id == td.ActualUseId).Select(x => x.Code).SingleAsync());
        (rollLam.ContainsKey("sectionIndex"), rollLam.ContainsKey("parcelNumber")).ShouldBe((true, true));
        tmcrRow["lam"]!["machineCount"]!.GetValue<int>().ShouldBe(0);
    }

    [Fact]
    public async Task Notice_Lam_CarriesTheItemKindAndTheAddresseesEmail()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var notice = await c.Services.GetRequiredService<INoticeService>().GenerateAsync(new GenerateNoticeRequest(c.Seed.AssessmentId));
        notice.IsSuccess.ShouldBeTrue(notice.IsSuccess ? null : notice.Message);

        var data = await c.DataAsync(FormSubjectType.NoticeOfAssessment, notice.Value.Id);

        var item = data["items"]!.AsArray().Single()!;
        item["lamKind"]!.GetValue<string>().ShouldBe("Land");
        item["lamActualUse"]!.GetValue<string>().ShouldNotBeNullOrWhiteSpace(); // L5-6: the LAM NOA's actual use and effectivity
        item["lamEffectivity"].ShouldNotBeNull();
        data["lam"]!["addresseeEmail"]!.GetValue<string>().ShouldBe("demo-owner@example.invalid");
        data["lam"]!["serviceMode"].ShouldBeNull();
    }

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(41, 0, 1, 42)]
    [InlineData(0, 20, 1, 1)]
    [InlineData(19, 20, 1, 20)]
    [InlineData(20, 20, 2, 1)]
    [InlineData(45, 20, 3, 6)]
    public void RollPosition_FollowsTheRowsPerPage(int index, int rowsPerPage, int page, int line)
    {
        RegisterFormDataProvider.RollPosition(index, rowsPerPage).ShouldBe((page, line));
    }

    /// <summary>Step L5-3 (§4.3): issuing a roll records each TD's page and line; the FAAS prints the latest valid one.</summary>
    [Fact]
    public async Task IssuingAnAssessmentRoll_RecordsTheEntry_AndTheFaasPrintsIt_UntilTheRollIsCancelled()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var td = await c.Db.TaxDeclarations.SingleAsync(x => x.Id == c.Seed.TaxDeclaration.Id);
        td.Status = WorkflowStatus.Approved;
        td.ApprovedAt = DateTimeOffset.UtcNow.AddDays(-1);
        await c.Db.SaveChangesAsync();
        var barangayId = await c.Db.Properties.Where(x => x.Id == c.Seed.PropertyId).Select(x => x.BarangayId).SingleAsync();
        var today = c.Services.GetRequiredService<IClock>().Today;
        var run = await c.Services.GetRequiredService<IRegisterService>().CreateRunAsync(
            new CreateRegisterRunRequest(RegisterKind.AssessmentRollTaxable, today, barangayId, null, null, null, null));
        run.IsSuccess.ShouldBeTrue(run.IsSuccess ? null : run.Message);
        var row = (await c.DataAsync(FormSubjectType.Register, run.Value.Id))["rows"]!.AsArray()
            .Single(r => r!["tdNumber"]!.GetValue<string>() == td.TaxDeclarationNumber)!;
        var (page, line) = (row["lam"]!["page"]!.GetValue<int>(), row["lam"]!["line"]!.GetValue<int>());
        (await c.DataAsync(FormSubjectType.Faas, td.Id))["lam"]!["assessmentRollEntry"].ShouldBeNull(); // not yet in an issued roll

        var forms = c.Services.GetRequiredService<IFormService>();
        var issued = await forms.IssueAsync(new IssueFormRequest(RegisterService.FormCode(RegisterKind.AssessmentRollTaxable), run.Value.Id));
        issued.IsSuccess.ShouldBeTrue(issued.IsSuccess ? null : issued.Message);

        var entry = await c.Db.AssessmentRollEntries.SingleAsync(x => x.IssuedFormId == issued.Value.Id && x.TaxDeclarationId == td.Id);
        (entry.Kind, entry.Page, entry.Line).ShouldBe((RegisterKind.AssessmentRollTaxable, page, line));
        var printed = (await c.DataAsync(FormSubjectType.Faas, td.Id))["lam"]!["assessmentRollEntry"]!;
        printed["kind"]!.GetValue<string>().ShouldBe("AssessmentRollTaxable");
        (printed["page"]!.GetValue<int>(), printed["line"]!.GetValue<int>()).ShouldBe((page, line));
        printed["date"]!.GetValue<string>().ShouldBe(entry.EnteredOn.ToString("yyyy-MM-dd"));

        // Issuing again returns the same roll and records nothing more.
        (await forms.IssueAsync(new IssueFormRequest(RegisterService.FormCode(RegisterKind.AssessmentRollTaxable), run.Value.Id))).Value.Id.ShouldBe(issued.Value.Id);
        (await c.Db.AssessmentRollEntries.CountAsync(x => x.TaxDeclarationId == td.Id)).ShouldBe(1);

        // A cancelled roll keeps its entries, but the FAAS no longer prints them.
        (await forms.CancelIssuedAsync(issued.Value.Id, "DEMO wrong period")).IsSuccess.ShouldBeTrue();
        (await c.Db.AssessmentRollEntries.CountAsync(x => x.TaxDeclarationId == td.Id)).ShouldBe(1);
        (await c.DataAsync(FormSubjectType.Faas, td.Id))["lam"]!["assessmentRollEntry"].ShouldBeNull();
    }

    /// <summary>Step L5-5 (§4.5, Q12): a past owner's form is set apart, printed only when the run asks for past owners.</summary>
    [Fact]
    public async Task OwnershipRecordForm_ListsPastHoldings_OnlyWhenTheRunAsks()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var td = await c.Db.TaxDeclarations.SingleAsync(x => x.Id == c.Seed.TaxDeclaration.Id);
        td.Status = WorkflowStatus.Approved;
        td.ApprovedAt = DateTimeOffset.UtcNow.AddDays(-30);
        await c.Db.SaveChangesAsync();
        var today = c.Services.GetRequiredService<IClock>().Today;
        var owner = await c.Db.Taxpayers.SingleAsync(x => x.Tin == "DEMO-TIN-L52");
        var registers = c.Services.GetRequiredService<IRegisterService>();
        CreateRegisterRunRequest Orf(bool past) => new(RegisterKind.OwnershipRecordCard, today, null, null, owner.Id, null, null, IncludePastOwners: past);

        // Held: listed, not past.
        var current = await registers.CreateRunAsync(Orf(past: false));
        current.IsSuccess.ShouldBeTrue(current.IsSuccess ? null : current.Message);
        var held = (await c.DataAsync(FormSubjectType.Register, current.Value.Id))["rows"]!.AsArray()
            .Single(r => r!["tdNumber"]!.GetValue<string>() == td.TaxDeclarationNumber)!;
        held["lam"]!["past"].ShouldBeNull();

        // Sold ten days ago: the form is set apart.
        var holding = await c.Db.PropertyTaxpayers.SingleAsync(x => x.TaxpayerId == owner.Id && x.PropertyId == c.Seed.PropertyId);
        (holding.EndDate, holding.EndReason, holding.IsCurrent) = (today.AddDays(-10), "DEMO sold", false);
        await c.Db.SaveChangesAsync();
        (await registers.CreateRunAsync(Orf(past: false))).Code.ShouldBe("OWNER_HOLDS_NO_PROPERTY");
        var setApart = await registers.CreateRunAsync(Orf(past: true));
        setApart.IsSuccess.ShouldBeTrue(setApart.IsSuccess ? null : setApart.Message);
        setApart.Value.IncludePastOwners.ShouldBeTrue();
        var past = (await c.DataAsync(FormSubjectType.Register, setApart.Value.Id))["rows"]!.AsArray().ShouldHaveSingleItem()!["lam"]!["past"]!;
        past["heldUntil"]!.GetValue<string>().ShouldBe(today.AddDays(-10).ToString("yyyy-MM-dd"));
        past["endReason"]!.GetValue<string>().ShouldBe("DEMO sold");

        // Only the ownership record form takes the option.
        (await registers.CreateRunAsync(new CreateRegisterRunRequest(RegisterKind.TaxMapControlRoll, today,
            await c.Db.Properties.Where(x => x.Id == c.Seed.PropertyId).Select(x => x.BarangayId).SingleAsync(), null, null, null, null, IncludePastOwners: true)))
            .Code.ShouldBe("VALIDATION_FAILED");
    }
}
