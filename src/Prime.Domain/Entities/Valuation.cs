using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>
/// CLAUDE.md §31 calculation-transparency record: one immutable row per
/// computed market value, carrying enough of the inputs and resolved rule
/// (<see cref="SmvId"/>/<see cref="SmvScheduleId"/>, <see cref="BreakdownJson"/>)
/// to reconstruct how <see cref="ComputedMarketValue"/> was derived, without
/// re-running the calculation. Never updated in place — recomputing creates
/// a new row, giving Rpu/Property a full valuation history for free.
/// </summary>
public sealed class Valuation : AuditableEntity, IVersioned
{
    /// <summary>Row version (xmin): optimistic concurrency, see <see cref="IVersioned"/>.</summary>
    public uint RowVersion { get; set; }

    public Guid RpuId { get; set; }
    public RealPropertyUnit? Rpu { get; set; }
    public Guid PropertyId { get; set; }
    public PropertyEntity? Property { get; set; }

    public ValuationSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }

    public Guid? SmvId { get; set; }
    public Smv? Smv { get; set; }
    public Guid? SmvScheduleId { get; set; }
    public SmvSchedule? SmvSchedule { get; set; }

    public ValuationMethod ValuationMethod { get; set; }
    public decimal ComputedMarketValue { get; set; }

    /// <summary>
    /// The transaction the unit was valued for, when given: it decides whether a building may take a
    /// new depreciation (docs/analysis/valuation-foundation.md §4.5, Q10). An assessment of a
    /// depreciated building is made under the same transaction.
    /// </summary>
    public Guid? TransactionTypeId { get; set; }

    /// <summary>
    /// Set when the unit was valued under the rules in force on another date than <see cref="EffectiveDate"/>:
    /// a back-tax period valued at the current construction cost (docs/analysis/valuation-foundation.md §4.8, Q14).
    /// </summary>
    public DateOnly? RulesAsOf { get; set; }

    /// <summary>
    /// JSON snapshot of the calculator's input/intermediate values (area,
    /// rate, depreciation, etc.) — same <c>System.Text.Json</c> approach
    /// <c>AuditSaveChangesInterceptor</c> already uses for its snapshots.
    /// </summary>
    public string BreakdownJson { get; set; } = "{}";

    public DateOnly EffectiveDate { get; set; }
    public DateTimeOffset ComputedAt { get; set; }

    /// <summary>
    /// The appraisal rows (docs/analysis/mrpaao-forms-model.md §8.2):
    /// <see cref="ComputedMarketValue"/> is their sum. Valuations made before
    /// lines existed were given one line carrying the same values.
    /// </summary>
    public List<ValuationLine> Lines { get; set; } = [];
}

/// <summary>
/// One FAAS appraisal row: what was valued, under which classification and
/// actual use, from which schedule rate, with its own breakdown. Immutable
/// like its <see cref="Valuation"/>. A null classification or actual use is
/// taken from the unit's Tax Declaration when the line is assessed (buildings
/// and machinery carry none of their own yet).
/// </summary>
public sealed class ValuationLine : Entity
{
    public Guid ValuationId { get; set; }
    public int Sequence { get; set; }

    public ValuationLineSource Source { get; set; }
    /// <summary>The row valued (Land, Building, Machinery …); no FK, since it spans tables.</summary>
    public Guid? SourceId { get; set; }
    public string? Description { get; set; }

    public Guid? ClassificationId { get; set; }
    public Classification? Classification { get; set; }
    public Guid? SubClassificationId { get; set; }
    public SubClassification? SubClassification { get; set; }
    public Guid? ActualUseId { get; set; }
    public ActualUse? ActualUse { get; set; }

    /// <summary>
    /// The class and sub-class the row was priced by, when they differ from the ones above,
    /// which assess it (docs/analysis/valuation-foundation.md §4.3). Null: the same.
    /// </summary>
    public Guid? PricedClassificationId { get; set; }
    public Classification? PricedClassification { get; set; }
    public Guid? PricedSubClassificationId { get; set; }
    public SubClassification? PricedSubClassification { get; set; }

    /// <summary>Area, floor area or count; with <see cref="Unit"/> and <see cref="UnitValue"/> when the row is rate-based.</summary>
    /// <summary>The independent appraisal the row was valued by, if any (valuation-foundation.md §4.7).</summary>
    public Guid? IndependentAppraisalId { get; set; }

    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal? UnitValue { get; set; }
    public Guid? SmvScheduleId { get; set; }
    public SmvSchedule? SmvSchedule { get; set; }

    public decimal MarketValue { get; set; }
    public string BreakdownJson { get; set; } = "{}";
}
