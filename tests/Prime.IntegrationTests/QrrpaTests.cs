using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.LevyRates;
using Prime.Application.Features.ReportConfiguration;
using Prime.Application.Features.Reports;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Exemptions;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Phase 11 step R4c (docs/analysis/reporting.md §10, Q15–Q18, Q20): the QRRPA in the rows of an approved row map, with
/// the building threshold, the levy rates and the collectibles; the row map and the system parameters as configuration
/// under maker-checker. Every code, rate and threshold here is DEMO data. Rolled back.
/// </summary>
public class QrrpaTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, CurrentUserService User, AppUser Maker, AppUser Checker,
        BillingFlowTests.Seed Seed, Guid MunicipalityId, string ClassCode, DateOnly Today)
    {
        public IReportService Reports => Services.GetRequiredService<IReportService>();
        public IReportRowMapService Maps => Services.GetRequiredService<IReportRowMapService>();
        public ISystemParameterService Parameters => Services.GetRequiredService<ISystemParameterService>();
    }

    private async Task<(Ctx C, IAsyncDisposable Scope)> BeginAsync()
    {
        var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, new DateOnly(2026, 1, 1));
        var td = await db.TaxDeclarations.SingleAsync(x => x.Id == seed.TaxDeclaration.Id);
        td.AssessmentId = seed.AssessmentId;
        td.ApprovedAt = DateTimeOffset.UtcNow.AddYears(-1);
        var users = Enumerable.Range(0, 2).Select(i => new AppUser
        {
            SupabaseUserId = Guid.NewGuid(), DisplayName = $"DEMO QRRPA User {(char)('A' + i)}", Email = $"demo-{Guid.NewGuid():N}@example.invalid",
        }).ToList();
        db.AppUsers.AddRange(users);
        await db.SaveChangesAsync();
        var municipalityId = await db.Properties.Where(p => p.Id == seed.PropertyId).Select(p => p.MunicipalityId).SingleAsync();
        var classCode = await db.Classifications.Where(c => c.Id == seed.ClassificationId).Select(c => c.Code).SingleAsync();
        var user = scope.ServiceProvider.GetRequiredService<CurrentUserService>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().Today;
        return (new Ctx(db, scope.ServiceProvider, user, users[0], users[1], seed, municipalityId, classCode, today), new Disposer(transaction, scope));
    }

    private sealed class Disposer(IAsyncDisposable transaction, IAsyncDisposable scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await scope.DisposeAsync();
        }
    }

    /// <summary>A further unit of the seeded property, its posted assessment (MV = 5 × AV) and an approved TD not declaring it.</summary>
    private static async Task<(RealPropertyUnit Rpu, TaxDeclaration Td)> AddUnitAsync(Ctx c, RpuType type, decimal assessedValue, Taxability taxability = Taxability.Taxable)
    {
        var seeded = await c.Db.Assessments.AsNoTracking().SingleAsync(x => x.Id == c.Seed.AssessmentId);
        var rpu = new RealPropertyUnit { PropertyId = c.Seed.PropertyId, RpuNumber = $"RPU-{Guid.NewGuid():N}", RpuType = type, EffectivityDate = new DateOnly(2024, 1, 1) };
        c.Db.RealPropertyUnits.Add(rpu);
        c.Db.Assessments.Add(new Assessment
        {
            Rpu = rpu, PropertyId = c.Seed.PropertyId, ValuationId = seeded.ValuationId, AssessmentYear = 2026, MarketValue = assessedValue * 5,
            AssessedValue = assessedValue, Status = WorkflowStatus.Posted, EffectiveDate = new DateOnly(2026, 1, 1),
        });
        var td = new TaxDeclaration
        {
            Rpu = rpu, PropertyId = c.Seed.PropertyId, TaxDeclarationNumber = $"TD-{Guid.NewGuid():N}", EffectivityDate = new DateOnly(2024, 1, 1),
            Taxability = taxability, ClassificationId = c.Seed.ClassificationId, ActualUseId = c.Seed.TaxDeclaration.ActualUseId, AssessmentYear = 2024,
            Status = WorkflowStatus.Approved, ApprovedAt = DateTimeOffset.UtcNow.AddYears(-1),
        };
        c.Db.TaxDeclarations.Add(td);
        await c.Db.SaveChangesAsync();
        return (rpu, td);
    }

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value, ReportRowMapDefinition.Json);

    /// <summary>Effective today, so it takes over from any map the database already holds.</summary>
    private static async Task<ReportRowMapDto> ApprovedMapAsync(Ctx c, ReportRowMapDefinition definition)
    {
        c.User.AppUserId = c.Maker.Id;
        var created = await c.Maps.CreateAsync(new CreateReportRowMapRequest("QRRPA", "DEMO rows", Json(definition), "DEMO — not the LAM's", c.Today, null));
        created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);
        (await c.Maps.ApproveAsync(created.Value.Id)).Code.ShouldBe("CANNOT_APPROVE_OWN_REPORT_ROW_MAP");
        c.User.AppUserId = c.Checker.Id;
        var approved = await c.Maps.ApproveAsync(created.Value.Id);
        approved.IsSuccess.ShouldBeTrue(approved.IsSuccess ? null : approved.Message);
        return approved.Value;
    }

    private static async Task<ReportPreviewDto> QrrpaAsync(Ctx c)
    {
        var result = await c.Reports.PreviewAsync("QRRPA", new ReportPreviewRequest
        {
            Parameters = new ReportRunRequest { FromDate = c.Today, MunicipalityId = c.MunicipalityId }, PageSize = 200,
        });
        result.IsSuccess.ShouldBeTrue(result.IsSuccess ? null : result.Message);
        return result.Value;
    }

    private static object? Cell(ReportPreviewDto report, object?[] row, string key) => row[report.Columns.ToList().FindIndex(c => c.Key == key)];

    private static object?[] Row(ReportPreviewDto report, string group, string label) =>
        report.Rows.Single(r => (string?)r[0] == group && (string?)r[1] == label);

    [Fact]
    public async Task Qrrpa_PutsEachPartInItsMappedRow_SplitsBuildingsAtTheThreshold_AndComputesTheCollectibles()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        await AddUnitAsync(c, RpuType.Building, 60_000m);   // MV 300,000: over the threshold
        await AddUnitAsync(c, RpuType.Building, 20_000m);   // MV 100,000: at or below
        var (exemptRpu, _) = await AddUnitAsync(c, RpuType.Building, 40_000m, Taxability.Exempt);
        var (_, restrictedTd) = await AddUnitAsync(c, RpuType.Machinery, 30_000m);

        var exemptionType = new ExemptionType
        {
            Code = $"DEMO-QX-{Guid.NewGuid():N}"[..16], Name = "DEMO QRRPA exemption", LegalBasis = "DEMO", EffectiveDate = new DateOnly(2020, 1, 1),
            Status = WorkflowStatus.Approved, ApprovedAt = DateTimeOffset.UtcNow,
        };
        var annotationType = new AnnotationType { Code = $"DEMO-QR-{Guid.NewGuid():N}"[..16], Name = "DEMO QRRPA restriction" };
        c.Db.AddRange(exemptionType, annotationType);
        c.Db.PropertyExemptions.Add(new PropertyExemption
        {
            PropertyId = c.Seed.PropertyId, RpuId = exemptRpu.Id, ExemptionType = exemptionType, ClaimedOn = new DateOnly(2024, 1, 1),
            ProofDueDate = new DateOnly(2024, 2, 1), Status = ExemptionStatus.Approved, EffectiveDate = new DateOnly(2024, 1, 1),
            DecidedBy = c.Checker.Id, DecidedAt = DateTimeOffset.UtcNow.AddYears(-1),
        });
        c.Db.Add(new TaxDeclarationAnnotation
        {
            TaxDeclarationId = restrictedTd.Id, AnnotationType = annotationType, Text = "DEMO restriction", EffectiveDate = new DateOnly(2024, 1, 1),
        });
        await c.Db.SaveChangesAsync();

        await ApprovedMapAsync(c, new ReportRowMapDefinition(
            [new RestrictionGroupSpec("RG", "DEMO restricted", [annotationType.Code])],
            [
                new ReportRowSpec(ReportRowSection.Taxable, "T1", "DEMO class row", Classifications: [c.ClassCode], SplitsBuildings: true),
                new ReportRowSpec(ReportRowSection.Taxable, "T9", "DEMO others", Others: true),
                new ReportRowSpec(ReportRowSection.Exempt, "E1", "DEMO exemption row", ExemptionTypes: [exemptionType.Code]),
                new ReportRowSpec(ReportRowSection.Restricted, "R1", "DEMO restricted, all", Restriction: "RG", Others: true),
                new ReportRowSpec(ReportRowSection.IdleLand, "I1", "DEMO idle"),
            ]));
        c.User.AppUserId = c.Maker.Id;
        var threshold = await c.Parameters.CreateAsync(new CreateSystemParameterRequest(SystemParameterCatalog.QrrpaResidentialBuildingThreshold, 200_000m,
            "DEMO — not an issuance", c.Today, null, null));
        threshold.IsSuccess.ShouldBeTrue(threshold.IsSuccess ? null : threshold.Message);
        var rates = c.Services.GetRequiredService<ILevyRateService>();
        var basic = (await rates.CreateAsync(new CreateLevyRateRequest(LevyKind.Basic, c.MunicipalityId, null, 1m, "DEMO", new DateOnly(2020, 1, 1), null, null))).Value;
        var sef = (await rates.CreateAsync(new CreateLevyRateRequest(LevyKind.SpecialEducationFund, c.MunicipalityId, null, 0.5m, "DEMO", new DateOnly(2020, 1, 1), null, null))).Value;
        c.User.AppUserId = c.Checker.Id;
        (await c.Parameters.ApproveAsync(threshold.Value.Id)).IsSuccess.ShouldBeTrue();
        (await rates.ApproveAsync(basic.Id)).IsSuccess.ShouldBeTrue();
        (await rates.ApproveAsync(sef.Id)).IsSuccess.ShouldBeTrue();

        var report = await QrrpaAsync(c);

        var taxable = Row(report, "Taxable", "DEMO class row");
        (Cell(report, taxable, "rpuLand"), Cell(report, taxable, "rpuBuilding"), Cell(report, taxable, "rpuTotal")).ShouldBe((1, 2, 3));
        (Cell(report, taxable, "landArea"), Cell(report, taxable, "mvLand")).ShouldBe((500m, 500_000m));
        (Cell(report, taxable, "mvBuildingUpTo"), Cell(report, taxable, "mvBuildingOver"), Cell(report, taxable, "mvBuilding")).ShouldBe((100_000m, 300_000m, 0m));
        Cell(report, taxable, "avTotal").ShouldBe(180_000m);
        (Cell(report, taxable, "rateBasic"), Cell(report, taxable, "rateSef")).ShouldBe((1m, 0.5m));
        (Cell(report, taxable, "collectBasic"), Cell(report, taxable, "collectSef"), Cell(report, taxable, "collectTotal")).ShouldBe((1_800m, 900m, 2_700m));
        Cell(report, Row(report, "Taxable", "DEMO others"), "rpuTotal").ShouldBe(0);

        var exempt = Row(report, "Exempt", "DEMO exemption row");
        (Cell(report, exempt, "rpuBuilding"), Cell(report, exempt, "avBuilding"), Cell(report, exempt, "mvBuilding")).ShouldBe((1, 40_000m, 200_000m));
        (Cell(report, exempt, "rateBasic"), Cell(report, exempt, "collectTotal")).ShouldBe((null, null));

        var restricted = Row(report, "DEMO restricted", "DEMO restricted, all");
        (Cell(report, restricted, "rpuMachinery"), Cell(report, restricted, "avMachinery"), Cell(report, restricted, "collectBasic")).ShouldBe((1, 30_000m, 300m));
        Cell(report, Row(report, "With restrictions", "Total, properties with restrictions"), "avTotal").ShouldBe(30_000m);
        Cell(report, Row(report, "Idle lands", "DEMO idle"), "avTotal").ShouldBeNull();

        (Cell(report, report.Totals!, "rpuTotal"), Cell(report, report.Totals!, "avTotal")).ShouldBe((5, 250_000m));
        (Cell(report, report.Totals!, "collectBasic"), Cell(report, report.Totals!, "collectSef")).ShouldBe((2_100m, 1_050m));
        report.Notes.ShouldContain("Barangays included: 1.");
        report.Notes.ShouldContain(n => n.StartsWith("Rows: DEMO rows"));
        report.Notes.ShouldContain(n => n.StartsWith("Residential buildings are split at a market value of 200,000.00"));
        report.ParameterLines.ShouldContain(l => l.StartsWith($"Quarter: Q{(c.Today.Month + 2) / 3} {c.Today.Year}"));
    }

    [Fact]
    public async Task Qrrpa_WithoutARowMap_ListsPrimesClassifications_AndSaysWhatIsMissing()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        // A quarter before any map: the DEMO maps PRIME and its checks create start in 2026 or later.
        var td = await c.Db.TaxDeclarations.SingleAsync(x => x.Id == c.Seed.TaxDeclaration.Id);
        td.ApprovedAt = new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero);
        await c.Db.SaveChangesAsync();
        var quarterEnd = new DateOnly(2025, 12, 31);
        (await c.Maps.InForceAsync("QRRPA", quarterEnd)).ShouldBeNull();

        var result = await c.Reports.PreviewAsync("QRRPA", new ReportPreviewRequest
        {
            Parameters = new ReportRunRequest { FromDate = new DateOnly(2025, 10, 1), MunicipalityId = c.MunicipalityId }, PageSize = 200,
        });
        result.IsSuccess.ShouldBeTrue(result.IsSuccess ? null : result.Message);
        var report = result.Value;

        var row = Row(report, "Taxable", "DEMO_Residential");
        (Cell(report, row, "rpuLand"), Cell(report, row, "avLand")).ShouldBe((1, 100_000m));
        (Cell(report, row, "mvBuildingUpTo"), Cell(report, row, "rateBasic"), Cell(report, row, "collectBasic")).ShouldBe((null, null, 0m));
        report.Notes.ShouldContain(n => n.StartsWith("No approved QRRPA row map is in force"));
        report.Notes.ShouldContain(n => n.Contains("have no basic or SEF rate in force"));
    }

    [Fact]
    public async Task ARowMap_IsCheckedAgainstPrimesCodes_AndItsStructure()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        c.User.AppUserId = c.Maker.Id;

        async Task<string?> Refusal(ReportRowMapDefinition d)
        {
            var r = await c.Maps.CreateAsync(new CreateReportRowMapRequest("QRRPA", "DEMO", Json(d), "DEMO", new DateOnly(2020, 1, 1), null));
            r.IsSuccess.ShouldBeFalse();
            return r.Message;
        }

        (await Refusal(new(null, [new ReportRowSpec(ReportRowSection.Taxable, "T1", "DEMO", Classifications: ["NO-SUCH-CLASS"])])))
            .ShouldContain("No classification has the code \"NO-SUCH-CLASS\"");
        (await Refusal(new(null, [new ReportRowSpec(ReportRowSection.Restricted, "R1", "DEMO", Restriction: "RX")])))
            .ShouldContain("does not define");
        (await Refusal(new(null, [new ReportRowSpec(ReportRowSection.Taxable, "T1", "DEMO", Others: true), new ReportRowSpec(ReportRowSection.Taxable, "T2", "DEMO", Others: true)])))
            .ShouldContain("more than one \"others\" row");
        (await c.Maps.CreateAsync(new CreateReportRowMapRequest("NOT_A_REPORT", "DEMO", Json(new ReportRowMapDefinition(null, [])), "DEMO", new DateOnly(2020, 1, 1), null)))
            .Code.ShouldBe("VALIDATION_FAILED");
        var bad = await c.Maps.CreateAsync(new CreateReportRowMapRequest("QRRPA", "DEMO", JsonSerializer.SerializeToElement(new { rows = 3 }), "DEMO", new DateOnly(2020, 1, 1), null));
        bad.Message!.ShouldContain("not a valid row map");

        c.Services.GetRequiredService<JurisdictionState>().Restrict([c.MunicipalityId]);
        (await c.Maps.CreateAsync(new CreateReportRowMapRequest("QRRPA", "DEMO", Json(new ReportRowMapDefinition(null,
            [new ReportRowSpec(ReportRowSection.Taxable, "T1", "DEMO", Others: true)])), "DEMO", new DateOnly(2020, 1, 1), null))).Code.ShouldBe("JURISDICTION_FORBIDDEN");
    }

    [Fact]
    public async Task ASystemParameter_IsOneOfTheCatalogues_AndApprovedByASecondUser()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        c.User.AppUserId = c.Maker.Id;
        (await c.Parameters.CreateAsync(new CreateSystemParameterRequest("NO_SUCH_PARAMETER", 1m, "DEMO", new DateOnly(2020, 1, 1), null, null))).Code.ShouldBe("VALIDATION_FAILED");
        var code = SystemParameterCatalog.QrrpaResidentialBuildingThreshold;
        var draft = (await c.Parameters.CreateAsync(new CreateSystemParameterRequest(code, 123m, "DEMO", new DateOnly(2090, 1, 1), null, null))).Value;
        (await c.Parameters.ApproveAsync(draft.Id)).Code.ShouldBe("CANNOT_APPROVE_OWN_SYSTEM_PARAMETER");
        c.User.AppUserId = c.Checker.Id;
        (await c.Parameters.ApproveAsync(draft.Id)).IsSuccess.ShouldBeTrue();
        (await c.Parameters.InForceAsync(code, new DateOnly(2090, 1, 1)))!.Value.ShouldBe(123m);
        (await c.Parameters.InForceAsync(code, new DateOnly(2089, 12, 31)))?.Id.ShouldNotBe(draft.Id);
    }
}
