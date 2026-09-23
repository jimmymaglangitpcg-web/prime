using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Buildings;

public sealed class BuildingService(IApplicationDbContext db, IValidator<CreateBuildingRequest> validator) : IBuildingService
{
    public async Task<Result<BuildingDto>> CreateAsync(CreateBuildingRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<BuildingDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var rpu = await db.RealPropertyUnits.FirstOrDefaultAsync(r => r.Id == request.RpuId, cancellationToken);
        if (rpu is null)
        {
            return Result.Failure<BuildingDto>("RPU_NOT_FOUND", "No RPU was found with the given id.");
        }
        if (rpu.RpuType != RpuType.Building)
        {
            return Result.Failure<BuildingDto>("RPU_TYPE_MISMATCH", "The specified RPU is not a Building RPU.");
        }
        if (await db.Buildings.AnyAsync(x => x.RpuId == request.RpuId, cancellationToken))
        {
            return Result.Failure<BuildingDto>("BUILDING_ALREADY_EXISTS_FOR_RPU", "A Building record already exists for this RPU.");
        }
        if (!await db.BuildingTypes.AnyAsync(x => x.Id == request.BuildingTypeId, cancellationToken))
        {
            return Result.Failure<BuildingDto>("BUILDING_TYPE_NOT_FOUND", "The specified building type does not exist.");
        }
        if (!await db.StructuralTypes.AnyAsync(x => x.Id == request.StructuralTypeId, cancellationToken))
        {
            return Result.Failure<BuildingDto>("STRUCTURAL_TYPE_NOT_FOUND", "The specified structural type does not exist.");
        }
        if (!await db.ActualUses.AnyAsync(x => x.Id == request.ActualUseId, cancellationToken))
        {
            return Result.Failure<BuildingDto>("ACTUAL_USE_NOT_FOUND", "The specified actual use does not exist.");
        }
        if (!await db.Conditions.AnyAsync(x => x.Id == request.ConditionId, cancellationToken))
        {
            return Result.Failure<BuildingDto>("CONDITION_NOT_FOUND", "The specified condition does not exist.");
        }

        var building = new Building
        {
            RpuId = request.RpuId,
            PropertyId = rpu.PropertyId,
            BuildingTypeId = request.BuildingTypeId,
            StructuralTypeId = request.StructuralTypeId,
            ActualUseId = request.ActualUseId,
            NumberOfStoreys = request.NumberOfStoreys ?? 1,
            FloorArea = request.FloorArea,
            TotalFloorArea = request.TotalFloorArea,
            YearConstructed = request.YearConstructed,
            YearCompleted = request.YearCompleted,
            ConditionId = request.ConditionId,
            CompletionPercentage = request.CompletionPercentage ?? 100m,
        };

        db.Buildings.Add(building);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(await MapToDto(building.Id, cancellationToken)
            ?? throw new InvalidOperationException("Building was just created but could not be reloaded."));
    }

    public async Task<Result<BuildingDto>> GetByIdAsync(Guid buildingId, CancellationToken cancellationToken = default)
    {
        var dto = await MapToDto(buildingId, cancellationToken);
        return dto is null
            ? Result.Failure<BuildingDto>("BUILDING_NOT_FOUND", "No Building record was found with the given id.")
            : Result.Success(dto);
    }

    public async Task<Result<BuildingDto>> GetByRpuAsync(Guid rpuId, CancellationToken cancellationToken = default)
    {
        var entity = await IncludeReferences(db.Buildings).SingleOrDefaultAsync(x => x.RpuId == rpuId, cancellationToken);
        return entity is null
            ? Result.Failure<BuildingDto>("BUILDING_NOT_FOUND", "No Building record was found for this RPU.")
            : Result.Success(ProjectToDto(entity));
    }

    private async Task<BuildingDto?> MapToDto(Guid id, CancellationToken cancellationToken)
    {
        var entity = await IncludeReferences(db.Buildings).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : ProjectToDto(entity);
    }

    private static IQueryable<Building> IncludeReferences(IQueryable<Building> query) => query
        .Include(x => x.BuildingType)
        .Include(x => x.StructuralType)
        .Include(x => x.ActualUse)
        .Include(x => x.Condition);

    private static BuildingDto ProjectToDto(Building x) => new(
        x.Id,
        x.RpuId,
        x.PropertyId,
        x.BuildingTypeId,
        x.BuildingType!.Name,
        x.StructuralTypeId,
        x.StructuralType!.Name,
        x.ActualUseId,
        x.ActualUse!.Name,
        x.NumberOfStoreys,
        x.FloorArea,
        x.TotalFloorArea,
        x.YearConstructed,
        x.YearCompleted,
        x.ConditionId,
        x.Condition!.Name,
        x.CompletionPercentage,
        x.MarketValue,
        x.Depreciation,
        x.DepreciatedValue,
        x.AssessedValue,
        x.Status,
        x.CreatedAt);
}
