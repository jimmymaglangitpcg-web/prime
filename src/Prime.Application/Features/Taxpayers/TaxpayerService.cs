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
    IValidator<AddPropertyOwnerRequest> addOwnerValidator) : ITaxpayerService
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
        if (!await db.Taxpayers.AnyAsync(t => t.Id == request.TaxpayerId, cancellationToken))
        {
            return Result.Failure<PropertyOwnerDto>("TAXPAYER_NOT_FOUND", "No taxpayer was found with the given id.");
        }
        if (!await db.OwnershipTypes.AnyAsync(o => o.Id == request.OwnershipTypeId, cancellationToken))
        {
            return Result.Failure<PropertyOwnerDto>("OWNERSHIP_TYPE_NOT_FOUND", "The specified ownership type does not exist.");
        }

        var currentTotal = await db.PropertyTaxpayers
            .Where(pt => pt.PropertyId == request.PropertyId && pt.IsCurrent)
            .SumAsync(pt => pt.OwnershipPercentage, cancellationToken);

        if (currentTotal + request.OwnershipPercentage > 100m)
        {
            return Result.Failure<PropertyOwnerDto>(
                "OWNERSHIP_PERCENTAGE_EXCEEDS_100",
                $"Current owners already hold {currentTotal}%; adding {request.OwnershipPercentage}% would exceed 100%.");
        }

        var propertyTaxpayer = new PropertyTaxpayer
        {
            PropertyId = request.PropertyId,
            TaxpayerId = request.TaxpayerId,
            OwnershipTypeId = request.OwnershipTypeId,
            OwnershipPercentage = request.OwnershipPercentage,
            StartDate = request.StartDate,
            IsCurrent = true,
        };

        db.PropertyTaxpayers.Add(propertyTaxpayer);
        await db.SaveChangesAsync(cancellationToken);

        var dto = await db.PropertyTaxpayers
            .Where(pt => pt.Id == propertyTaxpayer.Id)
            .Select(pt => new
            {
                pt.Id,
                pt.TaxpayerId,
                Taxpayer = new { pt.Taxpayer!.TaxpayerType, pt.Taxpayer.LastName, pt.Taxpayer.FirstName, pt.Taxpayer.MiddleName, pt.Taxpayer.Suffix, pt.Taxpayer.CorporateName },
                OwnershipTypeName = pt.OwnershipType!.Name,
                pt.OwnershipPercentage,
                pt.StartDate,
                pt.EndDate,
                pt.IsCurrent,
            })
            .SingleAsync(cancellationToken);

        return Result.Success(new PropertyOwnerDto(
            dto.Id,
            dto.TaxpayerId,
            TaxpayerNameFormatter.Format(dto.Taxpayer.TaxpayerType, dto.Taxpayer.LastName, dto.Taxpayer.FirstName, dto.Taxpayer.MiddleName, dto.Taxpayer.Suffix, dto.Taxpayer.CorporateName),
            dto.OwnershipTypeName,
            dto.OwnershipPercentage,
            dto.StartDate,
            dto.EndDate,
            dto.IsCurrent));
    }

    public async Task<Result<IReadOnlyList<PropertyOwnerDto>>> GetOwnershipHistoryAsync(Guid propertyId, CancellationToken cancellationToken = default)
    {
        if (!await db.Properties.AnyAsync(p => p.Id == propertyId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<PropertyOwnerDto>>("PROPERTY_NOT_FOUND", "No property was found with the given id.");
        }

        var rows = await db.PropertyTaxpayers
            .Where(pt => pt.PropertyId == propertyId)
            .OrderByDescending(pt => pt.IsCurrent).ThenByDescending(pt => pt.StartDate)
            .Select(pt => new
            {
                pt.Id,
                pt.TaxpayerId,
                Taxpayer = new { pt.Taxpayer!.TaxpayerType, pt.Taxpayer.LastName, pt.Taxpayer.FirstName, pt.Taxpayer.MiddleName, pt.Taxpayer.Suffix, pt.Taxpayer.CorporateName },
                OwnershipTypeName = pt.OwnershipType!.Name,
                pt.OwnershipPercentage,
                pt.StartDate,
                pt.EndDate,
                pt.IsCurrent,
            })
            .ToListAsync(cancellationToken);

        IReadOnlyList<PropertyOwnerDto> history = rows.Select(r => new PropertyOwnerDto(
            r.Id,
            r.TaxpayerId,
            TaxpayerNameFormatter.Format(r.Taxpayer.TaxpayerType, r.Taxpayer.LastName, r.Taxpayer.FirstName, r.Taxpayer.MiddleName, r.Taxpayer.Suffix, r.Taxpayer.CorporateName),
            r.OwnershipTypeName,
            r.OwnershipPercentage,
            r.StartDate,
            r.EndDate,
            r.IsCurrent)).ToList();

        return Result.Success(history);
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
