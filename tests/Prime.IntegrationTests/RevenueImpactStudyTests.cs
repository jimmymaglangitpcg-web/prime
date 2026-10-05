using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Forms;
using Prime.Application.Features.Smv;
using Prime.Application.Features.SmvSimulations;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step L6-5 (docs/analysis/smv-preparation-general-revision.md §4.5): a revenue compliance and tax impact study over a simulation.
/// The seed's land is assessed at 100,000 now and at 150,000 under the proposed 1,500/sqm; the rates (1% + 1%) and the year's
/// collection are DEMO figures standing for the Treasurer's. Rolled back.
/// </summary>
public class RevenueImpactStudyTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Compliance_TaxGap_AndTheTaxImpactOfTheNewValues_AndOfEachOption()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync();
        await TestSeed.UseReferenceFormsAsync(db);
        var user = scope.ServiceProvider.GetRequiredService<CurrentUserService>();
        user.AppUserId = null;
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, new DateOnly(2026, 1, 1));
        var demo = new AppUser { SupabaseUserId = Guid.NewGuid(), DisplayName = "DEMO", Email = $"demo-{Guid.NewGuid():N}@example.invalid" };
        db.AppUsers.Add(demo);
        await db.SaveChangesAsync();
        user.AppUserId = demo.Id;
        var municipalityId = await db.Properties.Where(x => x.Id == seed.PropertyId).Select(x => x.MunicipalityId).SingleAsync();
        var smvs = scope.ServiceProvider.GetRequiredService<ISmvService>();
        var proposed = (await smvs.CreateSmvAsync(new CreateSmvRequest($"DEMO-RI-{Guid.NewGuid():N}"[..24], new DateOnly(2026, 12, 1), null,
            new DateOnly(2027, 1, 1), 2027, "DEMO proposed SMV for RevenueImpactStudyTests"))).Value;
        var row = await db.SmvSchedules.AsNoTracking().SingleAsync(x => x.SmvId == seed.SmvId);
        await smvs.CreateScheduleAsync(proposed.Id, new CreateSmvScheduleRequest(row.ClassificationId, row.ActualUseId, row.PropertyTypeId, null, "per sqm", 1_500m,
            null, null, new DateOnly(2027, 1, 1)));
        var run = new SmvSimulationRun
        {
            SmvId = proposed.Id, AsOf = new DateOnly(2027, 1, 1), StartedBy = demo.Id, StartedAt = DateTimeOffset.UtcNow,
            Scope = [new SmvSimulationScope { MunicipalityId = municipalityId }],
        };
        db.SmvSimulationRuns.Add(run);
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<SmvSimulationRunner>().RunAsync(run.Id, CancellationToken.None);
        db.ChangeTracker.Clear();
        (await db.SmvSimulationResultLines.CountAsync(l => db.SmvSimulationResults.Any(r => r.Id == l.SmvSimulationResultId && r.SmvSimulationRunId == run.Id))).ShouldBe(1);

        var studies = scope.ServiceProvider.GetRequiredService<IRevenueImpactStudyService>();
        SaveRevenueImpactStudyRequest Request(IReadOnlyList<RevenueImpactOptionInput> options, decimal? collected = 1_500m, string? source = "DEMO treasurer's figures") =>
            new("DEMO study", run.Id, 2026, new DateOnly(2026, 6, 1), collected, collected is null ? null : 100m, source, false, null,
                [new("DEMO basic", 1m, "DEMO"), new("DEMO SEF", 1m, "DEMO")], options);
        RevenueImpactOptionInput[] options =
        [
            new("DEMO option 1", 1.5m, "Lower level and rate", [new(row.ClassificationId, null, 0m, null, 15m)]),
            new("DEMO option 2", 2m, "Rates only, no level", []),
        ];
        var created = await studies.CreateAsync(Request(options));
        created.IsSuccess.ShouldBeTrue(created.Message);
        var s = created.Value;
        s.ExistingRatePercent.ShouldBe(2m);
        var c = s.Compliance.ShouldNotBeNull();
        (c.TaxableAssessedValue, c.TaxPotential, c.TotalCollection, c.TaxGap, c.ComplianceRatePercent, c.CollectionEfficiencyPercent)
            .ShouldBe((100_000m, 2_000m, 1_600m, 400m, (decimal?)80m, (decimal?)75m));
        var byKey = s.Scenarios.ToDictionary(x => x.Key);
        (byKey["new"].Summary.Units, byKey["new"].Summary.CurrentTax, byKey["new"].Summary.ScenarioTax, byKey["new"].Summary.Higher,
            byKey["new"].Summary.LargestIncrease, byKey["new"].Summary.LargestIncreasePercent)
            .ShouldBe((1, 2_000m, 3_000m, 1, (decimal?)1_000m, (decimal?)50m));
        (byKey["option-1"].Summary.ScenarioTax, byKey["option-1"].Summary.Lower, byKey["option-1"].RowsAtExistingLevel).ShouldBe((1_687.50m, 1, 0));
        (byKey["option-2"].Summary.ScenarioTax, byKey["option-2"].RowsAtExistingLevel).ShouldBe((3_000m, 1));

        var units = (await studies.SearchUnitsAsync(s.Id, new TaxImpactUnitSearch(Change: "higher"))).Value;
        var unit = units.Items.Single();
        (unit.CurrentTaxableAssessedValue, unit.NewTaxableAssessedValue, unit.CurrentTax, unit.NewTax, unit.OptionTaxes[0], unit.OptionTaxes[1])
            .ShouldBe((100_000m, 150_000m, 2_000m, 3_000m, 1_687.50m, 3_000m));
        (await studies.SearchUnitsAsync(s.Id, new TaxImpactUnitSearch(Change: "lower"))).Value.TotalCount.ShouldBe(0);

        // The study is replaced as a whole on update.
        var updated = (await studies.UpdateAsync(s.Id, Request([options[0]], collected: null, source: null))).Value;
        (updated.Options.Count, updated.Compliance).ShouldBe((1, (Prime.Domain.DomainServices.RevenueCompliance?)null));

        var preview = await scope.ServiceProvider.GetRequiredService<IFormService>().PreviewAsync("REVENUE_TAX_IMPACT_REPORT", s.Id);
        preview.IsSuccess.ShouldBeTrue(preview.Message);
        preview.Value.Html.ShouldContain("REVENUE AND TAX IMPACT REPORT");
        preview.Value.Html.ShouldContain("1,687.50");

        // Refusals: too many options, collection without its source, a run not completed, a municipal office.
        (await studies.CreateAsync(Request([options[0], options[0], options[0], options[0]]))).Code.ShouldBe("VALIDATION_FAILED");
        (await studies.CreateAsync(Request(options, source: " "))).Code.ShouldBe("COLLECTION_SOURCE_REQUIRED");
        await db.SmvSimulationRuns.Where(x => x.Id == run.Id).ExecuteUpdateAsync(x => x.SetProperty(r => r.Status, JobExecutionStatus.Running));
        (await studies.CreateAsync(Request(options))).Code.ShouldBe("SMV_SIMULATION_NOT_COMPLETED");
        scope.ServiceProvider.GetRequiredService<JurisdictionState>().Restrict([municipalityId]);
        (await studies.CreateAsync(Request(options))).Code.ShouldBe(SmvPreparationService.ForbiddenCode);
    }
}
