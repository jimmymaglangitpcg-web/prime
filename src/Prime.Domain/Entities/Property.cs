using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>
/// The long-lived physical property identity (CLAUDE.md §19). Never use a
/// Tax Declaration number as the permanent identity of the physical
/// property — that role belongs to <see cref="PropertyIdentificationNumber"/>
/// on this entity (CLAUDE.md §4).
/// </summary>
public sealed class PropertyEntity : AuditableEntity
{
    public string PropertyIdentificationNumber { get; set; } = string.Empty;

    public Guid ProvinceId { get; set; }
    public Province? Province { get; set; }
    public Guid MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }
    public Guid BarangayId { get; set; }
    public Barangay? Barangay { get; set; }
    public Guid? ZoneId { get; set; }
    public Zone? Zone { get; set; }

    public string? Street { get; set; }
    public string? Sitio { get; set; }
    public string? LotNumber { get; set; }
    public string? BlockNumber { get; set; }
    public string? SurveyNumber { get; set; }
    public string? TitleNumber { get; set; }
    public string? TaxMapNumber { get; set; }

    public RecordStatus Status { get; set; } = RecordStatus.Active;
}
