using System.Text.RegularExpressions;

namespace Prime.Application.Features.PropertyIdentification;

/// <summary>
/// "Pin" configuration (docs/analysis/identification-numbering.md §4.1, Q1): the barangay index number has
/// four digits in one place of the LAM and three in another (Book II p.36), so its width is the province's choice.
/// </summary>
public sealed class PinOptions
{
    public const string SectionName = "Pin";

    /// <summary>3 or 4; 4 by default.</summary>
    public int BarangayIndexDigits { get; set; } = 4;

    /// <summary>A barangay index number of the configured width.</summary>
    public bool IsBarangayIndex(string? number) => number is not null && Regex.IsMatch(number, $"^[0-9]{{{BarangayIndexDigits}}}$");
}
