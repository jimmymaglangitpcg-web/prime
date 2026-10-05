using Prime.Domain.DomainServices;
using Prime.Domain.Entities;
using Shouldly;
using Xunit;

namespace Prime.Domain.Tests.DomainServices;

/// <summary>Step L6-2 (docs/analysis/smv-preparation-general-revision.md §4.2): the order of an SMV's steps and what falls due. DEMO periods.</summary>
public class SmvPreparationFlowTests
{
    private static readonly SmvReviewPeriods Periods = new(45, 30, 30, 30, 10, 15);

    [Theory]
    [InlineData(SmvPreparationStatus.Preparing, SmvPreparationEventKind.PublishedForComment, SmvPreparationStatus.PublishedForComment)]
    [InlineData(SmvPreparationStatus.PublishedForComment, SmvPreparationEventKind.SubmittedToRegionalOffice, SmvPreparationStatus.Submitted)]
    [InlineData(SmvPreparationStatus.Submitted, SmvPreparationEventKind.EndorsedByRegionalOffice, SmvPreparationStatus.UnderReview)]
    [InlineData(SmvPreparationStatus.UnderReview, SmvPreparationEventKind.Remanded, SmvPreparationStatus.Remanded)]
    [InlineData(SmvPreparationStatus.Remanded, SmvPreparationEventKind.Resubmitted, SmvPreparationStatus.UnderReview)]
    [InlineData(SmvPreparationStatus.UnderReview, SmvPreparationEventKind.Certified, SmvPreparationStatus.Certified)]
    [InlineData(SmvPreparationStatus.UnderReview, SmvPreparationEventKind.NotCertified, SmvPreparationStatus.NotCertified)]
    [InlineData(SmvPreparationStatus.Certified, SmvPreparationEventKind.Published, SmvPreparationStatus.Published)]
    [InlineData(SmvPreparationStatus.Published, SmvPreparationEventKind.TransmittedToSanggunian, SmvPreparationStatus.Published)]
    public void Apply_FollowsThePath(SmvPreparationStatus from, SmvPreparationEventKind kind, SmvPreparationStatus expected) =>
        SmvPreparationFlow.Apply(from, kind).ShouldBe(((SmvPreparationStatus?)expected, (string?)null));

    [Theory]
    [InlineData(SmvPreparationStatus.Preparing, SmvPreparationEventKind.Certified)]
    [InlineData(SmvPreparationStatus.Submitted, SmvPreparationEventKind.Published)]
    [InlineData(SmvPreparationStatus.Remanded, SmvPreparationEventKind.Certified)]
    [InlineData(SmvPreparationStatus.NotCertified, SmvPreparationEventKind.Resubmitted)]
    [InlineData(SmvPreparationStatus.Cancelled, SmvPreparationEventKind.PublishedForComment)]
    public void Apply_RefusesAnOrderThatCannotHaveHappened(SmvPreparationStatus from, SmvPreparationEventKind kind)
    {
        var (next, problem) = SmvPreparationFlow.Apply(from, kind);
        next.ShouldBeNull();
        problem.ShouldNotBeNullOrWhiteSpace();
    }

    private static SmvPreparationEvent E(SmvPreparationEventKind kind, int month, int day) => new() { Kind = kind, OccurredOn = new DateOnly(2027, month, day) };

    [Fact]
    public void NextDue_FollowsTheLatestStep()
    {
        SmvPreparationFlow.NextDue([], Periods).ShouldBeNull();
        SmvPreparationFlow.NextDue([E(SmvPreparationEventKind.SubmittedToRegionalOffice, 3, 1)], Periods)
            .ShouldBe(new SmvPreparationDue("Review by the BLGF Regional Office", new DateOnly(2027, 4, 15)));
        SmvPreparationFlow.NextDue([E(SmvPreparationEventKind.SubmittedToRegionalOffice, 3, 1), E(SmvPreparationEventKind.Remanded, 4, 10)], Periods)!
            .DueOn.ShouldBe(new DateOnly(2027, 5, 10));
        SmvPreparationFlow.NextDue([E(SmvPreparationEventKind.Remanded, 4, 10), E(SmvPreparationEventKind.Resubmitted, 5, 1)], Periods)!
            .DueOn.ShouldBe(new DateOnly(2027, 5, 11));
        // A transmittal after publication does not hide the effectivity.
        SmvPreparationFlow.NextDue([E(SmvPreparationEventKind.Published, 6, 1), E(SmvPreparationEventKind.TransmittedToSanggunian, 6, 5)], Periods)
            .ShouldBe(new SmvPreparationDue("Effectivity of the SMV", new DateOnly(2027, 6, 16)));
        SmvPreparationFlow.NextDue([E(SmvPreparationEventKind.Certified, 5, 20)], Periods).ShouldBeNull();
    }
}
