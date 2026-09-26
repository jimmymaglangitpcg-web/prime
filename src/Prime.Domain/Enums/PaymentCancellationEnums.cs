namespace Prime.Domain.Enums;

public enum PaymentCancellationStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
}

/// <summary>
/// Fixed when a cancellation is approved (docs/analysis/collection.md §4.4):
/// a <see cref="Void"/> is approved on the payment's own date (and, from
/// step 9e, before remittance); anything later is a <see cref="Reversal"/>.
/// </summary>
public enum PaymentCancellationKind
{
    Void = 0,
    Reversal = 1,
}
