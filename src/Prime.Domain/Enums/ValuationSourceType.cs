namespace Prime.Domain.Enums;

/// <summary>Which physical asset a <see cref="Entities.Valuation"/> row was computed for.</summary>
public enum ValuationSourceType
{
    Land = 0,
    Building = 1,
    Machinery = 2,
}
