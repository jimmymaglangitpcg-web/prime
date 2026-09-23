namespace Prime.Domain.Enums;

/// <summary>
/// Fixed set per CLAUDE.md §20 ("Support: Individual, Corporation,
/// Partnership, Government entity, Estate, Association, Other legal
/// entities") — a closed legal-entity-type taxonomy, not LGU-configurable.
/// </summary>
public enum TaxpayerType
{
    Individual = 0,
    Corporation = 1,
    Partnership = 2,
    Government = 3,
    Estate = 4,
    Association = 5,
    Other = 6,
}
