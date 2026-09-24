using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Converters;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Common;
using Prime.Domain.Entities.Gis;

namespace Prime.Application.Features.Gis.ReferenceLayers;

public sealed class ReferenceLayerService(IApplicationDbContext db, ICurrentUserService currentUser, IClock clock) : IReferenceLayerService
{
    // Technical limits, not business rules.
    public const int MaxImportFeatures = 10_000;
    public const int DefaultFeatureLimit = 2000;
    public const int MaxFeatureLimit = 5000;

    private static readonly GeometryFactory GeometryFactory =
        NtsGeometryServices.Instance.CreateGeometryFactory(SpatialReference.StorageSrid);

    private static readonly JsonSerializerOptions GeoJsonOptions = new() { Converters = { new GeoJsonConverterFactory() } };

    // RFC 7946 GeoJSON is always WGS84; legacy files may still carry a
    // "crs" member, which is accepted only if it says the same thing.
    private static readonly HashSet<string> Wgs84CrsNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "EPSG:4326",
        "urn:ogc:def:crs:EPSG::4326",
        "urn:ogc:def:crs:OGC:1.3:CRS84",
        "urn:ogc:def:crs:OGC::CRS84",
    };

    private sealed record ParsedFeature(int Index, string Key, Geometry Geometry, string? Name, string? RoadTypeCode);

    // ------------------------------------------------------------------ import

    public async Task<Result<ImportReferenceLayerResult>> ImportAsync(
        ReferenceLayer layer, ImportReferenceLayerRequest request, bool dryRun, CancellationToken cancellationToken = default)
    {
        var errors = new List<ImportIssue>();

        if (request.EffectiveDate == default)
        {
            errors.Add(new ImportIssue(null, "EFFECTIVE_DATE_REQUIRED", "effectiveDate is required."));
        }
        if (string.IsNullOrWhiteSpace(request.Source) || request.Source.Length > 300)
        {
            errors.Add(new ImportIssue(null, "SOURCE_REQUIRED", "source is required (max 300 characters) — official map data must record where it came from."));
        }
        if (request.SourceReference is { Length: > 300 })
        {
            errors.Add(new ImportIssue(null, "SOURCE_REFERENCE_TOO_LONG", "sourceReference must be at most 300 characters."));
        }

        var parsed = ParseFeatures(layer, request.FeatureCollection, errors, out var featureCount);

        // Resolve whatever parsed cleanly even if other errors exist, so a
        // dry run reports every problem in one pass.
        var plan = parsed.Count > 0
            ? await PlanAsync(layer, parsed, request.EffectiveDate, errors, cancellationToken)
            : null;

        if (errors.Count > 0 || dryRun || plan is null)
        {
            return Result.Success(new ImportReferenceLayerResult(
                layer, dryRun, false, null, featureCount, plan?.NewKeys ?? 0, plan?.ToSupersede.Count ?? 0, errors));
        }

        var batchId = Guid.NewGuid();
        currentUser.Reason = $"GIS import {batchId}: {layer} from \"{request.Source.Trim()}\" effective {request.EffectiveDate:yyyy-MM-dd}";

        // Close current versions first, then insert successors: the
        // "one current version per key" unique index is not deferrable, so
        // the order matters. One transaction keeps the whole batch atomic.
        var ownsTransaction = db.Database.CurrentTransaction is null;
        var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            foreach (var current in plan.ToSupersede)
            {
                current.EndDate = request.EffectiveDate;
            }
            await db.SaveChangesAsync(cancellationToken);

            plan.AddNewVersions(batchId, request.EffectiveDate, request.Source.Trim(), request.SourceReference?.Trim());
            await db.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateException)
        {
            // Most likely a concurrent import of the same keys tripping the
            // unique current-version index. Nothing was committed.
            return Result.Failure<ImportReferenceLayerResult>(
                "LAYER_IMPORT_CONCURRENCY_CONFLICT", "Another change to the same features was saved at the same time. Nothing was imported; re-run the dry run and try again.");
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
            currentUser.Reason = null;
        }

        return Result.Success(new ImportReferenceLayerResult(
            layer, false, true, batchId, featureCount, plan.NewKeys, plan.ToSupersede.Count, errors));
    }

    private static List<ParsedFeature> ParseFeatures(ReferenceLayer layer, JsonElement json, List<ImportIssue> errors, out int featureCount)
    {
        featureCount = 0;
        var result = new List<ParsedFeature>();
        if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty("type", out var type) || type.GetString() != "FeatureCollection")
        {
            errors.Add(new ImportIssue(null, "INVALID_GEOJSON", "featureCollection must be a GeoJSON FeatureCollection object."));
            return result;
        }
        if (json.TryGetProperty("crs", out var crs))
        {
            var name = crs.TryGetProperty("properties", out var props) && props.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (name is null || !Wgs84CrsNames.Contains(name))
            {
                errors.Add(new ImportIssue(null, "UNSUPPORTED_CRS",
                    $"Only WGS84 (EPSG:4326) GeoJSON is accepted; this file declares \"{name ?? "unknown"}\". Reproject it (e.g. from PRS92) before import — see docs/GIS.md §2."));
                return result;
            }
        }

        FeatureCollection collection;
        try
        {
            collection = JsonSerializer.Deserialize<FeatureCollection>(json.GetRawText(), GeoJsonOptions)
                ?? throw new JsonException("Empty FeatureCollection.");
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            errors.Add(new ImportIssue(null, "INVALID_GEOJSON", $"Could not read the FeatureCollection: {ex.Message}"));
            return result;
        }

        featureCount = collection.Count;
        if (collection.Count == 0)
        {
            errors.Add(new ImportIssue(null, "NO_FEATURES", "The FeatureCollection contains no features."));
            return result;
        }
        if (collection.Count > MaxImportFeatures)
        {
            errors.Add(new ImportIssue(null, "TOO_MANY_FEATURES", $"At most {MaxImportFeatures} features per import; split the file."));
            return result;
        }

        var keyProperty = KeyProperty(layer);
        var seenKeys = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < collection.Count; i++)
        {
            var feature = collection[i];
            var key = Attribute(feature, keyProperty);
            if (string.IsNullOrWhiteSpace(key))
            {
                errors.Add(new ImportIssue(i, "KEY_REQUIRED", $"Feature is missing the \"{keyProperty}\" property."));
                continue;
            }
            if (seenKeys.TryGetValue(key, out var firstIndex))
            {
                errors.Add(new ImportIssue(i, "DUPLICATE_KEY", $"\"{key}\" also appears at feature {firstIndex}; each {keyProperty} may appear once per import."));
                continue;
            }
            seenKeys[key] = i;

            var geometryIssue = NormalizeGeometry(layer, feature.Geometry, out var geometry);
            if (geometryIssue is not null)
            {
                errors.Add(new ImportIssue(i, geometryIssue.Value.Code, geometryIssue.Value.Message));
                continue;
            }

            result.Add(new ParsedFeature(i, key, geometry!, Attribute(feature, "name"), Attribute(feature, "roadTypeCode")));
        }
        return result;
    }

    private static (string Code, string Message)? NormalizeGeometry(ReferenceLayer layer, Geometry? input, out Geometry? output)
    {
        output = null;
        if (input is null || input.IsEmpty)
        {
            return ("GEOMETRY_REQUIRED", "Feature has no geometry.");
        }

        // WGS84 degrees only; projected metres (e.g. PRS92 zone
        // coordinates exported without a crs member) land far outside.
        if (input.Coordinates.Any(c => !double.IsFinite(c.X) || !double.IsFinite(c.Y) || c.X is < -180 or > 180 || c.Y is < -90 or > 90))
        {
            return ("COORDINATES_OUT_OF_RANGE", "Coordinates must be WGS84 longitude/latitude in degrees; this looks like projected (metre) coordinates.");
        }

        output = (layer, input) switch
        {
            (ReferenceLayer.Roads, MultiLineString m) => m,
            (ReferenceLayer.Roads, LineString l) => GeometryFactory.CreateMultiLineString([l]),
            (not ReferenceLayer.Roads, MultiPolygon m) => m,
            (not ReferenceLayer.Roads, Polygon p) => GeometryFactory.CreateMultiPolygon([p]),
            _ => null,
        };
        if (output is null)
        {
            var expected = layer == ReferenceLayer.Roads ? "LineString or MultiLineString" : "Polygon or MultiPolygon";
            return ("INVALID_GEOMETRY_TYPE", $"{layer} features must be {expected}, not {input.GeometryType}.");
        }
        if (!output.IsValid)
        {
            output = null;
            return ("INVALID_GEOMETRY", "Geometry is not valid (e.g. self-intersecting ring) — CLAUDE.md §61.");
        }
        output = GeometryFactory.CreateGeometry(output);
        output.SRID = SpatialReference.StorageSrid;
        return null;
    }

    // ------------------------------------------------------------ planning

    /// <summary>What a commit would do; also the source of dry-run counts.</summary>
    private sealed class ImportPlan
    {
        public List<SpatialLayerFeature> ToSupersede { get; } = [];
        public int NewKeys { get; set; }
        public required Action<Guid, DateOnly, string, string?> AddNewVersions { get; init; }
    }

    private async Task<ImportPlan> PlanAsync(
        ReferenceLayer layer, List<ParsedFeature> features, DateOnly effectiveDate, List<ImportIssue> errors, CancellationToken cancellationToken)
    {
        var keys = features.Select(f => f.Key).ToList();
        return layer switch
        {
            ReferenceLayer.Barangays => await PlanKeyedPolygonsAsync(
                features, effectiveDate, errors,
                await db.Barangays.Where(b => keys.Contains(b.PsgcCode)).ToDictionaryAsync(b => b.PsgcCode, b => b.Id, cancellationToken),
                "BARANGAY_NOT_FOUND", "No barangay has PSGC code",
                ids => db.BarangayBoundaries.Where(v => ids.Contains(v.BarangayId)).ToListAsync(cancellationToken),
                v => v.BarangayId,
                (id, geometry) => new BarangayBoundary { BarangayId = id, Geometry = geometry },
                v => db.BarangayBoundaries.Add(v)),
            ReferenceLayer.Zones => await PlanKeyedPolygonsAsync(
                features, effectiveDate, errors,
                await db.Zones.Where(z => keys.Contains(z.Code)).ToDictionaryAsync(z => z.Code, z => z.Id, cancellationToken),
                "ZONE_NOT_FOUND", "No zone has code",
                ids => db.ZoneBoundaries.Where(v => ids.Contains(v.ZoneId)).ToListAsync(cancellationToken),
                v => v.ZoneId,
                (id, geometry) => new ZoneBoundary { ZoneId = id, Geometry = geometry },
                v => db.ZoneBoundaries.Add(v)),
            _ => await PlanRoadsAsync(features, effectiveDate, errors, cancellationToken),
        };
    }

    private static async Task<ImportPlan> PlanKeyedPolygonsAsync<TVersion>(
        List<ParsedFeature> features,
        DateOnly effectiveDate,
        List<ImportIssue> errors,
        Dictionary<string, Guid> idsByKey,
        string notFoundCode,
        string notFoundMessage,
        Func<List<Guid>, Task<List<TVersion>>> loadVersions,
        Func<TVersion, Guid> ownerId,
        Func<Guid, MultiPolygon, TVersion> create,
        Action<TVersion> add)
        where TVersion : SpatialLayerFeature
    {
        var resolved = new List<(ParsedFeature Feature, Guid Id)>();
        foreach (var feature in features)
        {
            if (idsByKey.TryGetValue(feature.Key, out var id))
            {
                resolved.Add((feature, id));
            }
            else
            {
                errors.Add(new ImportIssue(feature.Index, notFoundCode, $"{notFoundMessage} \"{feature.Key}\". Register it in reference data first."));
            }
        }

        var versions = (await loadVersions(resolved.Select(r => r.Id).ToList())).ToLookup(ownerId);
        var plan = new ImportPlan
        {
            AddNewVersions = (batchId, date, source, sourceReference) =>
            {
                foreach (var (feature, id) in resolved)
                {
                    var version = create(id, (MultiPolygon)feature.Geometry);
                    Stamp(version, batchId, date, source, sourceReference);
                    add(version);
                }
            },
        };
        foreach (var (feature, id) in resolved)
        {
            PlanVersion(feature, versions[id].Cast<SpatialLayerFeature>().ToList(), effectiveDate, errors, plan);
        }
        return plan;
    }

    private async Task<ImportPlan> PlanRoadsAsync(List<ParsedFeature> features, DateOnly effectiveDate, List<ImportIssue> errors, CancellationToken cancellationToken)
    {
        var roadTypeCodes = features.Where(f => f.RoadTypeCode is not null).Select(f => f.RoadTypeCode!).Distinct().ToList();
        var roadTypes = await db.RoadTypes.Where(t => roadTypeCodes.Contains(t.Code)).ToDictionaryAsync(t => t.Code, t => t.Id, cancellationToken);
        var keys = features.Select(f => f.Key).ToList();
        var versions = (await db.RoadSegments.Where(v => keys.Contains(v.Code)).ToListAsync(cancellationToken)).ToLookup(v => v.Code);

        var accepted = new List<ParsedFeature>();
        foreach (var feature in features)
        {
            if (feature.RoadTypeCode is not null && !roadTypes.ContainsKey(feature.RoadTypeCode))
            {
                errors.Add(new ImportIssue(feature.Index, "ROAD_TYPE_NOT_FOUND", $"No road type has code \"{feature.RoadTypeCode}\"."));
                continue;
            }
            accepted.Add(feature);
        }

        var plan = new ImportPlan
        {
            AddNewVersions = (batchId, date, source, sourceReference) =>
            {
                foreach (var feature in accepted)
                {
                    var road = new RoadSegment
                    {
                        Code = feature.Key,
                        Name = feature.Name,
                        RoadTypeId = feature.RoadTypeCode is null ? null : roadTypes[feature.RoadTypeCode],
                        Geometry = (MultiLineString)feature.Geometry,
                    };
                    Stamp(road, batchId, date, source, sourceReference);
                    db.RoadSegments.Add(road);
                }
            },
        };
        foreach (var feature in accepted)
        {
            PlanVersion(feature, versions[feature.Key].Cast<SpatialLayerFeature>().ToList(), effectiveDate, errors, plan);
        }
        return plan;
    }

    /// <summary>
    /// History is append-only: a new version must start after every existing
    /// version of the same key started. Corrections to past versions are not
    /// an import concern (they would need their own reviewed workflow).
    /// </summary>
    private static void PlanVersion(ParsedFeature feature, List<SpatialLayerFeature> existing, DateOnly effectiveDate, List<ImportIssue> errors, ImportPlan plan)
    {
        var latest = existing.MaxBy(v => v.EffectiveDate);
        if (latest is not null && latest.EffectiveDate >= effectiveDate)
        {
            errors.Add(new ImportIssue(feature.Index, "VERSION_NOT_AFTER_EXISTING",
                $"\"{feature.Key}\" already has a version effective {latest.EffectiveDate:yyyy-MM-dd}; a new version must be effective after it."));
            return;
        }

        var current = existing.SingleOrDefault(v => v.EndDate is null);
        if (current is not null)
        {
            plan.ToSupersede.Add(current);
        }
        else
        {
            plan.NewKeys++;
        }
    }

    private static void Stamp(SpatialLayerFeature version, Guid batchId, DateOnly effectiveDate, string source, string? sourceReference)
    {
        version.ImportBatchId = batchId;
        version.EffectiveDate = effectiveDate;
        version.Source = source;
        version.SourceReference = sourceReference;
    }

    private static string KeyProperty(ReferenceLayer layer) => layer switch
    {
        ReferenceLayer.Barangays => "psgcCode",
        ReferenceLayer.Zones => "zoneCode",
        _ => "code",
    };

    private static string? Attribute(IFeature feature, string name)
    {
        var value = feature.Attributes?.GetOptionalValue(name);
        var text = value switch
        {
            null => null,
            JsonElement { ValueKind: JsonValueKind.Null } => null,
            JsonElement element => element.ToString(),
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
        };
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    // --------------------------------------------------------------- query

    public async Task<Result<ReferenceLayerFeatureCollection>> GetFeaturesAsync(
        ReferenceLayer layer, string? bbox, DateOnly? asOf, int? limit, CancellationToken cancellationToken = default)
    {
        var envelope = GisService.ParseBbox(bbox);
        if (envelope.IsFailure)
        {
            return Result.Failure<ReferenceLayerFeatureCollection>(envelope.Code!, envelope.Message!);
        }

        var date = asOf ?? clock.Today;
        var effectiveLimit = Math.Clamp(limit ?? DefaultFeatureLimit, 1, MaxFeatureLimit);
        var extent = GeometryFactory.ToGeometry(envelope.Value);

        // Versions valid on `date` (half-open [EffectiveDate, EndDate)) that
        // intersect the extent — ST_Intersects uses each table's GiST index.
        var rows = layer switch
        {
            ReferenceLayer.Barangays => await db.BarangayBoundaries
                .Where(v => v.EffectiveDate <= date && (v.EndDate == null || v.EndDate > date) && v.Geometry.Intersects(extent))
                .OrderBy(v => v.Id).Take(effectiveLimit + 1)
                .Select(v => new LayerRow(v.Id, v.Barangay!.PsgcCode, v.Barangay.Name, v.EffectiveDate, v.EndDate, v.Source, v.SourceReference, v.Geometry))
                .ToListAsync(cancellationToken),
            ReferenceLayer.Zones => await db.ZoneBoundaries
                .Where(v => v.EffectiveDate <= date && (v.EndDate == null || v.EndDate > date) && v.Geometry.Intersects(extent))
                .OrderBy(v => v.Id).Take(effectiveLimit + 1)
                .Select(v => new LayerRow(v.Id, v.Zone!.Code, v.Zone.Name, v.EffectiveDate, v.EndDate, v.Source, v.SourceReference, v.Geometry))
                .ToListAsync(cancellationToken),
            _ => await db.RoadSegments
                .Where(v => v.EffectiveDate <= date && (v.EndDate == null || v.EndDate > date) && v.Geometry.Intersects(extent))
                .OrderBy(v => v.Id).Take(effectiveLimit + 1)
                .Select(v => new LayerRow(v.Id, v.Code, v.Name, v.EffectiveDate, v.EndDate, v.Source, v.SourceReference, v.Geometry))
                .ToListAsync(cancellationToken),
        };

        var truncated = rows.Count > effectiveLimit;
        var features = rows.Take(effectiveLimit).Select(r => new ReferenceLayerFeature(
            "Feature",
            r.Id,
            JsonSerializer.SerializeToElement(r.Geometry, GeoJsonOptions),
            new ReferenceLayerFeatureProperties(r.Key, r.Name, r.EffectiveDate, r.EndDate, r.Source, r.SourceReference))).ToList();

        return Result.Success(new ReferenceLayerFeatureCollection("FeatureCollection", layer, date, features, truncated, effectiveLimit));
    }

    private sealed record LayerRow(Guid Id, string Key, string? Name, DateOnly EffectiveDate, DateOnly? EndDate, string Source, string? SourceReference, Geometry Geometry);
}
