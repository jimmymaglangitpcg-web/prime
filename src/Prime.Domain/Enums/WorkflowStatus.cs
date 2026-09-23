namespace Prime.Domain.Enums;

/// <summary>
/// Fixed workflow status set shared by every maker-checker-governed entity
/// (Tax Declaration, Assessment, PropertyTransaction, TaxBill correction,
/// PropertyExemption, ...). Fixed by CLAUDE.md §45 — not a configurable
/// reference table, unlike LGU-specific classifications/actual uses/etc.
/// </summary>
public enum WorkflowStatus
{
    Draft = 0,
    Submitted = 1,
    PendingReview = 2,
    Approved = 3,
    Rejected = 4,
    Posted = 5,
    Cancelled = 6,
    Voided = 7,
}
