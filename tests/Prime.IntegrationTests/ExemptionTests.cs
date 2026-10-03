using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Exemptions;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step L3-1a (docs/analysis/assessment-listing-exemptions.md §4.1): exemption types as approved configuration, and
/// claims on a unit with a proof deadline (LGC §206), evidence, a decision by a second user, and an end. Every type
/// and legal basis here is DEMO data. Rolled back.
/// </summary>
public class ExemptionTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, CurrentUserService User, AppUser Maker, AppUser Checker,
        BillingFlowTests.Seed Seed)
    {
        public IExemptionService Exemptions => Services.GetRequiredService<IExemptionService>();
    }

    private async Task<(Ctx C, IAsyncDisposable Scope)> BeginAsync()
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        var users = Enumerable.Range(0, 2).Select(i => new AppUser
        {
            SupabaseUserId = Guid.NewGuid(), DisplayName = $"DEMO User {(char)('A' + i)}", Email = $"demo-{Guid.NewGuid():N}@example.invalid",
        }).ToList();
        db.AppUsers.AddRange(users);
        await db.SaveChangesAsync();
        var user = scope.ServiceProvider.GetRequiredService<CurrentUserService>();
        user.AppUserId = null;
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, new DateOnly(2026, 1, 1));
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

    /// <summary>A DEMO type created by the maker and approved by the checker.</summary>
    private static async Task<ExemptionTypeDto> ApprovedTypeAsync(Ctx c, ExemptionAppliesTo appliesTo = ExemptionAppliesTo.All, bool requiresProof = true)
    {
        c.User.AppUserId = c.Maker.Id;
        var created = await c.Exemptions.CreateTypeAsync(new CreateExemptionTypeRequest("DEMO — cites no law", new DateOnly(2020, 1, 1), null,
            $"DEMO-EX-{Guid.NewGuid():N}"[..16], "DEMO cooperative", null, appliesTo, requiresProof, null));
        created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);
        (await c.Exemptions.ApproveTypeAsync(created.Value.Id)).Code.ShouldBe("CANNOT_APPROVE_OWN_EXEMPTION_TYPE");
        c.User.AppUserId = c.Checker.Id;
        var approved = await c.Exemptions.ApproveTypeAsync(created.Value.Id);
        approved.IsSuccess.ShouldBeTrue(approved.IsSuccess ? null : approved.Message);
        return approved.Value;
    }

    [Fact]
    public async Task Claim_IsTaxableUntilProven_ThenDecidedByASecondUser_AndEnded()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var type = await ApprovedTypeAsync(c);
        var claimedOn = c.Services.GetRequiredService<IClock>().Today.AddDays(-40);

        c.User.AppUserId = c.Maker.Id;
        var claim = await c.Exemptions.ClaimAsync(new ClaimExemptionRequest(c.Seed.RpuId, type.Id, null, null, null, claimedOn, "DEMO CDA reg. 001", null));
        claim.IsSuccess.ShouldBeTrue(claim.IsSuccess ? null : claim.Message);
        (claim.Value.Status, claim.Value.ProofDueDate, claim.Value.ProofOverdue).ShouldBe((ExemptionStatus.Claimed, claimedOn.AddDays(30), true));
        (await c.Exemptions.ListOpenAsync()).Value.First(x => x.Id == claim.Value.Id).ProofOverdue.ShouldBeTrue();
        (await c.Exemptions.ClaimAsync(new ClaimExemptionRequest(c.Seed.RpuId, type.Id, null, null, null, claimedOn, null, null)))
            .Code.ShouldBe("EXEMPTION_ALREADY_CLAIMED");

        // No proof: cannot be approved (LGC §206 — listed as taxable until proven).
        c.User.AppUserId = c.Checker.Id;
        (await c.Exemptions.ApproveAsync(claim.Value.Id, new ApproveExemptionRequest(claimedOn, null, null))).Code.ShouldBe("EXEMPTION_NOT_READY");

        // Late proof is accepted and noted.
        c.User.AppUserId = c.Maker.Id;
        var filed = await c.Exemptions.AddEvidenceAsync(claim.Value.Id, new AddExemptionEvidenceRequest("DEMO certificate of registration", "DEMO-001", claimedOn, null));
        filed.IsSuccess.ShouldBeTrue(filed.IsSuccess ? null : filed.Message);
        (filed.Value.Status, filed.Value.ProofLate, filed.Value.ProofOverdue, filed.Value.Evidence.Count).ShouldBe((ExemptionStatus.ProofFiled, true, false, 1));

        // Whoever recorded the claim cannot decide it.
        (await c.Exemptions.ApproveAsync(claim.Value.Id, new ApproveExemptionRequest(claimedOn, null, null))).Code.ShouldBe("CANNOT_DECIDE_OWN_EXEMPTION_CLAIM");
        c.User.AppUserId = c.Checker.Id;
        var approved = await c.Exemptions.ApproveAsync(claim.Value.Id, new ApproveExemptionRequest(claimedOn, null, "DEMO verified"));
        approved.IsSuccess.ShouldBeTrue(approved.IsSuccess ? null : approved.Message);
        (approved.Value.Status, approved.Value.EffectiveDate, approved.Value.DecidedByName).ShouldBe((ExemptionStatus.Approved, claimedOn, c.Checker.DisplayName));
        (await c.Exemptions.AddEvidenceAsync(claim.Value.Id, new AddExemptionEvidenceRequest("late", null, null, null))).Code.ShouldBe("EXEMPTION_NOT_OPEN");

        (await c.Exemptions.EndAsync(claim.Value.Id, new EndExemptionRequest(claimedOn.AddDays(-1), "DEMO"))).Code.ShouldBe("VALIDATION_FAILED");
        var ended = await c.Exemptions.EndAsync(claim.Value.Id, new EndExemptionRequest(claimedOn.AddDays(10), "DEMO: sold to a taxable person"));
        (ended.Value.Status, ended.Value.EndedOn).ShouldBe((ExemptionStatus.Ended, claimedOn.AddDays(10)));
        (await c.Exemptions.ListByPropertyAsync(c.Seed.PropertyId)).Value.ShouldHaveSingleItem().Status.ShouldBe(ExemptionStatus.Ended);
    }

    [Fact]
    public async Task Claim_NeedsATypeInForce_ThatAppliesToTheUnit_AndCanBeRejected()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var buildingsOnly = await ApprovedTypeAsync(c, ExemptionAppliesTo.Building);
        c.User.AppUserId = c.Maker.Id;
        (await c.Exemptions.ClaimAsync(new ClaimExemptionRequest(c.Seed.RpuId, buildingsOnly.Id, null, null, null, null, null, null)))
            .Code.ShouldBe("EXEMPTION_TYPE_NOT_APPLICABLE");
        (await c.Exemptions.ClaimAsync(new ClaimExemptionRequest(c.Seed.RpuId, buildingsOnly.Id, null, null, null, new DateOnly(2019, 1, 1), null, null)))
            .Code.ShouldBe("EXEMPTION_TYPE_NOT_IN_FORCE");

        // A type that needs no documentary proof goes straight to the decision; a portion is named by its actual use.
        var noProof = await ApprovedTypeAsync(c, requiresProof: false);
        c.User.AppUserId = c.Maker.Id;
        var claim = await c.Exemptions.ClaimAsync(new ClaimExemptionRequest(c.Seed.RpuId, noProof.Id, c.Seed.TaxDeclaration.ActualUseId,
            "DEMO part not leased", null, null, null, null));
        claim.IsSuccess.ShouldBeTrue(claim.IsSuccess ? null : claim.Message);
        (claim.Value.Status, claim.Value.ActualUseId).ShouldBe((ExemptionStatus.ProofFiled, (Guid?)c.Seed.TaxDeclaration.ActualUseId));

        c.User.AppUserId = c.Checker.Id;
        (await c.Exemptions.RejectAsync(claim.Value.Id, " ")).Code.ShouldBe("VALIDATION_FAILED");
        var rejected = await c.Exemptions.RejectAsync(claim.Value.Id, "DEMO: the part is leased to a taxable person");
        rejected.Value.Status.ShouldBe(ExemptionStatus.Rejected);
        (await c.Exemptions.EndAsync(claim.Value.Id, new EndExemptionRequest(new DateOnly(2026, 1, 1), "x"))).Code.ShouldBe("EXEMPTION_NOT_APPROVED");
        (await c.Exemptions.ListOpenAsync()).Value.ShouldNotContain(x => x.Id == claim.Value.Id);
    }
}
