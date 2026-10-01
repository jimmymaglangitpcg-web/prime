using Prime.Domain.Common;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>One acquisition cost item of a machine (LAM Bk III p.75).</summary>
public sealed class MachineryCostItem : Entity
{
    public Guid MachineryId { get; set; }
    public int Sequence { get; set; }
    public MachineryCostItemKind Kind { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// A peso exchange rate of a currency on a date, from its source (BSP), for converting imported
/// machinery (LAM Bk III pp.73–74; docs/analysis/valuation-foundation.md §4.6). A Draft is approved
/// by a second user; an approved rate is never edited. Rates are observations, entered in any
/// order: a valuation takes the latest approved rate on or before the date it needs.
/// </summary>
public sealed class ExchangeRate : AuditableEntity
{
    /// <summary>ISO 4217, e.g. "USD".</summary>
    public string Currency { get; set; } = string.Empty;
    public DateOnly RateDate { get; set; }
    public decimal PesosPerUnit { get; set; }
    public string Source { get; set; } = string.Empty;
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;
    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? Remarks { get; set; }
}

/// <summary>
/// A price index of a series (an origin country's, or the local one) for a year, from its source,
/// for trending machinery cost (LAM Bk III pp.73–74). The factor applied is the index of the
/// valuation year over that of the acquisition year. Approved by a second user; never edited.
/// </summary>
public sealed class PriceIndex : AuditableEntity
{
    public string Series { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal Value { get; set; }
    public string Source { get; set; } = string.Empty;
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;
    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? Remarks { get; set; }
}
