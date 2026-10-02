using Prime.Domain.Common;

namespace Prime.Domain.Entities;

/// <summary>
/// The back taxes of a unit declared for the first time or discovered (LAM Bk III pp.78–80; LGC §222;
/// docs/analysis/valuation-foundation.md §4.8): the year it should have been declared from, with its basis,
/// split into periods at each SMV effectivity date. Each period has its own valuation and assessment, posted
/// in order; each posted assessment gets its own FAAS/TD, cancelling the one before (Q15).
/// </summary>
public sealed class BackTaxRun : AuditableEntity
{
    public Guid RpuId { get; set; }
    public RealPropertyUnit? Rpu { get; set; }
    /// <summary>The year it should have been declared from (completion, acquisition, occupation …).</summary>
    public int DeclaredFromYear { get; set; }
    /// <summary>Why that year: e.g. the certificate of completion or the deed of sale.</summary>
    public string Basis { get; set; } = string.Empty;
    /// <summary>The year it is first assessed; the back taxes reach back at most the configured number of years before it.</summary>
    public int InitialAssessmentYear { get; set; }
    public Guid? TransactionTypeId { get; set; }
    public List<BackTaxPeriod> Periods { get; set; } = [];
}

/// <summary>One period of a back-tax run, with the valuation and the assessment made for it.</summary>
public sealed class BackTaxPeriod : Entity
{
    public Guid BackTaxRunId { get; set; }
    public int Sequence { get; set; }
    public DateOnly StartDate { get; set; }
    /// <summary>Null: the current period.</summary>
    public DateOnly? EndDate { get; set; }
    public Guid ValuationId { get; set; }
    public Valuation? Valuation { get; set; }
    public Guid AssessmentId { get; set; }
    public Assessment? Assessment { get; set; }
}
