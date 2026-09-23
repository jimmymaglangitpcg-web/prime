using Prime.Domain.Enums;

namespace Prime.Domain.DomainServices;

/// <summary>
/// The output of a single <see cref="ValuationCalculator"/> call — enough to
/// persist a CLAUDE.md §31 calculation breakdown (<c>Prime.Domain.Entities.Valuation</c>)
/// without the calculator itself knowing anything about persistence.
/// </summary>
public sealed record ValuationCalculationResult(
    ValuationMethod Method,
    decimal MarketValue,
    IReadOnlyDictionary<string, decimal> Breakdown);
