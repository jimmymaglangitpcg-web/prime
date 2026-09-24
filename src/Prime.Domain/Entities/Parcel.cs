using NetTopologySuite.Geometries;
using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>
/// CLAUDE.md §21. Geometry is a MultiPolygon in
/// <see cref="SpatialReference.StorageSrid"/> (docs/GIS.md §2).
/// <see cref="Area"/> is the declared area (e.g. from the title/technical
/// description) and is never overwritten by the geometry's measured area.
/// </summary>
public sealed class Parcel : AuditableEntity
{
    public Guid PropertyId { get; set; }
    public PropertyEntity? Property { get; set; }

    public Geometry? Geometry { get; set; }
    public decimal? Area { get; set; }

    public string? SurveyNumber { get; set; }
    public string? LotNumber { get; set; }
    public string? BlockNumber { get; set; }

    public Guid BarangayId { get; set; }
    public Barangay? Barangay { get; set; }
    public Guid? ZoneId { get; set; }
    public Zone? Zone { get; set; }

    public RecordStatus Status { get; set; } = RecordStatus.Active;

    /// <summary>
    /// Optimistic-concurrency token (CLAUDE.md §66), mapped to PostgreSQL's
    /// <c>xmin</c> system column — no physical column is added. Clients echo
    /// it back on updates; a stale value is rejected rather than silently
    /// overwriting someone else's edit.
    /// </summary>
    public uint Version { get; set; }
}
