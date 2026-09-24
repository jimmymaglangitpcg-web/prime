using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prime.Application.Common.Interfaces;
using Prime.Infrastructure.Persistence;

namespace Prime.Infrastructure.GIS;

public sealed class GeometryMeasurementService(PrimeDbContext db, IOptions<GisOptions> options) : IGeometryMeasurementService
{
    private readonly int? measurementSrid = options.Value.MeasurementSrid;

    public string AreaBasis => measurementSrid is { } srid ? $"PROJECTED_EPSG_{srid}" : "GEODESIC_WGS84";

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetParcelAreasAsync(IReadOnlyCollection<Guid> parcelIds, CancellationToken cancellationToken = default)
    {
        if (parcelIds.Count == 0)
        {
            return new Dictionary<Guid, decimal>();
        }

        var ids = parcelIds.ToArray();
        var rows = measurementSrid is { } srid
            ? await db.Database.SqlQuery<ParcelAreaRow>(
                $"""SELECT "Id", ST_Area(ST_Transform("Geometry", {srid})) AS "Area" FROM "Parcels" WHERE "Id" = ANY({ids}) AND "Geometry" IS NOT NULL""")
                .ToListAsync(cancellationToken)
            : await db.Database.SqlQuery<ParcelAreaRow>(
                $"""SELECT "Id", ST_Area("Geometry"::geography) AS "Area" FROM "Parcels" WHERE "Id" = ANY({ids}) AND "Geometry" IS NOT NULL""")
                .ToListAsync(cancellationToken);

        // Rounded to the Parcel.Area column's scale (numeric(14,4)).
        return rows.ToDictionary(r => r.Id, r => Math.Round((decimal)r.Area, 4));
    }

    private sealed record ParcelAreaRow(Guid Id, double Area);
}
