using Microsoft.EntityFrameworkCore;
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
            r.Taxpayer?.Address)).ToList();
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
