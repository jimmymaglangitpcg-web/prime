namespace Prime.Domain.Enums;

/// <summary>docs/BILLING.md §3.7. The tax an increase is measured against.</summary>
public enum TaxIncreaseCapBaseline
{
    /// <summary>The tax assessed on the property before the capped SMV took effect (the statutory baseline).</summary>
    TaxBeforeSmv = 0,

    /// <summary>The tax billed for the previous tax year — only if an ordinance says so.</summary>
    PreviousTaxYear = 1,
}
