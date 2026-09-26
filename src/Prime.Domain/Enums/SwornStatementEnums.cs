namespace Prime.Domain.Enums;

/// <summary>docs/analysis/mrpaao-forms-model.md §16.3.</summary>
public enum SwornStatementStatus
{
    Draft = 0,
    Filed = 1,
    /// <summary>A filed statement replaced by a later filed one naming it.</summary>
    Superseded = 2,
    Cancelled = 3,
}

/// <summary>The declarant's capacity (Att. 11, item 1).</summary>
public enum DeclarantCapacity
{
    Owner = 0,
    Administrator = 1,
    AuthorizedRepresentative = 2,
}

/// <summary>The section the statement is filed under, as the user records it.</summary>
public enum SwornStatementFilingBasis
{
    /// <summary>LGC §202: declaration of real property by the owner or administrator.</summary>
    Section202 = 0,
    /// <summary>LGC §203: declaration of a new property or of improvements.</summary>
    Section203 = 1,
    Other = 2,
}

/// <summary>The parts of Att. 11.</summary>
public enum SwornStatementItemKind
{
    Land = 0,
    Building = 1,
    Machinery = 2,
    /// <summary>Perennial trees and plants.</summary>
    OtherImprovement = 3,
}
