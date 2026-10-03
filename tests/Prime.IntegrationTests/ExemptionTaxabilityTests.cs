using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.AssessmentLevels;
using Prime.Application.Features.Assessments;
using Prime.Application.Features.Exemptions;
using Prime.Application.Features.Forms;
using Prime.Application.Features.Numbering;
using Prime.Application.Features.Registers;
using Prime.Application.Features.TaxDeclarations;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step L3-1b (docs/analysis/assessment-listing-exemptions.md §4.1): taxability per assessment line from the
/// exemptions approved and in force, the TD's summary, the rolls by line with the legal basis, and the reassessment an
/// exemption decided after the assessment opens. The seeded land: posted assessment MV 500,000, AV 100,000 (20%),
/// effective 2026-01-01, declared by an approved TD. Every type and basis is DEMO data. Rolled back.
/// </summary>
public class ExemptionTaxabilityTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly DateOnly Jan1 = new(2026, 1, 1);

    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, CurrentUserService User, AppUser Maker, AppUser Checker,
        BillingFlowTests.Seed Seed, Guid BarangayId, DateOnly Today)
    {
        public IExemptionService Exemptions => Services.GetRequiredService<IExemptionService>();
        public IAssessmentService Assessments => Services.GetRequiredService<IAssessmentService>();
        public ITaxDeclarationService Tds => Services.GetRequiredService<ITaxDeclarationService>();
    }

    private async Task<(Ctx C, IAsyncDisposable Scope)> BeginAsync(Taxability seedTaxability = Taxability.Taxable)
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        // The test's own numbering schemes start today; set aside the database's approved ones (rolled back with the test).
        await db.NumberingSchemes.Where(x => x.Status == WorkflowStatus.Approved)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null));
        var users = Enumerable.Range(0, 2).Select(i => new AppUser
        {
            SupabaseUserId = Guid.NewGuid(), DisplayName = $"DEMO User {(char)('A' + i)}", Email = $"demo-{Guid.NewGuid():N}@example.invalid",
        }).ToList();
        db.AppUsers.AddRange(users);
        await db.SaveChangesAsync();
        var user = scope.ServiceProvider.GetRequiredService<CurrentUserService>();
        user.AppUserId = null;
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, Jan1, seedTaxability);
        var td = await db.TaxDeclarations.SingleAsync(x => x.Id == seed.TaxDeclaration.Id);
        td.AssessmentId = seed.AssessmentId;
        await db.SaveChangesAsync();
        // A DEMO TD numbering scheme in force, so posting an assessment prepares its replacing TD.
        var numbering = scope.ServiceProvider.GetRequiredService<INumberingService>();
        var scheme = await numbering.CreateAsync(new CreateNumberingSchemeRequest("DEMO — not an LGU format", DateOnly.FromDateTime(DateTime.Today), null,
            NumberedDocumentKind.TaxDeclaration, "DEMO TD", "DEMO-EX-{YEAR}-{SEQ:6}", null, false));
        scheme.IsSuccess.ShouldBeTrue(scheme.IsSuccess ? null : scheme.Message);
        (await numbering.ApproveAsync(scheme.Value.Id)).IsSuccess.ShouldBeTrue();
        var barangayId = await db.Properties.Where(p => p.Id == seed.PropertyId).Select(p => p.BarangayId).SingleAsync();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().Today;
        return (new Ctx(db, scope.ServiceProvider, user, users[0], users[1], seed, barangayId, today), new Scoped(transaction, scope));
    }

    private sealed class Scoped(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    private static async Task<ExemptionTypeDto> TypeAsync(Ctx c, decimal? ceiling = null)
    {
        c.User.AppUserId = c.Maker.Id;
        var created = await c.Exemptions.CreateTypeAsync(new CreateExemptionTypeRequest($"DEMO basis {Guid.NewGuid():N}"[..20], new DateOnly(2020, 1, 1), null,
            $"DEMO-EX-{Guid.NewGuid():N}"[..16], "DEMO exemption", null, ExemptionAppliesTo.All, true, ceiling));
        c.User.AppUserId = c.Checker.Id;
        var approved = await c.Exemptions.ApproveTypeAsync(created.Value.Id);
        approved.IsSuccess.ShouldBeTrue(approved.IsSuccess ? null : approved.Message);
        c.User.AppUserId = null;
        return approved.Value;
    }

    /// <summary>Claimed and proven by the maker, approved by the checker.</summary>
    private static async Task<PropertyExemptionDto> ApprovedClaimAsync(Ctx c, ExemptionTypeDto type, Guid? actualUseId, DateOnly effective)
    {
        c.User.AppUserId = c.Maker.Id;
        var claim = await c.Exemptions.ClaimAsync(new ClaimExemptionRequest(c.Seed.RpuId, type.Id, actualUseId, null, null, c.Today.AddDays(-5), "DEMO ref", null));
        claim.IsSuccess.ShouldBeTrue(claim.IsSuccess ? null : claim.Message);
        (await c.Exemptions.AddEvidenceAsync(claim.Value.Id, new AddExemptionEvidenceRequest("DEMO certificate", "DEMO-1", null, null))).IsSuccess.ShouldBeTrue();
        c.User.AppUserId = c.Checker.Id;
        var approved = await c.Exemptions.ApproveAsync(claim.Value.Id, new ApproveExemptionRequest(effective, null, null));
        approved.IsSuccess.ShouldBeTrue(approved.IsSuccess ? null : approved.Message);
        c.User.AppUserId = null;
        return approved.Value;
    }

    /// <summary>Approves and posts the assessment, then approves the TD posting prepared; returns that TD.</summary>
    private static async Task<TaxDeclarationDto> MakeAndDeclareAsync(Ctx c, Guid assessmentId)
    {
        (await c.Assessments.SubmitForReviewAsync(assessmentId)).IsSuccess.ShouldBeTrue();
        var approved = await c.Assessments.ApproveAsync(assessmentId);
        approved.IsSuccess.ShouldBeTrue(approved.IsSuccess ? null : approved.Message);
        var posted = await c.Assessments.PostAsync(assessmentId);
        posted.IsSuccess.ShouldBeTrue(posted.IsSuccess ? null : posted.Message);
        var td = await c.Db.TaxDeclarations.AsNoTracking().SingleAsync(x => x.AssessmentId == assessmentId && x.Status == WorkflowStatus.Draft);
        (await c.Tds.SubmitForReviewAsync(td.Id)).IsSuccess.ShouldBeTrue();
        var declared = await c.Tds.ApproveAsync(td.Id);
        declared.IsSuccess.ShouldBeTrue(declared.IsSuccess ? null : declared.Message);
        return declared.Value;
    }

    private static async Task<JsonArray> RollRowsAsync(Ctx c, RegisterKind kind)
    {
        var run = await c.Services.GetRequiredService<IRegisterService>().CreateRunAsync(new CreateRegisterRunRequest(kind, c.Today, c.BarangayId, null, null, null, "DEMO"));
        run.IsSuccess.ShouldBeTrue(run.IsSuccess ? null : run.Message);
        var data = await c.Services.GetServices<IFormDataProvider>().Single(p => p.SubjectType == FormSubjectType.Register).BuildAsync(run.Value.Id, default);
        return data!.Data["rows"]!.AsArray();
    }

    [Fact]
    public async Task ExemptPart_MakesAPartlyExemptTd_ListedOnBothRolls()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        // The land's second actual use, a DEMO school portion valued 200,000 (AV 40,000) beside 300,000 (AV 60,000).
        var seedAssessment = await c.Db.Assessments.AsNoTracking().SingleAsync(x => x.Id == c.Seed.AssessmentId);
        var valuation = await c.Db.Valuations.AsNoTracking().Include(v => v.Lines).SingleAsync(v => v.Id == seedAssessment.ValuationId);
        var first = valuation.Lines.Single();
        var school = new ActualUse { Code = $"AU{Guid.NewGuid():N}"[..8], Name = "DEMO_School" };
        c.Db.Add(school);
        await c.Db.SaveChangesAsync();
        var levels = c.Services.GetRequiredService<IAssessmentLevelService>();
        var level = await levels.CreateAsync(new CreateAssessmentLevelRequest($"ORD-{Guid.NewGuid():N}", Jan1, c.Seed.ClassificationId, school.Id,
            (await TestSeed.LandPropertyTypeAsync(c.Db)).Id, 0m, 1_000_000m, 20m, Jan1));
        (await levels.ApproveAsync(level.Value.Id)).IsSuccess.ShouldBeTrue();
        var split = new Valuation
        {
            RpuId = valuation.RpuId, PropertyId = valuation.PropertyId, SourceType = valuation.SourceType, SourceId = valuation.SourceId,
            SmvId = valuation.SmvId, ValuationMethod = valuation.ValuationMethod, ComputedMarketValue = 500_000m, EffectiveDate = Jan1, ComputedAt = DateTimeOffset.UtcNow,
            Lines =
            [
                new ValuationLine { Sequence = 1, Source = first.Source, ClassificationId = first.ClassificationId, ActualUseId = first.ActualUseId, MarketValue = 300_000m },
                new ValuationLine { Sequence = 2, Source = first.Source, ClassificationId = first.ClassificationId, ActualUseId = school.Id, MarketValue = 200_000m },
            ],
        };
        c.Db.Valuations.Add(split);
        await c.Db.SaveChangesAsync();

        var type = await TypeAsync(c);
        // An unproven claim does not exempt (LGC §206).
        c.User.AppUserId = c.Maker.Id;
        (await c.Exemptions.ClaimAsync(new ClaimExemptionRequest(c.Seed.RpuId, type.Id, null, null, null, null, null, null))).IsSuccess.ShouldBeTrue();
        c.User.AppUserId = null;
        var unproven = await c.Assessments.PreviewAsync(new CreateAssessmentRequest(split.Id, 2026, Jan1, c.Seed.AssessmentId, null, "DEMO"));
        unproven.Value.Lines.ShouldAllBe(l => l.Taxability == Taxability.Taxable);

        // The school portion's exemption, approved: the posted assessment (one taxable line) needs no reassessment.
        var exemption = await ApprovedClaimAsync(c, type, school.Id, Jan1);
        (exemption.ReassessmentId, exemption.ReassessmentNote).ShouldBe((null, "No reassessment is needed: the assessment in force is already taxable or exempt as the exemptions in force on 2026-01-01 make it."));

        var created = await c.Assessments.CreateAsync(new CreateAssessmentRequest(split.Id, 2026, Jan1, c.Seed.AssessmentId, null, "DEMO"));
        created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);
        created.Value.Lines.Select(l => (l.MarketValue, l.AssessedValue, l.Taxability, l.PropertyExemptionId, l.ExemptionLegalBasis)).ShouldBe(
        [
            (300_000m, 60_000m, Taxability.Taxable, null, null),
            (200_000m, 40_000m, Taxability.Exempt, exemption.Id, type.LegalBasis),
        ]);
        var td = await MakeAndDeclareAsync(c, created.Value.Id);
        td.Taxability.ShouldBe(Taxability.PartlyExempt);

        // Each roll lists its part (Q4); the exempt one with the exemption's legal basis.
        var taxable = (await RollRowsAsync(c, RegisterKind.AssessmentRollTaxable)).Single()!;
        (taxable["tdNumber"]!.GetValue<string>(), taxable["assessedValue"]!.GetValue<decimal>(), taxable["legalBasis"]).ShouldBe((td.TaxDeclarationNumber, 60_000m, null));
        taxable["lam"]!["partlyExempt"]!.GetValue<bool>().ShouldBeTrue();
        var exempt = (await RollRowsAsync(c, RegisterKind.AssessmentRollExempt)).Single()!;
        (exempt["tdNumber"]!.GetValue<string>(), exempt["assessedValue"]!.GetValue<decimal>(), exempt["legalBasis"]!.GetValue<string>())
            .ShouldBe((td.TaxDeclarationNumber, 40_000m, type.LegalBasis));

        // The TD form's rows say which line is exempt.
        var tdData = await c.Services.GetServices<IFormDataProvider>().Single(p => p.SubjectType == FormSubjectType.TaxDeclaration).BuildAsync(td.Id, default);
        var rows = tdData!.Data["assessment"]!["lines"]!.AsArray();
        rows.Select(r => r!["taxability"]!.GetValue<string>()).ShouldBe(["Taxable", "Exempt"]);
        rows[1]!["legalBasis"]!.GetValue<string>().ShouldBe(type.LegalBasis);
    }

    [Fact]
    public async Task ProofAfterTheAssessment_OpensAReassessment_ThatMovesTheUnitToTheExemptRoll_AndEndingItMovesItBack()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var type = await TypeAsync(c);
        var oldTd = c.Seed.TaxDeclaration.Id;

        var exemption = await ApprovedClaimAsync(c, type, null, new DateOnly(2026, 7, 1));
        exemption.ReassessmentId.ShouldNotBeNull();
        exemption.ReassessmentNote!.ShouldStartWith("A draft reassessment effective 2026-07-01 was opened");
        var draft = await c.Db.Assessments.AsNoTracking().Include(a => a.Lines).SingleAsync(a => a.Id == exemption.ReassessmentId);
        (draft.Status, draft.PreviousAssessmentId, draft.MarketValue, draft.AssessedValue, draft.EffectiveDate)
            .ShouldBe((WorkflowStatus.Draft, (Guid?)c.Seed.AssessmentId, 500_000m, 100_000m, new DateOnly(2026, 7, 1)));
        draft.Lines.Single().Taxability.ShouldBe(Taxability.Exempt);
        // Posted records never change.
        (await c.Db.AssessmentLines.AsNoTracking().SingleAsync(l => l.AssessmentId == c.Seed.AssessmentId)).Taxability.ShouldBe(Taxability.Taxable);

        var exemptTd = await MakeAndDeclareAsync(c, draft.Id);
        (exemptTd.Taxability, exemptTd.PreviousTaxDeclarationId).ShouldBe((Taxability.Exempt, (Guid?)oldTd));
        (await c.Db.TaxDeclarations.AsNoTracking().SingleAsync(x => x.Id == oldTd)).Status.ShouldBe(WorkflowStatus.Cancelled);
        (await RollRowsAsync(c, RegisterKind.AssessmentRollTaxable)).ShouldBeEmpty();
        (await RollRowsAsync(c, RegisterKind.AssessmentRollExempt)).Single()!["legalBasis"]!.GetValue<string>().ShouldBe(type.LegalBasis);

        // Ended (e.g. sold to a taxable person): a reassessment makes it taxable again from the end.
        c.User.AppUserId = c.Checker.Id;
        var ended = await c.Exemptions.EndAsync(exemption.Id, new EndExemptionRequest(new DateOnly(2026, 9, 1), "DEMO sold to a taxable person"));
        ended.IsSuccess.ShouldBeTrue(ended.IsSuccess ? null : ended.Message);
        c.User.AppUserId = null;
        var retax = await c.Db.Assessments.AsNoTracking().Include(a => a.Lines).SingleAsync(a => a.Id == ended.Value.ReassessmentId);
        (retax.EffectiveDate, retax.Lines.Single().Taxability, retax.PreviousAssessmentId).ShouldBe((new DateOnly(2026, 9, 1), Taxability.Taxable, (Guid?)draft.Id));
        (await MakeAndDeclareAsync(c, retax.Id)).Taxability.ShouldBe(Taxability.Taxable);
    }

    [Fact]
    public async Task ALineOverTheCeiling_StaysTaxable_WithTheReason()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var type = await TypeAsync(c, ceiling: 50_000m);

        var exemption = await ApprovedClaimAsync(c, type, null, Jan1);
        exemption.ReassessmentId.ShouldBeNull(); // AV 100,000 is over the ceiling: nothing to reassess

        var valuationId = await c.Db.Assessments.Where(x => x.Id == c.Seed.AssessmentId).Select(x => x.ValuationId).SingleAsync();
        var preview = await c.Assessments.PreviewAsync(new CreateAssessmentRequest(valuationId, 2026, Jan1, c.Seed.AssessmentId, null, "DEMO"));
        var line = preview.Value.Lines.Single();
        (line.Taxability, line.PropertyExemptionId).ShouldBe((Taxability.Taxable, (Guid?)exemption.Id));
        line.TaxabilityNote.ShouldBe($"Taxable: the assessed value 100,000.00 is over the 50,000.00 ceiling of exemption {type.Code}.");
    }

    [Fact]
    public async Task AUnitDeclaredExemptBeforeExemptionRecords_StaysExempt_WithANote()
    {
        var (c, scope) = await BeginAsync(Taxability.Exempt);
        await using var _ = scope;
        var valuationId = await c.Db.Assessments.Where(x => x.Id == c.Seed.AssessmentId).Select(x => x.ValuationId).SingleAsync();

        var preview = await c.Assessments.PreviewAsync(new CreateAssessmentRequest(valuationId, 2026, Jan1, c.Seed.AssessmentId, null, "DEMO"));

        var line = preview.Value.Lines.Single();
        (line.Taxability, line.PropertyExemptionId).ShouldBe((Taxability.Exempt, (Guid?)null));
        line.TaxabilityNote.ShouldBe($"Exempt as declared on TD {c.Seed.TaxDeclaration.TaxDeclarationNumber}, before exemption records were kept; record the exemption.");
    }
}
