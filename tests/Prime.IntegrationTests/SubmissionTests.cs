using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Submissions;
using Prime.Application.Features.TaxDeclarations;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Entities.Offices;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step LP-6 (docs/analysis/province-wide-operation.md §3.7): a TD's final
/// approval freezes its printed copy, which the province's list opens; the
/// monthly assessment roll is prepared and submitted by the municipal office
/// and acknowledged or returned by the province. Rolled back.
/// </summary>
public class SubmissionTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(
        IServiceProvider Services, PrimeDbContext Db, CurrentUserService User, DateOnly Today, Municipality Town, Barangay Barangay,
        AppUser Appraiser, AppUser Provincial, AppUser Outsider)
    {
        public ISubmissionService Submissions => Services.GetRequiredService<ISubmissionService>();
    }

    private async Task<(Ctx C, IAsyncDisposable Scope)> BeginAsync()
    {
        var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().Today;
        var tag = Random.Shared.Next(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);

        var province = new Province { PsgcCode = $"87{tag}", Name = $"DEMO LP6 Province {tag}" };
        var town = new Municipality { Province = province, PsgcCode = $"87{tag[..6]}01", Name = $"DEMO LP6 Town {tag}" };
        var otherTown = new Municipality { Province = province, PsgcCode = $"87{tag[..6]}02", Name = $"DEMO LP6 Other Town {tag}" };
        var barangay = new Barangay { Municipality = town, PsgcCode = $"87{tag}1", Name = "DEMO LP6 Barangay A" };
        db.Barangays.AddRange(barangay, new Barangay { Municipality = town, PsgcCode = $"87{tag}2", Name = "DEMO LP6 Barangay B (no entries)" });
        var office = new Office { Code = $"T-LP6-{tag}", Name = "DEMO LP6 Municipal Office", Kind = OfficeKind.Municipal };
        var other = new Office { Code = $"T-LP6O-{tag}", Name = "DEMO LP6 Other Office", Kind = OfficeKind.Municipal };
        OfficeJurisdiction Covers(Office o, Municipality m) => new()
        {
            Office = o, Municipality = m, EffectiveDate = new DateOnly(2020, 1, 1), LegalBasis = "DEMO", Status = WorkflowStatus.Approved, ApprovedAt = DateTimeOffset.UtcNow,
        };
        db.OfficeJurisdictions.AddRange(Covers(office, town), Covers(other, otherTown));
        var provincialOffice = await db.Offices.SingleAsync(o => o.Kind == OfficeKind.Provincial);
        var roles = await db.Roles.ToDictionaryAsync(r => r.Code);
        AppUser User(string name) => new() { SupabaseUserId = Guid.NewGuid(), DisplayName = name, Email = $"demo-{Guid.NewGuid():N}@example.invalid" };
        var appraiser = User("DEMO LP6 Municipal Appraiser");
        var provincial = User("DEMO LP6 Provincial Assessor");
        var outsider = User("DEMO LP6 Other Appraiser");
        db.AppUsers.AddRange(appraiser, provincial, outsider);
        OfficeAssignment Assign(AppUser u, Office o, string role) => new()
        {
            AppUserId = u.Id, Office = o, EffectiveDate = new DateOnly(2020, 1, 1), LegalBasis = "DEMO", Status = WorkflowStatus.Approved,
            ApprovedAt = DateTimeOffset.UtcNow, Roles = [new OfficeAssignmentRole { RoleId = roles[role].Id }],
        };
        db.OfficeAssignments.AddRange(Assign(appraiser, office, RoleCodes.Appraiser), Assign(provincial, provincialOffice, RoleCodes.Assessor),
            Assign(outsider, other, RoleCodes.Appraiser));
        await db.SaveChangesAsync();
        var user = scope.ServiceProvider.GetRequiredService<CurrentUserService>();
        return (new Ctx(scope.ServiceProvider, db, user, today, town, barangay, appraiser, provincial, outsider), new Disposer(transaction, scope));
    }

    private sealed class Disposer(IAsyncDisposable transaction, IAsyncDisposable scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await scope.DisposeAsync();
        }
    }

    private static async Task<TaxDeclaration> TdAsync(Ctx c, WorkflowStatus status, DateTimeOffset? approvedAt = null)
    {
        var tag = Guid.NewGuid().ToString("N")[..10];
        var property = new PropertyEntity
        {
            PropertyIdentificationNumber = $"DEMO-LP6-{tag}", ProvinceId = c.Town.ProvinceId, MunicipalityId = c.Town.Id, BarangayId = c.Barangay.Id,
            Status = RecordStatus.Active,
        };
        var rpu = new RealPropertyUnit { Property = property, RpuNumber = $"DEMO-LP6-{tag}", RpuType = RpuType.Land, EffectivityDate = new DateOnly(2020, 1, 1) };
        var td = new TaxDeclaration
        {
            Rpu = rpu, Property = property, TaxDeclarationNumber = $"DEMO-LP6-{tag}", EffectivityDate = new DateOnly(2020, 1, 1), AssessmentYear = 2020,
            Status = status, ApprovedAt = approvedAt, Taxability = Taxability.Taxable,
            ClassificationId = await c.Db.Classifications.Select(x => x.Id).FirstAsync(), ActualUseId = await c.Db.ActualUses.Select(x => x.Id).FirstAsync(),
        };
        c.Db.TaxDeclarations.Add(td);
        await c.Db.SaveChangesAsync();
        td.CreatedBy = c.Appraiser.Id;
        await c.Db.SaveChangesAsync();
        return td;
    }

    [Fact]
    public async Task Final_approval_freezes_the_printed_TD_and_the_province_lists_it()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var td = await TdAsync(c, WorkflowStatus.PendingReview);

        // The dev database has no TD chain in force today: the two-person check, here by the provincial assessor.
        c.User.AppUserId = c.Provincial.Id;
        var approved = await c.Services.GetRequiredService<ITaxDeclarationService>().ApproveAsync(td.Id);
        approved.IsSuccess.ShouldBeTrue(approved.Message);
        approved.Value.Status.ShouldBe(WorkflowStatus.Approved);
        var issued = await c.Db.IssuedForms.Where(f => f.SubjectId == td.Id).ToListAsync();
        issued.ShouldContain(f => f.FormCode == "TAX_DECLARATION" && f.Status == WorkflowStatus.Posted && f.IssuedBy == c.Provincial.Id);
        issued.ShouldNotContain(f => f.SubjectType == FormSubjectType.Faas); // the TD declares no assessment

        var list = await c.Submissions.ListApprovedAsync(c.Town.Id, c.Today.AddDays(-1), c.Today);
        var row = list.Value.ShouldHaveSingleItem();
        (row.TaxDeclarationId, row.ApprovedBy, row.FaasFormId).ShouldBe((td.Id, c.Provincial.DisplayName, (Guid?)null));
        row.TaxDeclarationFormId.ShouldBe(issued.Single(f => f.FormCode == "TAX_DECLARATION").Id);
        (await c.Submissions.ListApprovedAsync(c.Town.Id, c.Today, c.Today.AddDays(-1))).Code.ShouldBe("VALIDATION_FAILED");
    }

    [Fact]
    public async Task The_monthly_roll_is_submitted_by_the_municipal_office_and_acknowledged_or_returned_by_the_province()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var thisMonth = new DateOnly(c.Today.Year, c.Today.Month, 1);
        var lastMonth = thisMonth.AddMonths(-1);
        // Entered (approved) mid last month — in the month's supplement; an older one is not.
        await TdAsync(c, WorkflowStatus.Approved, new DateTimeOffset(lastMonth.AddDays(14).ToDateTime(new TimeOnly(4, 0)), TimeSpan.Zero));
        await TdAsync(c, WorkflowStatus.Approved, new DateTimeOffset(lastMonth.AddMonths(-3).ToDateTime(new TimeOnly(4, 0)), TimeSpan.Zero));
        var request = new CreateRollSubmissionRequest(c.Town.Id, lastMonth.Year, lastMonth.Month, "DEMO");

        c.User.AppUserId = c.Appraiser.Id;
        (await c.Submissions.SubmitRollAsync(request with { Year = thisMonth.Year, Month = thisMonth.Month })).Code.ShouldBe("ROLL_MONTH_NOT_OVER");
        c.User.AppUserId = c.Provincial.Id;
        (await c.Submissions.SubmitRollAsync(request)).Code.ShouldBe("ROLL_SUBMISSION_FORBIDDEN");
        c.User.AppUserId = c.Outsider.Id;
        (await c.Submissions.SubmitRollAsync(request)).Code.ShouldBe("ROLL_SUBMISSION_FORBIDDEN");

        c.User.AppUserId = c.Appraiser.Id;
        var first = await c.Submissions.SubmitRollAsync(request);
        first.IsSuccess.ShouldBeTrue(first.Message);
        first.Value.Status.ShouldBe(AssessmentRollSubmissionStatus.Submitted);
        first.Value.SubmittedBy.ShouldBe(c.Appraiser.DisplayName);
        var item = first.Value.Items.ShouldHaveSingleItem(); // barangay B had no entries; no exempt entries
        (item.Kind, item.BarangayId, item.EntryCount).ShouldBe((RegisterKind.AssessmentRollTaxable, c.Barangay.Id, 1));
        var form = await c.Db.IssuedForms.SingleAsync(f => f.Id == item.IssuedFormId);
        (form.FormCode, form.SubjectId, form.Status).ShouldBe(("AR_TAXABLE", item.RegisterRunId, WorkflowStatus.Posted));
        (await c.Db.RegisterRuns.CountAsync(r => r.Barangay!.MunicipalityId == c.Town.Id)).ShouldBe(1); // empty runs were not kept
        (await c.Submissions.SubmitRollAsync(request)).Code.ShouldBe("ROLL_SUBMISSION_EXISTS");

        // Only the province reviews; a return needs remarks.
        (await c.Submissions.AcknowledgeRollAsync(first.Value.Id, new(null))).Code.ShouldBe("ROLL_REVIEW_FORBIDDEN");
        c.User.AppUserId = c.Provincial.Id;
        (await c.Submissions.ReturnRollAsync(first.Value.Id, new(" "))).Code.ShouldBe("VALIDATION_FAILED");
        var returned = await c.Submissions.ReturnRollAsync(first.Value.Id, new("DEMO: barangay B missing"));
        (returned.Value.Status, returned.Value.ReviewedBy, returned.Value.ReviewRemarks)
            .ShouldBe((AssessmentRollSubmissionStatus.Returned, c.Provincial.DisplayName, "DEMO: barangay B missing"));

        // A new submission for the same month; the returned one stays.
        c.User.AppUserId = c.Appraiser.Id;
        var second = await c.Submissions.SubmitRollAsync(request);
        second.IsSuccess.ShouldBeTrue(second.Message);
        c.User.AppUserId = c.Provincial.Id;
        (await c.Submissions.AcknowledgeRollAsync(second.Value.Id, new(null))).Value.Status.ShouldBe(AssessmentRollSubmissionStatus.Acknowledged);
        (await c.Submissions.AcknowledgeRollAsync(second.Value.Id, new(null))).Code.ShouldBe("ROLL_SUBMISSION_NOT_PENDING");
        var all = (await c.Submissions.ListRollsAsync(c.Town.Id)).Value;
        all.Select(s => s.Status).OrderBy(s => s).ShouldBe([AssessmentRollSubmissionStatus.Acknowledged, AssessmentRollSubmissionStatus.Returned]);
    }
}
