using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Properties;
using Prime.Application.Features.RealPropertyUnits;
using Prime.Application.Features.TaxDeclarations;
using Prime.Application.Features.Taxpayers;
using Prime.Domain.Common;
using Prime.Domain.Entities.Forms;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Prime.WebApi.Authentication;
using Prime.WebApi.Concurrency;
using Prime.WebApi.Contracts;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests.Persistence;

/// <summary>
/// Concurrency (docs/analysis/production-hardening.md §4.4, H2): row versions on workflow and editable records,
/// If-Match on writes, 409 CONCURRENCY_CONFLICT, and parallel transitions of which exactly one succeeds. These tests
/// commit through real HTTP requests (two requests cannot share a rolled-back transaction); their rows hang off the
/// fixed TEST_ reference set of <see cref="PropertyRegistrationFlowTests"/> and are identified by the run's id.
/// </summary>
public class ConcurrencyTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Tables whose records go through a workflow status or are edited in place (§4.4); each must carry a row version.</summary>
    private static readonly string[] Versioned =
    [
        "Assessments", "AssessmentLevels", "Valuations", "TaxDeclarations", "TaxDeclarationCancellationRequests",
        "PropertyExemptions", "PropertyTransactions", "Smvs", "SmvPreparations", "ApprovalDelegations", "Offices",
        "ApprovalChains", "NumberingSchemes", "FormDefinitions", "GeneralRevisionProgrammes", "NoticesOfAssessment",
        "SwornStatements", "Taxpayers", "Property", "Parcels", "Lands", "Buildings", "MachineryUnits", "RealPropertyUnit",
        "SignUpRequests", "RolePermissionChanges",
    ];

    [Fact]
    public void EveryWorkflowAndEditableTable_HasAnXminRowVersion()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var versioned = db.Model.GetEntityTypes()
            .Where(t => typeof(IVersioned).IsAssignableFrom(t.ClrType))
            .ToDictionary(t => t.GetTableName()!, t => t.FindProperty(nameof(IVersioned.RowVersion))!);

        foreach (var table in Versioned)
        {
            versioned.ShouldContainKey(table);
        }
        foreach (var (table, property) in versioned)
        {
            property.IsConcurrencyToken.ShouldBeTrue(table);
            property.GetColumnName().ShouldBe("xmin", table);
        }
    }

    [Theory]
    [InlineData("42", true, 42u)]
    [InlineData("\"42\"", true, 42u)]
    [InlineData("W/\"42\"", true, 42u)]
    [InlineData("-1", false, 0u)]
    [InlineData("\"abc\"", false, 0u)]
    [InlineData("4294967296", false, 0u)]
    public void IfMatch_AcceptsABareOrQuotedVersion(string header, bool valid, uint expected)
    {
        IfMatchFilter.TryParse(header, out var version).ShouldBe(valid);
        version.ShouldBe(expected);
    }

    [Fact]
    public async Task ParallelApprovals_OfOneTaxDeclaration_ExactlyOneSucceeds()
    {
        var td = await PendingTaxDeclarationAsync();
        var checker = Client("checker");

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => checker.PostAsync($"/api/tax-declarations/{td.Id}/approve", null)));

        responses.Count(r => r.IsSuccessStatusCode).ShouldBe(1, string.Join(", ", responses.Select(r => (int)r.StatusCode)));
        responses.Where(r => !r.IsSuccessStatusCode).ShouldAllBe(r => r.StatusCode == HttpStatusCode.Conflict || r.StatusCode == HttpStatusCode.BadRequest);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        (await db.TaxDeclarations.AsNoTracking().SingleAsync(x => x.Id == td.Id)).Status.ShouldBe(WorkflowStatus.Approved);
        (await db.AuditLogs.CountAsync(a => a.TableName == "TaxDeclarations" && a.RecordId == td.Id && a.Action == AuditAction.Approve)).ShouldBe(1);
    }

    [Fact]
    public async Task Approval_WithAStaleIfMatch_IsRefusedAndChangesNothing()
    {
        var td = await PendingTaxDeclarationAsync();
        td.RowVersion.ShouldNotBe(0u);
        var checker = Client("checker");

        var stale = await SendAsync(checker, HttpMethod.Post, $"/api/tax-declarations/{td.Id}/approve", null, unchecked(td.RowVersion - 1));
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        // The §63 shape in camelCase, as the frontend reads it (the middleware once wrote PascalCase).
        (await stale.Content.ReadAsStringAsync()).ShouldContain("\"code\":\"CONCURRENCY_CONFLICT\"");
        var unchanged = await checker.GetFromJsonAsync<TaxDeclarationDto>($"/api/tax-declarations/{td.Id}", JsonOptions);
        unchanged!.Status.ShouldBe(WorkflowStatus.PendingReview);
        unchanged.RowVersion.ShouldBe(td.RowVersion);

        var malformed = await SendAsync(checker, HttpMethod.Post, $"/api/tax-declarations/{td.Id}/approve", null, ifMatch: null, rawIfMatch: "\"v1\"");
        malformed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await malformed.Content.ReadFromJsonAsync<ApiError>(JsonOptions))!.Code.ShouldBe("INVALID_IF_MATCH");

        var current = await SendAsync(checker, HttpMethod.Post, $"/api/tax-declarations/{td.Id}/approve", null, td.RowVersion);
        current.StatusCode.ShouldBe(HttpStatusCode.OK, await current.Content.ReadAsStringAsync());
        var approved = (await current.Content.ReadFromJsonAsync<TaxDeclarationDto>(JsonOptions))!;
        approved.Status.ShouldBe(WorkflowStatus.Approved);
        approved.RowVersion.ShouldNotBe(td.RowVersion);

        // A screen still showing it pending is told the record changed (and reloads), before any rule is checked.
        var again = await SendAsync(checker, HttpMethod.Post, $"/api/tax-declarations/{td.Id}/approve", null, td.RowVersion);
        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await again.Content.ReadFromJsonAsync<ApiError>(JsonOptions))!.Code.ShouldBe("CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task TwoEditorsOfOneTaxpayer_TheSecondIsRefused_NotSilentlyOverwriting()
    {
        var client = Client(null);
        var testId = Guid.NewGuid().ToString("N")[..8];
        var created = await client.PostAsJsonAsync("/api/taxpayers", new CreateTaxpayerRequest(TaxpayerType.Individual, "TEST_Concurrency", "Editor",
            null, null, null, $"TIN-H2-{testId}", null, null, null, null, null, null));
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var loaded = (await created.Content.ReadFromJsonAsync<TaxpayerDto>(JsonOptions))!;

        // Both editors opened the record at the same version.
        var first = await SendAsync(client, HttpMethod.Put, $"/api/taxpayers/{loaded.Id}/details",
            new UpdateTaxpayerDetailsRequest(null, "TEST first editor's address", null, null, null, "TEST H2 first"), loaded.RowVersion);
        first.StatusCode.ShouldBe(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        var second = await SendAsync(client, HttpMethod.Put, $"/api/taxpayers/{loaded.Id}/details",
            new UpdateTaxpayerDetailsRequest(null, "TEST second editor's address", null, null, null, "TEST H2 second"), loaded.RowVersion);
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var now = await client.GetFromJsonAsync<TaxpayerDto>($"/api/taxpayers/{loaded.Id}", JsonOptions);
        now!.Address.ShouldBe("TEST first editor's address");
    }

    /// <summary>
    /// One allocator issues every number (TD, NOA, FAAS, PIN, transaction; numbering schemes, §114): concurrent
    /// issuers in one scope each get a different number, with no gap.
    /// </summary>
    [Fact]
    public async Task ParallelNumberAllocation_GivesDistinctConsecutiveNumbers()
    {
        Guid schemeId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
            // A fixed Draft scheme (never in force) reused by every run; numbering schemes are history and stay.
            var scheme = await db.NumberingSchemes.FirstOrDefaultAsync(x => x.Name == "TEST_H2_Allocator");
            if (scheme is null)
            {
                scheme = new NumberingScheme
                {
                    Name = "TEST_H2_Allocator", AppliesTo = NumberedDocumentKind.TaxDeclaration, Pattern = "TEST-{seq}",
                    LegalBasis = "TEST fixture", EffectiveDate = new DateOnly(2099, 12, 31),
                };
                db.NumberingSchemes.Add(scheme);
                await db.SaveChangesAsync();
            }
            schemeId = scheme.Id;
        }
        var scopeKey = $"TEST-{Guid.NewGuid():N}";

        var numbers = await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var next = await scope.ServiceProvider.GetRequiredService<INumberSequenceAllocator>().NextAsync(schemeId, scopeKey);
            await transaction.CommitAsync();
            return next;
        }));

        numbers.Order().ShouldBe(Enumerable.Range(1, 12).Select(i => (long)i));
    }

    private HttpClient Client(string? actAs)
    {
        var client = factory.CreateClient();
        if (actAs is not null)
        {
            client.DefaultRequestHeaders.Add(DevelopmentAuthenticationHandler.ActAsHeader, actAs);
        }
        return client;
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, object? body, uint? ifMatch,
        string? rawIfMatch = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = body is null ? null : JsonContent.Create(body, options: JsonOptions) };
        if ((rawIfMatch ?? (ifMatch is { } v ? $"\"{v}\"" : null)) is { } header)
        {
            request.Headers.TryAddWithoutValidation("If-Match", header);
        }
        return client.SendAsync(request);
    }

    /// <summary>A land TD prepared by the usual development user and submitted for review, committed.</summary>
    private async Task<TaxDeclarationDto> PendingTaxDeclarationAsync()
    {
        var client = Client(null);
        var testId = Guid.NewGuid().ToString("N")[..8];
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var (province, municipality, barangay, classification, actualUse, _) = await PropertyRegistrationFlowTests.TestReferenceAsync(db);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var property = await PostAsync<PropertyDto>(client, "/api/properties", new CreatePropertyRequest(
            $"TEST-H2-{testId}", province.Id, municipality.Id, barangay.Id, null, null, null, null, null, null, null, null));
        var rpu = await PostAsync<RpuDto>(client, "/api/rpus", new CreateRpuRequest(property.Id, $"TEST-H2-RPU-{testId}", RpuType.Land, today, null));
        var td = await PostAsync<TaxDeclarationDto>(client, "/api/tax-declarations", new CreateTaxDeclarationRequest(
            RpuId: rpu.Id, TaxDeclarationNumber: $"TEST-H2-TD-{testId}", EffectivityDate: today, Taxability: Taxability.Taxable,
            ClassificationId: classification.Id, ActualUseId: actualUse.Id, SubClassificationId: null, AssessmentYear: today.Year,
            PreviousTaxDeclarationId: null, Remarks: null));
        var submitted = await client.PostAsync($"/api/tax-declarations/{td.Id}/submit-for-review", null);
        submitted.StatusCode.ShouldBe(HttpStatusCode.OK, await submitted.Content.ReadAsStringAsync());
        return (await submitted.Content.ReadFromJsonAsync<TaxDeclarationDto>(JsonOptions))!;
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string path, object body)
    {
        var response = await client.PostAsJsonAsync(path, body, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }
}
