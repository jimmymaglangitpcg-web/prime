using NetTopologySuite.Geometries;
using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>
/// CLAUDE.md §21. Geometry SRID is DOMAIN VERIFICATION REQUIRED — see
/// docs/DATABASE.md §9. Stored as WGS84 (EPSG:4326) pending confirmation of
/// the target LGU's actual survey/GIS data CRS; do not treat 4326 as a
/// settled decision.
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
}
