using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Parcels;

public sealed class ParcelService(IApplicationDbContext db, IValidator<CreateParcelRequest> validator) : IParcelService
{
    // SRID DOMAIN VERIFICATION REQUIRED — see docs/DATABASE.md §9. 4326
    // (WGS84) applied here pending confirmation of the target LGU's actual
    // survey/GIS data CRS.
    private const int Srid = 4326;
    private static readonly NtsGeometryServices GeometryServices = new(NtsGeometryServices.Instance.DefaultCoordinateSequenceFactory, NtsGeometryServices.Instance.DefaultPrecisionModel, Srid);
    private static readonly GeometryFactory GeometryFactory = GeometryServices.CreateGeometryFactory(Srid);
    private static readonly WKTReader WktReader = new(GeometryServices);

    public async Task<Result<ParcelDto>> CreateAsync(CreateParcelRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<ParcelDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        if (!await db.Properties.AnyAsync(p => p.Id == request.PropertyId, cancellationToken))
        {
            return Result.Failure<ParcelDto>("PROPERTY_NOT_FOUND", "No property was found with the given id.");
        }
        if (!await db.Barangays.AnyAsync(b => b.Id == request.BarangayId, cancellationToken))
        {
            return Result.Failure<ParcelDto>("BARANGAY_NOT_FOUND", "The specified barangay does not exist.");
        }

        Geometry? geometry = null;
        if (!string.IsNullOrWhiteSpace(request.GeometryWkt))
        {
            try
            {
                geometry = WktReader.Read(request.GeometryWkt);
                geometry.SRID = Srid;
                if (!geometry.IsValid)
                {
                    return Result.Failure<ParcelDto>("INVALID_GEOMETRY", "The supplied geometry is not a valid shape (CLAUDE.md §61 data quality).");
                }
            }
            catch (Exception ex) when (ex is ParseException or FormatException)
            {
                return Result.Failure<ParcelDto>("INVALID_GEOMETRY", $"Could not parse geometry: {ex.Message}");
            }
        }

        var parcel = new Parcel
        {
            PropertyId = request.PropertyId,
            BarangayId = request.BarangayId,
            ZoneId = request.ZoneId,
            Geometry = geometry,
            Area = request.Area,
            SurveyNumber = request.SurveyNumber,
            LotNumber = request.LotNumber,
            BlockNumber = request.BlockNumber,
            Status = RecordStatus.Active,
        };

        db.Parcels.Add(parcel);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(await MapToDto(parcel.Id, cancellationToken)
            ?? throw new InvalidOperationException("Parcel was just created but could not be reloaded."));
    }

    public async Task<Result<ParcelDto>> GetByIdAsync(Guid parcelId, CancellationToken cancellationToken = default)
    {
        var dto = await MapToDto(parcelId, cancellationToken);
        return dto is null
            ? Result.Failure<ParcelDto>("PARCEL_NOT_FOUND", "No parcel was found with the given id.")
            : Result.Success(dto);
    }

    public async Task<Result<IReadOnlyList<ParcelDto>>> ListByPropertyAsync(Guid propertyId, CancellationToken cancellationToken = default)
    {
        var parcels = await db.Parcels.Where(p => p.PropertyId == propertyId).Include(p => p.Barangay).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<ParcelDto>>(parcels.Select(ProjectToDto).ToList());
    }

    private async Task<ParcelDto?> MapToDto(Guid parcelId, CancellationToken cancellationToken)
    {
        var parcel = await db.Parcels.Where(p => p.Id == parcelId).Include(p => p.Barangay).SingleOrDefaultAsync(cancellationToken);
        return parcel is null ? null : ProjectToDto(parcel);
    }

    // Materializes the entity first, then calls Geometry.AsText() in
    // memory — NetTopologySuite's AsText() cannot be translated to SQL
    // inside an EF LINQ projection, unlike simple scalar properties.
    private static ParcelDto ProjectToDto(Parcel p) => new(
        p.Id,
        p.PropertyId,
        p.BarangayId,
        p.Barangay!.Name,
        p.ZoneId,
        p.Geometry?.AsText(),
        p.Area,
        p.SurveyNumber,
        p.LotNumber,
        p.BlockNumber,
        p.Status,
        p.CreatedAt);
}
