using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities.Registers;

/// <summary>
/// One dated run of an MRPAAO register (Att. 5–9; docs/analysis/mrpaao-forms-model.md
/// §15): its kind, scope and date. The rows are never kept by hand — they are
/// derived from the FAAS (TD + assessment) in force, and printing the run
/// issues it as a form, whose snapshot freezes them. A later run (e.g. the
/// Assessment Roll's quarterly supplement) is a new run.
/// </summary>
public sealed class RegisterRun : AuditableEntity
{
    public RegisterKind Kind { get; set; }

    /// <summary>Tax Map Control Roll, Assessment Rolls, Record of Assessment: the barangay.</summary>
    public Guid? BarangayId { get; set; }
    public Barangay? Barangay { get; set; }

    /// <summary>Record of Assessment: the classification the ledger is kept for.</summary>
    public Guid? ClassificationId { get; set; }
    public Classification? Classification { get; set; }

    /// <summary>Ownership Record Card: the owner.</summary>
    public Guid? TaxpayerId { get; set; }
    public Taxpayer? Taxpayer { get; set; }

    /// <summary>
    /// Record of Assessment: the first day of the period; Assessment Roll: a
    /// supplement lists only FAAS entered on or after this day. Null: all.
    /// </summary>
    public DateOnly? FromDate { get; set; }

    /// <summary>The register as of this day (the last day of a Record of Assessment period).</summary>
    public DateOnly AsOf { get; set; }

    public string? Remarks { get; set; }
}
