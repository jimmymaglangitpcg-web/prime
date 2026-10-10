using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Forms;
using Prime.Application.Features.Offices;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Entities.Offices;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Entities.Registers;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step LP-5 (docs/analysis/province-wide-operation.md §3.6): a form prints the
/// letterhead of the office covering the record's municipality, else the
/// provincial office; a record kept province-wide prints the issuing user's
/// office; treasury forms keep the <c>Lgu:</c> settings. Rolled back.
/// </summary>
public class LetterheadTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Forms_print_the_letterhead_of_the_office_covering_the_record()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<PrimeDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var today = services.GetRequiredService<IClock>().Today;
        var settings = services.GetRequiredService<IOptions<LguOptions>>().Value;
        var offices = services.GetRequiredService<IOfficeContext>();
        var user = services.GetRequiredService<CurrentUserService>();
        var tag = Random.Shared.Next(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);

        var province = new Province { PsgcCode = $"88{tag}", Name = $"DEMO LP5 Province {tag}" };
        var town = new Municipality { Province = province, PsgcCode = $"88{tag[..6]}01", Name = $"DEMO LP5 Town {tag}" };
        var uncovered = new Municipality { Province = province, PsgcCode = $"88{tag[..6]}02", Name = $"DEMO LP5 Uncovered Town {tag}" };
        var plainTown = new Municipality { Province = province, PsgcCode = $"88{tag[..6]}03", Name = $"DEMO LP5 Plain Town {tag}" };
        var office = new Office
        {
            Code = $"T-LP5-{tag}", Name = "DEMO LP5 Municipal Assessor's Office", Kind = OfficeKind.Municipal, LguName = $"DEMO Municipality of LP5 {tag}",
            HeadPosition = "DEMO Municipal Assessor", Address = "DEMO LP5 Town Hall", Contact = "DEMO 0000",
        };
        var plain = new Office { Code = $"T-LP5P-{tag}", Name = "DEMO LP5 Office without LGU name", Kind = OfficeKind.Municipal };
        OfficeJurisdiction Covers(Office o, Municipality m) => new()
        {
            Office = o, Municipality = m, EffectiveDate = new DateOnly(2020, 1, 1), LegalBasis = "DEMO", Status = WorkflowStatus.Approved, ApprovedAt = DateTimeOffset.UtcNow,
        };
        db.OfficeJurisdictions.AddRange(Covers(office, town), Covers(plain, plainTown));
        db.Municipalities.Add(uncovered);
        var clerk = new AppUser { SupabaseUserId = Guid.NewGuid(), DisplayName = "DEMO LP5 Clerk", Email = $"demo-{Guid.NewGuid():N}@example.invalid" };
        db.AppUsers.Add(clerk);
        db.OfficeAssignments.Add(new OfficeAssignment
        {
            AppUserId = clerk.Id, Office = office, EffectiveDate = new DateOnly(2020, 1, 1), LegalBasis = "DEMO", Status = WorkflowStatus.Approved,
            ApprovedAt = DateTimeOffset.UtcNow, Roles = [new OfficeAssignmentRole { RoleId = await db.Roles.Where(r => r.Code == RoleCodes.Appraiser).Select(r => r.Id).SingleAsync() }],
        });
        await db.SaveChangesAsync();
        var provincial = await db.Offices.SingleAsync(o => o.Kind == OfficeKind.Provincial);

        Task<(System.Text.Json.Nodes.JsonObject Lgu, System.Text.Json.Nodes.JsonObject? Office)> Letterhead(FormSubjectType type, Guid id) =>
            FormLetterhead.BuildAsync(db, offices, settings, type, id, today, CancellationToken.None);

        // A TD in the covered town: that office's letterhead, the province's items from settings.
        var td = await TdAsync(db, town);
        var (lgu, json) = await Letterhead(FormSubjectType.TaxDeclaration, td.Id);
        lgu["name"]!.GetValue<string>().ShouldBe(office.LguName);
        lgu["office"]!.GetValue<string>().ShouldBe(office.Name);
        lgu["address"]!.GetValue<string>().ShouldBe("DEMO LP5 Town Hall");
        lgu["contact"]!.GetValue<string>().ShouldBe("DEMO 0000");
        lgu["province"]?.GetValue<string>().ShouldBe(settings.Province);
        json!["code"]!.GetValue<string>().ShouldBe(office.Code);
        json["kind"]!.GetValue<string>().ShouldBe("Municipal");
        json["headPosition"]!.GetValue<string>().ShouldBe("DEMO Municipal Assessor");
        (await Letterhead(FormSubjectType.Faas, td.Id)).Office!["code"]!.GetValue<string>().ShouldBe(office.Code);

        // The rendered form carries it (the TAX_DECLARATION version in force prints lgu.name).
        user.AppUserId = null;
        var preview = await services.GetRequiredService<IFormService>().PreviewAsync("TAX_DECLARATION", td.Id);
        preview.IsSuccess.ShouldBeTrue(preview.Message);
        preview.Value.Html.ShouldContain(office.LguName!);

        // An office without an LGU name prints none — it never borrows Lgu:Name.
        var plainTd = await TdAsync(db, plainTown);
        var (plainLgu, plainJson) = await Letterhead(FormSubjectType.TaxDeclaration, plainTd.Id);
        plainJson!["code"]!.GetValue<string>().ShouldBe(plain.Code);
        plainLgu["name"].ShouldBeNull();
        plainLgu["address"].ShouldBeNull();

        // No office covers the town: the provincial office.
        var orphan = await TdAsync(db, uncovered);
        (await Letterhead(FormSubjectType.TaxDeclaration, orphan.Id)).Office!["code"]!.GetValue<string>().ShouldBe(provincial.Code);

        // A register kept by owner has no municipality: the issuing user's office, else the provincial one.
        // Its own DEMO owner: a fresh (CI) database has no taxpayers unless another test committed one first.
        var owner = new Taxpayer { TaxpayerType = TaxpayerType.Individual, LastName = $"DEMO_LP5_{tag}", FirstName = "DEMO" };
        var orc = new RegisterRun { Kind = RegisterKind.OwnershipRecordCard, Taxpayer = owner, AsOf = today };
        db.RegisterRuns.Add(orc);
        await db.SaveChangesAsync();
        user.AppUserId = clerk.Id;
        (await Letterhead(FormSubjectType.Register, orc.Id)).Office!["code"]!.GetValue<string>().ShouldBe(office.Code);
        user.AppUserId = null;
        (await Letterhead(FormSubjectType.Register, orc.Id)).Office!["code"]!.GetValue<string>().ShouldBe(provincial.Code);

        // An inactive covering office falls back to the province.
        office.Status = RecordStatus.Inactive;
        await db.SaveChangesAsync();
        (await Letterhead(FormSubjectType.TaxDeclaration, td.Id)).Office!["code"]!.GetValue<string>().ShouldBe(provincial.Code);

        // Treasury forms (frozen, CLAUDE.md §0) keep the Lgu: settings and no office.
        var (treasury, treasuryOffice) = await Letterhead(FormSubjectType.TaxBill, Guid.NewGuid());
        treasuryOffice.ShouldBeNull();
        treasury["name"]?.GetValue<string>().ShouldBe(settings.Name);
        treasury["office"]?.GetValue<string>().ShouldBe(settings.Office);
    }

    private static async Task<TaxDeclaration> TdAsync(PrimeDbContext db, Municipality town)
    {
        var tag = Guid.NewGuid().ToString("N")[..10];
        var barangay = new Barangay { Municipality = town, PsgcCode = $"B{tag}", Name = "DEMO LP5 Barangay" };
        var property = new PropertyEntity
        {
            PropertyIdentificationNumber = $"DEMO-LP5-{tag}", ProvinceId = town.ProvinceId, Municipality = town, Barangay = barangay, Status = RecordStatus.Active,
        };
        var rpu = new RealPropertyUnit { Property = property, RpuNumber = $"DEMO-LP5-{tag}", RpuType = RpuType.Land, EffectivityDate = new DateOnly(2026, 1, 1) };
        var td = new TaxDeclaration
        {
            Rpu = rpu, Property = property, TaxDeclarationNumber = $"DEMO-LP5-{tag}", EffectivityDate = new DateOnly(2026, 1, 1), AssessmentYear = 2026,
            Status = WorkflowStatus.PendingReview,
            ClassificationId = await db.Classifications.Select(x => x.Id).FirstAsync(), ActualUseId = await db.ActualUses.Select(x => x.Id).FirstAsync(),
        };
        db.TaxDeclarations.Add(td);
        await db.SaveChangesAsync();
        return td;
    }
}
