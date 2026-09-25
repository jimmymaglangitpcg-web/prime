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

    public async Task<Result<BuildingDto>> AddUsePortionAsync(Guid buildingId, AddBuildingUsePortionRequest request, CancellationToken cancellationToken = default)
    {
        var building = await db.Buildings.Include(x => x.UsePortions).FirstOrDefaultAsync(x => x.Id == buildingId, cancellationToken);
        if (building is null)
        {
            return NotFound();
        }
        if (request.FloorArea <= 0)
        {
            return Fail("VALIDATION_FAILED", "The floor area must be greater than zero.");
        }
        if (!await db.Classifications.AnyAsync(x => x.Id == request.ClassificationId, cancellationToken))
        {
            return Fail("CLASSIFICATION_NOT_FOUND", "The specified classification does not exist.");
        }
        if (!await db.ActualUses.AnyAsync(x => x.Id == request.ActualUseId, cancellationToken))
        {
            return Fail("ACTUAL_USE_NOT_FOUND", "The specified actual use does not exist.");
        }
        if (building.UsePortions.Any(x => x.ClassificationId == request.ClassificationId && x.ActualUseId == request.ActualUseId))
        {
            return Fail("BUILDING_USE_PORTION_DUPLICATE", "A portion under this classification and actual use already exists.");
        }
        var covered = building.UsePortions.Sum(x => x.FloorArea) + request.FloorArea;
        if (covered > building.TotalFloorArea)
        {
            return Fail("BUILDING_USE_PORTIONS_EXCEED_FLOOR_AREA",
                $"The portions would cover {covered:#,0.####} sqm, more than the building's {building.TotalFloorArea:#,0.####} sqm total floor area.");
        }
        var portion = new BuildingUsePortion
        {
            BuildingId = building.Id, Sequence = building.UsePortions.Count == 0 ? 1 : building.UsePortions.Max(x => x.Sequence) + 1,
            ClassificationId = request.ClassificationId, ActualUseId = request.ActualUseId, FloorArea = request.FloorArea,
        };
        db.BuildingUsePortions.Add(portion);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success((await MapToDto(buildingId, cancellationToken))!);
    }

    public async Task<Result<BuildingDto>> AddComponentAsync(Guid buildingId, AddBuildingComponentRequest request, CancellationToken cancellationToken = default)
    {
        var building = await db.Buildings.Include(x => x.UsePortions).FirstOrDefaultAsync(x => x.Id == buildingId, cancellationToken);
        if (building is null)
        {
            return NotFound();
        }
        if (request.Quantity < 0 || request.UnitCost < 0 || request.Cost < 0 || request.Description?.Length > 500)
        {
            return Fail("VALIDATION_FAILED", "Quantity, unit cost and cost cannot be negative; description max 500.");
        }
        var cost = request.Cost ?? (request.Quantity is { } q && request.UnitCost is { } u ? Math.Round(q * u, 2, MidpointRounding.AwayFromZero) : null);
        if (request.IsAdditionalItem && cost is null)
        {
            return Fail("VALIDATION_FAILED", "An additional item needs a cost, or a quantity and unit cost.");
        }
        if (request.BuildingUsePortionId is { } portionId && (!request.IsAdditionalItem || building.UsePortions.All(x => x.Id != portionId)))
        {
            return Fail("BUILDING_USE_PORTION_NOT_FOUND", "Only an additional item can name a use portion, and it must be one of this building's.");
        }
        if (!await db.BuildingComponentTypes.AnyAsync(x => x.Id == request.ComponentTypeId && x.IsActive, cancellationToken))
        {
            return Fail("COMPONENT_TYPE_NOT_FOUND", "The specified component type does not exist or is inactive.");
        }
        db.BuildingComponents.Add(new BuildingComponent
        {
            BuildingId = building.Id, ComponentTypeId = request.ComponentTypeId,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Quantity = request.Quantity, UnitCost = request.UnitCost, Cost = cost,
            IsAdditionalItem = request.IsAdditionalItem, BuildingUsePortionId = request.BuildingUsePortionId,
        });
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success((await MapToDto(buildingId, cancellationToken))!);
    }

    private static Result<BuildingDto> NotFound() => Fail("BUILDING_NOT_FOUND", "No Building record was found with the given id.");

    private static Result<BuildingDto> Fail(string code, string message) => Result.Failure<BuildingDto>(code, message);

    private async Task<BuildingDto?> MapToDto(Guid id, CancellationToken cancellationToken)
    {
        var entity = await IncludeReferences(db.Buildings).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : ProjectToDto(entity);
    }

    private static IQueryable<Building> IncludeReferences(IQueryable<Building> query) => query
        .Include(x => x.BuildingType)
        .Include(x => x.StructuralType)
        .Include(x => x.ActualUse)
        .Include(x => x.Condition)
        .Include(x => x.UsePortions).ThenInclude(p => p.Classification)
        .Include(x => x.UsePortions).ThenInclude(p => p.ActualUse)
        .Include(x => x.Components).ThenInclude(c => c.ComponentType)
        .Include(x => x.Floors)
        .Include(x => x.Materials).ThenInclude(m => m.StructuralPart)
        .Include(x => x.Materials).ThenInclude(m => m.StructuralMaterial);

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
        x.CreatedAt,
        x.UsePortions.OrderBy(p => p.Sequence).Select(p => new BuildingUsePortionDto(
            p.Id, p.Sequence, p.ClassificationId, p.Classification!.Name, p.ActualUseId, p.ActualUse!.Name, p.FloorArea)).ToList(),
        x.Components.OrderBy(c => c.CreatedAt).Select(c => new BuildingComponentDto(
            c.Id, c.ComponentTypeId, c.ComponentType!.Name, c.Description, c.Quantity, c.UnitCost, c.Cost, c.IsAdditionalItem, c.BuildingUsePortionId)).ToList(),
        x.BuildingPermitNumber, x.BuildingPermitDate, x.CondominiumCertificateNumber, x.CertificateOfCompletionDate, x.CertificateOfOccupancyDate,
        x.DateConstructed, x.DateOccupied,
        x.Floors.OrderBy(f => f.FloorNumber).Select(f => new BuildingFloorDto(f.Id, f.FloorNumber, f.Area)).ToList(),
        x.Materials.OrderBy(m => m.StructuralPart!.SortOrder).ThenBy(m => m.StructuralPart!.Name).ThenBy(m => m.FloorNumber)
            .Select(m => new BuildingMaterialDto(m.Id, m.StructuralPartId, m.StructuralPart!.Name, m.StructuralMaterialId,
                m.StructuralMaterial != null ? m.StructuralMaterial.Name : m.OtherSpecify!, m.FloorNumber)).ToList());
}
