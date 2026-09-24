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
}

/// <summary>How another property takes part in a transaction.</summary>
public enum TransactionPropertyRole
{
    /// <summary>A property the transaction starts from (e.g. a consolidation's sources).</summary>
    Source = 0,

    /// <summary>A property the transaction produces (e.g. a subdivision's lots).</summary>
    Result = 1,
}
