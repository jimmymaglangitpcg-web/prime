using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Numbering;
using Prime.Application.Features.Smv;
using Prime.Application.Features.TaxDeclarations;
using Prime.Domain.DomainServices;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step L2-1 (docs/analysis/identification-numbering.md §4.1; exit criterion 1): the LAM's TD number (general-revision
/// year, municipal and barangay index, an assessment count restarting per barangay) and its NOA number (the indices
/// and the TD's count). Every index number and pattern is DEMO data.
/// </summary>
public class LamNumberingTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, Guid MunicipalityId, Guid[] BarangayIds, Guid ClassificationId, Guid UseId,
        Guid PropertyA, Guid PropertyB, DateOnly Today)
    {
        public INumberingService Numbering => Services.GetRequiredService<INumberingService>();
        public ITaxDeclarationService Tds => Services.GetRequiredService<ITaxDeclarationService>();
    }

    /// <summary>A DEMO province (index chosen free), town 07, barangays 0101 and 0102, and a DEMO SMV of revision year 2025 covering the town.</summary>
    private async Task<(Ctx Ctx, IAsyncDisposable Transaction)> BeginAsync()
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        // The test's own numbering schemes start today; set aside the database's approved ones (rolled back with the test).
        await db.NumberingSchemes.Where(x => x.Status == WorkflowStatus.Approved)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null));
        var used = await db.Provinces.Where(x => x.PinIndexNumber != null).Select(x => x.PinIndexNumber!).ToListAsync();
        var provinceIndex = Enumerable.Range(900, 99).Select(i => i.ToString()).First(i => !used.Contains(i));
        var province = new Province { PsgcCode = $"P{Guid.NewGuid():N}"[..10], Name = "DEMO Province", PinIndexNumber = provinceIndex };
        var town = new Municipality { Province = province, PsgcCode = $"M{Guid.NewGuid():N}"[..10], Name = "DEMO Town", PinIndexNumber = "07" };
        var a = new Barangay { Municipality = town, PsgcCode = $"B{Guid.NewGuid():N}"[..10], Name = "DEMO A", PinIndexNumber = "0101" };
        var b = new Barangay { Municipality = town, PsgcCode = $"B{Guid.NewGuid():N}"[..10], Name = "DEMO B", PinIndexNumber = "0102" };
        var classification = new Classification { Code = $"CL{Guid.NewGuid():N}"[..8], Name = "DEMO_Residential" };
        var use = new ActualUse { Code = $"AU{Guid.NewGuid():N}"[..8], Name = "DEMO_Residential Use" };
        var propertyA = new PropertyEntity { PropertyIdentificationNumber = $"PIN-{Guid.NewGuid():N}", Province = province, Municipality = town, Barangay = a };
        var propertyB = new PropertyEntity { PropertyIdentificationNumber = $"PIN-{Guid.NewGuid():N}", Province = province, Municipality = town, Barangay = b };
        db.AddRange(province, town, a, b, classification, use, propertyA, propertyB);
        await db.SaveChangesAsync();
        var smv = scope.ServiceProvider.GetRequiredService<ISmvService>();
        var created = await smv.CreateSmvAsync(new CreateSmvRequest($"DEMO-L21-{Guid.NewGuid():N}"[..24], new DateOnly(2024, 9, 1), new DateOnly(2024, 10, 1),
            new DateOnly(2025, 1, 1), 2025, "DEMO SMV for LamNumberingTests", MunicipalityIds: [town.Id]));
        created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);
        (await smv.ApproveSmvAsync(created.Value.Id)).IsSuccess.ShouldBeTrue();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().Today;
        return (new Ctx(db, scope.ServiceProvider, town.Id, [a.Id, b.Id], classification.Id, use.Id, propertyA.Id, propertyB.Id, today),
            new Scoped(transaction, scope));
    }

    private sealed class Scoped(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    private static async Task SchemeAsync(Ctx c, NumberedDocumentKind kind, string pattern, DateOnly effective)
    {
        var scheme = await c.Numbering.CreateAsync(new CreateNumberingSchemeRequest("DEMO — not an LGU format", effective, null, kind, $"DEMO {kind}", pattern, null, false));
        scheme.IsSuccess.ShouldBeTrue(scheme.IsSuccess ? null : scheme.Message);
        var approved = await c.Numbering.ApproveAsync(scheme.Value.Id);
        approved.IsSuccess.ShouldBeTrue(approved.IsSuccess ? null : approved.Message);
    }

    private static async Task<TaxDeclarationDto> TdAsync(Ctx c, Guid propertyId)
    {
        var rpu = new RealPropertyUnit { PropertyId = propertyId, RpuNumber = $"RPU-{Guid.NewGuid():N}", RpuType = RpuType.Land, EffectivityDate = new DateOnly(2025, 6, 1) };
        c.Db.Add(rpu);
        await c.Db.SaveChangesAsync();
        var td = await c.Tds.CreateAsync(new CreateTaxDeclarationRequest(rpu.Id, null, new DateOnly(2025, 6, 1), Taxability.Taxable, c.ClassificationId, c.UseId,
            null, 2025, null, "DEMO"));
        td.IsSuccess.ShouldBeTrue(td.IsSuccess ? null : td.Message);
        return td.Value;
    }

    [Fact]
    public async Task TdNumbers_StartWithTheRevisionYear_AndCountPerBarangay_TheNoaRepeatingTheCount()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        await SchemeAsync(c, NumberedDocumentKind.TaxDeclaration, "{GRYEAR}{MUNIDX}{BRGYIDX}{SEQ:5}", c.Today);

        var first = await TdAsync(c, c.PropertyA);
        var second = await TdAsync(c, c.PropertyA);
        var other = await TdAsync(c, c.PropertyB);

        (first.TaxDeclarationNumber, first.AssessmentCount).ShouldBe(("2025070101" + "00001", 1L));
        (second.TaxDeclarationNumber, second.AssessmentCount).ShouldBe(("2025070101" + "00002", 2L));
        (other.TaxDeclarationNumber, other.AssessmentCount).ShouldBe(("2025070102" + "00001", 1L)); // restarts for each barangay

        // The NOA number: the indices and the TD's count; an older TD without a count keeps the earlier scheme's numbering.
        await SchemeAsync(c, NumberedDocumentKind.NoticeOfAssessment, "DEMO-NOA-{SEQ:5}", c.Today);
        await SchemeAsync(c, NumberedDocumentKind.NoticeOfAssessment, "{MUNIDX}{BRGYIDX}{TDCOUNT:5}", c.Today.AddDays(1));
        var context = await NumberContexts.ForPropertyAsync(c.Db, c.PropertyA, 2025, CancellationToken.None, new DateOnly(2025, 6, 1));
        (await c.Numbering.GenerateIfConfiguredAsync(NumberedDocumentKind.NoticeOfAssessment, context with { TdCount = second.AssessmentCount }, c.Today.AddDays(1)))
            .Value.ShouldBe("070101" + "00002");
        (await c.Numbering.GenerateIfConfiguredAsync(NumberedDocumentKind.NoticeOfAssessment, context, c.Today.AddDays(1))).Value.ShouldBe("DEMO-NOA-00001");
    }

    [Fact]
    public async Task ARevisionYearIsTheSmvCoveringTheProperty_AndADerivedPatternWithoutAFallbackIsRefused()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        // The DEMO SMV covers only this town: elsewhere its revision year does not apply.
        (await NumberContexts.RevisionYearAsync(c.Db, new DateOnly(2025, 6, 1), CancellationToken.None, c.MunicipalityId)).ShouldBe(2025);
        var elsewhere = new Municipality { ProvinceId = (await c.Db.Municipalities.SingleAsync(x => x.Id == c.MunicipalityId)).ProvinceId,
            PsgcCode = $"M{Guid.NewGuid():N}"[..10], Name = "DEMO Elsewhere" };
        c.Db.Add(elsewhere);
        await c.Db.SaveChangesAsync();
        (await NumberContexts.RevisionYearAsync(c.Db, new DateOnly(2025, 6, 1), CancellationToken.None, elsewhere.Id)).ShouldNotBe(2025);

        await SchemeAsync(c, NumberedDocumentKind.NoticeOfAssessment, "{MUNIDX}{BRGYIDX}{TDCOUNT:5}", c.Today);
        var context = await NumberContexts.ForPropertyAsync(c.Db, c.PropertyA, 2025, CancellationToken.None, new DateOnly(2025, 6, 1));
        (await c.Numbering.GenerateIfConfiguredAsync(NumberedDocumentKind.NoticeOfAssessment, context, c.Today)).Code.ShouldBe("NUMBER_TD_COUNT_MISSING");
    }

    [Fact]
    public void Patterns_TakeTheNewTokens_AndADerivedPatternHasNoSequence()
    {
        NumberPattern.Validate("{GRYEAR}{MUNIDX}{BRGYIDX}{SEQ:5}").ShouldBeNull();
        NumberPattern.Validate("{MUNIDX}{BRGYIDX}{TDCOUNT:5}").ShouldBeNull();
        NumberPattern.Validate("{TDCOUNT}{SEQ}").ShouldNotBeNull();
        NumberPattern.Validate("{TDCOUNT}{TDCOUNT}").ShouldNotBeNull();
        NumberPattern.Validate("{MUNIDX}{BRGYIDX}").ShouldNotBeNull(); // neither a sequence nor a TD count
    }
}
