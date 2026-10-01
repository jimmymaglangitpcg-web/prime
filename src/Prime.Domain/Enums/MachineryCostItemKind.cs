namespace Prime.Domain.Enums;

/// <summary>
/// An item of a machine's acquisition cost (LAM Bk III pp.73–75). Freight and insurance are part
/// of the cost, insurance and freight that is converted and trended; the others are added at their
/// recorded cost (docs/analysis/valuation-foundation.md §4.6).
/// </summary>
public enum MachineryCostItemKind
{
    Freight = 0,
    Insurance = 1,
    BankCharges = 2,
    Brokerage = 3,
    Arrastre = 4,
    Duties = 5,
    InlandTransport = 6,
    Installation = 7,
    Other = 8,
}
