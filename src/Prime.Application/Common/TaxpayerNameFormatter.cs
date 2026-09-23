using Prime.Domain.Enums;

namespace Prime.Application.Common;

/// <summary>
/// Single source for taxpayer display-name formatting — used by both the
/// Properties and Taxpayers features (Rule 9: don't duplicate business
/// logic, even for something this small).
/// </summary>
public static class TaxpayerNameFormatter
{
    /// <summary>"LastName, FirstName MiddleName Suffix" for individuals (conventional PH civil-registry order), or the corporate name otherwise.</summary>
    public static string Format(TaxpayerType type, string? lastName, string? firstName, string? middleName, string? suffix, string? corporateName)
    {
        if (type != TaxpayerType.Individual)
        {
            return corporateName ?? string.Empty;
        }

        var display = string.IsNullOrWhiteSpace(lastName) ? string.Empty : $"{lastName}, ";
        display += string.Join(" ", new[] { firstName, middleName, suffix }.Where(p => !string.IsNullOrWhiteSpace(p)));
        return display.Trim(' ', ',');
    }
}
