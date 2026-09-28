using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Offices;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Entities.Offices;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step LP-3 (docs/analysis/province-wide-operation.md §3.4, Q6–Q7): delegations
/// of final approval are the province's to give, need a second provincial
/// user, may not overlap, can be renewed and revoked (not retroactively), and
/// are found by office, record type, property kind and date. Rolled back.
/// </summary>
public class ApprovalDelegationTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly ApprovalSubjectType[] Faas = [ApprovalSubjectType.TaxDeclaration, ApprovalSubjectType.Assessment];

    private sealed record Ctx(IServiceProvider Services, PrimeDbContext Db, CurrentUserService User, AppUser Maker, AppUser Checker, AppUser Municipal,
        Office Office, DateOnly Today)
    {
        public IApprovalDelegationService Delegations => Services.GetRequiredService<IApprovalDelegationService>();

        public CreateApprovalDelegationRequest Request(DateOnly from, DateOnly to, ApprovalSubjectType[]? subjects = null, RpuType[]? kinds = null, Guid? renews = null) =>
            new(Office.Id, "DEMO Provincial Assessor", "DEMO Provincial Assessor", $"DEMO Office Order {from:yyyyMMdd}", from, subjects ?? Faas, kinds, from, to, renews, null);

        /// <summary>Entered by the maker, approved by the checker.</summary>
        public async Task<ApprovalDelegationDto> ApprovedAsync(CreateApprovalDelegationRequest request)
        {
            User.AppUserId = Maker.Id;
            var draft = (await Delegations.CreateAsync(request)).Value;
            User.AppUserId = Checker.Id;
            var approved = (await Delegations.ApproveAsync(draft.Id)).Value;
            User.AppUserId = Maker.Id;
            return approved;
        }
    }

    private async Task<(Ctx C, IAsyncDisposable Scope)> BeginAsync()
    {
        var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        var users = new[] { "DEMO Provincial Maker", "DEMO Provincial Checker", "DEMO Municipal Assessor" }.Select(n => new AppUser
        {
            SupabaseUserId = Guid.NewGuid(), DisplayName = n, Email = $"demo-{Guid.NewGuid():N}@example.invalid",
        }).ToList();
        db.AppUsers.AddRange(users);
        var office = new Office { Code = $"T-DLG-{Guid.NewGuid():N}"[..14].ToUpperInvariant(), Name = "DEMO LP3 Municipal Office", Kind = OfficeKind.Municipal };
        db.Offices.Add(office);
        var provincial = await db.Offices.SingleAsync(o => o.Kind == OfficeKind.Provincial);
        var assessor = await db.Roles.SingleAsync(r => r.Code == RoleCodes.Assessor);
        OfficeAssignment Assign(AppUser u, Office o) => new()
        {
            AppUserId = u.Id, Office = o, EffectiveDate = new DateOnly(2020, 1, 1), LegalBasis = "DEMO", Status = WorkflowStatus.Approved, ApprovedAt = DateTimeOffset.UtcNow,
            Roles = [new OfficeAssignmentRole { RoleId = assessor.Id }],
        };
        db.OfficeAssignments.AddRange(Assign(users[0], provincial), Assign(users[1], provincial), Assign(users[2], office));
        await db.SaveChangesAsync();
        var user = scope.ServiceProvider.GetRequiredService<CurrentUserService>();
        user.AppUserId = users[0].Id;
        var today = scope.ServiceProvider.GetRequiredService<IClock>().Today;
        return (new Ctx(scope.ServiceProvider, db, user, users[0], users[1], users[2], office, today), new Disposer(transaction, scope));
    }

    private sealed class Disposer(IAsyncDisposable transaction, IAsyncDisposable scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await scope.DisposeAsync();
        }
    }

    [Fact]
    public async Task OnlyTheProvince_Delegates_AndASecondProvincialUserApproves()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var (from, to) = (c.Today.AddDays(-30), c.Today.AddDays(300));

        c.User.AppUserId = c.Municipal.Id;
        (await c.Delegations.CreateAsync(c.Request(from, to))).Code.ShouldBe("DELEGATION_FORBIDDEN");

        c.User.AppUserId = c.Maker.Id;
        var draft = (await c.Delegations.CreateAsync(c.Request(from, to))).Value;
        (draft.Status, draft.State).ShouldBe((WorkflowStatus.Draft, DelegationState.Draft));
        (await c.Delegations.FindInForceAsync(c.Office.Id, ApprovalSubjectType.TaxDeclaration, null, c.Today)).ShouldBeNull();
        (await c.Delegations.ApproveAsync(draft.Id)).Code.ShouldBe("CANNOT_APPROVE_OWN_DELEGATION");
        c.User.AppUserId = c.Municipal.Id;
        (await c.Delegations.ApproveAsync(draft.Id)).Code.ShouldBe("DELEGATION_FORBIDDEN");
        c.User.AppUserId = c.Checker.Id;
        (await c.Delegations.ApproveAsync(draft.Id)).Value.State.ShouldBe(DelegationState.InForce);

        (await c.Delegations.FindInForceAsync(c.Office.Id, ApprovalSubjectType.TaxDeclaration, RpuType.Building, c.Today)).ShouldNotBeNull().Id.ShouldBe(draft.Id);
        (await c.Delegations.FindInForceAsync(c.Office.Id, ApprovalSubjectType.PropertyTransaction, null, c.Today)).ShouldBeNull();
        (await c.Delegations.FindInForceAsync(c.Office.Id, ApprovalSubjectType.TaxDeclaration, null, to.AddDays(1))).ShouldBeNull();
        (await c.Delegations.FindInForceAsync(c.Office.Id, ApprovalSubjectType.TaxDeclaration, null, from.AddDays(-1))).ShouldBeNull();

        // A provincial office is never delegated to.
        var provincial = await c.Db.Offices.SingleAsync(o => o.Kind == OfficeKind.Provincial);
        (await c.Delegations.CreateAsync(c.Request(from, to) with { OfficeId = provincial.Id })).Code.ShouldBe("OFFICE_NOT_MUNICIPAL");
    }

    [Fact]
    public async Task Delegations_DoNotOverlap_AreRenewed_AndMayBeLimitedToPropertyKinds()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var year = new DateOnly(c.Today.Year, 1, 1);
        var first = await c.ApprovedAsync(c.Request(year, year.AddYears(1).AddDays(-1)));

        (await c.Delegations.CreateAsync(c.Request(year.AddMonths(6), year.AddYears(1).AddMonths(6)))).Code.ShouldBe("DELEGATION_OVERLAP_CONFLICT");
        (await c.Delegations.CreateAsync(c.Request(year.AddYears(1), year.AddYears(2).AddDays(-1), renews: first.Id) with { ValidFrom = year }))
            .Code.ShouldBe("DELEGATION_RENEWAL_INVALID");
        var renewal = await c.ApprovedAsync(c.Request(year.AddYears(1), year.AddYears(2).AddDays(-1), renews: first.Id));
        (renewal.RenewsDelegationId, renewal.State).ShouldBe((first.Id, DelegationState.Scheduled));

        // Transactions on land only, in the same period: a different set of records, so no overlap.
        var land = await c.ApprovedAsync(c.Request(year, year.AddYears(1).AddDays(-1), [ApprovalSubjectType.PropertyTransaction], [RpuType.Land]));
        land.PropertyKinds.ShouldBe([RpuType.Land]);
        (await c.Delegations.FindInForceAsync(c.Office.Id, ApprovalSubjectType.PropertyTransaction, RpuType.Land, c.Today)).ShouldNotBeNull();
        (await c.Delegations.FindInForceAsync(c.Office.Id, ApprovalSubjectType.PropertyTransaction, RpuType.Building, c.Today)).ShouldBeNull();
        (await c.Delegations.FindInForceAsync(c.Office.Id, ApprovalSubjectType.PropertyTransaction, null, c.Today)).ShouldBeNull();
    }

    [Fact]
    public async Task Revocation_TakesEffectFromTodayOrLater_AndDraftsAreRejectedNotRevoked()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var delegation = await c.ApprovedAsync(c.Request(c.Today.AddDays(-10), c.Today.AddDays(100)));

        (await c.Delegations.RevokeAsync(delegation.Id, new(c.Today.AddDays(-1), "DEMO"))).Code.ShouldBe("VALIDATION_FAILED");
        c.User.AppUserId = c.Municipal.Id;
        (await c.Delegations.RevokeAsync(delegation.Id, new(c.Today.AddDays(1), "DEMO"))).Code.ShouldBe("DELEGATION_FORBIDDEN");
        c.User.AppUserId = c.Maker.Id;
        var revoked = (await c.Delegations.RevokeAsync(delegation.Id, new(c.Today.AddDays(1), "DEMO revocation order"))).Value;
        (revoked.RevokedFrom, revoked.RevocationReason, revoked.State).ShouldBe((c.Today.AddDays(1), "DEMO revocation order", DelegationState.InForce));
        (await c.Delegations.FindInForceAsync(c.Office.Id, ApprovalSubjectType.Assessment, null, c.Today)).ShouldNotBeNull();
        (await c.Delegations.FindInForceAsync(c.Office.Id, ApprovalSubjectType.Assessment, null, c.Today.AddDays(1))).ShouldBeNull();
        (await c.Delegations.RevokeAsync(delegation.Id, new(c.Today.AddDays(2), "again"))).Code.ShouldBe("DELEGATION_NOT_REVOCABLE");

        // After the revocation, a new delegation may start the day it takes effect.
        var next = (await c.Delegations.CreateAsync(c.Request(c.Today.AddDays(1), c.Today.AddDays(200)))).Value;
        (await c.Delegations.RejectAsync(next.Id, new(""))).Code.ShouldBe("VALIDATION_FAILED");
        (await c.Delegations.RejectAsync(next.Id, new("DEMO wrong instrument"))).Value.State.ShouldBe(DelegationState.Rejected);
        (await c.Delegations.RejectAsync(delegation.Id, new("DEMO"))).Code.ShouldBe("DELEGATION_NOT_DRAFT");
    }
}
