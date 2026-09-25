using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Forms;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// docs/analysis/mrpaao-forms-model.md §13 step 5a — the MRPAAO FAAS and TD
/// layouts, rendered from PRIME's records (seeded at startup). DEMO data.
/// </summary>
public class MrpaaoFormsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, BillingFlowTests.Seed Seed)
    {
        public IFormService Forms => Services.GetRequiredService<IFormService>();
    }

    /// <summary>The seeded land RPU, whose approved TD declares the seeded posted assessment (AV 100,000; effective 2026-01-01).</summary>
    private async Task<(Ctx Ctx, IAsyncDisposable Transaction)> BeginAsync(bool declare = true)
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, new DateOnly(2026, 1, 1));
        if (declare)
        {
            var td = await db.TaxDeclarations.SingleAsync(x => x.Id == seed.TaxDeclaration.Id);
            td.AssessmentId = seed.AssessmentId;
            td.TransactionCode = "DC";
            await db.SaveChangesAsync();
        }
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
    public async Task LandFaas_IssuesFromTheTd_WithTheManualsBlocks()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;

        var issued = await c.Forms.IssueAsync(new IssueFormRequest("FAAS_LAND", c.Seed.TaxDeclaration.Id));

        issued.IsSuccess.ShouldBeTrue(issued.IsSuccess ? null : issued.Message);
        issued.Value.Authority.ShouldBe(FormAuthority.Mrpaao);
        issued.Value.DocumentNumber.ShouldBe(c.Seed.TaxDeclaration.TaxDeclarationNumber); // FAAS = ARP = TD number
        var html = issued.Value.Html.ShouldNotBeNull();
        html.ShouldContain("MRPAAO 2004, Attachment 1");
        html.ShouldNotContain("PROVISIONAL LAYOUT"); // no provisional watermark or banner
        foreach (var block in new[] { "LAND APPRAISAL", "OTHER IMPROVEMENTS", "MARKET VALUE", "PROPERTY ASSESSMENT", "RECORD OF SUPERSEDED ASSESSMENT" })
        {
            html.ShouldContain(block);
        }
        html.ShouldContain(">DC<"); // transaction code
        html.ShouldContain("1,000.00"); // unit value
        html.ShouldContain("500,000.00"); // base and market value
        html.ShouldContain("100,000.00"); // assessed value
        html.ShouldContain("<b>Q1</b> Qtr. <b>2024</b>"); // effectivity of the TD (2024-01-01)
        html.ShouldContain("None — first assessment of this unit.");
        html.ShouldNotContain("use the");
    }

    [Fact]
    public async Task TaxDeclaration_UsesTheManualsLayout_WithAmountInWordsAndNote()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;

        var preview = await c.Forms.PreviewAsync("TAX_DECLARATION", c.Seed.TaxDeclaration.Id);

        preview.IsSuccess.ShouldBeTrue(preview.IsSuccess ? null : preview.Message);
        preview.Value.FormVersion.ShouldBe(3);
        preview.Value.Authority.ShouldBe(FormAuthority.Mrpaao);
        var html = preview.Value.Html;
        html.ShouldContain("TAX DECLARATION OF REAL PROPERTY");
        html.ShouldContain("KIND OF PROPERTY ASSESSED");
        html.ShouldContain("ONE HUNDRED THOUSAND PESOS ONLY");
        html.ShouldContain("This declaration cancels TD No.");
        html.ShouldContain("does not and cannot by itself alone confer any ownership");
        html.ShouldContain("500 sqm"); // area of the declared classification
    }

    [Fact]
    public async Task FaasOfATdWithoutAssessment_PreviewsOnly_AndTheWrongKindSaysSo()
    {
        var (c, tx) = await BeginAsync(declare: false);
        await using var _ = tx;

        (await c.Forms.IssueAsync(new IssueFormRequest("FAAS_LAND", c.Seed.TaxDeclaration.Id))).Code.ShouldBe("FORM_SUBJECT_NOT_ISSUABLE");
        var wrongKind = await c.Forms.PreviewAsync("FAAS_BUILDING", c.Seed.TaxDeclaration.Id);
        wrongKind.Value.Html.ShouldContain("This Tax Declaration is for Land; use the LAND FAAS.");
    }
}
