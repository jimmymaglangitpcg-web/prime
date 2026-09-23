using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Lands;

public sealed class LandService(IApplicationDbContext db, IValidator<CreateLandRequest> validator) : ILandService
{
    public async Task<Result<LandDto>> CreateAsync(CreateLandRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<LandDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var rpu = await db.RealPropertyUnits.FirstOrDefaultAsync(r => r.Id == request.RpuId, cancellationToken);
        if (rpu is null)
        {
            return Result.Failure<LandDto>("RPU_NOT_FOUND", "No RPU was found with the given id.");
        }
        if (rpu.RpuType != RpuType.Land)
        {
            return Result.Failure<LandDto>("RPU_TYPE_MISMATCH", "The specified RPU is not a Land RPU.");
        }
        if (await db.Lands.AnyAsync(x => x.RpuId == request.RpuId, cancellationToken))
        {
            return Result.Failure<LandDto>("LAND_ALREADY_EXISTS_FOR_RPU", "A Land record already exists for this RPU.");
        }
        if (!await db.Classifications.AnyAsync(x => x.Id == request.ClassificationId, cancellationToken))
        {
            return Result.Failure<LandDto>("CLASSIFICATION_NOT_FOUND", "The specified classification does not exist.");
        }
        if (!await db.ActualUses.AnyAsync(x => x.Id == request.ActualUseId, cancellationToken))
        {
            return Result.Failure<LandDto>("ACTUAL_USE_NOT_FOUND", "The specified actual use does not exist.");
        }
        if (request.SubClassificationId is not null && !await db.SubClassifications.AnyAsync(x => x.Id == request.SubClassificationId, cancellationToken))
        {
            return Result.Failure<LandDto>("SUB_CLASSIFICATION_NOT_FOUND", "The specified sub-classification does not exist.");
        }
        if (request.ZoneId is not null && !await db.Zones.AnyAsync(x => x.Id == request.ZoneId, cancellationToken))
        {
            return Result.Failure<LandDto>("ZONE_NOT_FOUND", "The specified zone does not exist.");
        }
        if (request.RoadTypeId is not null && !await db.RoadTypes.AnyAsync(x => x.Id == request.RoadTypeId, cancellationToken))
        {
            return Result.Failure<LandDto>("ROAD_TYPE_NOT_FOUND", "The specified road type does not exist.");
        }

        var land = new Domain.Entities.Land
        {
            RpuId = request.RpuId,
            PropertyId = rpu.PropertyId,
            Area = request.Area,
            AreaUnit = string.IsNullOrWhiteSpace(request.AreaUnit) ? "sqm" : request.AreaUnit,
            ClassificationId = request.ClassificationId,
            ActualUseId = request.ActualUseId,
            SubClassificationId = request.SubClassificationId,
            ZoneId = request.ZoneId,
            LocationFactor = request.LocationFactor,
            RoadFrontage = request.RoadFrontage,
            RoadTypeId = request.RoadTypeId,
            IsCornerLot = request.IsCornerLot,
            Zoning = request.Zoning,
        };

        db.Lands.Add(land);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(await MapToDto(land.Id, cancellationToken)
            ?? throw new InvalidOperationException("Land was just created but could not be reloaded."));
    }

    public async Task<Result<LandDto>> GetByIdAsync(Guid landId, CancellationToken cancellationToken = default)
    {
        var dto = await MapToDto(landId, cancellationToken);
        return dto is null
            ? Result.Failure<LandDto>("LAND_NOT_FOUND", "No Land record was found with the given id.")
            : Result.Success(dto);
    }

    public async Task<Result<LandDto>> GetByRpuAsync(Guid rpuId, CancellationToken cancellationToken = default)
    {
        var entity = await IncludeReferences(db.Lands).SingleOrDefaultAsync(x => x.RpuId == rpuId, cancellationToken);
        return entity is null
            ? Result.Failure<LandDto>("LAND_NOT_FOUND", "No Land record was found for this RPU.")
            : Result.Success(ProjectToDto(entity));
    }

    private async Task<LandDto?> MapToDto(Guid id, CancellationToken cancellationToken)
    {
        var entity = await IncludeReferences(db.Lands).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : ProjectToDto(entity);
    }

    private static IQueryable<Domain.Entities.Land> IncludeReferences(IQueryable<Domain.Entities.Land> query) => query
        .Include(x => x.Classification)
        .Include(x => x.ActualUse);

    private static LandDto ProjectToDto(Domain.Entities.Land x) => new(
        x.Id,
        x.RpuId,
        x.PropertyId,
        x.Area,
        x.AreaUnit,
        x.ClassificationId,
        x.Classification!.Name,
        x.ActualUseId,
        x.ActualUse!.Name,
        x.SubClassificationId,
        x.ZoneId,
        x.LocationFactor,
        x.RoadFrontage,
        x.RoadTypeId,
        x.IsCornerLot,
        x.Zoning,
        x.MarketValue,
        x.AssessedValue,
        x.Status,
        x.CreatedAt);
}
