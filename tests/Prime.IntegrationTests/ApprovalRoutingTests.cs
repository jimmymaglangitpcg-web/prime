using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Approvals;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Entities.Offices;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Entities.Workflow;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step LP-4 (docs/analysis/province-wide-operation.md §3.4): a record's
/// approval follows its office's chain (else the provincial default); each
/// step's signer office and role are enforced; the final provincial step goes
/// to the preparing office's Assessor while a delegation is in force, and the
/// signature records it. Rolled back.
/// </summary>
public class ApprovalRoutingTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(
        IServiceProvider Services, PrimeDbContext Db, CurrentUserService User, DateOnly Today,
        Office Office, Office OtherOffice, Office Provincial, Municipality Town,
        AppUser Creator, AppUser MunAssessor, AppUser MunAssessor2, AppUser ProvAssessor, AppUser ProvClerk, AppUser Outsider)
    {
        public IApprovalChainService Chains => Services.GetRequiredService<IApprovalChainService>();

        /// <summary>Signs the next step of <paramref name="td"/> as <paramref name="user"/>; saves on success.</summary>
        public async Task<(string? Code, ApprovalStepOutcome? Outcome)> SignAsync(TaxDeclaration td, AppUser user)
        {
            User.AppUserId = user.Id;
            var result = await Chains.SignNextStepAsync(ApprovalSubjectType.TaxDeclaration, td.Id, td.CreatedBy, Today, null);
            if (result.IsFailure)
            {
                return (result.Code, null);
            }
            await Db.SaveChangesAsync();
            return (null, result.Value);
        }
    }

    private async Task<(Ctx C, IAsyncDisposable Scope)> BeginAsync()
    {
        var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().Today;
        var tag = Random.Shared.Next(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);

        var province = new Province { PsgcCode = $"89{tag}", Name = $"DEMO LP4 Province {tag}" };
        var town = new Municipality { Province = province, PsgcCode = $"89{tag[..6]}01", Name = $"DEMO LP4 Town {tag}" };
        var otherTown = new Municipality { Province = province, PsgcCode = $"89{tag[..6]}02", Name = $"DEMO LP4 Other Town {tag}" };
        var office = new Office { Code = $"T-LP4-{tag}", Name = "DEMO LP4 Municipal Office", Kind = OfficeKind.Municipal, HeadPosition = "DEMO Municipal Assessor" };
        var other = new Office { Code = $"T-LP4O-{tag}", Name = "DEMO LP4 Other Office", Kind = OfficeKind.Municipal };
        db.OfficeJurisdictions.AddRange(
            new OfficeJurisdiction { Office = office, Municipality = town, EffectiveDate = new DateOnly(2020, 1, 1), LegalBasis = "DEMO", Status = WorkflowStatus.Approved, ApprovedAt = DateTimeOffset.UtcNow },
            new OfficeJurisdiction { Office = other, Municipality = otherTown, EffectiveDate = new DateOnly(2020, 1, 1), LegalBasis = "DEMO", Status = WorkflowStatus.Approved, ApprovedAt = DateTimeOffset.UtcNow });
        var provincial = await db.Offices.SingleAsync(o => o.Kind == OfficeKind.Provincial);
        var roles = await db.Roles.ToDictionaryAsync(r => r.Code);

        AppUser User(string name) => new() { SupabaseUserId = Guid.NewGuid(), DisplayName = name, Email = $"demo-{Guid.NewGuid():N}@example.invalid" };
        var creator = User("DEMO Municipal Appraiser");
        var munAssessor = User("DEMO Municipal Assessor");
        var munAssessor2 = User("DEMO Municipal Assessor 2");
        var provAssessor = User("DEMO Provincial Assessor");
        var provClerk = User("DEMO Provincial Reviewer");
        var outsider = User("DEMO Other Municipal Assessor");
        db.AppUsers.AddRange(creator, munAssessor, munAssessor2, provAssessor, provClerk, outsider);
        OfficeAssignment Assign(AppUser u, Office o, string role) => new()
        {
            AppUserId = u.Id, Office = o, EffectiveDate = new DateOnly(2020, 1, 1), LegalBasis = "DEMO", Status = WorkflowStatus.Approved,
            ApprovedAt = DateTimeOffset.UtcNow, Roles = [new OfficeAssignmentRole { RoleId = roles[role].Id }],
        };
        db.OfficeAssignments.AddRange(
            Assign(creator, office, RoleCodes.Appraiser), Assign(munAssessor, office, RoleCodes.Assessor), Assign(munAssessor2, office, RoleCodes.Assessor),
            Assign(provAssessor, provincial, RoleCodes.Assessor), Assign(provClerk, provincial, RoleCodes.AssessmentReviewer), Assign(outsider, other, RoleCodes.Assessor));

        // The office's own TD chain: its Assessor reviews, the Provincial Assessor gives final approval.
        db.ApprovalChains.Add(new ApprovalChain
        {
            SubjectType = ApprovalSubjectType.TaxDeclaration, Name = "DEMO LP4 office chain", Office = office, LegalBasis = "DEMO",
            EffectiveDate = new DateOnly(2020, 1, 1), Status = WorkflowStatus.Approved, ApprovedAt = DateTimeOffset.UtcNow,
            Steps =
            [
                new ApprovalChainStep { Sequence = 1, StepCode = "DEMO_REVIEW", Label = "DEMO Reviewed", SignerOffice = ApprovalSigner.PreparingOffice, RequiredRole = RoleCodes.Assessor },
                new ApprovalChainStep { Sequence = 2, StepCode = "DEMO_APPROVE", Label = "DEMO Approved", SignatoryPosition = "DEMO Provincial Assessor",
                    SignerOffice = ApprovalSigner.ProvincialOffice, RequiredRole = RoleCodes.Assessor, IsFinalApproval = true },
            ],
        });
        await db.SaveChangesAsync();

        var user = scope.ServiceProvider.GetRequiredService<CurrentUserService>();
        return (new Ctx(scope.ServiceProvider, db, user, today, office, other, provincial, town,
            creator, munAssessor, munAssessor2, provAssessor, provClerk, outsider), new Disposer(transaction, scope));
    }

    private sealed class Disposer(IAsyncDisposable transaction, IAsyncDisposable scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await scope.DisposeAsync();
        }
    }

    /// <summary>A pending TD in <paramref name="town"/>, created by <paramref name="creator"/>.</summary>
    private static async Task<TaxDeclaration> TdAsync(PrimeDbContext db, Municipality town, AppUser creator, RpuType kind = RpuType.Land)
    {
        var tag = Guid.NewGuid().ToString("N")[..10];
        var barangay = new Barangay { Municipality = town, PsgcCode = $"B{tag}", Name = "DEMO LP4 Barangay" };
        var property = new PropertyEntity
        {
            PropertyIdentificationNumber = $"DEMO-LP4-{tag}", ProvinceId = town.ProvinceId, Municipality = town, Barangay = barangay, Status = RecordStatus.Active,
        };
        var rpu = new RealPropertyUnit { Property = property, RpuNumber = $"DEMO-LP4-{tag}", RpuType = kind, EffectivityDate = new DateOnly(2026, 1, 1) };
        var td = new TaxDeclaration
        {
            Rpu = rpu, Property = property, TaxDeclarationNumber = $"DEMO-LP4-{tag}", EffectivityDate = new DateOnly(2026, 1, 1), AssessmentYear = 2026,
            Status = WorkflowStatus.PendingReview,
            ClassificationId = await db.Classifications.Select(x => x.Id).FirstAsync(), ActualUseId = await db.ActualUses.Select(x => x.Id).FirstAsync(),
        };
        db.TaxDeclarations.Add(td);
        await db.SaveChangesAsync();
        // Stamped as created by the municipal appraiser (the audit interceptor sets CreatedBy from the acting user).
        td.CreatedBy = creator.Id;
        await db.SaveChangesAsync();
        return td;
    }

    private static ApprovalDelegation Delegation(Office office, DateOnly from, DateOnly to, RpuType[]? kinds = null) => new()
    {
        Office = office, DelegatingOfficialName = "DEMO Provincial Assessor", DelegatingOfficialPosition = "DEMO Provincial Assessor",
        InstrumentReference = "DEMO Office Order LP4", InstrumentDate = from, SubjectTypes = [ApprovalSubjectType.TaxDeclaration],
        PropertyKinds = [.. kinds ?? []], ValidFrom = from, ValidTo = to, Status = WorkflowStatus.Approved, ApprovedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task WithoutDelegation_ThePreparingOfficeReviews_AndTheProvinceGivesFinalApproval()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var td = await TdAsync(c.Db, c.Town, c.Creator);

        (await c.SignAsync(td, c.Creator)).Code.ShouldBe("CANNOT_SIGN_OWN_RECORD");
        (await c.SignAsync(td, c.ProvAssessor)).Code.ShouldBe("APPROVAL_STEP_FORBIDDEN");   // step 1 is the preparing office's
        (await c.SignAsync(td, c.Outsider)).Code.ShouldBe("APPROVAL_STEP_FORBIDDEN");       // another municipal office
        var review = (await c.SignAsync(td, c.MunAssessor)).Outcome.ShouldNotBeNull();
        (review.Completed, review.Record!.SignerOfficeId).ShouldBe((false, (Guid?)c.Office.Id));

        (await c.SignAsync(td, c.MunAssessor2)).Code.ShouldBe("APPROVAL_STEP_FORBIDDEN");   // final step is the province's
        (await c.SignAsync(td, c.ProvClerk)).Code.ShouldBe("APPROVAL_STEP_FORBIDDEN");      // lacks the ASSESSOR role
        var final = (await c.SignAsync(td, c.ProvAssessor)).Outcome.ShouldNotBeNull();
        final.Completed.ShouldBeTrue();
        (final.Record!.DelegationId, final.Record.UnderDelegation, final.Record.SignatoryPosition, final.Record.SignerOfficeId)
            .ShouldBe(((Guid?)null, (string?)null, "DEMO Provincial Assessor", (Guid?)c.Provincial.Id));
    }

    [Fact]
    public async Task UnderDelegation_TheMunicipalAssessorGivesFinalApproval_AndTheSignatureSaysSo()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        c.Db.ApprovalDelegations.Add(Delegation(c.Office, c.Today.AddDays(-10), c.Today.AddDays(100)));
        await c.Db.SaveChangesAsync();
        var td = await TdAsync(c.Db, c.Town, c.Creator);

        (await c.SignAsync(td, c.MunAssessor)).Outcome.ShouldNotBeNull();
        (await c.SignAsync(td, c.ProvAssessor)).Code.ShouldBe("APPROVAL_STEP_FORBIDDEN");  // delegated: not the province now
        (await c.SignAsync(td, c.Outsider)).Code.ShouldBe("APPROVAL_STEP_FORBIDDEN");      // not sub-delegable to another office
        // The queue routes it the same way, naming the delegation.
        (await QueuedAsync(c, c.ProvAssessor, td)).ShouldBeNull();
        (await QueuedAsync(c, c.MunAssessor2, td)).ShouldNotBeNull().UnderDelegation.ShouldNotBeNull().ShouldStartWith("DEMO Office Order LP4");
        var final = (await c.SignAsync(td, c.MunAssessor2)).Outcome.ShouldNotBeNull();
        final.Completed.ShouldBeTrue();
        final.Record!.DelegationId.ShouldNotBeNull();
        final.Record.UnderDelegation.ShouldNotBeNull().ShouldStartWith("DEMO Office Order LP4 dated");
        final.Record.SignatoryPosition.ShouldBe("DEMO Municipal Assessor"); // the office head's position, not the province's
    }

    [Fact]
    public async Task ADelegationForOtherKinds_OrRevoked_LeavesFinalApprovalWithTheProvince()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var buildingsOnly = Delegation(c.Office, c.Today.AddDays(-10), c.Today.AddDays(100), [RpuType.Building]);
        c.Db.ApprovalDelegations.Add(buildingsOnly);
        await c.Db.SaveChangesAsync();

        var land = await TdAsync(c.Db, c.Town, c.Creator, RpuType.Land);
        await c.SignAsync(land, c.MunAssessor);
        // The queue decides per property kind: a building at the same step would go to the municipal Assessor.
        var waitingBuilding = await TdAsync(c.Db, c.Town, c.Creator, RpuType.Building);
        await c.SignAsync(waitingBuilding, c.MunAssessor);
        (await QueuedAsync(c, c.MunAssessor2, land)).ShouldBeNull();
        (await QueuedAsync(c, c.ProvAssessor, land)).ShouldNotBeNull();
        (await QueuedAsync(c, c.MunAssessor2, waitingBuilding)).ShouldNotBeNull();
        (await c.SignAsync(land, c.MunAssessor2)).Code.ShouldBe("APPROVAL_STEP_FORBIDDEN");
        (await c.SignAsync(land, c.ProvAssessor)).Outcome!.Record!.DelegationId.ShouldBeNull();

        var building = await TdAsync(c.Db, c.Town, c.Creator, RpuType.Building);
        await c.SignAsync(building, c.MunAssessor);
        buildingsOnly.RevokedFrom = c.Today;
        buildingsOnly.RevokedAt = DateTimeOffset.UtcNow;
        buildingsOnly.RevocationReason = "DEMO";
        await c.Db.SaveChangesAsync();
        (await c.SignAsync(building, c.MunAssessor2)).Code.ShouldBe("APPROVAL_STEP_FORBIDDEN"); // revoked from today
        (await c.SignAsync(building, c.ProvAssessor)).Outcome.ShouldNotBeNull();
    }

    /// <summary>The record's entry in <paramref name="user"/>'s approval queue, if listed.</summary>
    private static async Task<ApprovalQueueItemDto?> QueuedAsync(Ctx c, AppUser user, TaxDeclaration td)
    {
        c.User.AppUserId = user.Id;
        return (await c.Chains.ListAwaitingAsync(c.Today)).Value.SingleOrDefault(i => i.SubjectId == td.Id);
    }

    [Fact]
    public async Task TheQueue_ListsARecordForWhoeverMaySignItsNextStep()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var td = await TdAsync(c.Db, c.Town, c.Creator);

        async Task<bool> Awaits(AppUser user)
        {
            c.User.AppUserId = user.Id;
            return (await c.Chains.ListAwaitingAsync(c.Today)).Value.Any(i => i.SubjectId == td.Id);
        }

        (await Awaits(c.MunAssessor)).ShouldBeTrue();   // step 1: the preparing office's Assessor
        (await Awaits(c.ProvAssessor)).ShouldBeFalse();
        (await Awaits(c.Creator)).ShouldBeFalse();      // never their own record
        await c.SignAsync(td, c.MunAssessor);
        (await Awaits(c.MunAssessor)).ShouldBeFalse();  // signed already
        (await Awaits(c.ProvAssessor)).ShouldBeTrue();  // step 2: the province

        c.User.AppUserId = c.ProvAssessor.Id;
        var item = (await c.Chains.ListAwaitingAsync(c.Today)).Value.Single(i => i.SubjectId == td.Id);
        (item.SubjectType, item.StepLabel, item.Reference, item.UnderDelegation).ShouldBe(
            (ApprovalSubjectType.TaxDeclaration, "DEMO Approved", $"TD {td.TaxDeclarationNumber}", (string?)null));
    }

    [Fact]
    public async Task AnOfficeWithoutItsOwnChain_UsesTheProvincialDefault()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        // Make this test's default chain the only one in force for TDs without an office.
        await c.Db.ApprovalChains.Where(x => x.SubjectType == ApprovalSubjectType.TaxDeclaration && x.OfficeId == null && x.Status == WorkflowStatus.Approved)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null));
        c.Db.ApprovalChains.Add(new ApprovalChain
        {
            SubjectType = ApprovalSubjectType.TaxDeclaration, Name = "DEMO LP4 provincial default", LegalBasis = "DEMO",
            EffectiveDate = new DateOnly(2020, 1, 1), Status = WorkflowStatus.Approved, ApprovedAt = DateTimeOffset.UtcNow,
            Steps = [new ApprovalChainStep { Sequence = 1, StepCode = "DEMO_PROV", Label = "DEMO Approved", SignerOffice = ApprovalSigner.ProvincialOffice, IsFinalApproval = true }],
        });
        await c.Db.SaveChangesAsync();
        var otherTown = await c.Db.OfficeJurisdictions.Where(j => j.OfficeId == c.OtherOffice.Id).Select(j => j.Municipality!).SingleAsync();

        var td = await TdAsync(c.Db, otherTown, c.Creator);
        (await c.SignAsync(td, c.Outsider)).Code.ShouldBe("APPROVAL_STEP_FORBIDDEN"); // the default's single step is provincial
        var signed = (await c.SignAsync(td, c.ProvClerk)).Outcome.ShouldNotBeNull();   // no role required by the default
        signed.Completed.ShouldBeTrue();
        (await c.Db.ApprovalChains.SingleAsync(x => x.Id == signed.Record!.ApprovalChainId)).OfficeId.ShouldBeNull();
    }
}
