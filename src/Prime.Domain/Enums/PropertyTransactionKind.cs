namespace Prime.Domain.Enums;

/// <summary>
/// The statutory events an assessment transaction records (CLAUDE.md §34;
/// baseline Q9). PRIME's behaviour keys off these; the official codes,
/// names and ranks are configuration on <c>TransactionType</c>.
/// </summary>
public enum PropertyTransactionKind
{
    NewDiscovery = 0,
    NewAssessment = 1,
    Transfer = 2,
    Subdivision = 3,
    Consolidation = 4,
    Reclassification = 5,
    Reassessment = 6,
    GeneralRevision = 7,
    Cancellation = 8,
    Correction = 9,
    AdditionOfImprovement = 10,
    RemovalOfImprovement = 11,
    /// <summary>A created LGU or a transferred territory: new PINs under new index numbers (LAM Bk II p.40; identification-numbering.md §4.3).</summary>
    TerritorialChange = 12,
    /// <summary>
    /// A cancellation, restoration or revival ordered by a court (LAM Bk III p.88 A.1; assessment-listing-exemptions.md §4.3).
    /// A restored declaration is a new TD naming the cancelled one, never the cancelled one reopened. The order is a
    /// mandatory requirement of the type.
    /// </summary>
    CourtOrder = 13,
    /// <summary>
    /// A machine moved to another property (Q7): filed on the receiving property; its unit there continues the moved
    /// unit (<c>PreviousRpuId</c>), whose TD it cancels across properties, and the moved unit is retired.
    /// </summary>
    MachineryRelocation = 14,
}

/// <summary>How another property takes part in a transaction.</summary>
public enum TransactionPropertyRole
{
    /// <summary>A property the transaction starts from (e.g. a consolidation's sources).</summary>
    Source = 0,

    /// <summary>A property the transaction produces (e.g. a subdivision's lots).</summary>
    Result = 1,
}
