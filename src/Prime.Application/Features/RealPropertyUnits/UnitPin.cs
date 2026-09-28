using Microsoft.EntityFrameworkCore;
using Prime.Application.Common.Interfaces;
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
    /// <remarks>
    /// While the land still carries a temporary PIN, a unit takes the temporary PIN with
    /// the postfix from <see cref="TemporaryPostfixesAsync"/> instead, e.g. 01-0001-0001B1.
    /// </remarks>
    public static string Compose(string propertyPin, int? suffix, bool ownedSeparately, string? temporaryPostfix = null)
    {
        if (temporaryPostfix is not null)
        {
            return propertyPin + temporaryPostfix;
        }
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

    /// <summary>
    /// The temporary unit PINs (MRPAAO Ch. II §2 A.c, p.51): for each property whose
    /// current PIN is temporary, its buildings and other structures are B1, B2 … and its
    /// machinery M1, M2 …, in the order of their postscripts (else of registration).
    /// Keyed by RPU id; units of properties with any other PIN are absent.
    /// </summary>
    public static async Task<Dictionary<Guid, string>> TemporaryPostfixesAsync(IApplicationDbContext db, IReadOnlyCollection<Guid> propertyIds, CancellationToken ct)
    {
        var temporary = await db.PinAssignments
            .Where(a => propertyIds.Contains(a.PropertyId) && a.RetiredAt == null && a.Kind == PinKind.Temporary)
            .Select(a => a.PropertyId).ToListAsync(ct);
        if (temporary.Count == 0)
        {
            return [];
        }
        var units = await db.RealPropertyUnits.Where(r => temporary.Contains(r.PropertyId) && r.RpuType != RpuType.Land)
            .Select(r => new { r.Id, r.PropertyId, r.RpuType, r.PinSuffix, r.CreatedAt }).ToListAsync(ct);
        var result = new Dictionary<Guid, string>();
        foreach (var group in units.GroupBy(r => (r.PropertyId, Letter: r.RpuType == RpuType.Machinery ? "M" : "B")))
        {
            var ordinal = 0;
            foreach (var unit in group.OrderBy(r => r.PinSuffix is null).ThenBy(r => r.PinSuffix).ThenBy(r => r.CreatedAt).ThenBy(r => r.Id))
            {
                result[unit.Id] = $"{group.Key.Letter}{++ordinal}";
            }
        }
        return result;
    }

    /// <summary>One unit's temporary postfix, or null when its property does not carry a temporary PIN.</summary>
    public static async Task<string?> TemporaryPostfixAsync(IApplicationDbContext db, Guid propertyId, Guid rpuId, CancellationToken ct) =>
        (await TemporaryPostfixesAsync(db, [propertyId], ct)).GetValueOrDefault(rpuId);
}
