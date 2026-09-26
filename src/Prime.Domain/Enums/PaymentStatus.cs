namespace Prime.Domain.Enums;

/// <summary>
/// A payment's state (docs/analysis/collection.md §3). Only a
/// <see cref="Posted"/> payment counts against what is owed.
/// </summary>
public enum PaymentStatus
{
    Posted = 0,

    /// <summary>Cancelled before remittance (step 9c).</summary>
    Voided = 1,

    /// <summary>Undone after remittance, e.g. a dishonoured check (step 9c).</summary>
    Reversed = 2,
}
