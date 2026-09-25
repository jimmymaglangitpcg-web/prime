using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.RealPropertyUnits;

/// <summary>
/// Real property units. Each building or machinery unit gets the next PIN
/// postscript of its series, and may name the land it stands on and (for
/// machinery) the building it is installed in — the FAAS "Land Reference" and
/// "Building Owner + PIN" blocks (docs/analysis/mrpaao-forms-model.md §6.2–6.3).
/// </summary>
public sealed class RealPropertyUnitService(IApplicationDbContext db, IValidator<CreateRpuRequest> validator, IOptions<UnitPinOptions> pinOptions)
    : IRealPropertyUnitService
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
        if (await LinkProblemAsync(request, cancellationToken) is { } problem)
        {
            return Result.Failure<RpuDto>(problem.Code, problem.Message);
        }

        var rpu = new RealPropertyUnit
        {
            PropertyId = request.PropertyId,
            RpuNumber = request.RpuNumber,
            RpuType = request.RpuType,
            EffectivityDate = request.EffectivityDate,
            PreviousRpuId = request.PreviousRpuId,
            LandRpuId = request.LandRpuId,
            HostRpuId = request.HostRpuId,
            Status = RecordStatus.Active,
        };
        db.RealPropertyUnits.Add(rpu);

        // The next postscript of the series. UX_RealPropertyUnit_Property_PinSuffix
        // catches a concurrent creation, which then takes the following number.
        for (var attempt = 1; ; attempt++)
        {
            rpu.PinSuffix = await NextSuffixAsync(request.PropertyId, request.RpuType, cancellationToken);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateException) when (attempt < 3 && rpu.PinSuffix is not null)
            {
            }
        }

        return Result.Success((await ToDtosAsync([rpu], cancellationToken)).Single());
    }

    public async Task<Result<RpuDto>> GetByIdAsync(Guid rpuId, CancellationToken cancellationToken = default)
    {
        var rpu = await db.RealPropertyUnits.FirstOrDefaultAsync(r => r.Id == rpuId, cancellationToken);
        return rpu is null
            ? Result.Failure<RpuDto>("RPU_NOT_FOUND", "No RPU was found with the given id.")
            : Result.Success((await ToDtosAsync([rpu], cancellationToken)).Single());
    }

    public async Task<Result<IReadOnlyList<RpuDto>>> ListByPropertyAsync(Guid propertyId, CancellationToken cancellationToken = default)
    {
        var items = await db.RealPropertyUnits
            .Where(r => r.PropertyId == propertyId)
            .OrderByDescending(r => r.EffectivityDate)
            .ToListAsync(cancellationToken);

        return Result.Success(await ToDtosAsync(items, cancellationToken));
    }

    private async Task<(string Code, string Message)?> LinkProblemAsync(CreateRpuRequest request, CancellationToken ct)
    {
        if (request.LandRpuId is { } landId)
        {
            if (request.RpuType == RpuType.Land)
            {
                return ("RPU_LAND_LINK_INVALID", "A land unit does not stand on another land unit.");
            }
            var land = await db.RealPropertyUnits.FirstOrDefaultAsync(r => r.Id == landId, ct);
            if (land is null || land.PropertyId != request.PropertyId || land.RpuType != RpuType.Land)
            {
                return ("RPU_LAND_LINK_INVALID", "The land unit must be a Land RPU of the same property.");
            }
        }
        if (request.HostRpuId is { } hostId)
        {
            if (request.RpuType != RpuType.Machinery)
            {
                return ("RPU_HOST_LINK_INVALID", "Only a machinery unit is installed in a building.");
            }
            var host = await db.RealPropertyUnits.FirstOrDefaultAsync(r => r.Id == hostId, ct);
            if (host is null || host.PropertyId != request.PropertyId || host.RpuType != RpuType.Building)
            {
                return ("RPU_HOST_LINK_INVALID", "The host unit must be a Building RPU of the same property.");
            }
        }
        return null;
    }

    private async Task<int?> NextSuffixAsync(Guid propertyId, RpuType type, CancellationToken ct)
    {
        if (!pinOptions.Value.SuffixStart.TryGetValue(type, out var start))
        {
            return null;
        }
        var last = await db.RealPropertyUnits.Where(r => r.PropertyId == propertyId && r.RpuType == type && r.PinSuffix != null)
            .MaxAsync(r => r.PinSuffix, ct);
        return last is null ? start : last + 1;
    }

    /// <summary>A unit is owned separately when it has current owners (or an unknown-owner declaration) of its own.</summary>
    private async Task<IReadOnlyList<RpuDto>> ToDtosAsync(IReadOnlyList<RealPropertyUnit> rpus, CancellationToken ct)
    {
        if (rpus.Count == 0)
        {
            return [];
        }
        var propertyIds = rpus.Select(r => r.PropertyId).Distinct().ToList();
        var pins = await db.Properties.Where(p => propertyIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.PropertyIdentificationNumber, ct);
        var ids = rpus.Select(r => (Guid?)r.Id).ToList();
        var separate = (await db.PropertyTaxpayers.Where(x => ids.Contains(x.RpuId) && x.IsCurrent
                && (x.Role == PropertyPartyRole.Owner || x.Role == PropertyPartyRole.UnknownOwner))
            .Select(x => x.RpuId!.Value).Distinct().ToListAsync(ct)).ToHashSet();
        return rpus.Select(r => new RpuDto(
            r.Id, r.PropertyId, r.RpuNumber, r.RpuType, r.Status, r.EffectivityDate, r.EndDate, r.PreviousRpuId, r.CreatedAt,
            r.PinSuffix, UnitPin.Compose(pins[r.PropertyId], r.PinSuffix, separate.Contains(r.Id)), separate.Contains(r.Id),
            r.LandRpuId, r.HostRpuId)).ToList();
    }
}
