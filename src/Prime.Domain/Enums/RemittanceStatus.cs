namespace Prime.Domain.Enums;

/// <summary>docs/analysis/collection.md §4.7: a remittance is submitted by the cashier, then accepted or returned by another user.</summary>
public enum RemittanceStatus
{
    Submitted = 0,
    Accepted = 1,
    /// <summary>Sent back with a reason; its payments are free for a later remittance.</summary>
    Returned = 2,
}
