using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Forms;
using Prime.Application.Features.Smv;
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
/// Step L6-2c (docs/analysis/smv-preparation-general-revision.md §4.2): sub-class criteria (Form 1), location and crop
/// descriptions on SMV rows, and the SMV's forms 1, 5, 9–12 in their provisional layouts. DEMO data. Rolled back.
/// </summary>
public class SmvFormsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, CurrentUserService User, AppUser B, Guid SmvId, Classification Res, Classification Agri,
        SubClassification R1, SubClassification R2, ActualUse Rice, PropertyType Land)
    {
        public ISmvSubClassCriteriaService Criteria => Services.GetRequiredService<ISmvSubClassCriteriaService>();
        public ISmvService Smvs => Services.GetRequiredService<ISmvService>();
    }

    private async Task<(Ctx Ctx, IAsyncDisposable Transaction)> BeginAsync()
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            // The provisional layouts, whatever versions (the LAM's) the dev database has approved.
            await TestSeed.UseReferenceFormsAsync(db);
            var a = new AppUser { SupabaseUserId = Guid.NewGuid(), DisplayName = "DEMO A", Email = $"demo-{Guid.NewGuid():N}@example.invalid" };
            var b = new AppUser { SupabaseUserId = Guid.NewGuid(), DisplayName = "DEMO B", Email = $"demo-{Guid.NewGuid():N}@example.invalid" };
            var res = new Classification { Code = $"CL{Guid.NewGuid():N}"[..8], Name = "DEMO_Residential_F" };
            var agri = new Classification { Code = $"CL{Guid.NewGuid():N}"[..8], Name = "DEMO_Agricultural_F" };
            var r1 = new SubClassification { Code = $"S{Guid.NewGuid():N}"[..8], Name = "DEMO R-1F" };
            var r2 = new SubClassification { Code = $"S{Guid.NewGuid():N}"[..8], Name = "DEMO R-2F" };
            var rice = new ActualUse { Code = $"AU{Guid.NewGuid():N}"[..8], Name = "DEMO Riceland" };
            db.AddRange(a, b, res, agri, r1, r2, rice);
            await db.SaveChangesAsync();
            var land = await TestSeed.LandPropertyTypeAsync(db);
            var user = scope.ServiceProvider.GetRequiredService<CurrentUserService>();
            user.AppUserId = a.Id;
            var smv = (await scope.ServiceProvider.GetRequiredService<ISmvPreparationService>().CreateAsync(new(2170 + Random.Shared.Next(0, 9),
                "DEMO preparation for SmvFormsTests", null, null, new DateOnly(2027, 1, 1), null, null))).Value.ProposedSmv.Id;
            return (new Ctx(db, scope.ServiceProvider, user, b, smv, res, agri, r1, r2, rice, land), new Scoped(transaction, scope));
        }
        catch
        {
            await transaction.DisposeAsync();
            scope.Dispose();
            throw;
        }
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
    public async Task Criteria_AreTheDraftSmvs_ReplacedAsAWhole_AndFixedOnceApproved()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        (await c.Criteria.SetAsync(c.SmvId, new([new(c.Res.Id, c.R1.Id, 1, "DEMO: along the national road"), new(c.Res.Id, c.R1.Id, 2, "again")])))
            .Code.ShouldBe("SUB_CLASS_CRITERIA_REPEATED");
        (await c.Criteria.SetAsync(c.SmvId, new([new(c.Res.Id, c.R1.Id, 1, " ")]))).Code.ShouldBe("VALIDATION_FAILED");
        var set = await c.Criteria.SetAsync(c.SmvId, new([
            new(c.Res.Id, c.R2.Id, 2, "DEMO: interior lots, barangay roads"), new(c.Res.Id, c.R1.Id, 1, "DEMO: along the national road")]));
        set.Value.Select(x => (x.SubClassificationName, x.Sequence)).ShouldBe([("DEMO R-1F", 1), ("DEMO R-2F", 2)]);
        (await c.Criteria.SetAsync(c.SmvId, new([new(c.Res.Id, c.R1.Id, 1, "DEMO: replaced")]))).Value.Single().Criteria.ShouldBe("DEMO: replaced");

        await c.Db.Smvs.Where(x => x.Id == c.SmvId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Approved)
            .SetProperty(x => x.CertificationReference, $"DEMO-{Guid.NewGuid():N}"[..20]));
        (await c.Criteria.SetAsync(c.SmvId, new([]))).Code.ShouldBe("SMV_NOT_DRAFT");
    }

    [Fact]
    public async Task TheSmvForms_ListItsCriteria_LandByUnit_AndItsBuildingTables()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        await c.Criteria.SetAsync(c.SmvId, new([new(c.Res.Id, c.R1.Id, 1, "DEMO: along the national road")]));
        var street = await c.Smvs.CreateScheduleAsync(c.SmvId, new(c.Res.Id, null, c.Land.Id, null, "per sqm", 4_500m, null, null, new DateOnly(2027, 1, 1),
            SubClassificationId: c.R1.Id, LocationDescription: "DEMO Rizal Street, both sides"));
        street.IsSuccess.ShouldBeTrue(street.Message);
        street.Value.LocationDescription.ShouldBe("DEMO Rizal Street, both sides");
        (await c.Smvs.CreateScheduleAsync(c.SmvId, new(c.Agri.Id, c.Rice.Id, c.Land.Id, null, "per hectare", 350_000m, null, null, new DateOnly(2027, 1, 1),
            CropDescription: "DEMO irrigated, first class"))).IsSuccess.ShouldBeTrue();

        var provider = c.Services.GetServices<IFormDataProvider>().Single(p => p.SubjectType == FormSubjectType.Smv);
        var data = (await provider.BuildAsync(c.SmvId, CancellationToken.None)).ShouldNotBeNull().Data;
        data["land"]!.AsArray().Single()!["location"]!.GetValue<string>().ShouldBe("DEMO Rizal Street, both sides");
        data["agricultural"]!.AsArray().Single()!["crop"]!.GetValue<string>().ShouldBe("DEMO irrigated, first class");
        data["smv"]!["proposed"]!.GetValue<bool>().ShouldBeTrue();

        var forms = c.Services.GetRequiredService<IFormService>();
        foreach (var (code, text) in new[]
        {
            ("SMV_FORM_1", "DEMO: along the national road"), ("SMV_FORM_5", "4,500.00"), ("SMV_FORM_9", "350,000.00"),
            ("SMV_FORM_10", "No construction cost"), ("SMV_FORM_11", "No depreciation table"), ("SMV_FORM_12", "No extra item cost"),
        })
        {
            var preview = await forms.PreviewAsync(code, c.SmvId);
            preview.IsSuccess.ShouldBeTrue($"{code}: {preview.Message}");
            preview.Value.Html.ShouldContain(text);
            preview.Value.Html.ShouldContain("PROPOSED");
            preview.Value.IssueBlocker.ShouldNotBeNull();
        }
        // While the SMV is being prepared its forms are previewed only; once submitted, the copy as submitted is issued.
        (await forms.IssueAsync(new IssueFormRequest("SMV_FORM_5", c.SmvId))).Code.ShouldBe("FORM_SUBJECT_NOT_ISSUABLE");
        var preparationId = await c.Db.SmvPreparations.Where(x => x.ProposedSmvId == c.SmvId).Select(x => x.Id).SingleAsync();
        (await c.Services.GetRequiredService<ISmvPreparationService>().RecordEventAsync(preparationId,
            new(SmvPreparationEventKind.SubmittedToRegionalOffice, new DateOnly(2026, 9, 1), null, null))).IsSuccess.ShouldBeTrue();
        var issued = await forms.IssueAsync(new IssueFormRequest("SMV_FORM_5", c.SmvId));
        issued.IsSuccess.ShouldBeTrue(issued.Message);
    }
}
