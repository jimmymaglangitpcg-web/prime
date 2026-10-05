using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Lands;
using Prime.Application.Features.Smv;
using Prime.Application.Features.Valuation;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step L6-7 (docs/analysis/smv-preparation-general-revision.md §4.7, Q17): an amendment of an approved SMV between
/// revisions carries only the rows it changes; from its effectivity they take the place of the amended SMV's rows for the
/// same key, and elsewhere the amended SMV still applies. DEMO SMVs, unit values and factors only; rolled back.
/// </summary>
public class SmvAmendmentTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly DateOnly Amended = new(2026, 7, 1);

    private static string Reference() => $"DEMO-AMD-{Guid.NewGuid():N}"[..30];

    [Fact]
    public async Task An_amendment_replaces_the_rows_it_changes_from_its_effectivity()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<PrimeDbContext>();
        await using var _ = await db.Database.BeginTransactionAsync();
        // 500 sqm of DEMO residential land; the 2026 province-wide DEMO SMV prices it at 1,000/sqm.
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(services, db, new DateOnly(2026, 1, 1));
        var property = await db.Properties.AsNoTracking().SingleAsync(x => x.Id == seed.PropertyId);
        var land = await db.Lands.AsNoTracking().SingleAsync(x => x.RpuId == seed.RpuId);
        var commercial = new Classification { Code = $"CC{Guid.NewGuid():N}"[..8], Name = "DEMO_Commercial" };
        db.Add(commercial);
        await db.SaveChangesAsync();
        var smv = services.GetRequiredService<ISmvService>();
        var valuations = services.GetRequiredService<IValuationService>();

        CreateSmvRequest Amendment(Guid amends, DateOnly effective, SmvAmendmentGround? ground = SmvAmendmentGround.Infrastructure, string? reference = null) =>
            new(null, null, null, effective, 0, "DEMO amendment", SmvBasis.Amendment, CertificationReference: reference, AmendsSmvId: amends,
                AmendmentGround: ground);

        // Refusals: no ground, an unknown SMV, an effectivity not after the amended SMV's, a link on a non-amendment.
        (await smv.CreateSmvAsync(Amendment(seed.SmvId, Amended, ground: null))).Code.ShouldBe("VALIDATION_FAILED");
        (await smv.CreateSmvAsync(Amendment(Guid.NewGuid(), Amended))).Code.ShouldBe("SMV_NOT_FOUND");
        (await smv.CreateSmvAsync(Amendment(seed.SmvId, new DateOnly(2026, 1, 1)))).Code.ShouldBe("SMV_AMENDMENT_EFFECTIVITY");
        (await smv.CreateSmvAsync(new CreateSmvRequest(null, null, null, Amended, 2026, "DEMO", SmvBasis.Certified, CertificationReference: Reference(),
            AmendsSmvId: seed.SmvId))).Code.ShouldBe("VALIDATION_FAILED");

        // An amendment is approved with its certification's reference.
        var uncertified = await smv.CreateSmvAsync(Amendment(seed.SmvId, new DateOnly(2026, 9, 1)));
        uncertified.IsSuccess.ShouldBeTrue(uncertified.Message);
        (await smv.ApproveSmvAsync(uncertified.Value.Id)).Code.ShouldBe("SMV_CERTIFICATION_REQUIRED");

        var created = await smv.CreateSmvAsync(Amendment(seed.SmvId, Amended, reference: Reference()));
        created.IsSuccess.ShouldBeTrue(created.Message);
        var amendment = created.Value;
        var seedSmv = (await smv.GetByIdAsync(seed.SmvId)).Value;
        (amendment.Basis, amendment.AmendsSmvId, amendment.AmendmentGround, amendment.RevisionYear)
            .ShouldBe((SmvBasis.Amendment, (Guid?)seed.SmvId, (SmvAmendmentGround?)SmvAmendmentGround.Infrastructure, seedSmv.RevisionYear));
        amendment.AmendsSmvReference.ShouldBe(seedSmv.Reference);
        (await smv.CreateSmvAsync(Amendment(amendment.Id, new DateOnly(2026, 10, 1)))).Code.ShouldBe("SMV_AMENDMENT_OF_AMENDMENT");

        // Its rows: the land's key at 1,200 (replacing 1,000), and a commercial row the amended SMV lacks.
        var row = (await smv.ListSchedulesAsync(seed.SmvId)).Value.Single(x => x.ClassificationId == seed.ClassificationId);
        async Task<SmvScheduleDto> Rate(Guid smvId, Guid classificationId, decimal value, DateOnly effective, bool approve = true)
        {
            var added = await smv.CreateScheduleAsync(smvId, new CreateSmvScheduleRequest(classificationId, row.ActualUseId, row.PropertyTypeId, row.ZoneId,
                row.Unit, value, null, null, effective, row.ImprovementKindId, row.SubClassificationId, row.BarangayId));
            added.IsSuccess.ShouldBeTrue(added.Message);
            if (approve)
            {
                (await smv.ApproveScheduleAsync(added.Value.Id)).IsSuccess.ShouldBeTrue();
            }
            return added.Value;
        }
        await Rate(amendment.Id, seed.ClassificationId, 1_200m, Amended);
        await Rate(amendment.Id, commercial.Id, 5_000m, Amended);
        (await smv.ListSchedulesAsync(amendment.Id)).Value.OrderBy(x => x.MarketValue).Select(x => x.ReplacesMarketValue)
            .ShouldBe([1_000m, null]);

        // A draft amendment prices nothing.
        async Task<decimal> Value(DateOnly asOf)
        {
            var valued = await valuations.ComputeForLandAsync(land.Id, asOf: asOf);
            valued.IsSuccess.ShouldBeTrue(valued.Message);
            return valued.Value.ComputedMarketValue;
        }
        (await Value(new DateOnly(2026, 8, 1))).ShouldBe(500_000m);

        // Approved: from its effectivity its row prices the land; before it, the amended SMV's row.
        (await smv.ApproveSmvAsync(amendment.Id)).IsSuccess.ShouldBeTrue();
        (await Value(new DateOnly(2026, 6, 30))).ShouldBe(500_000m);
        (await Value(new DateOnly(2026, 8, 1))).ShouldBe(600_000m);

        // An adjustment factor of the amended SMV applies to the amended row; the amendment's own factor of the same code wins.
        var factors = services.GetRequiredService<IAdjustmentFactorService>();
        async Task Factor(Guid smvId, decimal percent, DateOnly effective)
        {
            var added = await factors.CreateAsync(new CreateAdjustmentFactorRequest(smvId, "L67-DEMO", "DEMO factor", percent, null, null,
                "DEMO — not an SMV provision", effective, null));
            added.IsSuccess.ShouldBeTrue(added.Message);
            (await factors.ApproveAsync(added.Value.Id)).IsSuccess.ShouldBeTrue();
        }
        await Factor(seed.SmvId, 10m, new DateOnly(2026, 1, 1));
        (await services.GetRequiredService<ILandService>().AddAdjustmentAsync(land.Id, new AddLandAdjustmentRequest("L67-DEMO", null, null)))
            .IsSuccess.ShouldBeTrue();
        (await Value(new DateOnly(2026, 8, 1))).ShouldBe(660_000m);
        await Factor(amendment.Id, 20m, Amended);
        (await Value(new DateOnly(2026, 8, 1))).ShouldBe(720_000m);
        (await Value(new DateOnly(2026, 6, 30))).ShouldBe(550_000m);

        // A proposed amendment is simulated over its family: its draft row wins; a key it lacks comes from the approved members.
        var proposed = await smv.CreateSmvAsync(Amendment(seed.SmvId, new DateOnly(2026, 10, 1), SmvAmendmentGround.Calamity));
        proposed.IsSuccess.ShouldBeTrue(proposed.Message);
        await Rate(proposed.Value.Id, seed.ClassificationId, 800m, new DateOnly(2026, 10, 1), approve: false);
        var mode = new ValuationMode(proposed.Value.Id, ComputeOnly: true);
        var asOf = new DateOnly(2026, 11, 1);
        (await valuations.LandRateAsync(seed.ClassificationId, row.SubClassificationId, row.ActualUseId, property.MunicipalityId, property.BarangayId, asOf, mode))!
            .UnitValue.ShouldBe(800m);
        (await valuations.LandRateAsync(commercial.Id, row.SubClassificationId, row.ActualUseId, property.MunicipalityId, property.BarangayId, asOf, mode))!
            .UnitValue.ShouldBe(5_000m);
        // Stored valuations still use the approved amendment.
        (await valuations.LandRateAsync(seed.ClassificationId, row.SubClassificationId, row.ActualUseId, property.MunicipalityId, property.BarangayId, asOf,
            ValuationMode.Stored))!.UnitValue.ShouldBe(1_200m);

        // Building tables are not amended; a new SMV changes them.
        (await services.GetRequiredService<IBuildingCostTableService>().CreateBuildingCostAsync(new CreateBuildingCostRequest(amendment.Id, Guid.NewGuid(),
            null, null, 1m, "DEMO", Amended, null))).Code.ShouldBe("SMV_AMENDMENT_TABLES_UNSUPPORTED");
    }

    [Fact]
    public async Task Only_an_approved_SMV_is_amended_and_within_its_coverage()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var _ = await db.Database.BeginTransactionAsync();
        var smv = scope.ServiceProvider.GetRequiredService<ISmvService>();
        var town = await db.Municipalities.AsNoTracking().FirstAsync();
        var draft = await smv.CreateSmvAsync(new CreateSmvRequest(null, null, null, new DateOnly(2027, 1, 1), 2027, "DEMO", SmvBasis.Certified,
            CertificationReference: Reference(), MunicipalityIds: [town.Id]));
        draft.IsSuccess.ShouldBeTrue(draft.Message);
        CreateSmvRequest Amendment(IReadOnlyList<Guid>? towns) => new(null, null, null, new DateOnly(2027, 7, 1), 0, "DEMO", SmvBasis.Amendment,
            CertificationReference: Reference(), MunicipalityIds: towns, AmendsSmvId: draft.Value.Id, AmendmentGround: SmvAmendmentGround.CorrectionOfErrors);

        (await smv.CreateSmvAsync(Amendment([town.Id]))).Code.ShouldBe("SMV_NOT_APPROVED");
        (await smv.ApproveSmvAsync(draft.Value.Id)).IsSuccess.ShouldBeTrue();
        // The amended SMV covers one town: the amendment names it, and no other.
        (await smv.CreateSmvAsync(Amendment(null))).Code.ShouldBe("SMV_AMENDMENT_COVERAGE");
        (await smv.CreateSmvAsync(Amendment([Guid.NewGuid()]))).Code.ShouldBe("MUNICIPALITY_NOT_FOUND");
        var ok = await smv.CreateSmvAsync(Amendment([town.Id]));
        ok.IsSuccess.ShouldBeTrue(ok.Message);
        (ok.Value.RevisionYear, ok.Value.Coverage!.Single().MunicipalityId).ShouldBe((2027, town.Id));
    }
}
