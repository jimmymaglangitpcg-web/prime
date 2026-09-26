using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities.SwornStatements;

/// <summary>
/// The owner's sworn statement of the true current and fair market value of
/// real properties (MRPAAO Att. 11; LGC §202/§203;
/// docs/analysis/mrpaao-forms-model.md §16). It records what the declarant
/// swore to, as written: the declared values are information for the
/// assessor and never feed valuation.
///
/// Draft → Filed, or Cancelled; a filed statement is replaced by a new one
/// naming it (<see cref="SupersedesId"/>), which marks it Superseded when filed.
/// </summary>
public sealed class SwornStatement : AuditableEntity
{
    /// <summary>The Sworn Statement Index No.: generated on filing when a scheme is in force, else typed or blank.</summary>
    public string? Number { get; set; }

    public string DeclarantName { get; set; } = string.Empty;
    /// <summary>Optional: the declarant as a registered taxpayer (a representative often is not one).</summary>
    public Guid? DeclarantTaxpayerId { get; set; }
    public Taxpayer? DeclarantTaxpayer { get; set; }
    public string? Citizenship { get; set; }
    public string? CivilStatus { get; set; }
    public string? PostalAddress { get; set; }
    public string? DeclarantTin { get; set; }
    public DeclarantCapacity Capacity { get; set; }
    /// <summary>The owners, as stated, when an administrator or representative files.</summary>
    public string? OwnerNames { get; set; }

    /// <summary>One statement covers one city or municipality (Att. 11, Note 1).</summary>
    public Guid MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }

    /// <summary>
    /// Recorded as the user states it; PRIME enforces no filing period
    /// (DOMAIN VERIFICATION REQUIRED: LGC §202/§203 as amended).
    /// </summary>
    public SwornStatementFilingBasis FilingBasis { get; set; }

    public DateOnly? SignedOn { get; set; }
    public string? SignedAt { get; set; }
    /// <summary>A thumbmarked statement needs two witnesses (Att. 11).</summary>
    public bool Thumbmarked { get; set; }
    public string? Witness1 { get; set; }
    public string? Witness2 { get; set; }

    // Jurat. The identity evidence is free text: which one a jurat requires is
    // a notarial rule (DOMAIN VERIFICATION REQUIRED), not the form's fixed CTC No.
    public DateOnly? SwornOn { get; set; }
    public string? SwornAt { get; set; }
    public string? AdministeringOfficer { get; set; }
    public string? OfficerTin { get; set; }
    public string? IdentityDocument { get; set; }
    public DateOnly? IdentityDocumentIssuedOn { get; set; }
    public string? IdentityDocumentIssuedAt { get; set; }

    /// <summary>The day the assessor's office received it (Att. 11, Note 2).</summary>
    public DateOnly? ReceivedOn { get; set; }

    public SwornStatementStatus Status { get; set; } = SwornStatementStatus.Draft;
    public DateTimeOffset? FiledAt { get; set; }
    public Guid? FiledBy { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }

    /// <summary>The filed statement this one corrects.</summary>
    public Guid? SupersedesId { get; set; }
    public SwornStatement? Supersedes { get; set; }

    public string? Remarks { get; set; }

    public List<SwornStatementItem> Items { get; set; } = [];
}

/// <summary>
/// One property the statement declares: land, a building, a machine or
/// trees and plants (Att. 11 parts A–C and Other Improvements). Its existing
/// declaration is a TD in PRIME, a number PRIME does not have, or neither
/// ("NEW", Note 3). Only the columns of its kind are filled.
/// </summary>
public sealed class SwornStatementItem : Entity
{
    public Guid SwornStatementId { get; set; }
    public SwornStatementItemKind Kind { get; set; }
    public int Sequence { get; set; }

    public Guid? TaxDeclarationId { get; set; }
    public TaxDeclaration? TaxDeclaration { get; set; }
    /// <summary>An existing TD number PRIME does not hold (e.g. before data migration).</summary>
    public string? ExistingTdNumber { get; set; }
    /// <summary>From the TD, or linked later once a NEW property is registered.</summary>
    public Guid? PropertyId { get; set; }
    public Guid? RpuId { get; set; }
    public RealPropertyUnit? Rpu { get; set; }

    public string? Location { get; set; }
    public decimal DeclaredMarketValue { get; set; }

    // A. Land
    public string? LotNumber { get; set; }
    public string? BlockNumber { get; set; }
    public string? CadastralNumber { get; set; }
    public string? TitleNumber { get; set; }
    public decimal? Area { get; set; }
    public string? AreaUnit { get; set; }
    public Guid? ClassificationId { get; set; }
    public Classification? Classification { get; set; }

    // B. Buildings and other structures
    public decimal? FloorArea { get; set; }
    public int? Storeys { get; set; }
    public string? Description { get; set; }
    public int? YearCompleted { get; set; }
    public Guid? ActualUseId { get; set; }
    public ActualUse? ActualUse { get; set; }
    public string? LotOwnerName { get; set; }

    // C. Machinery (Description above)
    public DateOnly? DateAcquired { get; set; }
    public DateOnly? DateOperationCommenced { get; set; }
    public decimal? AcquisitionCost { get; set; }
    public decimal? InstallationCost { get; set; }
    public decimal? Depreciation { get; set; }

    // Other improvements (perennial trees/plants)
    public Guid? ImprovementKindId { get; set; }
    public ImprovementKind? ImprovementKind { get; set; }
    public int? ProductiveCount { get; set; }
    public int? NonProductiveCount { get; set; }
    public string? AnnualProduct { get; set; }
    public string? Ages { get; set; }
}
