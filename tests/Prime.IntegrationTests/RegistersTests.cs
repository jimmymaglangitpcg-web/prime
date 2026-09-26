using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Forms;
using Prime.Application.Features.Registers;
using Prime.Application.Features.Taxpayers;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// docs/analysis/mrpaao-forms-model.md §15 step 6 — the MRPAAO registers, as
/// dated runs whose rows come from the FAAS in force. DEMO data.
/// </summary>
public class RegistersTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, BillingFlowTests.Seed Seed, Guid BarangayId, Guid OwnerId, DateOnly Today)
    {
        public IFormService Forms => Services.GetRequiredService<IFormService>();
        public IRegisterService Registers => Services.GetRequiredService<IRegisterService>();
    }

    /// <summary>
    /// The seeded land RPU: approved TD effective 2024-01-01 (entered today), declaring
    /// the posted assessment (MV 500,000; AV 100,000), owned by DEMO_RegisterOwner.
    /// </summary>
    private async Task<(Ctx Ctx, IAsyncDisposable Transaction)> BeginAsync()
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, new DateOnly(2026, 1, 1));
        var td = await db.TaxDeclarations.SingleAsync(x => x.Id == seed.TaxDeclaration.Id);
        td.AssessmentId = seed.AssessmentId;
        td.TransactionCode = "DC";
        var owner = new Taxpayer { TaxpayerType = TaxpayerType.Individual, LastName = "DEMO_RegisterOwner", FirstName = "Juan", Address = "DEMO Address 7" };
        var sole = new OwnershipType { Code = $"OT{Guid.NewGuid():N}"[..8], Name = "DEMO_Sole" };
        db.AddRange(owner, sole);
        await db.SaveChangesAsync();
        (await scope.ServiceProvider.GetRequiredService<ITaxpayerService>().AddOwnerAsync(
            new AddPropertyOwnerRequest(seed.PropertyId, owner.Id, sole.Id, 100m, new DateOnly(2020, 1, 1)))).IsSuccess.ShouldBeTrue();
        var barangayId = await db.Properties.Where(p => p.Id == seed.PropertyId).Select(p => p.BarangayId).SingleAsync();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().Today;
        return (new Ctx(db, scope.ServiceProvider, seed, barangayId, owner.Id, today), new Scoped(transaction, scope));
    }

    private sealed class Scoped(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    private static async Task<string> PreviewAsync(Ctx c, RegisterRunDto run, string? formCode = null)
    {
        var preview = await c.Forms.PreviewAsync(formCode ?? run.FormCode, run.Id);
        preview.IsSuccess.ShouldBeTrue(preview.IsSuccess ? null : preview.Message);
        return preview.Value.Html;
    }

    private static async Task<RegisterRunDto> RunAsync(Ctx c, RegisterKind kind, DateOnly asOf, DateOnly? from = null, bool byOwner = false, bool byClass = false)
    {
        var run = await c.Registers.CreateRunAsync(new CreateRegisterRunRequest(kind, asOf, byOwner ? null : c.BarangayId,
            byClass ? c.Seed.ClassificationId : null, byOwner ? c.OwnerId : null, from, "DEMO run"));
        run.IsSuccess.ShouldBeTrue(run.IsSuccess ? null : run.Message);
        return run.Value;
    }

    [Fact]
    public async Task TaxMapControlRoll_ListsTheParcelInForce_AndIssuesFrozen()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var run = await RunAsync(c, RegisterKind.TaxMapControlRoll, c.Today);

        var issued = await c.Forms.IssueAsync(new IssueFormRequest("TMCR", run.Id));

        issued.IsSuccess.ShouldBeTrue(issued.IsSuccess ? null : issued.Message);
        issued.Value.Authority.ShouldBe(FormAuthority.Mrpaao);
        var html = issued.Value.Html.ShouldNotBeNull();
        html.ShouldContain("TAX MAP CONTROL ROLL");
        html.ShouldContain(c.Seed.TaxDeclaration.TaxDeclarationNumber);
        html.ShouldContain("DEMO_REGISTEROWNER, Juan", Case.Insensitive);
        html.ShouldContain("500 sqm");
        html.ShouldContain(">DC<");
        html.ShouldContain("Parcels listed: <b>1</b>");
        html.ShouldNotContain("print it with form");
        // Reprinting returns the same frozen issue.
        (await c.Forms.IssueAsync(new IssueFormRequest("TMCR", run.Id))).Value.Id.ShouldBe(issued.Value.Id);
    }

    [Fact]
    public async Task Registers_AsOfBeforeTheTdTookEffect_AreEmpty()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;

        var html = await PreviewAsync(c, await RunAsync(c, RegisterKind.TaxMapControlRoll, new DateOnly(2023, 12, 31)));

        html.ShouldContain("No land parcel is declared in this barangay as of this date.");
        html.ShouldNotContain(c.Seed.TaxDeclaration.TaxDeclarationNumber);
    }

    [Fact]
    public async Task AssessmentRolls_SplitTaxableAndExempt_AndASupplementListsOnlyNewEntries()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;

        var taxable = await PreviewAsync(c, await RunAsync(c, RegisterKind.AssessmentRollTaxable, c.Today));
        taxable.ShouldContain("ASSESSMENT ROLL — TAXABLE PROPERTIES");
        taxable.ShouldContain(c.Seed.TaxDeclaration.TaxDeclarationNumber);
        taxable.ShouldContain("100,000.00");
        taxable.ShouldContain("DEMO Address 7");
        taxable.ShouldContain("General revision: <b>2026</b>");

        (await PreviewAsync(c, await RunAsync(c, RegisterKind.AssessmentRollExempt, c.Today)))
            .ShouldContain("No FAAS to list for this barangay and period.");

        // Entered today: in a supplement from today, not in one from tomorrow.
        (await PreviewAsync(c, await RunAsync(c, RegisterKind.AssessmentRollTaxable, c.Today, from: c.Today)))
            .ShouldContain(c.Seed.TaxDeclaration.TaxDeclarationNumber);
        var later = await PreviewAsync(c, await RunAsync(c, RegisterKind.AssessmentRollTaxable, c.Today.AddDays(2), from: c.Today.AddDays(1)));
        later.ShouldContain("SUPPLEMENT");
        later.ShouldNotContain(c.Seed.TaxDeclaration.TaxDeclarationNumber);
    }

    [Fact]
    public async Task OwnershipRecordCard_ListsTheOwnersUnitsWithValues()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;

        var html = await PreviewAsync(c, await RunAsync(c, RegisterKind.OwnershipRecordCard, c.Today, byOwner: true));

        html.ShouldContain("OWNERSHIP RECORD CARD");
        html.ShouldContain("DEMO Address 7");
        html.ShouldContain(c.Seed.TaxDeclaration.TaxDeclarationNumber);
        html.ShouldContain("500,000.00");
        html.ShouldContain("100,000.00");
    }

    [Fact]
    public async Task RecordOfAssessment_ListsEntriesOfThePeriod_SplitByKind()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;

        var html = await PreviewAsync(c, await RunAsync(c, RegisterKind.RecordOfAssessment, c.Today, from: c.Today.AddDays(-30), byClass: true));

        html.ShouldContain("RECORD OF ASSESSMENT");
        html.ShouldContain(c.Seed.TaxDeclaration.TaxDeclarationNumber);
        html.ShouldContain("TOTAL (1)");
        html.ShouldContain("500,000.00");
        html.ShouldContain(">2024<"); // year taxes begin: the TD's effectivity

        var before = await PreviewAsync(c, await RunAsync(c, RegisterKind.RecordOfAssessment, c.Today.AddDays(-1), from: c.Today.AddDays(-30), byClass: true));
        before.ShouldContain("TOTAL (0)");
    }

    [Fact]
    public async Task Runs_RequireTheirScope_AndTheWrongFormSaysSo()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;

        (await c.Registers.CreateRunAsync(new CreateRegisterRunRequest(RegisterKind.OwnershipRecordCard, c.Today, c.BarangayId, null, null, null, null)))
            .Code.ShouldBe("VALIDATION_FAILED");
        (await c.Registers.CreateRunAsync(new CreateRegisterRunRequest(RegisterKind.RecordOfAssessment, c.Today, c.BarangayId, null, null, null, null)))
            .Code.ShouldBe("VALIDATION_FAILED");
        (await c.Registers.CreateRunAsync(new CreateRegisterRunRequest(RegisterKind.TaxMapControlRoll, c.Today, c.BarangayId, null, null, c.Today.AddDays(1), null)))
            .Code.ShouldBe("VALIDATION_FAILED");

        var run = await RunAsync(c, RegisterKind.TaxMapControlRoll, c.Today);
        (await PreviewAsync(c, run, "ROA")).ShouldContain("This run is a TaxMapControlRoll register; print it with form TMCR.");
    }
}
