using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.RealPropertyUnits;

public sealed class RealPropertyUnitService(IApplicationDbContext db, IValidator<CreateRpuRequest> validator) : IRealPropertyUnitService
{
    public async Task<Result<RpuDto>> CreateAsync(CreateRpuRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<RpuDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        if (!await db.Properties.AnyAsync(p => p.Id == request.PropertyId, cancellationToken))
        {
            return Result.Failure<RpuDto>("PROPERTY_NOT_FOUND", "No property was found with the given id.");
        }
        if (await db.RealPropertyUnits.AnyAsync(r => r.RpuNumber == request.RpuNumber, cancellationToken))
        {
            return Result.Failure<RpuDto>("RPU_NUMBER_DUPLICATE", $"An RPU with number '{request.RpuNumber}' already exists.");
        }
        if (request.PreviousRpuId is not null && !await db.RealPropertyUnits.AnyAsync(r => r.Id == request.PreviousRpuId, cancellationToken))
        {
            return Result.Failure<RpuDto>("PREVIOUS_RPU_NOT_FOUND", "The specified previous RPU does not exist.");
        }

        var rpu = new RealPropertyUnit
        {
            PropertyId = request.PropertyId,
            RpuNumber = request.RpuNumber,
            RpuType = request.RpuType,
            EffectivityDate = request.EffectivityDate,
            PreviousRpuId = request.PreviousRpuId,
            Status = RecordStatus.Active,
        };

        db.RealPropertyUnits.Add(rpu);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(ProjectToDto(rpu));
    }

    public async Task<Result<RpuDto>> GetByIdAsync(Guid rpuId, CancellationToken cancellationToken = default)
    {
        var rpu = await db.RealPropertyUnits.FirstOrDefaultAsync(r => r.Id == rpuId, cancellationToken);
        return rpu is null
            ? Result.Failure<RpuDto>("RPU_NOT_FOUND", "No RPU was found with the given id.")
            : Result.Success(ProjectToDto(rpu));
    }

    public async Task<Result<IReadOnlyList<RpuDto>>> ListByPropertyAsync(Guid propertyId, CancellationToken cancellationToken = default)
    {
        var items = await db.RealPropertyUnits
            .Where(r => r.PropertyId == propertyId)
            .OrderByDescending(r => r.EffectivityDate)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<RpuDto>>(items.Select(ProjectToDto).ToList());
    }

    private static RpuDto ProjectToDto(RealPropertyUnit r) => new(
        r.Id, r.PropertyId, r.RpuNumber, r.RpuType, r.Status, r.EffectivityDate, r.EndDate, r.PreviousRpuId, r.CreatedAt);
}
