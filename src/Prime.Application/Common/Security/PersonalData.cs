namespace Prime.Application.Common.Security;

/// <summary>
/// Masking of an individual taxpayer's personal data for users without <see cref="Permissions.TaxpayerViewPersonal"/>
/// (CLAUDE.md §68; docs/analysis/workflow-security.md §4.5, Q16). Names stay visible: they are on the TD and the rolls.
/// Only API responses are masked; forms and notices are official records and print what the record holds.
/// </summary>
public static class PersonalData
{
    /// <summary>Shown in place of a hidden address.</summary>
    public const string HiddenAddress = "(hidden)";

    /// <summary>"123-456-789-000" becomes "***-***-***-000": the last three digits stay, enough to tell two apart.</summary>
    public static string? MaskTin(string? tin) => KeepLastDigits(tin, 3);

    /// <summary>The last four digits stay.</summary>
    public static string? MaskContact(string? contact) => KeepLastDigits(contact, 4);

    /// <summary>"juan.cruz@example.ph" becomes "j***@example.ph".</summary>
    public static string? MaskEmail(string? email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return email;
        }
        var at = email.IndexOf('@');
        return at <= 0 ? "***" : $"{email[0]}***{email[at..]}";
    }

    public static string? MaskAddress(string? address) => string.IsNullOrEmpty(address) ? address : HiddenAddress;

    private static string? KeepLastDigits(string? value, int keep)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }
        var digits = value.Count(char.IsDigit);
        var seen = 0;
        return string.Concat(value.Select(ch =>
        {
            if (char.IsDigit(ch))
            {
                return ++seen > digits - keep ? ch : '*';
            }
            return char.IsLetter(ch) ? '*' : ch; // separators stay, so the shape is kept
        }));
    }
}
