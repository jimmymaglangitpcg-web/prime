using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.AssessmentLevels;
using Prime.Application.Features.Assessments;
using Prime.Application.Features.Lands;
using Prime.Application.Features.Smv;
using Prime.Application.Features.Valuation;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step L1-3 (docs/analysis/valuation-foundation.md §4.3): certified and ordinance SMVs, SMV
/// coverage by municipality, rates by sub-class and barangay with the actual use optional, and a
/// strip priced by another class than the one that assesses it. DEMO SMVs and unit values only;
/// rolled back.
/// </summary>
public class SmvModelTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly DateOnly Revision2027 = new(2027, 1, 1);

    [Fact]
    public async Task A_certified_SMV_names_its_certification_and_an_ordinance_SMV_its_ordinance()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var _ = await db.Database.BeginTransactionAsync();
        var smv = scope.ServiceProvider.GetRequiredService<ISmvService>();

        var missing = await smv.CreateSmvAsync(new CreateSmvRequest(null, null, null, Revision2027, 2027, "DEMO", Basis: SmvBasis.Certified));
        missing.Message!.ShouldContain("certificationReference is required");
        (await smv.CreateSmvAsync(new CreateSmvRequest(null, null, null, Revision2027, 2027, "DEMO"))).Message!.ShouldContain("ordinanceNumber is required");

        var reference = $"DEMO-CERT-{Guid.NewGuid():N}"[..30];
        var certified = await smv.CreateSmvAsync(new CreateSmvRequest(null, null, null, Revision2027, 2027, "DEMO", Basis: SmvBasis.Certified,
            CertifiedOn: new DateOnly(2026, 11, 2), CertificationReference: reference, PublishedOn: new DateOnly(2026, 11, 20), PublicationReference: "DEMO gazette"));
        certified.IsSuccess.ShouldBeTrue(certified.Message);
        (certified.Value.Basis, certified.Value.Reference, certified.Value.OrdinanceNumber, certified.Value.Coverage!.Count)
            .ShouldBe((SmvBasis.Certified, reference, (string?)null, 0));
        (await smv.CreateSmvAsync(new CreateSmvRequest(null, null, null, Revision2027, 2027, "DEMO", Basis: SmvBasis.Certified, CertificationReference: reference)))
            .Code.ShouldBe("SMV_CERTIFICATION_DUPLICATE");
    }

    [Fact]
    public async Task Rates_follow_coverage_sub_class_barangay_and_the_valuation_key()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<PrimeDbContext>();
        await using var _ = await db.Database.BeginTransactionAsync();
        // 500 sqm of DEMO residential land; the 2026 province-wide SMV prices it at 1,000/sqm; level 20 % up to 1,000,000.
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(services, db, new DateOnly(2026, 1, 1));
        var property = await db.Properties.AsNoTracking().SingleAsync(x => x.Id == seed.PropertyId);
        var land = await db.Lands.AsNoTracking().SingleAsync(x => x.RpuId == seed.RpuId);
        var landType = await TestSeed.LandPropertyTypeAsync(db);
        var otherProvince = await db.Municipalities.AsNoTracking().SingleAsync(x => x.Id == property.MunicipalityId);
        var otherMunicipality = new Municipality { ProvinceId = otherProvince.ProvinceId, PsgcCode = $"M{Guid.NewGuid():N}"[..10], Name = "DEMO other town" };
        var commercial = new Classification { Code = $"CC{Guid.NewGuid():N}"[..8], Name = "DEMO_Commercial" };
        var subClass = new SubClassification { Code = $"SC{Guid.NewGuid():N}"[..8], Name = "DEMO R-1" };
        db.AddRange(otherMunicipality, commercial, subClass);
        await db.SaveChangesAsync();

        var smv = services.GetRequiredService<ISmvService>();
        async Task<Guid> Certified(Guid municipalityId)
        {
            var created = await smv.CreateSmvAsync(new CreateSmvRequest(null, null, null, Revision2027, 2027, "DEMO", Basis: SmvBasis.Certified,
                CertificationReference: $"DEMO-CERT-{Guid.NewGuid():N}"[..30], MunicipalityIds: [municipalityId]));
            created.IsSuccess.ShouldBeTrue(created.Message);
            (await TestSeed.AsCheckerAsync(scope.ServiceProvider, () => smv.ApproveSmvAsync(created.Value.Id))).IsSuccess.ShouldBeTrue();
            return created.Value.Id;
        }
        async Task Rate(Guid smvId, Guid classificationId, decimal value, Guid? sub = null, Guid? barangay = null)
        {
            var created = await smv.CreateScheduleAsync(smvId, new CreateSmvScheduleRequest(classificationId, null, landType.Id, null, "per sqm", value,
                null, null, Revision2027, SubClassificationId: sub, BarangayId: barangay));
            created.IsSuccess.ShouldBeTrue(created.Message);
            (await TestSeed.AsCheckerAsync(scope.ServiceProvider, () => smv.ApproveScheduleAsync(created.Value.Id))).IsSuccess.ShouldBeTrue();
        }

        // An SMV for another town does not price this land, nor take a barangay outside it.
        var elsewhere = await Certified(otherMunicipality.Id);
        await Rate(elsewhere, seed.ClassificationId, 3_000m);
        (await smv.CreateScheduleAsync(elsewhere, new CreateSmvScheduleRequest(seed.ClassificationId, null, landType.Id, null, "per sqm", 1m, null, null,
            Revision2027, BarangayId: property.BarangayId))).Code.ShouldBe("BARANGAY_OUTSIDE_SMV_COVERAGE");
        var valuations = services.GetRequiredService<IValuationService>();
        (await valuations.ComputeForLandAsync(land.Id, asOf: Revision2027)).Value.ComputedMarketValue.ShouldBe(500_000m);

        // The SMV covering this town: plain 2,000; this barangay 2,500; sub-class R-1 2,200; commercial 5,000. No actual use on any.
        var covering = await Certified(property.MunicipalityId);
        await Rate(covering, seed.ClassificationId, 2_000m);
        await Rate(covering, seed.ClassificationId, 2_500m, barangay: property.BarangayId);
        await Rate(covering, seed.ClassificationId, 2_200m, sub: subClass.Id);
        await Rate(covering, commercial.Id, 5_000m);

        var lands = services.GetRequiredService<ILandService>();
        // Strip 1 (500 sqm, no sub-class) is made from the land; strip 2 has sub-class R-1; strip 3 is priced as commercial.
        (await lands.AddStripAsync(land.Id, new AddLandStripRequest(seed.ClassificationId, subClass.Id, land.ActualUseId, null, 100m))).IsSuccess.ShouldBeTrue();
        var priced = await lands.AddStripAsync(land.Id, new AddLandStripRequest(seed.ClassificationId, null, land.ActualUseId, null, 50m,
            ValuationClassificationId: commercial.Id));
        priced.IsSuccess.ShouldBeTrue(priced.Message);
        priced.Value.Strips.Single(s => s.Sequence == 3).ValuationClassificationName.ShouldBe("DEMO_Commercial");

        var valued = await valuations.ComputeForLandAsync(land.Id, asOf: Revision2027);
        valued.IsSuccess.ShouldBeTrue(valued.Message);
        var lines = valued.Value.Lines!;
        lines.Select(l => (l.UnitValue, l.MarketValue)).ShouldBe([(2_500m, 1_250_000m), (2_200m, 220_000m), (5_000m, 250_000m)]);
        (lines[2].ClassificationName, lines[2].PricedClassificationName).ShouldBe(("DEMO_Residential", "DEMO_Commercial"));
        lines[0].PricedClassificationName.ShouldBeNull();
        valued.Value.ComputedMarketValue.ShouldBe(1_720_000m);

        // Assessed under its own class: the residential level (a DEMO bracket above 1,000,000 at 20 %), not a commercial one.
        var levels = services.GetRequiredService<IAssessmentLevelService>();
        var bracket = await levels.CreateAsync(new CreateAssessmentLevelRequest("DEMO-ORD", new DateOnly(2026, 1, 1), seed.ClassificationId, land.ActualUseId,
            landType.Id, 1_000_000m, null, 20m, new DateOnly(2026, 1, 1)));
        bracket.IsSuccess.ShouldBeTrue(bracket.Message);
        (await TestSeed.AsCheckerAsync(scope.ServiceProvider, () => levels.ApproveAsync(bracket.Value.Id))).IsSuccess.ShouldBeTrue();
        var preview = await services.GetRequiredService<IAssessmentService>().PreviewAsync(
            new CreateAssessmentRequest(valued.Value.Id, null, Revision2027, null, null, "DEMO"));
        preview.IsSuccess.ShouldBeTrue(preview.Message);
        preview.Value.Lines.Select(l => (l.ClassificationName, l.AssessmentPercentage)).ShouldBe([("DEMO_Residential", 20m)]);
        preview.Value.AssessedValue.ShouldBe(344_000m);
    }
}
