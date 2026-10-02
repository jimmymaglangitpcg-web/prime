using Microsoft.EntityFrameworkCore;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.RealPropertyUnits;

/// <summary>
/// Where each unit type's PIN series start (docs/analysis/mrpaao-forms-model.md §6.2;
/// docs/analysis/identification-numbering.md §4.2). The LAM (Book II pp.38–39) numbers buildings 1001 …,
/// machinery 2001 …, leasing properties (condominiums) 3001 … and mineral rights 4001 …; the numbers are
/// configuration. A type with no entry gets no series.
/// </summary>
public sealed class UnitPinOptions
{
    public const string SectionName = "UnitPin";

    public Dictionary<RpuType, int> SuffixStart { get; set; } = [];

    /// <summary>The series of buildings that are leasing properties (condominiums); LAM 3001.</summary>
    public int LeasingPropertyStart { get; set; } = 3001;

    /// <summary>Floor prefixes of a leasing property's units: F (floor), B (basement), P (parking), M (mezzanine).</summary>
    public List<string> FloorPrefixes { get; set; } = ["F", "B", "P", "M"];

    /// <summary>Which number goes in parentheses (Q6): the LAM's leasing-property number, or the MRPAAO's parcel number of a unit owned apart from the land.</summary>
    public UnitPinParentheses Parentheses { get; set; } = UnitPinParentheses.LeasingProperty;
}

/// <summary>Which number a unit PIN puts in parentheses (docs/analysis/identification-numbering.md §4.2, Q6).</summary>
public enum UnitPinParentheses
{
    /// <summary>LAM (Book II p.38): the leasing-property (condominium) number, e.g. …-005-(3001)F01-001.</summary>
    LeasingProperty = 0,
    /// <summary>MRPAAO (p.42): the parcel number when the unit is owned apart from the land, e.g. …-002-(05)-1001.</summary>
    Parcel = 1,
}

/// <summary>
/// The parts of a unit PIN after the property PIN: its own series number (building 1001, machinery 2001, mineral
/// right 4001), the leasing property's number (3001 …), the floor and the unit number.
/// </summary>
public sealed record UnitPinParts(int? Suffix, int? LeasingIndex = null, string? FloorPrefix = null, int? FloorNumber = null, int? UnitNumber = null);

public static class UnitPin
{
    /// <summary>As before step L2-2: one postscript, the MRPAAO's parentheses.</summary>
    public static string Compose(string propertyPin, int? suffix, bool ownedSeparately, string? temporaryPostfix = null) =>
        Compose(propertyPin, new UnitPinParts(suffix), ownedSeparately, UnitPinParentheses.Parcel, temporaryPostfix);

    /// <summary>
    /// The unit's full PIN (LAM Book II pp.38–39): the property PIN, then — for a unit of a leasing property —
    /// the leasing number (in parentheses under the LAM's convention), the floor and the unit number or machine
    /// number; otherwise the unit's series number. Under the MRPAAO's convention the parcel number is
    /// parenthesised when the unit is owned apart from the land. While the land carries a temporary PIN, a unit
    /// takes the temporary PIN with its postfix instead.
    /// </summary>
    public static string Compose(string propertyPin, UnitPinParts parts, bool ownedSeparately, UnitPinParentheses style, string? temporaryPostfix = null)
    {
        if (temporaryPostfix is not null)
        {
            return propertyPin + temporaryPostfix;
        }
        var pin = propertyPin;
        if (style == UnitPinParentheses.Parcel && ownedSeparately && (parts.Suffix is not null || parts.LeasingIndex is not null))
        {
            var cut = propertyPin.LastIndexOf('-');
            pin = cut < 0 ? $"({propertyPin})" : $"{propertyPin[..(cut + 1)]}({propertyPin[(cut + 1)..]})";
        }
        if (parts.LeasingIndex is { } leasing)
        {
            var result = style == UnitPinParentheses.LeasingProperty ? $"{pin}-({leasing})" : $"{pin}-{leasing}";
            if (parts.FloorNumber is { } floor)
            {
                result += $"{parts.FloorPrefix ?? "F"}{floor:D2}";
            }
            if (parts.UnitNumber is { } unit)
            {
                result += $"-{unit:D3}";
            }
            else if (parts.Suffix is { } machine)
            {
                result += $"-{machine}";
            }
            return result;
        }
        return parts.Suffix is { } suffix ? $"{pin}-{suffix}" : pin;
    }

    /// <summary>
    /// A unit's PIN parts from the units of its property (<paramref name="units"/> by id): a leasing property's
    /// own number; a unit of one takes the leasing number of its host and its own floor and unit number; a machine
    /// installed in such a unit takes the unit's leasing number and floor, then its machine number.
    /// </summary>
    public static UnitPinParts Parts(RealPropertyUnit rpu, IReadOnlyDictionary<Guid, RealPropertyUnit> units)
    {
        if (rpu.IsLeasingProperty)
        {
            return new UnitPinParts(null, rpu.PinSuffix);
        }
        var host = rpu.HostRpuId is { } hostId ? units.GetValueOrDefault(hostId) : null;
        if (rpu.FloorNumber is not null && host is { IsLeasingProperty: true })
        {
            return new UnitPinParts(null, host.PinSuffix, rpu.FloorPrefix, rpu.FloorNumber, rpu.UnitNumber);
        }
        if (host is { FloorNumber: not null } && host.HostRpuId is { } leasingId && units.GetValueOrDefault(leasingId) is { IsLeasingProperty: true } leasingProperty)
        {
            return new UnitPinParts(rpu.PinSuffix, leasingProperty.PinSuffix, host.FloorPrefix, host.FloorNumber);
        }
        return new UnitPinParts(rpu.PinSuffix);
    }

    /// <summary>One unit's full PIN, loading what it needs (its property's units, its temporary postfix).</summary>
    public static async Task<string> ForUnitAsync(IApplicationDbContext db, UnitPinOptions options, RealPropertyUnit rpu, string propertyPin,
        bool ownedSeparately, CancellationToken ct)
    {
        var units = await db.RealPropertyUnits.AsNoTracking().Where(r => r.PropertyId == rpu.PropertyId).ToDictionaryAsync(r => r.Id, ct);
        units[rpu.Id] = rpu;
        return Compose(propertyPin, Parts(rpu, units), ownedSeparately, options.Parentheses, await TemporaryPostfixAsync(db, rpu.PropertyId, rpu.Id, ct));
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
