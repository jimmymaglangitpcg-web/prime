using Prime.Domain.Exceptions;

namespace Prime.Domain.DomainServices;

/// <summary>
/// Amounts as PRIME stores them (CLAUDE.md §65): <c>numeric(18,2)</c>, pesos to the centavo. A computed market or
/// assessed value is rounded here, half away from zero, before anything reads it, so a value assessed in the request
/// that computed it and one read back from the database are the same number (docs/analysis/production-hardening.md
/// §9, H3). The rounding rule is DOMAIN VERIFICATION REQUIRED (docs/BUSINESS-RULES.md).
/// </summary>
public static class Money
{
    /// <summary>The largest amount a <c>numeric(18,2)</c> column holds.</summary>
    public const decimal MaxAmount = 9_999_999_999_999_999.99m;

    /// <summary><paramref name="value"/> to the centavo, half away from zero.</summary>
    public static decimal ToCentavo(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary><paramref name="value"/>, or <see cref="ValueOutOfRangeException"/> when it cannot be stored.</summary>
    public static decimal Checked(decimal value, string what) =>
        Math.Abs(value) <= MaxAmount ? value : throw new ValueOutOfRangeException(what, value);
}
