namespace Prime.Domain.Enums;

/// <summary>How an SMV came into force (docs/analysis/valuation-foundation.md §4.3).</summary>
public enum SmvBasis
{
    /// <summary>Enacted by a local ordinance (before RA 12001).</summary>
    Ordinance = 0,

    /// <summary>Certified by the Secretary of Finance and published (RA 12001).</summary>
    Certified = 1,

    /// <summary>
    /// An amendment of an approved SMV between revisions (LAM 2025 Book IV Ch. III §4): only the rows it changes, which
    /// take precedence over the amended SMV's rows for the same key from its effectivity
    /// (docs/analysis/smv-preparation-general-revision.md §4.7, Q17).
    /// </summary>
    Amendment = 2,
}

/// <summary>Why an SMV is amended between revisions (LAM 2025 Book IV Ch. III §4).</summary>
public enum SmvAmendmentGround
{
    /// <summary>Roads or similar infrastructure introduced by right of way.</summary>
    Infrastructure = 0,

    /// <summary>A calamity or disaster, man-made or natural.</summary>
    Calamity = 1,

    /// <summary>A pandemic, a declared public health emergency or an analogous adverse circumstance.</summary>
    PandemicOrEmergency = 2,

    /// <summary>Correction of errors and inequalities in the SMV.</summary>
    CorrectionOfErrors = 3,
}
