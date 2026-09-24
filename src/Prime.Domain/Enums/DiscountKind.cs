namespace Prime.Domain.Enums;

/// <summary>docs/BILLING.md §3.4. Which payment behaviour a discount rewards; the rate itself is ordinance data.</summary>
public enum DiscountKind
{
    /// <summary>An installment paid on or before its due date.</summary>
    PromptPayment = 0,

    /// <summary>The whole year's tax paid on or before a configured cutoff.</summary>
    AdvancePayment = 1,
}
