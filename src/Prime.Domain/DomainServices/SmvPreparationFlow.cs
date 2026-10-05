using Prime.Domain.Entities;

namespace Prime.Domain.DomainServices;

/// <summary>The statutory periods of an SMV's review, in days (RA 12001 §§15–16 and its IRR; LAM 2025 Book IV Ch. III). Settings.</summary>
public sealed record SmvReviewPeriods(
    int RegionalOfficeReviewDays, int BlgfReviewDays, int CertificationDays, int ResubmissionDays, int ResubmittedDecisionDays,
    int EffectivityDaysAfterPublication);

/// <summary>A date the office should watch: what is due and by when. A reminder only.</summary>
public sealed record SmvPreparationDue(string What, DateOnly DueOn);

/// <summary>
/// Which step of an SMV's path may follow which (docs/analysis/smv-preparation-general-revision.md §4.2), and what falls due
/// after each. The steps record what happened; PRIME refuses only an order that cannot have happened.
/// </summary>
public static class SmvPreparationFlow
{
    /// <summary>The status after <paramref name="kind"/>, or why it cannot follow <paramref name="status"/>.</summary>
    public static (SmvPreparationStatus? Next, string? Problem) Apply(SmvPreparationStatus status, SmvPreparationEventKind kind)
    {
        SmvPreparationStatus? next = (kind, status) switch
        {
            (SmvPreparationEventKind.PublishedForComment, SmvPreparationStatus.Preparing or SmvPreparationStatus.PublishedForComment)
                => SmvPreparationStatus.PublishedForComment,
            (SmvPreparationEventKind.SubmittedToRegionalOffice, SmvPreparationStatus.Preparing or SmvPreparationStatus.PublishedForComment)
                => SmvPreparationStatus.Submitted,
            (SmvPreparationEventKind.EndorsedByRegionalOffice, SmvPreparationStatus.Submitted) => SmvPreparationStatus.UnderReview,
            (SmvPreparationEventKind.EndorsedByBlgf, SmvPreparationStatus.UnderReview) => SmvPreparationStatus.UnderReview,
            (SmvPreparationEventKind.Remanded, SmvPreparationStatus.Submitted or SmvPreparationStatus.UnderReview) => SmvPreparationStatus.Remanded,
            (SmvPreparationEventKind.Resubmitted, SmvPreparationStatus.Remanded) => SmvPreparationStatus.UnderReview,
            (SmvPreparationEventKind.Certified, SmvPreparationStatus.Submitted or SmvPreparationStatus.UnderReview) => SmvPreparationStatus.Certified,
            (SmvPreparationEventKind.NotCertified, SmvPreparationStatus.Submitted or SmvPreparationStatus.UnderReview) => SmvPreparationStatus.NotCertified,
            (SmvPreparationEventKind.Published, SmvPreparationStatus.Certified) => SmvPreparationStatus.Published,
            (SmvPreparationEventKind.TransmittedToSanggunian, SmvPreparationStatus.Certified or SmvPreparationStatus.Published) => status,
            _ => null,
        };
        return next is null ? (null, $"A {Label(kind)} cannot be recorded while the preparation is {status}.") : (next, null);
    }

    /// <summary>
    /// What falls due after the latest step: the Regional Office's review after submission, the BLGF's after its endorsement,
    /// the certification after the BLGF's, the resubmission after a remand and the decision after it, the effectivity after
    /// publication. Null when nothing is pending.
    /// </summary>
    public static SmvPreparationDue? NextDue(IEnumerable<SmvPreparationEvent> events, SmvReviewPeriods periods)
    {
        var last = events.Where(e => e.Kind != SmvPreparationEventKind.TransmittedToSanggunian)
            .OrderBy(e => e.OccurredOn).ThenBy(e => e.CreatedAt).LastOrDefault();
        return last?.Kind switch
        {
            SmvPreparationEventKind.SubmittedToRegionalOffice =>
                new("Review by the BLGF Regional Office", last.OccurredOn.AddDays(periods.RegionalOfficeReviewDays)),
            SmvPreparationEventKind.EndorsedByRegionalOffice => new("Review by the BLGF", last.OccurredOn.AddDays(periods.BlgfReviewDays)),
            SmvPreparationEventKind.EndorsedByBlgf => new("Certification by the Secretary of Finance", last.OccurredOn.AddDays(periods.CertificationDays)),
            SmvPreparationEventKind.Remanded => new("Resubmission of the revised SMV", last.OccurredOn.AddDays(periods.ResubmissionDays)),
            SmvPreparationEventKind.Resubmitted => new("Decision on the resubmitted SMV", last.OccurredOn.AddDays(periods.ResubmittedDecisionDays)),
            SmvPreparationEventKind.Published => new("Effectivity of the SMV", Effectivity(last.OccurredOn, periods)),
            _ => null,
        };
    }

    /// <summary>The SMV takes effect the given number of days after its publication.</summary>
    public static DateOnly Effectivity(DateOnly publishedOn, SmvReviewPeriods periods) => publishedOn.AddDays(periods.EffectivityDaysAfterPublication);

    public static string Label(SmvPreparationEventKind kind) => kind switch
    {
        SmvPreparationEventKind.PublishedForComment => "publication for comment",
        SmvPreparationEventKind.SubmittedToRegionalOffice => "submission to the BLGF Regional Office",
        SmvPreparationEventKind.EndorsedByRegionalOffice => "endorsement by the Regional Office",
        SmvPreparationEventKind.EndorsedByBlgf => "endorsement by the BLGF",
        SmvPreparationEventKind.Remanded => "remand",
        SmvPreparationEventKind.Resubmitted => "resubmission",
        SmvPreparationEventKind.Certified => "certification",
        SmvPreparationEventKind.NotCertified => "lapse without certification",
        SmvPreparationEventKind.Published => "publication of the certified SMV",
        SmvPreparationEventKind.TransmittedToSanggunian => "transmittal to the LCE and Sanggunian",
        _ => kind.ToString(),
    };
}
