using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Properties;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Taxpayers;

public sealed class TaxpayerService(
    IApplicationDbContext db,
    IValidator<CreateTaxpayerRequest> createValidator,
    IValidator<AddPropertyOwnerRequest> addOwnerValidator,
    ICurrentUserService currentUser) : ITaxpayerService
{
    public async Task<Result<TaxpayerDto>> CreateAsync(CreateTaxpayerRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<TaxpayerDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var taxpayer = new Taxpayer
        {
            TaxpayerType = request.TaxpayerType,
            LastName = request.LastName,
            FirstName = request.FirstName,
            MiddleName = request.MiddleName,
            Suffix = request.Suffix,
            CorporateName = request.CorporateName,
            Tin = request.Tin,
            Address = request.Address,
            BarangayId = request.BarangayId,
            MunicipalityId = request.MunicipalityId,
            ProvinceId = request.ProvinceId,
            ContactNumber = request.ContactNumber,
            Email = request.Email,
            Status = RecordStatus.Active,
        };

        db.Taxpayers.Add(taxpayer);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(ProjectToDto(taxpayer));
    }

    public async Task<Result<TaxpayerDto>> GetByIdAsync(Guid taxpayerId, CancellationToken cancellationToken = default)
    {
        var taxpayer = await db.Taxpayers.FirstOrDefaultAsync(t => t.Id == taxpayerId, cancellationToken);
        return taxpayer is null
            ? Result.Failure<TaxpayerDto>("TAXPAYER_NOT_FOUND", "No taxpayer was found with the given id.")
            : Result.Success(ProjectToDto(taxpayer));
    }

    public async Task<Result<PagedResult<TaxpayerDto>>> SearchAsync(TaxpayerSearchRequest request, CancellationToken cancellationToken = default)
    {
        var query = db.Taxpayers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            // .ToLower().Contains() (not EF.Functions.ILike) — keeps
            // Application decoupled from the Npgsql-specific provider.
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(t =>
                (t.LastName != null && t.LastName.ToLower().Contains(term)) ||
                (t.FirstName != null && t.FirstName.ToLower().Contains(term)) ||
                (t.CorporateName != null && t.CorporateName.ToLower().Contains(term)) ||
                (t.Tin != null && t.Tin.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(t => t.LastName).ThenBy(t => t.CorporateName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<TaxpayerDto>
        {
            Items = items.Select(ProjectToDto).ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
        });
    }

    /// <summary>
    /// Adds a party in a statutory capacity (LGC §§204–205). Only owners carry
    /// an ownership type and count toward 100%. An unknown-owner declaration
    /// cannot coexist with current owners; it is ended automatically ("owner
    /// identified") when the first owner is added.
    /// </summary>
    public async Task<Result<PropertyOwnerDto>> AddOwnerAsync(AddPropertyOwnerRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await addOwnerValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<PropertyOwnerDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        if (!await db.Properties.AnyAsync(p => p.Id == request.PropertyId, cancellationToken))
        {
            return Result.Failure<PropertyOwnerDto>("PROPERTY_NOT_FOUND", "No property was found with the given id.");
        }
        if (request.TaxpayerId is { } taxpayerId && !await db.Taxpayers.AnyAsync(t => t.Id == taxpayerId, cancellationToken))
        {
            return Result.Failure<PropertyOwnerDto>("TAXPAYER_NOT_FOUND", "No taxpayer was found with the given id.");
        }
        if (request.OwnershipTypeId is { } typeId && !await db.OwnershipTypes.AnyAsync(o => o.Id == typeId, cancellationToken))
        {
            return Result.Failure<PropertyOwnerDto>("OWNERSHIP_TYPE_NOT_FOUND", "The specified ownership type does not exist.");
        }

        // A party of the whole property, or of one unit owned apart from the land (MRPAAO p.42).
        if (request.RpuId is { } unitId && !await db.RealPropertyUnits.AnyAsync(
                r => r.Id == unitId && r.PropertyId == request.PropertyId && r.RpuType != RpuType.Land, cancellationToken))
        {
            return Result.Failure<PropertyOwnerDto>("PROPERTY_PARTY_UNIT_INVALID",
                "The unit must be a building, machinery or other-improvement RPU of the property; the land's parties are the property's.");
        }
        // Shares and the unknown-owner rule apply within the same scope.
        var current = await db.PropertyTaxpayers
            .Where(pt => pt.PropertyId == request.PropertyId && pt.RpuId == request.RpuId && pt.IsCurrent)
            .ToListAsync(cancellationToken);

        if (request.Role == PropertyPartyRole.UnknownOwner && current.Any(pt => pt.Role == PropertyPartyRole.Owner))
        {
            return Result.Failure<PropertyOwnerDto>("PROPERTY_HAS_KNOWN_OWNER",
                "The property has current owners; it cannot also be declared against an unknown owner (LGC §204).");
        }
        if (request.Role == PropertyPartyRole.UnknownOwner && current.Any(pt => pt.Role == PropertyPartyRole.UnknownOwner))
        {
            return Result.Failure<PropertyOwnerDto>("UNKNOWN_OWNER_ALREADY_DECLARED", "The property is already declared against an unknown owner.");
        }
        if (request.TaxpayerId is { } tp && current.Any(pt => pt.TaxpayerId == tp && pt.Role == request.Role))
        {
            return Result.Failure<PropertyOwnerDto>("PROPERTY_PARTY_DUPLICATE", "This taxpayer is already a current party in that capacity.");
        }

        if (request.Role == PropertyPartyRole.Owner)
        {
            var currentTotal = current.Where(pt => pt.Role == PropertyPartyRole.Owner).Sum(pt => pt.OwnershipPercentage);
            if (currentTotal + request.OwnershipPercentage > 100m)
            {
                return Result.Failure<PropertyOwnerDto>(
                    "OWNERSHIP_PERCENTAGE_EXCEEDS_100",
                    $"Current owners already hold {currentTotal}%; adding {request.OwnershipPercentage}% would exceed 100%.");
            }
            foreach (var unknown in current.Where(pt => pt.Role == PropertyPartyRole.UnknownOwner))
            {
                if (request.StartDate <= unknown.StartDate)
                {
                    return Result.Failure<PropertyOwnerDto>("OWNER_START_DATE_CONFLICT",
                        $"The unknown-owner declaration starts {unknown.StartDate:yyyy-MM-dd}; the identified owner must start after it.");
                }
                unknown.IsCurrent = false;
                unknown.EndDate = request.StartDate.AddDays(-1);
                unknown.EndReason = "Owner identified";
            }
        }

        var propertyTaxpayer = new PropertyTaxpayer
        {
            PropertyId = request.PropertyId,
            RpuId = request.RpuId,
            Role = request.Role,
            TaxpayerId = request.TaxpayerId,
            OwnershipTypeId = request.OwnershipTypeId,
            OwnershipPercentage = request.OwnershipPercentage,
            StartDate = request.StartDate,
            IsCurrent = true,
        };

        db.PropertyTaxpayers.Add(propertyTaxpayer);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success((await PropertyParties.ProjectAsync(db.PropertyTaxpayers.Where(pt => pt.Id == propertyTaxpayer.Id), cancellationToken)).Single());
    }

    /// <summary>Ends a party's current link (history is kept). Transfers that replace owners come with PropertyTransaction (plan A5).</summary>
    public async Task<Result<PropertyOwnerDto>> EndPartyAsync(Guid propertyTaxpayerId, EndPropertyPartyRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500)
        {
            return Result.Failure<PropertyOwnerDto>("VALIDATION_FAILED", "A reason is required (max 500).");
        }
        var row = await db.PropertyTaxpayers.FirstOrDefaultAsync(pt => pt.Id == propertyTaxpayerId, cancellationToken);
        if (row is null)
        {
            return Result.Failure<PropertyOwnerDto>("PROPERTY_PARTY_NOT_FOUND", "No property party link was found with the given id.");
        }
        if (!row.IsCurrent)
        {
            return Result.Failure<PropertyOwnerDto>("PROPERTY_PARTY_ALREADY_ENDED", "This party link has already ended.");
        }
        if (request.EndDate < row.StartDate)
        {
            return Result.Failure<PropertyOwnerDto>("VALIDATION_FAILED", $"endDate cannot be before the start date {row.StartDate:yyyy-MM-dd}.");
        }

        row.IsCurrent = false;
        row.EndDate = request.EndDate;
        row.EndReason = request.Reason;
        currentUser.Reason = request.Reason;
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success((await PropertyParties.ProjectAsync(db.PropertyTaxpayers.Where(pt => pt.Id == row.Id), cancellationToken)).Single());
    }

    public async Task<Result<IReadOnlyList<PropertyOwnerDto>>> GetOwnershipHistoryAsync(Guid propertyId, CancellationToken cancellationToken = default)
    {
        if (!await db.Properties.AnyAsync(p => p.Id == propertyId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<PropertyOwnerDto>>("PROPERTY_NOT_FOUND", "No property was found with the given id.");
        }
        return Result.Success<IReadOnlyList<PropertyOwnerDto>>(
            await PropertyParties.ProjectAsync(db.PropertyTaxpayers.Where(pt => pt.PropertyId == propertyId), cancellationToken));
    }

    private static TaxpayerDto ProjectToDto(Taxpayer t) => new(
        t.Id,
        t.TaxpayerType,
        TaxpayerNameFormatter.Format(t.TaxpayerType, t.LastName, t.FirstName, t.MiddleName, t.Suffix, t.CorporateName),
        t.LastName,
        t.FirstName,
        t.MiddleName,
        t.Suffix,
        t.CorporateName,
        t.Tin,
        t.Address,
        t.ContactNumber,
        t.Email,
        t.Status,
        t.CreatedAt);
}
