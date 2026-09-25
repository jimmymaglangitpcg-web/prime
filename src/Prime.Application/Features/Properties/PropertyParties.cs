using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common.Interfaces;
using Prime.Application.Common;
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

    public static async Task<List<PropertyOwnerDto>> ProjectAsync(IQueryable<PropertyTaxpayer> query, CancellationToken ct)
    {
        var rows = await query
            .OrderByDescending(pt => pt.IsCurrent).ThenBy(pt => pt.Role).ThenByDescending(pt => pt.StartDate)
            .Select(pt => new
            {
                pt.Id,
                pt.Role,
                pt.TaxpayerId,
                Taxpayer = pt.Taxpayer == null ? null : new
                {
                    pt.Taxpayer.TaxpayerType, pt.Taxpayer.LastName, pt.Taxpayer.FirstName, pt.Taxpayer.MiddleName,
                    pt.Taxpayer.Suffix, pt.Taxpayer.CorporateName, pt.Taxpayer.Address,
                },
                OwnershipTypeName = pt.OwnershipType == null ? null : pt.OwnershipType.Name,
                pt.OwnershipPercentage,
                pt.StartDate,
                pt.EndDate,
                pt.IsCurrent,
                pt.EndReason,
                pt.RpuId,
                RpuNumber = pt.Rpu == null ? null : pt.Rpu.RpuNumber,
            })
            .ToListAsync(ct);

        return rows.Select(r => new PropertyOwnerDto(
            r.Id,
            r.TaxpayerId,
            r.Taxpayer is { } t
                ? TaxpayerNameFormatter.Format(t.TaxpayerType, t.LastName, t.FirstName, t.MiddleName, t.Suffix, t.CorporateName)
                : UnknownOwnerName,
            r.OwnershipTypeName,
            r.OwnershipPercentage,
            r.StartDate,
            r.EndDate,
            r.IsCurrent,
            r.Role,
            r.EndReason,
            r.Taxpayer?.Address,
            r.RpuId,
            r.RpuNumber)).ToList();
    }

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
