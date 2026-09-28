using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.ContentPacks;
using Prime.Application.Features.Forms;
using Prime.Domain.Entities.Forms;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step C3 of the LGU content pack (docs/analysis/lgu-content-pack.md §3.3):
/// transaction types, numbering schemes, approval chains and forms enter as
/// Draft versions a second user must approve. Everything is rolled back.
/// </summary>
public class ContentPackVersionedTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(IServiceProvider Services, PrimeDbContext Db, CurrentUserService User, AppUser Importer, AppUser Checker)
    {
        public IContentPackService Packs => Services.GetRequiredService<IContentPackService>();
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Prime.slnx")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Repository root (Prime.slnx) not found.");
    }

    private async Task<(Ctx C, IAsyncDisposable Scope)> BeginAsync(string root)
    {
        var host = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["ContentPacks:RootPath"] = root })));
        var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        await db.Provinces.Where(x => x.PinIndexNumber == "998").ExecuteUpdateAsync(s => s.SetProperty(x => x.PinIndexNumber, (string?)null));
        var users = Enumerable.Range(0, 2).Select(i => new AppUser
        {
            SupabaseUserId = Guid.NewGuid(), DisplayName = i == 0 ? "DEMO Importer" : "DEMO Checker", Email = $"demo-{Guid.NewGuid():N}@example.invalid",
        }).ToList();
        db.AppUsers.AddRange(users);
        await db.SaveChangesAsync();
        var user = scope.ServiceProvider.GetRequiredService<CurrentUserService>();
        user.AppUserId = users[0].Id;
        return (new Ctx(scope.ServiceProvider, db, user, users[0], users[1]), new Disposer(transaction, scope, host));
    }

    private sealed class Disposer(IAsyncDisposable transaction, IAsyncDisposable scope, IAsyncDisposable host) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await scope.DisposeAsync();
            await host.DisposeAsync();
        }
    }

    private static string TempPack(string pack, IReadOnlyDictionary<string, string> files)
    {
        var root = Path.Combine(Path.GetTempPath(), $"prime-cp-{Guid.NewGuid():N}");
        foreach (var (path, content) in files)
        {
            var full = Path.Combine(root, pack, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }
        return root;
    }

    private static IEnumerable<string> Codes(ContentPackPreviewDto p) => p.Issues.Concat(p.Files.SelectMany(f => f.Issues)).Select(i => i.Code);

    [Fact]
    public async Task DemoCatalogues_BecomeDrafts_ThatOnlyASecondUserCanApprove()
    {
        var (c, scope) = await BeginAsync(Path.Combine(RepoRoot(), "samples"));
        await using var _ = scope;

        var preview = (await c.Packs.PreviewAsync("content-demo")).Value;
        var record = (await c.Packs.ImportAsync("content-demo", new(preview.Fingerprint))).Value.Import.ShouldNotBeNull();
        var items = (await c.Packs.ListImportItemsAsync(record.Id, new() { PageSize = 100 })).Value.Items;
        items.Where(i => i.EntityType is "TransactionType" or "NumberingScheme" or "ApprovalChain" or "FormDefinition").Select(i => i.EntityType)
            .ShouldBe(["TransactionType", "NumberingScheme", "ApprovalChain", "FormDefinition"], ignoreOrder: true);

        var formId = items.Single(i => i.EntityType == "FormDefinition").EntityId;
        var form = await c.Db.FormDefinitions.AsNoTracking().SingleAsync(x => x.Id == formId);
        (form.Code, form.Version, form.Status, form.Authority, form.CreatedBy).ShouldBe(("DEMO_CP_NOTICE", 1, WorkflowStatus.Draft, FormAuthority.Other, c.Importer.Id));
        form.TemplateBody.ShouldContain("DEMO — not an official form");
        var type = await c.Db.TransactionTypes.AsNoTracking().Include(x => x.Requirements).SingleAsync(x => x.Code == "DEMO-CP-TR");
        (type.Status, type.Kind, type.Requirements.Count).ShouldBe((WorkflowStatus.Draft, PropertyTransactionKind.Transfer, 2));
        type.Requirements.Single(r => r.Code == "DEMO-ID").IsMandatory.ShouldBeFalse();

        var forms = c.Services.GetRequiredService<IFormService>();
        (await forms.ApproveDefinitionAsync(formId)).Code.ShouldBe("CANNOT_APPROVE_OWN_FORM_DEFINITION");
        c.User.AppUserId = c.Checker.Id;
        (await forms.ApproveDefinitionAsync(formId)).Value.Status.ShouldBe(WorkflowStatus.Approved);

        // Same content again: the approved form and the pending drafts match, so nothing new.
        c.User.AppUserId = c.Importer.Id;
        var again = (await c.Packs.PreviewAsync("content-demo")).Value;
        again.Files.Where(f => ContentFileKinds.Versioned.Contains(f.Kind)).ShouldAllBe(f => f.New == 0 && f.Unchanged == 1);
        (await c.Packs.ImportAsync("content-demo", new(again.Fingerprint))).Value.Applied.ShouldBeFalse();
    }

    [Fact]
    public async Task Catalogues_RefuseTreasuryKinds_BuiltInAuthorities_Offices_AndUnapprovableDates()
    {
        var root = TempPack("versioned", new Dictionary<string, string>
        {
            ["manifest.json"] = """
                { "schemaVersion": 1, "pack": "versioned", "version": "V1", "files": [
                  { "kind": "numbering-schemes", "path": "n.json", "source": "DEMO" },
                  { "kind": "approval-chains", "path": "a.json", "source": "DEMO" },
                  { "kind": "transaction-types", "path": "t.json", "source": "DEMO" },
                  { "kind": "forms", "path": "f.json", "source": "DEMO" } ] }
                """,
            ["n.json"] = """
                [ { "appliesTo": "TaxBill", "name": "DEMO", "pattern": "B-{SEQ}", "legalBasis": "DEMO", "effectiveDate": "2099-01-01" },
                  { "appliesTo": "Nope", "name": "DEMO", "pattern": "X-{SEQ}", "legalBasis": "DEMO", "effectiveDate": "2099-01-01" },
                  { "appliesTo": "SwornStatement", "name": "DEMO", "pattern": "S-{SEQ}", "legalBasis": "DEMO", "effectiveDate": "01/01/2099" },
                  { "appliesTo": "Faas", "name": "DEMO", "pattern": "no sequence here", "legalBasis": "DEMO", "effectiveDate": "2099-01-01" } ]
                """,
            ["a.json"] = """[ { "subjectType": "Assessment", "name": "DEMO", "office": "DEMO Municipal Office", "legalBasis": "DEMO", "effectiveDate": "2099-01-01", "steps": [ { "stepCode": "DEMO_A", "label": "DEMO" } ] } ]""",
            ["t.json"] = """[ { "code": "DEMO-T", "name": "DEMO", "kind": "Transfer", "colour": "blue", "legalBasis": "DEMO", "effectiveDate": "2099-01-01" } ]""",
            ["f.json"] = """
                [ { "code": "DEMO_CP_BUILTIN", "title": "DEMO", "subjectType": "NoticeOfAssessment", "authority": "Mrpaao", "template": "ok.liquid", "legalBasis": "DEMO", "effectiveDate": "2099-01-01" },
                  { "code": "DEMO_CP_RECEIPT", "title": "DEMO", "subjectType": "Payment", "authority": "Other", "template": "ok.liquid", "legalBasis": "DEMO", "effectiveDate": "2099-01-01" },
                  { "code": "DEMO_CP_ESCAPE", "title": "DEMO", "subjectType": "NoticeOfAssessment", "authority": "Other", "template": "../outside.liquid", "legalBasis": "DEMO", "effectiveDate": "2099-01-01" },
                  { "code": "DEMO_CP_MISSING", "title": "DEMO", "subjectType": "NoticeOfAssessment", "authority": "Other", "template": "missing.liquid", "legalBasis": "DEMO", "effectiveDate": "2099-01-01" },
                  { "code": "DEMO_CP_BROKEN", "title": "DEMO", "subjectType": "NoticeOfAssessment", "authority": "Other", "template": "broken.liquid", "legalBasis": "DEMO", "effectiveDate": "2099-01-01" },
                  { "code": "DEMO_CP_LATE", "title": "DEMO changed", "subjectType": "NoticeOfAssessment", "authority": "Other", "template": "ok.liquid", "legalBasis": "DEMO", "effectiveDate": "2098-01-01" } ]
                """,
            ["ok.liquid"] = "<p>DEMO</p>",
            ["broken.liquid"] = "{% if %}<p>DEMO</p>",
        });
        var (c, scope) = await BeginAsync(root);
        await using var _ = scope;
        // An approved version of DEMO_CP_LATE already starts later than the pack's.
        c.Db.FormDefinitions.Add(new FormDefinition
        {
            Code = "DEMO_CP_LATE", Version = 1, Title = "DEMO", SubjectType = FormSubjectType.NoticeOfAssessment, Authority = FormAuthority.Other,
            LegalBasis = "DEMO", TemplateBody = "<p>DEMO</p>", EffectiveDate = new DateOnly(2098, 6, 1), Status = WorkflowStatus.Approved, ApprovedAt = DateTimeOffset.UtcNow,
        });
        await c.Db.SaveChangesAsync();

        var preview = (await c.Packs.PreviewAsync("versioned")).Value;
        var codes = Codes(preview).ToList();

        preview.CanImport.ShouldBeFalse();
        codes.Count(x => x == "TREASURY_FROZEN").ShouldBe(2);   // TaxBill numbering, Payment form
        codes.ShouldContain("VALUE_INVALID");                  // appliesTo "Nope"
        codes.ShouldContain("DATE_INVALID");                   // 01/01/2099
        codes.ShouldContain("VALIDATION_FAILED");              // pattern without {SEQ}; broken Liquid
        codes.ShouldContain("OFFICE_NOT_YET_SUPPORTED");
        codes.ShouldContain("JSON_INVALID");                   // unknown member "colour"
        codes.ShouldContain("AUTHORITY_BUILT_IN");
        codes.ShouldContain("TEMPLATE_PATH");
        codes.ShouldContain("TEMPLATE_UNREADABLE");
        codes.ShouldContain("EFFECTIVE_DATE_CONFLICT");
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task ChangingATemplateAfterThePreview_ChangesTheFingerprint()
    {
        var root = TempPack("template", new Dictionary<string, string>
        {
            ["manifest.json"] = """{ "schemaVersion": 1, "pack": "template", "version": "V1", "files": [ { "kind": "forms", "path": "f.json", "source": "DEMO" } ] }""",
            ["f.json"] = """[ { "code": "DEMO_CP_TPL", "title": "DEMO", "subjectType": "NoticeOfAssessment", "authority": "Other", "template": "t.liquid", "legalBasis": "DEMO", "effectiveDate": "2099-01-01" } ]""",
            ["t.liquid"] = "<p>DEMO one</p>",
        });
        var (c, scope) = await BeginAsync(root);
        await using var _ = scope;

        var preview = (await c.Packs.PreviewAsync("template")).Value;
        preview.CanImport.ShouldBeTrue();
        File.WriteAllText(Path.Combine(root, "template", "t.liquid"), "<p>DEMO two</p>");

        (await c.Packs.ImportAsync("template", new(preview.Fingerprint))).Code.ShouldBe("CONTENT_PACK_CONFLICT");
        (await c.Db.FormDefinitions.AnyAsync(x => x.Code == "DEMO_CP_TPL")).ShouldBeFalse();
        Directory.Delete(root, recursive: true);
    }
}
