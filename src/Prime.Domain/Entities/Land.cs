using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>
/// CLAUDE.md §24. <see cref="MarketValue"/>/<see cref="AssessedValue"/> are
/// computed by the Phase 5 valuation/assessment engine, never entered
/// directly — nullable here because no valuation exists yet in Phase 3.
/// </summary>
public sealed class Land : AuditableEntity
{
    public Guid RpuId { get; set; }
    public RealPropertyUnit? Rpu { get; set; }
    public Guid PropertyId { get; set; }
    public PropertyEntity? Property { get; set; }

    public decimal Area { get; set; }
    public string AreaUnit { get; set; } = "sqm";

    public Guid ClassificationId { get; set; }
    public Classification? Classification { get; set; }
    public Guid ActualUseId { get; set; }
    public ActualUse? ActualUse { get; set; }
    public Guid? SubClassificationId { get; set; }
    public SubClassification? SubClassification { get; set; }
    public Guid? ZoneId { get; set; }
    public Zone? Zone { get; set; }

    public decimal? LocationFactor { get; set; }
    public decimal? RoadFrontage { get; set; }
    public Guid? RoadTypeId { get; set; }
    public RoadType? RoadType { get; set; }
    public bool IsCornerLot { get; set; }
    public string? Zoning { get; set; }

    public decimal? MarketValue { get; set; }
    public decimal? AssessedValue { get; set; }

    public RecordStatus Status { get; set; } = RecordStatus.Active;
}
