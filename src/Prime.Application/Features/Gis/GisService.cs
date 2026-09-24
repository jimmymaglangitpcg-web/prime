using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Converters;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Common;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Gis;

public sealed class GisService(IApplicationDbContext db) : IGisService
{
    // Server-side cap on features per extent request (CLAUDE.md §71 — never
    // stream the whole inventory to the browser). A rendering/performance
    // limit, not a business rule.
    public const int DefaultFeatureLimit = 2000;
    public const int MaxFeatureLimit = 5000;

    private static readonly GeometryFactory GeometryFactory =
        NtsGeometryServices.Instance.CreateGeometryFactory(SpatialReference.StorageSrid);

    private static readonly JsonSerializerOptions GeoJsonOptions = new() { Converters = { new GeoJsonConverterFactory() } };

    public async Task<Result<ParcelFeatureCollection>> GetParcelsInExtentAsync(string? bbox, int? limit, CancellationToken cancellationToken = default)
    {
        var envelopeResult = ParseBbox(bbox);
        if (envelopeResult.IsFailure)
        {
            return Result.Failure<ParcelFeatureCollection>(envelopeResult.Code!, envelopeResult.Message!);
        }

        var effectiveLimit = Math.Clamp(limit ?? DefaultFeatureLimit, 1, MaxFeatureLimit);
        var extent = GeometryFactory.ToGeometry(envelopeResult.Value);

        var features = await QueryFeatures(
            ActiveParcelsIntersecting(db.Parcels, extent),
            effectiveLimit + 1,
            cancellationToken);

        var truncated = features.Count > effectiveLimit;
        return Result.Success(new ParcelFeatureCollection(
            "FeatureCollection",
            truncated ? features.Take(effectiveLimit).ToList() : features,
            truncated,
            effectiveLimit));
    }

    public async Task<Result<ParcelFeatureCollection>> GetParcelsAtPointAsync(double lon, double lat, CancellationToken cancellationToken = default)
    {
        if (!IsValidLonLat(lon, lat))
        {
            return Result.Failure<ParcelFeatureCollection>("INVALID_COORDINATE", "lon must be within [-180, 180] and lat within [-90, 90] (WGS84 degrees).");
        }

        var point = GeometryFactory.CreatePoint(new Coordinate(lon, lat));

        // Intersects rather than Contains so a click exactly on a shared
        // boundary still returns both neighbours instead of nothing.
        const int pointLimit = 50;
        var features = await QueryFeatures(
            ActiveParcelsIntersecting(db.Parcels, point),
            pointLimit,
            cancellationToken);

        return Result.Success(new ParcelFeatureCollection("FeatureCollection", features, false, pointLimit));
    }

    /// <summary>
    /// The one spatial filter every GIS read uses. Translates to
    /// ST_Intersects, which is index-aware: PostGIS adds the && bounding-box
    /// test served by the GiST index on Parcels.Geometry (verified with
    /// EXPLAIN — docs/GIS.md §4).
    /// </summary>
    public static IQueryable<Domain.Entities.Parcel> ActiveParcelsIntersecting(IQueryable<Domain.Entities.Parcel> parcels, Geometry area) =>
        parcels.Where(p => p.Status == RecordStatus.Active && p.Geometry != null && p.Geometry.Intersects(area));

    private static async Task<List<ParcelFeature>> QueryFeatures(IQueryable<Domain.Entities.Parcel> parcels, int take, CancellationToken cancellationToken)
    {
        var rows = await parcels
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                p.PropertyId,
                p.Property!.PropertyIdentificationNumber,
                p.LotNumber,
                p.BlockNumber,
                p.SurveyNumber,
                BarangayName = p.Barangay!.Name,
                p.Geometry,
            })
            .Take(take)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new ParcelFeature(
            "Feature",
            r.Id,
            JsonSerializer.SerializeToElement(r.Geometry!, GeoJsonOptions),
            new ParcelFeatureProperties(r.Id, r.PropertyId, r.PropertyIdentificationNumber, r.LotNumber, r.BlockNumber, r.SurveyNumber, r.BarangayName)))
            .ToList();
    }

    public static Result<Envelope> ParseBbox(string? bbox)
    {
        const string expected = "bbox must be \"minLon,minLat,maxLon,maxLat\" in WGS84 degrees.";
        if (string.IsNullOrWhiteSpace(bbox))
        {
            return Result.Failure<Envelope>("INVALID_BBOX", expected);
        }

        var parts = bbox.Split(',');
        var values = new double[4];
        if (parts.Length != 4 || !parts.Select((part, i) => double.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out values[i])).All(ok => ok))
        {
            return Result.Failure<Envelope>("INVALID_BBOX", expected);
        }

        var (minLon, minLat, maxLon, maxLat) = (values[0], values[1], values[2], values[3]);
        if (!IsValidLonLat(minLon, minLat) || !IsValidLonLat(maxLon, maxLat) || minLon >= maxLon || minLat >= maxLat)
        {
            return Result.Failure<Envelope>("INVALID_BBOX", expected + " min must be less than max, within [-180, 180] / [-90, 90].");
        }

        return Result.Success(new Envelope(minLon, maxLon, minLat, maxLat));
    }

    private static bool IsValidLonLat(double lon, double lat) =>
        double.IsFinite(lon) && double.IsFinite(lat) && lon is >= -180 and <= 180 && lat is >= -90 and <= 90;
}
