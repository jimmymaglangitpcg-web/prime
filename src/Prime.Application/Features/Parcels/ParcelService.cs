using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Common;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Parcels;

public sealed class ParcelService(
    IApplicationDbContext db,
    IValidator<CreateParcelRequest> validator,
    IGeometryMeasurementService measurement,
    ICurrentUserService currentUser) : IParcelService
{
    private const int Srid = SpatialReference.StorageSrid;
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

        var geometryResult = ParseParcelGeometry(request.GeometryWkt);
        if (geometryResult.IsFailure)
        {
            return Result.Failure<ParcelDto>(geometryResult.Code!, geometryResult.Message!);
        }
        var geometry = geometryResult.Value;

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

    public async Task<Result<ParcelDto>> SetGeometryAsync(Guid parcelId, SetParcelGeometryRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.GeometryWkt))
        {
            // Removing a mapped boundary is not a supported edit — it would
            // make the parcel vanish from the tax map with no replacement.
            return Result.Failure<ParcelDto>("VALIDATION_FAILED", "GeometryWkt is required.");
        }

        var parcel = await db.Parcels.SingleOrDefaultAsync(p => p.Id == parcelId, cancellationToken);
        if (parcel is null)
        {
            return Result.Failure<ParcelDto>("PARCEL_NOT_FOUND", "No parcel was found with the given id.");
        }
        if (parcel.Status != RecordStatus.Active)
        {
            // Subdivided/consolidated/superseded parcels are historical
            // records (CLAUDE.md §36/§37/§76) — their boundary is part of
            // the history and is not edited in place.
            return Result.Failure<ParcelDto>("PARCEL_NOT_ACTIVE", $"Only an Active parcel's geometry can be changed; this parcel is {parcel.Status}.");
        }
        if (parcel.Geometry is not null && string.IsNullOrWhiteSpace(request.Reason))
        {
            return Result.Failure<ParcelDto>("VALIDATION_FAILED", "A reason is required when replacing an existing parcel boundary.");
        }

        var geometryResult = ParseParcelGeometry(request.GeometryWkt);
        if (geometryResult.IsFailure)
        {
            return Result.Failure<ParcelDto>(geometryResult.Code!, geometryResult.Message!);
        }

        // The client's Version becomes the concurrency check value: EF adds
        // "WHERE xmin = @version" to the UPDATE, so a stale edit matches no
        // row and throws instead of overwriting.
        db.Entry(parcel).Property(p => p.Version).OriginalValue = request.Version;
        parcel.Geometry = geometryResult.Value;
        currentUser.Reason = request.Reason?.Trim();

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Drop the rejected in-memory edit so nothing later in this
            // scope can accidentally save it.
            db.Entry(parcel).State = EntityState.Detached;
            currentUser.Reason = null;
            return Result.Failure<ParcelDto>("PARCEL_CONCURRENCY_CONFLICT", "This parcel was changed by someone else since it was loaded. Reload it and try again.");
        }

        return Result.Success(await MapToDto(parcel.Id, cancellationToken)
            ?? throw new InvalidOperationException("Parcel was just updated but could not be reloaded."));
    }

    public async Task<Result<IReadOnlyList<ParcelDto>>> ListByPropertyAsync(Guid propertyId, CancellationToken cancellationToken = default)
    {
        var parcels = await db.Parcels.Where(p => p.PropertyId == propertyId).Include(p => p.Barangay).ToListAsync(cancellationToken);
        var areas = await measurement.GetParcelAreasAsync(parcels.Select(p => p.Id).ToList(), cancellationToken);
        return Result.Success<IReadOnlyList<ParcelDto>>(parcels.Select(p => ProjectToDto(p, areas)).ToList());
    }

    /// <summary>
    /// Parses WKT into a storage-SRID MultiPolygon. Only areal geometry is a
    /// parcel boundary; a single Polygon is wrapped so the typed
    /// geometry(MultiPolygon) column accepts it. Null/blank WKT means "no
    /// geometry yet" and is not an error.
    /// </summary>
    public static Result<MultiPolygon?> ParseParcelGeometry(string? wkt)
    {
        if (string.IsNullOrWhiteSpace(wkt))
        {
            return Result.Success<MultiPolygon?>(null);
        }

        Geometry geometry;
        try
        {
            geometry = WktReader.Read(wkt);
        }
        catch (Exception ex) when (ex is ParseException or FormatException or ArgumentException)
        {
            return Result.Failure<MultiPolygon?>("INVALID_GEOMETRY", $"Could not parse geometry: {ex.Message}");
        }

        var multiPolygon = geometry switch
        {
            MultiPolygon mp => mp,
            Polygon p => GeometryFactory.CreateMultiPolygon([p]),
            _ => null,
        };
        if (multiPolygon is null)
        {
            return Result.Failure<MultiPolygon?>("INVALID_GEOMETRY_TYPE", $"A parcel boundary must be a POLYGON or MULTIPOLYGON, not {geometry.GeometryType.ToUpperInvariant()}.");
        }
        if (multiPolygon.IsEmpty || !multiPolygon.IsValid)
        {
            return Result.Failure<MultiPolygon?>("INVALID_GEOMETRY", "The supplied geometry is not a valid shape (CLAUDE.md §61 data quality).");
        }

        multiPolygon.SRID = Srid;
        return Result.Success<MultiPolygon?>(multiPolygon);
    }

    private async Task<ParcelDto?> MapToDto(Guid parcelId, CancellationToken cancellationToken)
    {
        var parcel = await db.Parcels.Where(p => p.Id == parcelId).Include(p => p.Barangay).SingleOrDefaultAsync(cancellationToken);
        if (parcel is null)
        {
            return null;
        }
        var areas = await measurement.GetParcelAreasAsync([parcel.Id], cancellationToken);
        return ProjectToDto(parcel, areas);
    }

    // Materializes the entity first, then calls Geometry.AsText() in
    // memory — NetTopologySuite's AsText() cannot be translated to SQL
    // inside an EF LINQ projection, unlike simple scalar properties.
    private ParcelDto ProjectToDto(Parcel p, IReadOnlyDictionary<Guid, decimal> measuredAreas) => new(
        p.Id,
        p.PropertyId,
        p.BarangayId,
        p.Barangay!.Name,
        p.ZoneId,
        p.Geometry?.AsText(),
        p.Area,
        measuredAreas.TryGetValue(p.Id, out var measured) ? measured : null,
        measuredAreas.ContainsKey(p.Id) ? measurement.AreaBasis : null,
        p.SurveyNumber,
        p.LotNumber,
        p.BlockNumber,
        p.Status,
        p.CreatedAt,
        p.Version);
}
