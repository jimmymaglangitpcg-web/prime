using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Prime.Domain.DomainServices;

/// <summary>
/// Values a number pattern can draw on. Location codes are the PSGC-style codes of the
/// property's location. The index numbers are the assessor's (MRPAAO Ch. II §1;
/// docs/analysis/property-identification.md §3.3): <see cref="LguIndex"/> is the
/// province's, or a city's or Metro Manila municipality's own 3-digit number;
/// <see cref="MunicipalityIndex"/> is the municipality's 2-digit number, or the city
/// district's; <see cref="SectionIndex"/> is the tax map section's.
/// </summary>
public sealed record NumberContext(
    int Year,
    string? ProvinceCode = null,
    string? MunicipalityCode = null,
    string? BarangayCode = null,
    string? LguIndex = null,
    string? MunicipalityIndex = null,
    string? BarangayIndex = null,
    string? SectionIndex = null,
    int? RevisionYear = null,
    long? TdCount = null);

/// <summary>
/// Parses and applies <c>NumberingScheme</c> patterns
/// (docs/FORMS-REVISION-PLAN.md §4.4). A pattern is literal text plus tokens:
/// <c>{YEAR}</c>, <c>{PROV}</c>, <c>{MUN}</c>, <c>{BRGY}</c> (PSGC codes),
/// <c>{LGUIDX}</c>, <c>{MUNIDX}</c>, <c>{BRGYIDX}</c>, <c>{SECT}</c> (the
/// assessor's index numbers), <c>{REV}</c> (the general revision in force: it is
/// not printed, but restarts the sequence at each general revision), <c>{GRYEAR}</c>
/// (the same year, printed: the LAM's TD number, Book I p.23), <c>{TDCOUNT}</c> or
/// <c>{TDCOUNT:n}</c> (the assessment count of the document's TD: the LAM's NOA number,
/// Book I p.24) and exactly one <c>{SEQ}</c> or <c>{SEQ:n}</c> (zero-padded to n digits,
/// 1–12). A pattern with <c>{TDCOUNT}</c> takes its number from the TD and has no
/// <c>{SEQ}</c> (docs/analysis/identification-numbering.md §4.1).
/// Examples: <c>TD-{MUN}-{YEAR}-{SEQ:5}</c> → <c>TD-0402-2026-00017</c>;
/// the MRPAAO PIN <c>{LGUIDX}-{MUNIDX}-{BRGYIDX}-{SECT}-{SEQ:2}</c> →
/// <c>020-15-0005-002-05</c>, where the sequence is the parcel number; the
/// MRPAAO ARPN (Ch. II §2 E.14) <c>{MUNIDX}-{BRGYIDX}-{REV}{SEQ:5}</c> →
/// <c>01-0001-00001</c>, restarting at <c>00001</c> with each general revision.
/// Sequences run separately per <see cref="ScopeKey"/> — the pattern with the
/// sequence left out — so <c>{YEAR}</c> restarts numbering each year and
/// <c>{SECT}</c> numbers each section's parcels on their own.
/// </summary>
public static partial class NumberPattern
{
    private const string SequencePlaceholder = "#";

    [GeneratedRegex(@"\{([A-Z]+)(?::(\d+))?\}")]
    private static partial Regex TokenRegex();

    private static readonly string[] ContextTokens = ["YEAR", "PROV", "MUN", "BRGY", "LGUIDX", "MUNIDX", "BRGYIDX", "SECT", "REV", "GRYEAR", "TDCOUNT"];

    /// <summary>Tokens that take a width, like <c>{SEQ:n}</c>.</summary>
    private static readonly string[] PaddedTokens = ["SEQ", "TDCOUNT"];

    /// <summary>Tokens that scope the sequence but are not printed in the number.</summary>
    private static readonly string[] ScopeOnlyTokens = ["REV"];

    /// <summary>Why <paramref name="pattern"/> is unusable, or null when it is valid.</summary>
    public static string? Validate(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return "pattern is required";
        }
        var sequences = 0;
        var tdCounts = 0;
        foreach (Match m in TokenRegex().Matches(pattern))
        {
            var name = m.Groups[1].Value;
            if (PaddedTokens.Contains(name))
            {
                sequences += name == "SEQ" ? 1 : 0;
                tdCounts += name == "TDCOUNT" ? 1 : 0;
                if (m.Groups[2].Success && int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) is < 1 or > 12)
                {
                    return $"{{{name}:n}} padding must be between 1 and 12";
                }
            }
            else if (!ContextTokens.Contains(name) || m.Groups[2].Success)
            {
                return $"unknown token {m.Value}; allowed: {{YEAR}} {{PROV}} {{MUN}} {{BRGY}} {{LGUIDX}} {{MUNIDX}} {{BRGYIDX}} {{SECT}} {{REV}} {{GRYEAR}} {{TDCOUNT}} {{TDCOUNT:n}} {{SEQ}} {{SEQ:n}}";
            }
        }
        if (tdCounts > 1 || tdCounts == 1 && sequences != 0)
        {
            return "a pattern with {TDCOUNT} takes its number from the TD: use it once and without {SEQ}";
        }
        if (tdCounts == 0 && sequences != 1)
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

    /// <summary>Whether the pattern takes its number from the TD (<c>{TDCOUNT}</c>) instead of a sequence of its own.</summary>
    public static bool IsDerived(string pattern) => TokenRegex().Matches(pattern).Any(m => m.Groups[1].Value == "TDCOUNT");

    /// <summary>The pattern with its sequence printed as zeros: the LAM's parcel 000 for structures over water (Book II p.38).</summary>
    public static string FormatZero(string pattern, NumberContext context) => Render(pattern, context, sequence: 0);

    /// <summary>A derived pattern's number (no sequence is allocated).</summary>
    public static string FormatDerived(string pattern, NumberContext context) =>
        IsDerived(pattern) ? Render(pattern, context, sequence: 0) : throw new ArgumentException("The pattern has a sequence of its own.", nameof(pattern));

    /// <summary>Context values the pattern needs but <paramref name="context"/> lacks, e.g. ["{BRGY}"]; empty when complete.</summary>
    public static IReadOnlyList<string> MissingValues(string pattern, NumberContext context) =>
        TokenRegex().Matches(pattern).Select(m => m.Groups[1].Value).Distinct()
            .Where(name => name != "SEQ" && string.IsNullOrWhiteSpace(Value(name, context)))
            .Select(name => $"{{{name}}}").ToList();

    /// <summary>The pattern rendered without its sequence: numbers with the same scope key share one sequence.</summary>
    public static string ScopeKey(string pattern, NumberContext context) => Render(pattern, context, sequence: null);

    /// <summary>Whether <paramref name="sequence"/> fits the pattern's <c>{SEQ:n}</c> width (always, for an unpadded <c>{SEQ}</c>).</summary>
    public static bool Fits(string pattern, long sequence)
    {
        var m = TokenRegex().Matches(pattern).FirstOrDefault(x => x.Groups[1].Value == "SEQ");
        return m is null || !m.Groups[2].Success
            || sequence.ToString(CultureInfo.InvariantCulture).Length <= int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
    }

    /// <summary>The pattern's context tokens, e.g. ["LGUIDX", "SECT"].</summary>
    public static IReadOnlyList<string> Tokens(string pattern) =>
        TokenRegex().Matches(pattern).Select(m => m.Groups[1].Value).Where(n => n != "SEQ").Distinct().ToList();

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
            else if (sequence is null || !ScopeOnlyTokens.Contains(name))
            {
                // The scope key carries every token; a printed number leaves out the scope-only ones.
                var value = Value(name, context);
                if (name == "TDCOUNT" && m.Groups[2].Success)
                {
                    value = value?.PadLeft(int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), '0');
                }
                result.Append(ScopeOnlyTokens.Contains(name) ? $"[{name}:{value}]" : value);
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
        "LGUIDX" => context.LguIndex,
        "MUNIDX" => context.MunicipalityIndex,
        "BRGYIDX" => context.BarangayIndex,
        "SECT" => context.SectionIndex,
        "REV" or "GRYEAR" => context.RevisionYear?.ToString("D4", CultureInfo.InvariantCulture),
        "TDCOUNT" => context.TdCount?.ToString(CultureInfo.InvariantCulture),
        _ => null,
    };
}
