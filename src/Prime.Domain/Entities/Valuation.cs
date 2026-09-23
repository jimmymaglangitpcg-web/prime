using Prime.Domain.Common;
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
public sealed class Valuation : AuditableEntity
{
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
    /// JSON snapshot of the calculator's input/intermediate values (area,
    /// rate, depreciation, etc.) — same <c>System.Text.Json</c> approach
    /// <c>AuditSaveChangesInterceptor</c> already uses for its snapshots.
    /// </summary>
    public string BreakdownJson { get; set; } = "{}";

    public DateOnly EffectiveDate { get; set; }
    public DateTimeOffset ComputedAt { get; set; }
}
