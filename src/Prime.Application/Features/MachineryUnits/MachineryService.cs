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
        // A machinery RPU may hold several machines (MRPAAO Att. 3: one row per machine).
        if (request.ClassificationId is { } c && !await db.Classifications.AnyAsync(x => x.Id == c, cancellationToken))
        {
            return Result.Failure<MachineryDto>("CLASSIFICATION_NOT_FOUND", "The specified classification does not exist.");
        }
        if (request.ActualUseId is { } u && !await db.ActualUses.AnyAsync(x => x.Id == u, cancellationToken))
        {
            return Result.Failure<MachineryDto>("ACTUAL_USE_NOT_FOUND", "The specified actual use does not exist.");
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
            ClassificationId = request.ClassificationId,
            ActualUseId = request.ActualUseId,
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

    /// <summary>The unit's first machine (the endpoint predates several machines per unit); see <see cref="ListByRpuAsync"/>.</summary>
    public async Task<Result<MachineryDto>> GetByRpuAsync(Guid rpuId, CancellationToken cancellationToken = default)
    {
        var entity = await IncludeReferences(db.MachineryUnits).Where(x => x.RpuId == rpuId)
            .OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        return entity is null
            ? Result.Failure<MachineryDto>("MACHINERY_NOT_FOUND", "No Machinery record was found for this RPU.")
            : Result.Success(ProjectToDto(entity));
    }

    public async Task<Result<IReadOnlyList<MachineryDto>>> ListByRpuAsync(Guid rpuId, CancellationToken cancellationToken = default)
    {
        var rows = await IncludeReferences(db.MachineryUnits).Where(x => x.RpuId == rpuId).OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<MachineryDto>>(rows.Select(ProjectToDto).ToList());
    }

    private async Task<MachineryDto?> MapToDto(Guid id, CancellationToken cancellationToken)
    {
        var entity = await IncludeReferences(db.MachineryUnits).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : ProjectToDto(entity);
    }

    private static IQueryable<Machinery> IncludeReferences(IQueryable<Machinery> query) => query
        .Include(x => x.MachineryType)
        .Include(x => x.Classification)
        .Include(x => x.ActualUse);

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
        x.CreatedAt,
        x.ClassificationId,
        x.Classification?.Name,
        x.ActualUseId,
        x.ActualUse?.Name,
        x.YearInstalled,
        x.YearOfInitialOperation,
        x.ConversionFactor);
}
