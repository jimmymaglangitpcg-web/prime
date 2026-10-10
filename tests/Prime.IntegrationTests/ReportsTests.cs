using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MiniExcelLibs;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Audit;
using Prime.Application.Features.Reports;
using Prime.Application.Features.Taxpayers;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Prime.WebApi.Authentication;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Phase 11 step R1 (docs/analysis/reporting.md §4.1–§4.2, §7): the report framework and the property reports, over the
/// FAAS in force on a date. DEMO data in a fresh municipality per test, rolled back.
/// </summary>
public class ReportsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, BillingFlowTests.Seed Seed, Guid MunicipalityId, Guid BarangayId, DateOnly Today)
    {
        public IReportService Reports => Services.GetRequiredService<IReportService>();
        public ReportRunRequest Here => new() { AsOf = Today, MunicipalityId = MunicipalityId };
    }

    /// <summary>The seeded land unit: approved TD effective 2024-01-01 declaring the posted assessment (MV 500,000; AV 100,000; 500 sqm).</summary>
    private async Task<(Ctx Ctx, IAsyncDisposable Scope)> BeginAsync(bool declareAssessment = true, Taxability taxability = Taxability.Taxable)
    {
        var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, new DateOnly(2026, 1, 1), taxability);
        var td = await db.TaxDeclarations.SingleAsync(x => x.Id == seed.TaxDeclaration.Id);
        td.AssessmentId = declareAssessment ? seed.AssessmentId : null;
        td.ApprovedAt = DateTimeOffset.UtcNow.AddDays(-30);
        var owner = new Taxpayer { TaxpayerType = TaxpayerType.Individual, LastName = "DEMO_ReportOwner", FirstName = "Ana", Address = "DEMO Address 9" };
        var sole = new OwnershipType { Code = $"OT{Guid.NewGuid():N}"[..8], Name = "DEMO_Sole" };
        db.AddRange(owner, sole);
        await db.SaveChangesAsync();
        (await scope.ServiceProvider.GetRequiredService<ITaxpayerService>().AddOwnerAsync(
            new AddPropertyOwnerRequest(seed.PropertyId, owner.Id, sole.Id, 100m, new DateOnly(2020, 1, 1)))).IsSuccess.ShouldBeTrue();
        var place = await db.Properties.Where(p => p.Id == seed.PropertyId).Select(p => new { p.MunicipalityId, p.BarangayId }).SingleAsync();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().Today;
        return (new Ctx(db, scope.ServiceProvider, seed, place.MunicipalityId, place.BarangayId, today), new Disposer(transaction, scope));
    }

    private sealed class Disposer(IAsyncDisposable transaction, IAsyncDisposable scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await scope.DisposeAsync();
        }
    }

    private static async Task<ReportPreviewDto> PreviewAsync(Ctx c, string code, ReportRunRequest? parameters = null)
    {
        var result = await c.Reports.PreviewAsync(code, new ReportPreviewRequest { Parameters = parameters ?? c.Here });
        result.IsSuccess.ShouldBeTrue(result.IsSuccess ? null : result.Message);
        return result.Value;
    }

    private static object? Cell(ReportPreviewDto report, object?[] row, string key) => row[report.Columns.ToList().FindIndex(c => c.Key == key)];

    [Fact]
    public async Task TheCatalogue_ListsTheInventoryAndTheFourSummaries()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;

        c.Reports.List().Select(r => r.Code).ShouldBe(
            ["PROPERTY_INVENTORY", "PROPERTIES_BY_BARANGAY", "PROPERTIES_BY_CLASSIFICATION", "PROPERTIES_BY_ACTUAL_USE", "PROPERTIES_BY_ZONE",
                "TD_LIST", "VALUE_SUMMARY", "ASSESSMENT_HISTORY", "REASSESSMENTS"],
            ignoreOrder: true);
        (await c.Reports.PreviewAsync("NO_SUCH_REPORT", new ReportPreviewRequest())).Code.ShouldBe("REPORT_NOT_FOUND");
    }

    [Fact]
    public async Task ByBarangay_SumsTheFaasInForce_AsTheAssessmentRollDoes()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;

        var report = await PreviewAsync(c, "PROPERTIES_BY_BARANGAY");

        var row = report.Rows.ShouldHaveSingleItem();
        (Cell(report, row, "barangay"), Cell(report, row, "properties"), Cell(report, row, "units")).ShouldBe(("Demo Barangay", 1, 1));
        Cell(report, row, "landArea").ShouldBe(500m);
        Cell(report, row, "taxableMarketValue").ShouldBe(500_000m);
        Cell(report, row, "taxableAssessedValue").ShouldBe(100_000m);
        Cell(report, row, "exemptAssessedValue").ShouldBe(0m);
        Cell(report, report.Totals!, "properties").ShouldBe(1);
        Cell(report, report.Totals!, "assessedValue").ShouldBe(100_000m);
        report.ParameterLines.ShouldContain($"As of: {c.Today:yyyy-MM-dd}");
        report.ParameterLines.ShouldContain("Municipality: Demo Municipality");
    }

    /// <summary>A further unit of the seeded property with its TD and, when <paramref name="assessedValue"/> is given, a posted assessment the TD does not declare.</summary>
    private static async Task AddUnitAsync(Ctx c, RpuType type, Taxability taxability, WorkflowStatus status, decimal? assessedValue,
        DateTimeOffset? cancelledAt = null)
    {
        var seeded = await c.Db.Assessments.AsNoTracking().SingleAsync(x => x.Id == c.Seed.AssessmentId);
        var rpu = new RealPropertyUnit { PropertyId = c.Seed.PropertyId, RpuNumber = $"RPU-{Guid.NewGuid():N}", RpuType = type, EffectivityDate = new DateOnly(2024, 1, 1) };
        c.Db.RealPropertyUnits.Add(rpu);
        if (assessedValue is { } av)
        {
            c.Db.Assessments.Add(new Assessment
            {
                Rpu = rpu, PropertyId = c.Seed.PropertyId, ValuationId = seeded.ValuationId, AssessmentYear = 2026, MarketValue = av * 5, AssessedValue = av,
                Status = WorkflowStatus.Posted, EffectiveDate = new DateOnly(2026, 1, 1),
            });
        }
        c.Db.TaxDeclarations.Add(new TaxDeclaration
        {
            Rpu = rpu, PropertyId = c.Seed.PropertyId, TaxDeclarationNumber = $"TD-{Guid.NewGuid():N}", EffectivityDate = new DateOnly(2024, 1, 1),
            Taxability = taxability, ClassificationId = c.Seed.TaxDeclaration.ClassificationId, ActualUseId = c.Seed.TaxDeclaration.ActualUseId,
            AssessmentYear = 2024, Status = status, ApprovedAt = status is WorkflowStatus.Approved or WorkflowStatus.Cancelled ? DateTimeOffset.UtcNow.AddDays(-30) : null,
            CancelledAt = cancelledAt,
        });
        await c.Db.SaveChangesAsync();
    }

    /// <summary>The assessed values an Assessment Roll of the seeded barangay lists on the date.</summary>
    private static async Task<decimal> RollTotalAsync(Ctx c, RegisterKind kind, DateOnly asOf)
    {
        var run = await c.Services.GetRequiredService<Prime.Application.Features.Registers.IRegisterService>().CreateRunAsync(
            new Prime.Application.Features.Registers.CreateRegisterRunRequest(kind, asOf, c.BarangayId, null, null, null, "DEMO report check"));
        run.IsSuccess.ShouldBeTrue(run.IsSuccess ? null : run.Message);
        var provider = c.Services.GetServices<Prime.Application.Features.Forms.IFormDataProvider>().Single(p => p.SubjectType == FormSubjectType.Register);
        var data = (await provider.BuildAsync(run.Value.Id, CancellationToken.None)).ShouldNotBeNull().Data;
        return data["rows"]!.AsArray().Sum(r => r!["assessedValue"]?.GetValue<decimal>() ?? 0m);
    }

    [Fact]
    public async Task TheFaasInForce_AgreeWithTheAssessmentRolls_TaxableAndExempt_OnEachDate()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        await AddUnitAsync(c, RpuType.Building, Taxability.Exempt, WorkflowStatus.Approved, 40_000m);    // exempt, no declared assessment
        await AddUnitAsync(c, RpuType.Machinery, Taxability.Taxable, WorkflowStatus.Cancelled, 30_000m, DateTimeOffset.UtcNow); // cancelled today
        await AddUnitAsync(c, RpuType.OtherImprovement, Taxability.Taxable, WorkflowStatus.Draft, 99_000m); // never in force

        foreach (var asOf in new[] { c.Today.AddDays(-1), c.Today })
        {
            var report = await PreviewAsync(c, "PROPERTIES_BY_BARANGAY", c.Here with { AsOf = asOf });
            var taxable = (decimal)Cell(report, report.Totals!, "taxableAssessedValue")!;
            var exempt = (decimal)Cell(report, report.Totals!, "exemptAssessedValue")!;
            taxable.ShouldBe(await RollTotalAsync(c, RegisterKind.AssessmentRollTaxable, asOf), $"taxable as of {asOf}");
            exempt.ShouldBe(await RollTotalAsync(c, RegisterKind.AssessmentRollExempt, asOf), $"exempt as of {asOf}");
            (taxable, exempt).ShouldBe(asOf < c.Today ? (130_000m, 40_000m) : (100_000m, 40_000m));
        }
    }

    [Fact]
    public async Task AsOfAPastDate_GivesTheValuesThenInForce_UnchangedByALaterRevision()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var yesterday = c.Today.AddDays(-1);
        var before = await PreviewAsync(c, "PROPERTIES_BY_CLASSIFICATION", c.Here with { AsOf = yesterday });

        // A revision today: the old TD cancelled now, a new one effective today declaring a new posted assessment.
        var old = await c.Db.TaxDeclarations.SingleAsync(x => x.Id == c.Seed.TaxDeclaration.Id);
        var revised = await c.Db.Assessments.AsNoTracking().SingleAsync(x => x.Id == c.Seed.AssessmentId);
        var newAssessment = new Assessment
        {
            RpuId = revised.RpuId, PropertyId = revised.PropertyId, ValuationId = revised.ValuationId, AssessmentYear = revised.AssessmentYear,
            MarketValue = 750_000m, AssessmentLevelId = revised.AssessmentLevelId, AssessmentPercentage = revised.AssessmentPercentage, AssessedValue = 150_000m,
            Status = WorkflowStatus.Posted, EffectiveDate = c.Today, PreviousAssessmentId = revised.Id,
        };
        c.Db.Assessments.Add(newAssessment);
        old.Status = WorkflowStatus.Cancelled;
        old.CancelledAt = DateTimeOffset.UtcNow;
        c.Db.TaxDeclarations.Add(new TaxDeclaration
        {
            RpuId = old.RpuId, PropertyId = old.PropertyId, TaxDeclarationNumber = $"TD-{Guid.NewGuid():N}", RevisionNumber = 2, EffectivityDate = c.Today,
            Taxability = Taxability.Taxable, ClassificationId = old.ClassificationId, ActualUseId = old.ActualUseId, AssessmentYear = c.Today.Year,
            Status = WorkflowStatus.Approved, ApprovedAt = DateTimeOffset.UtcNow, PreviousTaxDeclarationId = old.Id, Assessment = newAssessment,
        });
        await c.Db.SaveChangesAsync();

        var then = await PreviewAsync(c, "PROPERTIES_BY_CLASSIFICATION", c.Here with { AsOf = yesterday });
        var now = await PreviewAsync(c, "PROPERTIES_BY_CLASSIFICATION");

        Cell(then, then.Totals!, "assessedValue").ShouldBe(100_000m);
        System.Text.Json.JsonSerializer.Serialize(then.Rows).ShouldBe(System.Text.Json.JsonSerializer.Serialize(before.Rows));
        Cell(now, now.Totals!, "assessedValue").ShouldBe(150_000m);
        Cell(now, now.Totals!, "units").ShouldBe(1);
        (await PreviewAsync(c, "PROPERTIES_BY_CLASSIFICATION", c.Here with { AsOf = new DateOnly(2023, 12, 31) })).Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnExemptTdWithoutAnAssessment_IsListedExempt_AtTheUnitsPostedValue()
    {
        var (c, scope) = await BeginAsync(declareAssessment: false, taxability: Taxability.Exempt);
        await using var _ = scope;

        var report = await PreviewAsync(c, "PROPERTIES_BY_ACTUAL_USE");

        var row = report.Rows.ShouldHaveSingleItem();
        Cell(report, row, "taxableAssessedValue").ShouldBe(0m);
        Cell(report, row, "exemptMarketValue").ShouldBe(500_000m);
        Cell(report, row, "exemptAssessedValue").ShouldBe(100_000m);
    }

    [Fact]
    public async Task Inventory_ListsThePropertyWithItsOwnerAndLandTd()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var pin = await c.Db.Properties.Where(p => p.Id == c.Seed.PropertyId).Select(p => p.PropertyIdentificationNumber).SingleAsync();

        var report = await PreviewAsync(c, "PROPERTY_INVENTORY");

        var row = report.Rows.ShouldHaveSingleItem();
        Cell(report, row, "pin").ShouldBe(pin);
        Cell(report, row, "owners").ShouldBe("DEMO_ReportOwner, Ana");
        Cell(report, row, "landTds").ShouldBe(c.Seed.TaxDeclaration.TaxDeclarationNumber);
        (Cell(report, row, "units"), Cell(report, row, "landArea"), Cell(report, row, "marketValue")).ShouldBe((1, 500m, 500_000m));
        report.TotalRows.ShouldBe(1);
    }

    [Fact]
    public async Task AMunicipalUser_CannotNameAnotherMunicipality_AndSeesOnlyTheirOwn()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var elsewhere = new Municipality { ProvinceId = await c.Db.Municipalities.Where(m => m.Id == c.MunicipalityId).Select(m => m.ProvinceId).SingleAsync(),
            PsgcCode = $"M{Guid.NewGuid():N}"[..10], Name = "DEMO Elsewhere" };
        c.Db.Municipalities.Add(elsewhere);
        await c.Db.SaveChangesAsync();
        c.Services.GetRequiredService<JurisdictionState>().Restrict([elsewhere.Id]);

        (await c.Reports.PreviewAsync("PROPERTIES_BY_BARANGAY", new ReportPreviewRequest { Parameters = c.Here })).Code.ShouldBe("JURISDICTION_FORBIDDEN");
        var own = await PreviewAsync(c, "PROPERTY_INVENTORY", new ReportRunRequest { AsOf = c.Today });
        own.Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExportCsv_WritesTheRowsWithABom_AndAuditsTheDownload()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;

        var export = await c.Reports.ExportAsync("PROPERTIES_BY_BARANGAY", c.Here, ReportFormat.Csv);

        export.IsSuccess.ShouldBeTrue(export.IsSuccess ? null : export.Message);
        export.Value.FileName.ShouldBe($"properties-by-barangay-{c.Today:yyyyMMdd}.csv");
        using var stream = new MemoryStream();
        await export.Value.WriteAsync(stream, CancellationToken.None);
        var bytes = stream.ToArray();
        bytes.Take(3).ShouldBe(new byte[] { 0xEF, 0xBB, 0xBF });
        var lines = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3).TrimEnd().Split("\r\n");
        lines[0].ShouldStartWith("Municipality,Barangay,Properties,");
        lines.Length.ShouldBe(2);
        lines[1].ShouldBe("Demo Municipality,Demo Barangay,1,1,500,500000.00,100000.00,0.00,0.00,500000.00,100000.00");

        var audit = await c.Db.AuditLogs.Where(a => a.Action == AuditAction.Export && a.TableName == ReportService.AuditTable)
            .OrderByDescending(a => a.Timestamp).FirstAsync();
        audit.Module.ShouldBe(AuditTrailService.ExportModule);
        audit.NewValue.ShouldNotBeNull().ShouldContain("Properties by barangay");
        audit.NewValue.ShouldContain("1 rows");
        audit.NewValue.ShouldContain("Csv");
    }

    [Fact]
    public async Task ExportExcel_HasTheHeaderBlock_TypedValues_AndTotals()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;

        var export = await c.Reports.ExportAsync("PROPERTY_INVENTORY", c.Here, ReportFormat.Xlsx);

        export.IsSuccess.ShouldBeTrue(export.IsSuccess ? null : export.Message);
        using var stream = new MemoryStream();
        await export.Value.WriteAsync(stream, CancellationToken.None);
        stream.Position = 0;
        var rows = (await stream.QueryAsync()).Cast<IDictionary<string, object?>>().ToList();
        rows.ShouldContain(r => Equals(r["A"], "Property inventory"));
        rows.ShouldContain(r => Equals(r["A"], "Municipality: Demo Municipality"));
        var titles = rows.FindIndex(r => Equals(r["A"], "PIN"));
        titles.ShouldBeGreaterThan(0);
        var data = rows[titles + 1];
        data["G"].ShouldBe("DEMO_ReportOwner, Ana");
        Convert.ToDecimal(data["L"]).ShouldBe(500_000m);
        rows[titles + 2]["A"].ShouldBe("Total: 1 properties");
    }

    [Fact]
    public async Task ADownloadOverTheRowLimit_IsRefused_WithoutReadingTheRows()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var property = await c.Db.Properties.AsNoTracking().SingleAsync(p => p.Id == c.Seed.PropertyId);
        c.Db.Properties.Add(new PropertyEntity
        {
            PropertyIdentificationNumber = $"PIN-{Guid.NewGuid():N}", ProvinceId = property.ProvinceId, MunicipalityId = property.MunicipalityId, BarangayId = property.BarangayId,
        });
        await c.Db.SaveChangesAsync();
        var limited = ActivatorUtilities.CreateInstance<ReportService>(c.Services, Options.Create(new ReportsOptions { SyncRowLimit = 1 }));

        var export = await limited.ExportAsync("PROPERTY_INVENTORY", c.Here, ReportFormat.Csv);

        export.Code.ShouldBe("REPORT_TOO_LARGE");
        export.Message.ShouldNotBeNull().ShouldContain("2 rows");
        (await limited.ExportAsync("PROPERTY_INVENTORY", c.Here with { BarangayId = c.BarangayId }, ReportFormat.Csv)).Code.ShouldBe("REPORT_TOO_LARGE");
    }

    [Fact]
    public void ACsvTextCell_ThatLooksLikeAFormula_IsNeutralized()
    {
        Prime.Infrastructure.Reporting.ReportCells.Neutralize("=HYPERLINK(\"x\")").ShouldBe("'=HYPERLINK(\"x\")");
        Prime.Infrastructure.Reporting.ReportCells.Neutralize("DEMO, Ana").ShouldBe("DEMO, Ana");
    }

    [Fact]
    public async Task TheApi_ShowsReportsWithRecordsView_AndDownloadsOnlyWithRecordsExport()
    {
        var client = factory.CreateClient();
        HttpRequestMessage Get(string path, string actAs)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add(DevelopmentAuthenticationHandler.ActAsHeader, actAs);
            return request;
        }

        (await client.SendAsync(Get("/api/reports", "viewer"))).StatusCode.ShouldBe(HttpStatusCode.OK);
        var refused = await client.SendAsync(Get("/api/reports/PROPERTIES_BY_ZONE/export?format=csv", "viewer"));
        refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var downloaded = await client.SendAsync(Get("/api/reports/PROPERTIES_BY_ZONE/export?format=csv", "checker"));
        downloaded.StatusCode.ShouldBe(HttpStatusCode.OK);
        downloaded.Content.Headers.ContentType!.MediaType.ShouldBe("text/csv");
        downloaded.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        (await client.SendAsync(Get("/api/reports/PROPERTIES_BY_ZONE/export?format=pdf", "checker"))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var noRun = $"/api/reports/register-runs/{Guid.NewGuid()}/export?format=csv";
        (await client.SendAsync(Get(noRun, "viewer"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.SendAsync(Get(noRun, "checker"))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.SendAsync(Get($"/api/reports/sales-report-runs/{Guid.NewGuid()}/export?format=xlsx", "checker"))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var preview = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/reports/PROPERTIES_BY_ZONE/preview")
        {
            Content = JsonContent.Create(new { parameters = new { }, page = 1, pageSize = 10 }),
            Headers = { { DevelopmentAuthenticationHandler.ActAsHeader, "viewer" } },
        });
        preview.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ---------------- Step R3: assessment reports and run downloads ----------------

    [Fact]
    public async Task ValueSummary_GivesEachKindAndClassification_WithASubtotalPerKind()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        await AddUnitAsync(c, RpuType.Building, Taxability.Taxable, WorkflowStatus.Approved, 20_000m);

        var report = await PreviewAsync(c, "VALUE_SUMMARY");

        report.Rows.Select(r => (Cell(report, r, "kind"), Cell(report, r, "assessedValue"))).ShouldBe(
            [("Land", 100_000m), ("Land", 100_000m), ("Building", 20_000m), ("Building", 20_000m)]);
        Cell(report, report.Rows[1], "classification").ShouldBe("Subtotal, land");
        Cell(report, report.Rows[0], "landArea").ShouldBe(500m);
        Cell(report, report.Rows[2], "landArea").ShouldBeNull();
        Cell(report, report.Rows[2], "marketValue").ShouldBe(100_000m);
        (Cell(report, report.Totals!, "properties"), Cell(report, report.Totals!, "units"), Cell(report, report.Totals!, "assessedValue"))
            .ShouldBe((1, 2, 120_000m));
    }

    [Fact]
    public async Task TdList_ListsTheTdWithItsValuesAndOwner_AndFiltersByStatusCodePinAndPeriod()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var td = await c.Db.TaxDeclarations.AsNoTracking().SingleAsync(x => x.Id == c.Seed.TaxDeclaration.Id);
        var pin = await c.Db.Properties.Where(p => p.Id == c.Seed.PropertyId).Select(p => p.PropertyIdentificationNumber).SingleAsync();
        var recorded = c.Services.GetRequiredService<IClock>().LocalDate(td.ApprovedAt!.Value);
        var lastTwoMonths = c.Here with { FromDate = c.Today.AddDays(-60), ToDate = c.Today };

        var report = await PreviewAsync(c, "TD_LIST", lastTwoMonths);

        var row = report.Rows.ShouldHaveSingleItem();
        (Cell(report, row, "tdNumber"), Cell(report, row, "pin"), Cell(report, row, "kind"), Cell(report, row, "status"))
            .ShouldBe((td.TaxDeclarationNumber, pin, "Land", "Approved"));
        (Cell(report, row, "owners"), Cell(report, row, "recordedOn"), Cell(report, row, "assessedValue"))
            .ShouldBe(("DEMO_ReportOwner, Ana", recorded, 100_000m));
        report.Totals![0].ShouldBe("Total: 1 Tax Declarations");
        report.ParameterLines.ShouldContain($"Period: {c.Today.AddDays(-60):yyyy-MM-dd} to {c.Today:yyyy-MM-dd}");

        (await PreviewAsync(c, "TD_LIST", lastTwoMonths with { Status = WorkflowStatus.Draft })).TotalRows.ShouldBe(0);
        (await PreviewAsync(c, "TD_LIST", lastTwoMonths with { Status = WorkflowStatus.Approved })).TotalRows.ShouldBe(1);
        (await PreviewAsync(c, "TD_LIST", lastTwoMonths with { TransactionCode = "DEMO_NONE" })).TotalRows.ShouldBe(0);
        (await PreviewAsync(c, "TD_LIST", lastTwoMonths with { Pin = pin[..12] })).TotalRows.ShouldBe(1);
        (await PreviewAsync(c, "TD_LIST", lastTwoMonths with { Pin = "NO-SUCH-PIN" })).TotalRows.ShouldBe(0);
        (await PreviewAsync(c, "TD_LIST", lastTwoMonths with { ToDate = recorded.AddDays(-1) })).TotalRows.ShouldBe(0);
        (await c.Reports.PreviewAsync("TD_LIST", new ReportPreviewRequest { Parameters = c.Here with { FromDate = c.Today, ToDate = c.Today.AddDays(-1) } }))
            .Code.ShouldBe("VALIDATION_FAILED");
    }

    [Fact]
    public async Task TdList_WithoutADeclaredAssessment_ShowsTheUnitsPostedValue()
    {
        var (c, scope) = await BeginAsync(declareAssessment: false);
        await using var _ = scope;
        var period = c.Here with { FromDate = c.Today.AddDays(-60) };

        // The seeded TD is effective 2024, before the unit's only posted assessment (2026): nothing was in force then.
        var before = await PreviewAsync(c, "TD_LIST", period);
        (Cell(before, before.Rows.Single(), "marketValue"), Cell(before, before.Rows.Single(), "assessedValue")).ShouldBe((null, null));

        var td = await c.Db.TaxDeclarations.SingleAsync(x => x.Id == c.Seed.TaxDeclaration.Id);
        td.EffectivityDate = new DateOnly(2026, 1, 1);
        await c.Db.SaveChangesAsync();
        var report = await PreviewAsync(c, "TD_LIST", period);

        var row = report.Rows.ShouldHaveSingleItem();
        (Cell(report, row, "marketValue"), Cell(report, row, "assessedValue")).ShouldBe((500_000m, 100_000m));
    }

    [Fact]
    public async Task AssessmentHistory_ShowsEachAssessmentBesideThePreviousOne_AndReassessmentsOnlyTheReassessment()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var first = await c.Db.Assessments.AsNoTracking().SingleAsync(x => x.Id == c.Seed.AssessmentId);
        var reassessment = new Prime.Domain.Entities.Transactions.TransactionType
        {
            Code = $"T{Guid.NewGuid():N}"[..12], Name = "DEMO reassessment", Kind = PropertyTransactionKind.Reassessment, LegalBasis = "DEMO",
            EffectiveDate = new DateOnly(2020, 1, 1), Status = WorkflowStatus.Approved, ApprovedAt = DateTimeOffset.UtcNow,
        };
        c.Db.TransactionTypes.Add(reassessment);
        c.Db.Assessments.Add(new Assessment
        {
            RpuId = first.RpuId, PropertyId = first.PropertyId, ValuationId = first.ValuationId, AssessmentYear = c.Today.Year + 1, MarketValue = 600_000m,
            AssessmentLevelId = first.AssessmentLevelId, AssessmentPercentage = first.AssessmentPercentage, AssessedValue = 120_000m,
            Status = WorkflowStatus.Posted, EffectiveDate = new DateOnly(c.Today.Year + 1, 1, 1), MadeOn = c.Today, PreviousAssessmentId = first.Id,
            TransactionTypeId = reassessment.Id, TransactionCode = "DEMO_RE", CauseDate = c.Today.AddDays(-10), Remarks = "DEMO extension built",
        });
        await c.Db.SaveChangesAsync();
        var pin = await c.Db.Properties.Where(p => p.Id == c.Seed.PropertyId).Select(p => p.PropertyIdentificationNumber).SingleAsync();
        var period = c.Here with { FromDate = c.Today.AddDays(-60), ToDate = c.Today, Pin = pin };

        var history = await PreviewAsync(c, "ASSESSMENT_HISTORY", period);

        history.Rows.Count.ShouldBe(2);
        var later = history.Rows[1];
        (Cell(history, later, "previousAssessedValue"), Cell(history, later, "assessedValue"), Cell(history, later, "change"))
            .ShouldBe((100_000m, 120_000m, 20_000m));
        Cell(history, later, "reason").ShouldBe("DEMO reassessment — DEMO extension built");
        Cell(history, history.Rows[0], "previousAssessedValue").ShouldBeNull();
        (Cell(history, history.Totals!, "assessedValue"), Cell(history, history.Totals!, "change")).ShouldBe((220_000m, 120_000m));

        var reassessments = await PreviewAsync(c, "REASSESSMENTS", period);
        var row = reassessments.Rows.ShouldHaveSingleItem();
        (Cell(reassessments, row, "causeDate"), Cell(reassessments, row, "madeOn"), Cell(reassessments, row, "change"))
            .ShouldBe((c.Today.AddDays(-10), c.Today, 20_000m));
        (await PreviewAsync(c, "REASSESSMENTS", period with { ToDate = c.Today.AddDays(-1) })).Rows.ShouldBeEmpty();
    }

    /// <summary>A taxable Assessment Roll run of the seeded barangay as of today.</summary>
    private static async Task<Guid> RollRunAsync(Ctx c)
    {
        var run = await c.Services.GetRequiredService<Prime.Application.Features.Registers.IRegisterService>().CreateRunAsync(
            new Prime.Application.Features.Registers.CreateRegisterRunRequest(RegisterKind.AssessmentRollTaxable, c.Today, c.BarangayId, null, null, null, "DEMO export"));
        run.IsSuccess.ShouldBeTrue(run.IsSuccess ? null : run.Message);
        return run.Value.Id;
    }

    private static async Task<List<IDictionary<string, object?>>> ExcelRowsAsync(ReportExport export)
    {
        using var stream = new MemoryStream();
        await export.WriteAsync(stream, CancellationToken.None);
        stream.Position = 0;
        return (await stream.QueryAsync()).Cast<IDictionary<string, object?>>().ToList();
    }

    [Fact]
    public async Task ARegisterRunNotIssued_IsDownloadedFromTheRecords_WithAddressesHiddenWithoutThePermission()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var runId = await RollRunAsync(c);
        var seesPersonal = (await c.Services.GetRequiredService<Prime.Application.Features.Security.IPermissionService>().GetAsync())
            .Contains(Prime.Application.Common.Security.Permissions.TaxpayerViewPersonal);

        var export = await c.Services.GetRequiredService<IRunExportService>().RegisterRunAsync(runId, ReportFormat.Xlsx);

        export.IsSuccess.ShouldBeTrue(export.IsSuccess ? null : export.Message);
        export.Value.FileName.ShouldBe($"ar-taxable-{c.Today:yyyyMMdd}.xlsx");
        var rows = await ExcelRowsAsync(export.Value);
        rows.ShouldContain(r => Equals(r["A"], "Assessment Roll, taxable properties"));
        rows.ShouldContain(r => Equals(r["A"], "Not issued: read from the records on the day of the download"));
        var titles = rows.FindIndex(r => Equals(r["A"], "Page"));
        var data = rows[titles + 1];
        data["D"].ShouldBe(c.Seed.TaxDeclaration.TaxDeclarationNumber);
        data["G"].ShouldBe("DEMO_ReportOwner, Ana");
        data["H"].ShouldBe(seesPersonal ? "DEMO Address 9" : Prime.Application.Common.Security.PersonalData.MaskAddress("DEMO Address 9"));
        Convert.ToDecimal(data["N"]).ShouldBe(100_000m);

        var audit = await c.Db.AuditLogs.Where(a => a.Action == AuditAction.Export && a.TableName == ReportService.AuditTable && a.RecordId == runId).SingleAsync();
        audit.NewValue.ShouldNotBeNull().ShouldContain("Not issued");
    }

    [Fact]
    public async Task AnIssuedRegisterRun_IsDownloadedFromItsSnapshot_UnchangedByLaterEdits()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var runId = await RollRunAsync(c);
        var issued = await c.Services.GetRequiredService<Prime.Application.Features.Forms.IFormService>()
            .IssueAsync(new Prime.Application.Features.Forms.IssueFormRequest("AR_TAXABLE", runId));
        issued.IsSuccess.ShouldBeTrue(issued.IsSuccess ? null : issued.Message);
        var assessment = await c.Db.Assessments.SingleAsync(x => x.Id == c.Seed.AssessmentId);
        assessment.AssessedValue = 1m;
        await c.Db.SaveChangesAsync();

        var export = await c.Services.GetRequiredService<IRunExportService>().RegisterRunAsync(runId, ReportFormat.Csv);

        export.IsSuccess.ShouldBeTrue(export.IsSuccess ? null : export.Message);
        using var stream = new MemoryStream();
        await export.Value.WriteAsync(stream, CancellationToken.None);
        var csv = Encoding.UTF8.GetString(stream.ToArray());
        csv.ShouldContain(c.Seed.TaxDeclaration.TaxDeclarationNumber);
        csv.ShouldContain("100000.00");
        var audit = await c.Db.AuditLogs.Where(a => a.Action == AuditAction.Export && a.RecordId == runId).SingleAsync();
        audit.NewValue.ShouldNotBeNull().ShouldContain($"Issued {c.Today:yyyy-MM-dd} on form AR_TAXABLE");
    }

    [Fact]
    public async Task RunDownloads_AreRefused_ForAnUnknownRun_AndForAnAbstract()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var runs = c.Services.GetRequiredService<IRunExportService>();
        var marketReports = c.Services.GetRequiredService<Prime.Application.Features.MarketData.IMarketDataReportService>();
        (await runs.RegisterRunAsync(Guid.NewGuid(), ReportFormat.Csv)).Code.ShouldBe("REGISTER_RUN_NOT_FOUND");
        (await runs.SalesReportRunAsync(Guid.NewGuid(), ReportFormat.Csv)).Code.ShouldBe("MARKET_DATA_REPORT_NOT_FOUND");
        var abstractRun = await marketReports.CreateAsync(new Prime.Application.Features.MarketData.CreateMarketDataReportRequest(
            MarketDataReportKind.TransactionsAbstract, c.MunicipalityId, c.Today.AddDays(-30), c.Today, null));
        abstractRun.IsSuccess.ShouldBeTrue(abstractRun.IsSuccess ? null : abstractRun.Message);
        (await runs.SalesReportRunAsync(abstractRun.Value.Id, ReportFormat.Csv)).Code.ShouldBe("VALIDATION_FAILED");
        var sales = await marketReports.CreateAsync(new Prime.Application.Features.MarketData.CreateMarketDataReportRequest(
            MarketDataReportKind.SalesReport, c.MunicipalityId, c.Today.AddDays(-30), c.Today, null));
        sales.IsSuccess.ShouldBeTrue(sales.IsSuccess ? null : sales.Message);
        (await runs.SalesReportRunAsync(sales.Value.Id, ReportFormat.Xlsx)).IsSuccess.ShouldBeTrue();
    }
}
