namespace Prime.Application.Features.Billing;

/// <summary>
/// "Billing" configuration section — engine policy choices awaiting LGU
/// confirmation (docs/BILLING.md §3.4, §5). Each bill freezes the value it
/// used, so changing it never alters an existing bill.
/// </summary>
public sealed class BillingOptions
{
    public const string SectionName = "Billing";

    /// <summary>
    /// false: when a prompt-payment and an advance-payment discount both
    /// apply, only the larger is given. DOMAIN VERIFICATION REQUIRED.
    /// </summary>
    public bool AllowDiscountStacking { get; set; }
}
