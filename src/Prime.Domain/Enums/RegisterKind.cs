namespace Prime.Domain.Enums;

/// <summary>The MRPAAO assessment registers (Att. 5–9; docs/analysis/mrpaao-forms-model.md §15).</summary>
public enum RegisterKind
{
    /// <summary>Tax Map Control Roll (Att. 5): parcels of a barangay.</summary>
    TaxMapControlRoll = 0,
    /// <summary>Assessment Roll — Taxable Properties (Att. 6).</summary>
    AssessmentRollTaxable = 1,
    /// <summary>Assessment Roll — Exempt Properties (Att. 7).</summary>
    AssessmentRollExempt = 2,
    /// <summary>Ownership Record Card (Att. 8): one owner's properties.</summary>
    OwnershipRecordCard = 3,
    /// <summary>Record of Assessment (Att. 9): assessment transactions of a barangay and classification in a period.</summary>
    RecordOfAssessment = 4,
}
