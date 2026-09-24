using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Prime.Domain.DomainServices;

/// <summary>Values a number pattern can draw on. Location codes are the PSGC-style codes of the property's location.</summary>
public sealed record NumberContext(int Year, string? ProvinceCode = null, string? MunicipalityCode = null, string? BarangayCode = null);

/// <summary>
/// Parses and applies <c>NumberingScheme</c> patterns
/// (docs/FORMS-REVISION-PLAN.md §4.4). A pattern is literal text plus tokens:
/// <c>{YEAR}</c>, <c>{PROV}</c>, <c>{MUN}</c>, <c>{BRGY}</c> and exactly one
/// <c>{SEQ}</c> or <c>{SEQ:n}</c> (zero-padded to n digits, 1–12).
/// Example: <c>TD-{MUN}-{YEAR}-{SEQ:5}</c> → <c>TD-0402-2026-00017</c>.
/// Sequences run separately per <see cref="ScopeKey"/> — the pattern with the
/// sequence left out — so <c>{YEAR}</c> in a pattern restarts numbering each
/// year. New tokens (e.g. a PIN's section and parcel numbers, once the LAM
/// defines them) are added here.
/// </summary>
public static partial class NumberPattern
{
    private const string SequencePlaceholder = "#";

    [GeneratedRegex(@"\{([A-Z]+)(?::(\d+))?\}")]
    private static partial Regex TokenRegex();

    private static readonly string[] ContextTokens = ["YEAR", "PROV", "MUN", "BRGY"];

    /// <summary>Why <paramref name="pattern"/> is unusable, or null when it is valid.</summary>
    public static string? Validate(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return "pattern is required";
        }
        var sequences = 0;
        foreach (Match m in TokenRegex().Matches(pattern))
        {
            var name = m.Groups[1].Value;
            if (name == "SEQ")
            {
                sequences++;
                if (m.Groups[2].Success && int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) is < 1 or > 12)
                {
                    return "{SEQ:n} padding must be between 1 and 12";
                }
            }
            else if (!ContextTokens.Contains(name) || m.Groups[2].Success)
            {
                return $"unknown token {m.Value}; allowed: {{YEAR}} {{PROV}} {{MUN}} {{BRGY}} {{SEQ}} {{SEQ:n}}";
            }
        }
        if (sequences != 1)
        {
            return "pattern must contain exactly one {SEQ} or {SEQ:n}";
        }
        var literal = TokenRegex().Replace(pattern, "");
        if (literal.Contains('{') || literal.Contains('}'))
        {
            return "braces are only allowed around tokens";
        }
        return null;
    }

    /// <summary>Context values the pattern needs but <paramref name="context"/> lacks, e.g. ["{BRGY}"]; empty when complete.</summary>
    public static IReadOnlyList<string> MissingValues(string pattern, NumberContext context) =>
        TokenRegex().Matches(pattern).Select(m => m.Groups[1].Value).Distinct()
            .Where(name => name != "SEQ" && string.IsNullOrWhiteSpace(Value(name, context)))
            .Select(name => $"{{{name}}}").ToList();

    /// <summary>The pattern rendered without its sequence: numbers with the same scope key share one sequence.</summary>
    public static string ScopeKey(string pattern, NumberContext context) => Render(pattern, context, sequence: null);

    public static string Format(string pattern, NumberContext context, long sequence)
    {
        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "Sequences start at 1.");
        }
        return Render(pattern, context, sequence);
    }

    private static string Render(string pattern, NumberContext context, long? sequence)
    {
        if (Validate(pattern) is { } problem)
        {
            throw new ArgumentException($"Invalid number pattern: {problem}.", nameof(pattern));
        }
        if (MissingValues(pattern, context) is { Count: > 0 } missing)
        {
            throw new ArgumentException($"Number pattern needs {string.Join(", ", missing)}.", nameof(context));
        }

        var result = new StringBuilder();
        var last = 0;
        foreach (Match m in TokenRegex().Matches(pattern))
        {
            result.Append(pattern, last, m.Index - last);
            var name = m.Groups[1].Value;
            if (name == "SEQ")
            {
                result.Append(sequence is { } s
                    ? s.ToString(CultureInfo.InvariantCulture).PadLeft(m.Groups[2].Success ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : 1, '0')
                    : SequencePlaceholder);
            }
            else
            {
                result.Append(Value(name, context));
            }
            last = m.Index + m.Length;
        }
        result.Append(pattern, last, pattern.Length - last);
        return result.ToString();
    }

    private static string? Value(string token, NumberContext context) => token switch
    {
        "YEAR" => context.Year.ToString("D4", CultureInfo.InvariantCulture),
        "PROV" => context.ProvinceCode,
        "MUN" => context.MunicipalityCode,
        "BRGY" => context.BarangayCode,
        _ => null,
    };
}
