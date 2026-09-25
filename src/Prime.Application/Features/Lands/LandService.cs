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
        // The first appraisal strip is the land as registered (docs/analysis/mrpaao-forms-model.md §8.3).
        land.Strips.Add(new Domain.Entities.LandStrip
        {
            Sequence = 1, ClassificationId = land.ClassificationId, SubClassificationId = land.SubClassificationId,
            ActualUseId = land.ActualUseId, Area = land.Area,
        });

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

    public async Task<Result<LandDto>> AddStripAsync(Guid landId, AddLandStripRequest request, CancellationToken cancellationToken = default)
    {
        var land = await db.Lands.Include(x => x.Strips).FirstOrDefaultAsync(x => x.Id == landId, cancellationToken);
        if (land is null)
        {
            return NotFound();
        }
        if (await LandParts.StripProblemAsync(db, request, cancellationToken) is { } problem)
        {
            return LandParts.Fail<LandDto>("VALIDATION_FAILED", problem);
        }
        if (land.Strips.Count == 0)
        {
            // A land recorded without strips keeps its registered area as strip 1.
            Track(land.Strips, db.LandStrips, new Domain.Entities.LandStrip
            {
                Sequence = 1, ClassificationId = land.ClassificationId, SubClassificationId = land.SubClassificationId,
                ActualUseId = land.ActualUseId, ZoneId = null, Area = land.Area,
            });
        }
        Track(land.Strips, db.LandStrips, new Domain.Entities.LandStrip
        {
            Sequence = land.Strips.Max(x => x.Sequence) + 1,
            ClassificationId = request.ClassificationId, SubClassificationId = request.SubClassificationId,
            ActualUseId = request.ActualUseId, ZoneId = request.ZoneId, Area = request.Area,
        });
        LandParts.MirrorPrincipal(land);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success((await MapToDto(landId, cancellationToken))!);
    }

    public async Task<Result<LandDto>> AddImprovementAsync(Guid landId, AddLandImprovementRequest request, CancellationToken cancellationToken = default)
    {
        var land = await db.Lands.Include(x => x.Improvements).FirstOrDefaultAsync(x => x.Id == landId, cancellationToken);
        if (land is null)
        {
            return NotFound();
        }
        if (request.Quantity <= 0 || request.Description?.Length > 500)
        {
            return LandParts.Fail<LandDto>("VALIDATION_FAILED", "The number must be greater than zero; description max 500.");
        }
        if (!await db.ImprovementKinds.AnyAsync(x => x.Id == request.ImprovementKindId && x.IsActive, cancellationToken))
        {
            return LandParts.Fail<LandDto>("IMPROVEMENT_KIND_NOT_FOUND", "The specified improvement kind does not exist or is inactive.");
        }
        if (request.ClassificationId is { } c && !await db.Classifications.AnyAsync(x => x.Id == c, cancellationToken))
        {
            return LandParts.Fail<LandDto>("CLASSIFICATION_NOT_FOUND", "The specified classification does not exist.");
        }
        if (request.ActualUseId is { } u && !await db.ActualUses.AnyAsync(x => x.Id == u, cancellationToken))
        {
            return LandParts.Fail<LandDto>("ACTUAL_USE_NOT_FOUND", "The specified actual use does not exist.");
        }
        Track(land.Improvements, db.LandImprovements, new Domain.Entities.LandImprovement
        {
            Sequence = land.Improvements.Count == 0 ? 1 : land.Improvements.Max(x => x.Sequence) + 1,
            ImprovementKindId = request.ImprovementKindId, Quantity = request.Quantity, IsProductive = request.IsProductive,
            ClassificationId = request.ClassificationId, ActualUseId = request.ActualUseId,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
        });
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success((await MapToDto(landId, cancellationToken))!);
    }

    /// <summary>
    /// Names an adjustment factor by code for the whole land or one strip. The code must exist
    /// in the approved catalogue; its percentage is taken at valuation from the version in force.
    /// </summary>
    public async Task<Result<LandDto>> AddAdjustmentAsync(Guid landId, AddLandAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        var land = await db.Lands.Include(x => x.Strips).Include(x => x.Adjustments).FirstOrDefaultAsync(x => x.Id == landId, cancellationToken);
        if (land is null)
        {
            return NotFound();
        }
        var code = request.FactorCode?.Trim() ?? "";
        if (code.Length is 0 or > 20 || request.Remarks?.Length > 500)
        {
            return LandParts.Fail<LandDto>("VALIDATION_FAILED", "factorCode is required (max 20); remarks max 500.");
        }
        if (request.LandStripId is { } stripId && land.Strips.All(x => x.Id != stripId))
        {
            return LandParts.Fail<LandDto>("LAND_STRIP_NOT_FOUND", "The strip does not belong to this land.");
        }
        if (!await db.AdjustmentFactors.AnyAsync(x => x.Code == code && x.Status == WorkflowStatus.Approved, cancellationToken))
        {
            return LandParts.Fail<LandDto>("ADJUSTMENT_FACTOR_UNKNOWN", $"No approved adjustment factor has the code '{code}'.");
        }
        if (land.Adjustments.Any(x => x.FactorCode == code && x.LandStripId == request.LandStripId))
        {
            return LandParts.Fail<LandDto>("LAND_ADJUSTMENT_DUPLICATE", $"'{code}' already applies here.");
        }
        Track(land.Adjustments, db.LandAdjustments, new Domain.Entities.LandAdjustment
        {
            FactorCode = code, LandStripId = request.LandStripId,
            Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim(),
        });
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success((await MapToDto(landId, cancellationToken))!);
    }

    /// <summary>
    /// Adds a new row to a loaded land's collection. Entity keys are set on construction, so the
    /// row is registered as Added explicitly; EF would otherwise take it for an existing row.
    /// </summary>
    private static void Track<T>(List<T> collection, Microsoft.EntityFrameworkCore.DbSet<T> set, T row) where T : class
    {
        set.Add(row);
        collection.Add(row);
    }

    private static Result<LandDto> NotFound() => LandParts.Fail<LandDto>("LAND_NOT_FOUND", "No Land record was found with the given id.");

    private async Task<LandDto?> MapToDto(Guid id, CancellationToken cancellationToken)
    {
        var entity = await IncludeReferences(db.Lands).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : ProjectToDto(entity);
    }

    private static IQueryable<Domain.Entities.Land> IncludeReferences(IQueryable<Domain.Entities.Land> query) => query
        .Include(x => x.Classification)
        .Include(x => x.ActualUse)
        .Include(x => x.Strips).ThenInclude(s => s.Classification)
        .Include(x => x.Strips).ThenInclude(s => s.SubClassification)
        .Include(x => x.Strips).ThenInclude(s => s.ActualUse)
        .Include(x => x.Strips).ThenInclude(s => s.Zone)
        .Include(x => x.Improvements).ThenInclude(i => i.ImprovementKind)
        .Include(x => x.Improvements).ThenInclude(i => i.Classification)
        .Include(x => x.Improvements).ThenInclude(i => i.ActualUse)
        .Include(x => x.Adjustments);

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
        x.CreatedAt,
        x.Strips.OrderBy(s => s.Sequence).Select(LandParts.ToDto).ToList(),
        x.Improvements.OrderBy(i => i.Sequence).Select(LandParts.ToDto).ToList(),
        x.Adjustments.OrderBy(a => a.FactorCode).Select(a => LandParts.ToDto(a, x.Strips)).ToList());
}
