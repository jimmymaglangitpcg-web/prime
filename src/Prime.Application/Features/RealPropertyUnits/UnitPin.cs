using Prime.Domain.Enums;

namespace Prime.Application.Features.RealPropertyUnits;

/// <summary>
/// Where each unit type's PIN postscripts start (docs/analysis/mrpaao-forms-model.md
/// §6.2). The MRPAAO (p.42) uses 1001 … for buildings and 2001 … for
/// machinery; the numbers are configuration, since the LAM may differ
/// (DOMAIN VERIFICATION REQUIRED). A type with no entry gets no postscript.
/// </summary>
public sealed class UnitPinOptions
{
    public const string SectionName = "UnitPin";

    public Dictionary<RpuType, int> SuffixStart { get; set; } = [];
}

public static class UnitPin
{
    /// <summary>
    /// The unit's full PIN: the property PIN, then the postscript. When the
    /// unit has owners of its own (owned apart from the land), the parcel
    /// number — the PIN's last segment — is parenthesised, e.g.
    /// 020-15-0005-002-(05)-1001 (MRPAAO p.42). Land and units without a
    /// postscript carry the property PIN.
    /// </summary>
    public static string Compose(string propertyPin, int? suffix, bool ownedSeparately)
    {
        if (suffix is null)
        {
            return propertyPin;
        }
        var pin = propertyPin;
        if (ownedSeparately)
        {
            var cut = propertyPin.LastIndexOf('-');
            pin = cut < 0 ? $"({propertyPin})" : $"{propertyPin[..(cut + 1)]}({propertyPin[(cut + 1)..]})";
        }
        return $"{pin}-{suffix}";
    }
}
