namespace Prime.Domain.Enums;

/// <summary>
/// docs/BILLING.md §4. What a bill line is. Basic RPT vs. an additional levy
/// is told apart by the line's tax type (an LGU-configured lookup), not by
/// this enum, so no tax type is special-cased in code.
/// </summary>
public enum BillingComponent
{
    /// <summary>The installment's share of the annual tax for one tax type (from a <c>TaxRate</c>).</summary>
    Tax = 0,

    /// <summary>A negative line from a <c>DiscountRule</c>.</summary>
    Discount = 1,

    /// <summary>A surcharge from a <c>PenaltyRule</c>.</summary>
    Penalty = 2,

    /// <summary>Interest on overdue tax from an <c>InterestRule</c>.</summary>
    Interest = 3,
}
