using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Converters;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Valuation;
using Prime.Domain.Common;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Gis;

/// <summary>
/// One land parcel on the land value map: its land's principal class, sub-class and use (of the largest strip), and the unit
/// value they take under the SMV, or why there is none. No owner, no market value of the property (CLAUDE.md §68).
/// </summary>
public sealed record LandValueFeatureProperties(
    Guid ParcelId, Guid PropertyId, string Pin, string BarangayName, string? Classification, string? SubClass, string? ActualUse,
    decimal? UnitValue, string? Unit, string? SmvReference, string? Problem);

public sealed record LandValueFeature(string Type, Guid Id, JsonElement Geometry, LandValueFeatureProperties Properties);

/// <summary>A sub-class on the map, with how many parcels and the unit values they take (for the legend).</summary>
public sealed record LandValueLegendItem(string Classification, string? SubClass, int Parcels, decimal? MinimumValue, decimal? MaximumValue, string? Unit);

public sealed record LandValueFeatureCollection(
    string Type, IReadOnlyList<LandValueFeature> Features, bool Truncated, int Limit, DateOnly AsOf, Guid? SmvId, string? SmvReference,
    IReadOnlyList<LandValueLegendItem> Legend);

public interface ILandValueMapService
{
    /// <summary>
    /// Land parcels in the extent with the unit value each takes on <paramref name="asOf"/>: under <paramref name="smvId"/> (a proposed
    /// SMV, any status) or, without one, under the approved SMV in force.
    /// </summary>
    Task<Result<LandValueFeatureCollection>> GetAsync(string? bbox, Guid? smvId, DateOnly? asOf, int? limit, CancellationToken cancellationToken = default);
}

/// <summary>
/// The land value map (LAM 2025 Book IV p.110; docs/analysis/smv-preparation-general-revision.md §4.4, Q11): derived from the land
/// records and the SMV, never drawn. Each parcel's rate is the valuation engine's own selection
/// (<see cref="IValuationService.LandRateAsync"/>) for the land's principal strip — the priced class and sub-class, the use, the zone
/// and the barangay — so the map shows what a valuation would use, without lot adjustments or independent appraisals.
/// </summary>
public sealed class LandValueMapService(IApplicationDbContext db, IValuationService valuation, IClock clock) : ILandValueMapService
{
    private static readonly GeometryFactory GeometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(SpatialReference.StorageSrid);
    private static readonly JsonSerializerOptions GeoJsonOptions = new() { Converters = { new GeoJsonConverterFactory() } };

    private sealed record Key(Guid Classification, Guid? SubClass, Guid ActualUse, Guid? Zone, Guid Municipality, Guid? Barangay);

    public async Task<Result<LandValueFeatureCollection>> GetAsync(string? bbox, Guid? smvId, DateOnly? asOf, int? limit, CancellationToken cancellationToken = default)
    {
        var ct = cancellationToken;
        var envelope = GisService.ParseBbox(bbox);
        if (envelope.IsFailure)
        {
            return Result.Failure<LandValueFeatureCollection>(envelope.Code!, envelope.Message!);
        }
        string? smvReference = null;
        if (smvId is { } id)
        {
            var smv = await db.Smvs.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Status, x.OrdinanceNumber, x.CertificationReference, x.RevisionYear })
                .FirstOrDefaultAsync(ct);
            if (smv is null || smv.Status is WorkflowStatus.Rejected or WorkflowStatus.Cancelled or WorkflowStatus.Voided)
            {
                return Result.Failure<LandValueFeatureCollection>("SMV_NOT_FOUND", "The SMV does not exist, or was rejected or cancelled.");
            }
            smvReference = smv.OrdinanceNumber ?? smv.CertificationReference ?? $"Proposed SMV {smv.RevisionYear}";
        }
        var date = asOf ?? clock.Today;
        var mode = smvId is { } proposed ? ValuationMode.Proposed(proposed) : ValuationMode.Stored;
        var effectiveLimit = Math.Clamp(limit ?? GisService.DefaultFeatureLimit, 1, GisService.MaxFeatureLimit);
        var extent = GeometryFactory.ToGeometry(envelope.Value);

        var parcels = await GisService.ActiveParcelsIntersecting(db.Parcels, extent)
            .OrderBy(p => p.Id).Take(effectiveLimit + 1)
            .Select(p => new
            {
                p.Id, p.PropertyId, Pin = p.Property!.PropertyIdentificationNumber, BarangayName = p.Barangay!.Name,
                p.Property.MunicipalityId, PropertyBarangayId = p.Property.BarangayId, p.Geometry,
            })
            .ToListAsync(ct);
        var truncated = parcels.Count > effectiveLimit;
        parcels = parcels.Take(effectiveLimit).ToList();

        // Each property's land: the largest active one; its principal strip, as the engine prices it.
        var propertyIds = parcels.Select(p => p.PropertyId).Distinct().ToList();
        var lands = (await db.Lands.AsNoTracking().Include(l => l.Strips)
                .Where(l => propertyIds.Contains(l.PropertyId) && l.Status == RecordStatus.Active && l.Rpu!.Status == RecordStatus.Active)
                .ToListAsync(ct))
            .GroupBy(l => l.PropertyId).ToDictionary(g => g.Key, g => g.OrderByDescending(l => l.Area).First());
        Key? KeyOf(Guid propertyId, Guid municipality, Guid? barangay)
        {
            if (!lands.TryGetValue(propertyId, out var land))
            {
                return null;
            }
            var strip = land.Strips.OrderByDescending(s => s.Area).ThenBy(s => s.Sequence).FirstOrDefault();
            if (strip is null)
            {
                return new Key(land.ClassificationId, land.SubClassificationId, land.ActualUseId, land.ZoneId, municipality, barangay);
            }
            var pricedApart = strip.ValuationClassificationId is not null || strip.ValuationSubClassificationId is not null;
            return new Key(strip.ValuationClassificationId ?? strip.ClassificationId,
                pricedApart ? strip.ValuationSubClassificationId : strip.SubClassificationId, strip.ActualUseId, strip.ZoneId ?? land.ZoneId, municipality, barangay);
        }

        var keys = parcels.ToDictionary(p => p.Id, p => KeyOf(p.PropertyId, p.MunicipalityId, p.PropertyBarangayId));
        var rates = new Dictionary<Key, SmvRateDto?>();
        foreach (var key in keys.Values.OfType<Key>().Distinct())
        {
            rates[key] = await valuation.LandRateAsync(key.Classification, key.SubClass, key.ActualUse, key.Municipality, key.Barangay, date, mode, ct, key.Zone);
        }
        var classIds = keys.Values.OfType<Key>().Select(k => k.Classification).Distinct().ToList();
        var subIds = keys.Values.OfType<Key>().Select(k => k.SubClass).OfType<Guid>().Distinct().ToList();
        var useIds = keys.Values.OfType<Key>().Select(k => k.ActualUse).Distinct().ToList();
        var smvIds = rates.Values.OfType<SmvRateDto>().Select(r => r.SmvId).Distinct().ToList();
        var classNames = await db.Classifications.AsNoTracking().Where(x => classIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var subNames = await db.SubClassifications.AsNoTracking().Where(x => subIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var useNames = await db.ActualUses.AsNoTracking().Where(x => useIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var smvNames = await db.Smvs.AsNoTracking().Where(x => smvIds.Contains(x.Id))
            .Select(x => new { x.Id, Name = x.OrdinanceNumber ?? x.CertificationReference ?? ("Proposed SMV " + x.RevisionYear) }).ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var features = parcels.Select(p =>
        {
            var key = keys[p.Id];
            var rate = key is null ? null : rates[key];
            var problem = key is null ? "No land is recorded for the property."
                : rate is null ? (smvId is null ? "No approved SMV in force gives a unit value for its class, sub-class and use." : "The SMV gives no unit value for its class, sub-class and use.")
                : null;
            return new LandValueFeature("Feature", p.Id, JsonSerializer.SerializeToElement(p.Geometry!, GeoJsonOptions), new LandValueFeatureProperties(
                p.Id, p.PropertyId, p.Pin, p.BarangayName, key is null ? null : classNames.GetValueOrDefault(key.Classification),
                key?.SubClass is { } s ? subNames.GetValueOrDefault(s) : null, key is null ? null : useNames.GetValueOrDefault(key.ActualUse),
                rate?.UnitValue, rate?.Unit, rate is null ? null : smvNames.GetValueOrDefault(rate.SmvId), problem));
        }).ToList();
        var legend = features.Where(f => f.Properties.Classification is not null)
            .GroupBy(f => (f.Properties.Classification!, f.Properties.SubClass))
            .Select(g => new LandValueLegendItem(g.Key.Item1, g.Key.SubClass, g.Count(), g.Min(f => f.Properties.UnitValue), g.Max(f => f.Properties.UnitValue),
                g.Select(f => f.Properties.Unit).FirstOrDefault(u => u is not null)))
            .OrderBy(x => x.Classification).ThenByDescending(x => x.MaximumValue).ThenBy(x => x.SubClass).ToList();
        return Result.Success(new LandValueFeatureCollection("FeatureCollection", features, truncated, effectiveLimit, date, smvId, smvReference, legend));
    }
}
