using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;

namespace Prime.Domain.Entities;

/// <summary>
/// The criteria of one sub-class of land in an SMV, as SMV Form 1 lists them (LAM 2025 Book IV p.114, Annex IV-A;
/// docs/analysis/smv-preparation-general-revision.md §4.2). The text is the province's; PRIME stores and prints it and never
/// uses it to classify a property. Kept with the SMV: set while it is a draft, fixed once it is approved.
/// </summary>
public sealed class SmvSubClassCriterion : AuditableEntity
{
    public Guid SmvId { get; set; }
    public Guid ClassificationId { get; set; }
    public Classification? Classification { get; set; }
    public Guid SubClassificationId { get; set; }
    public SubClassification? SubClassification { get; set; }
    /// <summary>The order the sub-classes are printed in within their class.</summary>
    public int Sequence { get; set; }
    public string Criteria { get; set; } = string.Empty;
}
