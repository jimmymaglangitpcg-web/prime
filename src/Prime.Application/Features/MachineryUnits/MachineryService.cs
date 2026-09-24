using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.MachineryUnits;

public sealed class MachineryService(IApplicationDbContext db, IValidator<CreateMachineryRequest> validator) : IMachineryService
{
    public async Task<Result<MachineryDto>> CreateAsync(CreateMachineryRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<MachineryDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var rpu = await db.RealPropertyUnits.FirstOrDefaultAsync(r => r.Id == request.RpuId, cancellationToken);
        if (rpu is null)
        {
            return Result.Failure<MachineryDto>("RPU_NOT_FOUND", "No RPU was found with the given id.");
        }
        if (rpu.RpuType != RpuType.Machinery)
        {
            return Result.Failure<MachineryDto>("RPU_TYPE_MISMATCH", "The specified RPU is not a Machinery RPU.");
        }
        if (await db.MachineryUnits.AnyAsync(x => x.RpuId == request.RpuId, cancellationToken))
        {
            return Result.Failure<MachineryDto>("MACHINERY_ALREADY_EXISTS_FOR_RPU", "A Machinery record already exists for this RPU.");
        }
        if (!await db.MachineryTypes.AnyAsync(x => x.Id == request.MachineryTypeId, cancellationToken))
        {
            return Result.Failure<MachineryDto>("MACHINERY_TYPE_NOT_FOUND", "The specified machinery type does not exist.");
        }

        var machinery = new Machinery
        {
            RpuId = request.RpuId,
            PropertyId = rpu.PropertyId,
            MachineryTypeId = request.MachineryTypeId,
            Description = request.Description,
            Brand = request.Brand,
            Model = request.Model,
            SerialNumber = request.SerialNumber,
            Capacity = request.Capacity,
            CapacityUnit = request.CapacityUnit,
            DateAcquired = request.DateAcquired,
            AcquisitionCost = request.AcquisitionCost,
            InstallationCost = request.InstallationCost,
            OtherCost = request.OtherCost,
            IsBrandNew = request.IsBrandNew,
            ReplacementCost = request.ReplacementCost,
            EconomicLifeYears = request.EconomicLifeYears,
            RemainingLifeYears = request.RemainingLifeYears,
        };

        db.MachineryUnits.Add(machinery);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(await MapToDto(machinery.Id, cancellationToken)
            ?? throw new InvalidOperationException("Machinery was just created but could not be reloaded."));
    }

    public async Task<Result<MachineryDto>> GetByIdAsync(Guid machineryId, CancellationToken cancellationToken = default)
    {
        var dto = await MapToDto(machineryId, cancellationToken);
        return dto is null
            ? Result.Failure<MachineryDto>("MACHINERY_NOT_FOUND", "No Machinery record was found with the given id.")
            : Result.Success(dto);
    }

    public async Task<Result<MachineryDto>> GetByRpuAsync(Guid rpuId, CancellationToken cancellationToken = default)
    {
        var entity = await IncludeReferences(db.MachineryUnits).SingleOrDefaultAsync(x => x.RpuId == rpuId, cancellationToken);
        return entity is null
            ? Result.Failure<MachineryDto>("MACHINERY_NOT_FOUND", "No Machinery record was found for this RPU.")
            : Result.Success(ProjectToDto(entity));
    }

    private async Task<MachineryDto?> MapToDto(Guid id, CancellationToken cancellationToken)
    {
        var entity = await IncludeReferences(db.MachineryUnits).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : ProjectToDto(entity);
    }

    private static IQueryable<Machinery> IncludeReferences(IQueryable<Machinery> query) => query
        .Include(x => x.MachineryType);

    private static MachineryDto ProjectToDto(Machinery x) => new(
        x.Id,
        x.RpuId,
        x.PropertyId,
        x.MachineryTypeId,
        x.MachineryType!.Name,
        x.Description,
        x.Brand,
        x.Model,
        x.SerialNumber,
        x.Capacity,
        x.CapacityUnit,
        x.DateAcquired,
        x.AcquisitionCost,
        x.InstallationCost,
        x.OtherCost,
        x.IsBrandNew,
        x.ReplacementCost,
        x.EconomicLifeYears,
        x.RemainingLifeYears,
        x.Depreciation,
        x.MarketValue,
        x.AssessedValue,
        x.Status,
        x.CreatedAt);
}
