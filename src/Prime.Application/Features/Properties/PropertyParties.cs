using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common.Interfaces;
using Prime.Application.Common;
using Prime.Application.Common.Security;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Properties;

/// <summary>
/// The one projection of property parties (owners, administrators, unknown
/// owner …) used by the profile, ownership history and forms, so a party is
/// named the same way everywhere.
/// </summary>
public static class PropertyParties
{
    public const string UnknownOwnerName = "Unknown owner (declared under LGC §204)";

    /// <summary>
    /// The parties of one unit matching <paramref name="when"/> (e.g. current, or
    /// as of a date): the unit's own rows when it has an owner or unknown-owner
    /// row among them, otherwise the whole property's rows
    /// (docs/analysis/mrpaao-forms-model.md §6.4). A null <paramref name="rpuId"/>
    /// gives the whole property's rows.
    /// </summary>
    public static async Task<IQueryable<PropertyTaxpayer>> ScopeAsync(IApplicationDbContext db, Guid propertyId, Guid? rpuId,
        Expression<Func<PropertyTaxpayer, bool>> when, CancellationToken ct)
    {
        var rows = db.PropertyTaxpayers.Where(x => x.PropertyId == propertyId).Where(when);
        if (rpuId is { } unit && await rows.AnyAsync(x => x.RpuId == unit
                && (x.Role == PropertyPartyRole.Owner || x.Role == PropertyPartyRole.UnknownOwner), ct))
        {
            return rows.Where(x => x.RpuId == unit);
        }
        return rows.Where(x => x.RpuId == null);
    }

    /// <summary>
    /// <see cref="ScopeAsync"/> over rows already loaded and filtered (the registers read a barangay's parties at once,
    /// production-hardening.md §9, H4): the unit's own rows when it has an owner or unknown-owner row among them,
    /// otherwise the property's.
    /// </summary>
    public static IEnumerable<PartyRow> Scope(IEnumerable<PartyRow> rows, Guid? rpuId)
    {
        var list = rows as IReadOnlyCollection<PartyRow> ?? rows.ToList();
        return rpuId is { } unit && list.Any(x => x.RpuId == unit && x.Role is PropertyPartyRole.Owner or PropertyPartyRole.UnknownOwner)
            ? list.Where(x => x.RpuId == unit)
            : list.Where(x => x.RpuId == null);
    }

    /// <summary>A party row as read for naming: the projection both <see cref="ProjectAsync"/> and the bulk reads use.</summary>
    public sealed record PartyRow(Guid Id, Guid PropertyId, PropertyPartyRole Role, Guid? TaxpayerId, TaxpayerType? TaxpayerType,
        string? LastName, string? FirstName, string? MiddleName, string? Suffix, string? CorporateName, string? Address,
        string? OwnershipTypeName, decimal OwnershipPercentage, DateOnly StartDate, DateOnly? EndDate, bool IsCurrent, string? EndReason,
        Guid? RpuId, string? RpuNumber);

    /// <summary>The rows of <paramref name="query"/> as <see cref="PartyRow"/>s, unordered.</summary>
    public static IQueryable<PartyRow> Rows(IQueryable<PropertyTaxpayer> query) => query.Select(pt => new PartyRow(
        pt.Id, pt.PropertyId, pt.Role, pt.TaxpayerId, pt.Taxpayer == null ? null : pt.Taxpayer.TaxpayerType,
        pt.Taxpayer == null ? null : pt.Taxpayer.LastName, pt.Taxpayer == null ? null : pt.Taxpayer.FirstName,
        pt.Taxpayer == null ? null : pt.Taxpayer.MiddleName, pt.Taxpayer == null ? null : pt.Taxpayer.Suffix,
        pt.Taxpayer == null ? null : pt.Taxpayer.CorporateName, pt.Taxpayer == null ? null : pt.Taxpayer.Address,
        pt.OwnershipType == null ? null : pt.OwnershipType.Name, pt.OwnershipPercentage, pt.StartDate, pt.EndDate, pt.IsCurrent, pt.EndReason,
        pt.RpuId, pt.Rpu == null ? null : pt.Rpu.RpuNumber));

    /// <summary>The order <see cref="ProjectAsync"/> lists parties in: current first, by role (as stored, its name), latest first.</summary>
    public static IEnumerable<PartyRow> Ordered(IEnumerable<PartyRow> rows) => rows
        .OrderByDescending(r => r.IsCurrent).ThenBy(r => r.Role.ToString(), StringComparer.Ordinal).ThenByDescending(r => r.StartDate);

    /// <summary>One party as listed and printed; see <see cref="ProjectAsync"/> for <paramref name="maskPersonal"/>.</summary>
    public static PropertyOwnerDto ToDto(PartyRow r, bool maskPersonal = false)
    {
        var masked = maskPersonal && r.TaxpayerType == TaxpayerType.Individual;
        return new PropertyOwnerDto(
            r.Id,
            r.TaxpayerId,
            r.TaxpayerType is { } type
                ? TaxpayerNameFormatter.Format(type, r.LastName, r.FirstName, r.MiddleName, r.Suffix, r.CorporateName)
                : UnknownOwnerName,
            r.OwnershipTypeName,
            r.OwnershipPercentage,
            r.StartDate,
            r.EndDate,
            r.IsCurrent,
            r.Role,
            r.EndReason,
            masked ? PersonalData.MaskAddress(r.Address) : r.Address,
            r.RpuId,
            r.RpuNumber,
            masked && r.Address is not null);
    }

    /// <param name="maskPersonal">For API responses to a user without taxpayer.view-personal: an individual's address is
    /// hidden (workflow-security.md Q16). Forms and notices never mask: they are official records.</param>
    public static async Task<List<PropertyOwnerDto>> ProjectAsync(IQueryable<PropertyTaxpayer> query, CancellationToken ct, bool maskPersonal = false) =>
        Ordered(await Rows(query).ToListAsync(ct)).Select(r => ToDto(r, maskPersonal)).ToList();

    /// <summary>Printed label of a role — English defaults until the LAM's wording is configured.</summary>
    public static string RoleLabel(PropertyPartyRole role) => role switch
    {
        PropertyPartyRole.Owner => "Owner",
        PropertyPartyRole.Administrator => "Administrator",
        PropertyPartyRole.LegalInterestHolder => "Person with legal interest",
        PropertyPartyRole.BeneficialUser => "Beneficial user",
        PropertyPartyRole.Claimant => "Claimant",
        PropertyPartyRole.UnknownOwner => "Unknown owner",
        _ => role.ToString(),
    };
}
