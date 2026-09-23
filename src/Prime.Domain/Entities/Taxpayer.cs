using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>CLAUDE.md §20.</summary>
public sealed class Taxpayer : AuditableEntity
{
    public TaxpayerType TaxpayerType { get; set; }

    // Individual
    public string? LastName { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? Suffix { get; set; }

    // Non-individual
    public string? CorporateName { get; set; }

    public string? Tin { get; set; }
    public string? Address { get; set; }
    public Guid? BarangayId { get; set; }
    public Barangay? Barangay { get; set; }
    public Guid? MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }
    public Guid? ProvinceId { get; set; }
    public Province? Province { get; set; }

    public string? ContactNumber { get; set; }
    public string? Email { get; set; }

    public RecordStatus Status { get; set; } = RecordStatus.Active;
}
