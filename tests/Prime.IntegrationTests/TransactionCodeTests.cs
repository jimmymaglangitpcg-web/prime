using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Appraisal;
using Prime.Application.Features.Assessments;
using Prime.Application.Features.Numbering;
using Prime.Application.Features.TaxDeclarations;
using Prime.Application.Features.Transactions;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// docs/analysis/mrpaao-forms-model.md §12 step 4 — the FAAS transaction code
/// (highest rank wins) and the Record of Assessment entry. The codes and ranks
/// below mirror the MRPAAO's list but are DEMO catalogue data.
/// </summary>
public class TransactionCodeTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, CurrentUserService User, AppUser A, AppUser B, BillingFlowTests.Seed Seed)
    {
        public ITaxDeclarationService Tds => Services.GetRequiredService<ITaxDeclarationService>();
        public ITransactionService Tx => Services.GetRequiredService<ITransactionService>();
    }

    private async Task<(Ctx Ctx, IAsyncDisposable Transaction)> BeginAsync(bool post = true)
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        foreach (var retire in new Func<Task>[]
        {
            () => db.TransactionTypes.Where(x => x.Status == WorkflowStatus.Approved)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null)),
            () => db.NumberingSchemes.Where(x => x.Status == WorkflowStatus.Approved)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null)),
            () => db.ApprovalChains.Where(x => x.Status == WorkflowStatus.Approved)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null)),
        })
        {
            await retire();
        }
        var users = Enumerable.Range(0, 2).Select(i => new AppUser
        {
            SupabaseUserId = Guid.NewGuid(), DisplayName = $"DEMO User {(char)('A' + i)}", Email = $"demo-{Guid.NewGuid():N}@example.invalid",
        }).ToList();
        db.AppUsers.AddRange(users);
        await db.SaveChangesAsync();
        var user = scope.ServiceProvider.GetRequiredService<CurrentUserService>();
        user.AppUserId = null;
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, new DateOnly(2026, 1, 1), post: post);
        return (new Ctx(db, scope.ServiceProvider, user, users[0], users[1], seed), new Scoped(transaction, scope));
    }

    private sealed class Scoped(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    /// <summary>An approved DEMO catalogue type; the code is made unique so the shared dev DB's own catalogue never collides.</summary>
    private static async Task<TransactionTypeDto> TypeAsync(Ctx c, string code, int rank, PropertyTransactionKind kind)
    {
        var created = (await c.Tx.CreateTypeAsync(new CreateTransactionTypeRequest(
            "DEMO — not LAM", new DateOnly(2020, 1, 1), null, code, $"DEMO {code}", kind, rank, null, []))).Value;
        (await c.Tx.ApproveTypeAsync(created.Id)).IsSuccess.ShouldBeTrue();
        return created;
    }

    private static CreateTaxDeclarationRequest Td(Ctx c, Guid? transactionId = null, string? code = null) =>
        new(c.Seed.RpuId, $"DEMO-TD-{Guid.NewGuid():N}"[..24], new DateOnly(2026, 7, 1), Taxability.Taxable, c.Seed.ClassificationId,
            c.Seed.TaxDeclaration.ActualUseId, null, 2026, c.Seed.TaxDeclaration.Id, "DEMO", transactionId, null, code);

    [Fact]
    public async Task TdUnderATransfer_TakesItsCode_AndANamedHigherRankWins()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var suffix = Guid.NewGuid().ToString("N")[..4];
        var transfer = await TypeAsync(c, $"TR{suffix}", 7, PropertyTransactionKind.Transfer);
        await TypeAsync(c, $"SD{suffix}", 1, PropertyTransactionKind.Subdivision);
        var t = (await c.Tx.OpenAsync(new OpenTransactionRequest(transfer.Id, c.Seed.PropertyId, new DateOnly(2026, 7, 1), "DEMO"))).Value;

        var plain = (await c.Tds.CreateAsync(Td(c, t.Id))).Value;
        plain.TransactionCode.ShouldBe($"TR{suffix}");
        plain.TransactionRank.ShouldBe(7);

        var both = (await c.Tds.CreateAsync(Td(c, t.Id, $"SD{suffix}"))).Value;
        both.TransactionCode.ShouldBe($"SD{suffix}"); // rank 1 outranks rank 7 (MRPAAO p.167)

        (await c.Tds.CreateAsync(Td(c, code: "NOPE"))).Code.ShouldBe("TRANSACTION_CODE_NOT_IN_FORCE");
    }

    [Fact]
    public async Task PostingAGeneralRevisionAssessment_StampsTheEntry_AndPreparesAGrTd()
    {
        var (c, tx) = await BeginAsync(post: false);
        await using var _ = tx;
        await TypeAsync(c, "GR", 9, PropertyTransactionKind.GeneralRevision);
        var numbering = c.Services.GetRequiredService<INumberingService>();
        c.User.AppUserId = c.A.Id;
        var scheme = (await numbering.CreateAsync(new CreateNumberingSchemeRequest(
            "DEMO — not an LGU format", new DateOnly(2020, 1, 1), null, NumberedDocumentKind.TaxDeclaration, "DEMO TD", "DEMO-TD-{YEAR}-{SEQ:4}", null, true))).Value;
        c.User.AppUserId = c.B.Id;
        (await numbering.ApproveAsync(scheme.Id)).IsSuccess.ShouldBeTrue();
        var job = new GeneralRevisionJob { RevisionYear = 2026, Status = JobExecutionStatus.Completed };
        c.Db.Add(job);
        var assessment = await c.Db.Assessments.SingleAsync(x => x.Id == c.Seed.AssessmentId);
        assessment.Status = WorkflowStatus.Approved;
        assessment.ApprovedAt = DateTimeOffset.UtcNow;
        assessment.RevisionReference = job.Id;
        await c.Db.SaveChangesAsync();

        c.User.AppUserId = c.B.Id;
        var posted = await c.Services.GetRequiredService<IAssessmentService>().PostAsync(c.Seed.AssessmentId);

        posted.IsSuccess.ShouldBeTrue(posted.IsSuccess ? null : posted.Message);
        posted.Value.PostedAt.ShouldNotBeNull();
        posted.Value.PostedBy.ShouldBe(c.B.Id);
        var prepared = (await c.Tds.ListByRpuAsync(c.Seed.RpuId)).Value.Single(t => t.Id != c.Seed.TaxDeclaration.Id);
        prepared.TransactionCode.ShouldBe("GR");
        prepared.TransactionRank.ShouldBe(9);
        var record = (await c.Services.GetRequiredService<IAppraisalRecordService>().GetAsync(c.Seed.AssessmentId)).Value;
        record.RecordEntry.ShouldNotBeNull().PostedBy.ShouldBe("DEMO User B");
    }
}
