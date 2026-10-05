using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Smv;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step L6-2a (docs/analysis/smv-preparation-general-revision.md §4.2): the SMV preparation work file, its proposed SMV,
/// consultations, and the review from submission to publication, with the stage dates written on the SMV and the SMV
/// approved only once published. DEMO data; the revision years are far in the future so they meet no real preparation.
/// Rolled back.
/// </summary>
public class SmvPreparationTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, CurrentUserService User, AppUser A, AppUser B)
    {
        public ISmvPreparationService Preparations => Services.GetRequiredService<ISmvPreparationService>();
        public ISmvService Smvs => Services.GetRequiredService<ISmvService>();
    }

    private async Task<(Ctx Ctx, IAsyncDisposable Transaction)> BeginAsync()
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var a = new AppUser { SupabaseUserId = Guid.NewGuid(), DisplayName = "DEMO Preparer", Email = $"demo-{Guid.NewGuid():N}@example.invalid" };
            var b = new AppUser { SupabaseUserId = Guid.NewGuid(), DisplayName = "DEMO Checker", Email = $"demo-{Guid.NewGuid():N}@example.invalid" };
            db.AppUsers.AddRange(a, b);
            await db.SaveChangesAsync();
            var user = scope.ServiceProvider.GetRequiredService<CurrentUserService>();
            user.AppUserId = a.Id;
            return (new Ctx(db, scope.ServiceProvider, user, a, b), new Scoped(transaction, scope));
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

    private static readonly int Year = 2190 + Random.Shared.Next(0, 9);

    private static async Task<SmvPreparationDto> CreateAsync(Ctx c, int year)
    {
        var created = await c.Preparations.CreateAsync(new(year, "DEMO revision of the SMV", new DateOnly(2026, 1, 2), new DateOnly(2026, 6, 30),
            new DateOnly(2027, 1, 1), null, null));
        created.IsSuccess.ShouldBeTrue(created.Message);
        return created.Value;
    }

    private static async Task<SmvPreparationDto> StepAsync(Ctx c, Guid id, SmvPreparationEventKind kind, DateOnly on, string? reference = null, string? note = null)
    {
        var r = await c.Preparations.RecordEventAsync(id, new(kind, on, reference, note));
        r.IsSuccess.ShouldBeTrue(r.Message);
        return r.Value;
    }

    [Fact]
    public async Task FromProposalToPublication_TheSmvTakesItsStageDates_AndIsApprovedOnlyOncePublished()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var p = await CreateAsync(c, Year);
        (p.Status, p.ProposedSmv.Status, p.ProposedSmv.Reference, p.Editable).ShouldBe((SmvPreparationStatus.Preparing, WorkflowStatus.Draft, "", true));
        var smv = await c.Db.Smvs.AsNoTracking().SingleAsync(x => x.Id == p.ProposedSmv.Id);
        (smv.Basis, smv.RevisionYear).ShouldBe((SmvBasis.Certified, Year));

        await StepAsync(c, p.Id, SmvPreparationEventKind.PublishedForComment, new DateOnly(2026, 3, 1), "DEMO newspaper");
        (await c.Preparations.AddConsultationAsync(p.Id, new(new DateOnly(2026, 3, 15), SmvConsultationMode.Hybrid, "DEMO hall", 40, "DEMO-MIN-1", null)))
            .IsSuccess.ShouldBeTrue();
        // One consultation of the two required, and a submission date other than the planned base valuation date: warnings only.
        var submitted = await StepAsync(c, p.Id, SmvPreparationEventKind.SubmittedToRegionalOffice, new DateOnly(2026, 4, 1), "DEMO-TRANSMITTAL");
        submitted.Status.ShouldBe(SmvPreparationStatus.Submitted);
        submitted.Warnings.Count.ShouldBe(2);
        submitted.BaseValuationDate.ShouldBe(new DateOnly(2026, 4, 1));
        submitted.NextDue!.DueOn.ShouldBe(new DateOnly(2026, 5, 16));

        await StepAsync(c, p.Id, SmvPreparationEventKind.EndorsedByRegionalOffice, new DateOnly(2026, 5, 10));
        (await c.Preparations.RecordEventAsync(p.Id, new(SmvPreparationEventKind.Remanded, new DateOnly(2026, 6, 1), null, null)))
            .Code.ShouldBe("REMAND_REASONS_REQUIRED");
        await StepAsync(c, p.Id, SmvPreparationEventKind.Remanded, new DateOnly(2026, 6, 1), "DEMO-LETTER", "DEMO: values of two sub-classes to be supported");
        (await c.Preparations.AddConsultationAsync(p.Id, new(new DateOnly(2026, 6, 10), SmvConsultationMode.Online, null, null, null, null)))
            .IsSuccess.ShouldBeTrue(); // one more after the remand
        await StepAsync(c, p.Id, SmvPreparationEventKind.Resubmitted, new DateOnly(2026, 6, 20));
        (await c.Preparations.RecordEventAsync(p.Id, new(SmvPreparationEventKind.Certified, new DateOnly(2026, 7, 1), null, null)))
            .Code.ShouldBe("REFERENCE_REQUIRED");
        var certificationReference = $"DEMO-CERT-{Guid.NewGuid():N}"[..30];
        await StepAsync(c, p.Id, SmvPreparationEventKind.Certified, new DateOnly(2026, 7, 1), certificationReference);

        // Certified but not yet published: not entered.
        c.User.AppUserId = c.B.Id;
        (await c.Smvs.ApproveSmvAsync(p.ProposedSmv.Id)).Code.ShouldBe("SMV_NOT_PUBLISHED");
        c.User.AppUserId = c.A.Id;

        var published = await StepAsync(c, p.Id, SmvPreparationEventKind.Published, new DateOnly(2026, 7, 10), "DEMO Official Gazette");
        published.Status.ShouldBe(SmvPreparationStatus.Published);
        published.NextDue.ShouldBe(new Prime.Domain.DomainServices.SmvPreparationDue("Effectivity of the SMV", new DateOnly(2026, 7, 25)));
        var stages = published.ProposedSmv;
        (stages.EffectivityDate, stages.PublishedForCommentOn, stages.ConsultationsHeldOn, stages.SubmittedToBlgfOn, stages.CertifiedOn,
            stages.CertificationReference, stages.PublishedOn)
            .ShouldBe((new DateOnly(2026, 7, 25), (DateOnly?)new DateOnly(2026, 3, 1), (DateOnly?)new DateOnly(2026, 6, 10),
                (DateOnly?)new DateOnly(2026, 6, 20), (DateOnly?)new DateOnly(2026, 7, 1), (string?)certificationReference, (DateOnly?)new DateOnly(2026, 7, 10)));
        published.Events.Count.ShouldBe(7);
        published.Editable.ShouldBeFalse();

        (await c.Smvs.ApproveSmvAsync(p.ProposedSmv.Id)).Code.ShouldBe("CANNOT_APPROVE_OWN_SMV");
        c.User.AppUserId = c.B.Id;
        (await c.Smvs.ApproveSmvAsync(p.ProposedSmv.Id)).Value.Status.ShouldBe(WorkflowStatus.Approved);
        c.User.AppUserId = c.A.Id;
        // Once approved, only the transmittal can still be recorded.
        await StepAsync(c, p.Id, SmvPreparationEventKind.TransmittedToSanggunian, new DateOnly(2026, 7, 20), "DEMO-TRANSMITTAL-SP");
    }

    [Fact]
    public async Task AnOrder_ADate_OrAYear_ThatCannotBe_IsRefused()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var p = await CreateAsync(c, Year);
        (await c.Preparations.CreateAsync(new(Year, "again", null, null, new DateOnly(2027, 1, 1), null, null))).Code.ShouldBe("SMV_PREPARATION_DUPLICATE");
        (await c.Preparations.RecordEventAsync(p.Id, new(SmvPreparationEventKind.Certified, new DateOnly(2026, 3, 1), "X", null))).Code.ShouldBe("SMV_PREPARATION_STAGE");
        (await c.Preparations.RecordEventAsync(p.Id, new(SmvPreparationEventKind.PublishedForComment, DateOnly.FromDateTime(DateTime.Today).AddDays(30), null, null)))
            .Code.ShouldBe("DATE_IN_FUTURE");
        await StepAsync(c, p.Id, SmvPreparationEventKind.PublishedForComment, new DateOnly(2026, 3, 1));
        (await c.Preparations.RecordEventAsync(p.Id, new(SmvPreparationEventKind.SubmittedToRegionalOffice, new DateOnly(2026, 2, 1), null, null)))
            .Code.ShouldBe("SMV_PREPARATION_DATE_ORDER");
        // Before its certification the proposed SMV cannot be approved: it has no certification reference.
        c.User.AppUserId = c.B.Id;
        (await c.Smvs.ApproveSmvAsync(p.ProposedSmv.Id)).Code.ShouldBe("SMV_CERTIFICATION_REQUIRED");
    }

    [Fact]
    public async Task Cancelling_CancelsTheProposedSmv_FreesTheYear_AndCloses()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var p = await CreateAsync(c, Year);
        (await c.Preparations.CancelAsync(p.Id, new(" "))).Code.ShouldBe("VALIDATION_FAILED");
        var cancelled = (await c.Preparations.CancelAsync(p.Id, new("DEMO: started in error"))).Value;
        (cancelled.Status, cancelled.ProposedSmv.Status, cancelled.NextDue).ShouldBe((SmvPreparationStatus.Cancelled, WorkflowStatus.Cancelled, (Prime.Domain.DomainServices.SmvPreparationDue?)null));
        (await c.Preparations.AddConsultationAsync(p.Id, new(new DateOnly(2026, 3, 15), SmvConsultationMode.InPerson, null, null, null, null)))
            .Code.ShouldBe("SMV_PREPARATION_CLOSED");
        (await CreateAsync(c, Year)).Status.ShouldBe(SmvPreparationStatus.Preparing);
    }

    [Fact]
    public async Task AMunicipalOffice_SeesThePreparation_ButCannotChangeIt()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var p = await CreateAsync(c, Year);
        var municipality = await c.Db.Municipalities.Select(x => x.Id).FirstAsync();
        c.Services.GetRequiredService<JurisdictionState>().Restrict([municipality]);
        var seen = (await c.Preparations.GetAsync(p.Id)).Value;
        seen.Editable.ShouldBeFalse();
        (await c.Preparations.ListAsync()).Value.ShouldContain(x => x.Id == p.Id);
        (await c.Preparations.RecordEventAsync(p.Id, new(SmvPreparationEventKind.PublishedForComment, new DateOnly(2026, 3, 1), null, null)))
            .Code.ShouldBe(SmvPreparationService.ForbiddenCode);
        (await c.Preparations.CreateAsync(new(Year + 1, "DEMO", null, null, new DateOnly(2027, 1, 1), null, null))).Code.ShouldBe(SmvPreparationService.ForbiddenCode);
    }
}
