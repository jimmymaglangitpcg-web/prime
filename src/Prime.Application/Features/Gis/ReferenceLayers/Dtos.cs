using System.Text.Json;

namespace Prime.Application.Features.Gis.ReferenceLayers;

/// <summary>Reference layers served beside parcels (docs/GIS.md §3). Route values: barangays, zones, roads.</summary>
public enum ReferenceLayer
{
    Barangays,
    Zones,
    Roads,
}

/// <summary>
/// A GeoJSON FeatureCollection (WGS84, RFC 7946) to import as a new version
/// of each feature it contains, effective from <see cref="EffectiveDate"/>.
/// Per-layer feature properties (docs/GIS.md §3):
/// barangays → "psgcCode"; zones → "zoneCode"; roads → "code", optional
/// "name" and "roadTypeCode".
/// </summary>
public sealed record ImportReferenceLayerRequest(
    DateOnly EffectiveDate,
    string Source,
    string? SourceReference,
    JsonElement FeatureCollection);

/// <param name="FeatureIndex">Zero-based index in the submitted features array; null for collection-level issues.</param>
public sealed record ImportIssue(int? FeatureIndex, string Code, string Message);

/// <summary>
/// Outcome of a validate-only (<see cref="DryRun"/>) or committing import.
/// Nothing is written unless <see cref="Committed"/> is true, and a commit
/// only happens when <see cref="Errors"/> is empty — all or nothing
/// (CLAUDE.md §60: never silently import invalid data).
/// </summary>
public sealed record ImportReferenceLayerResult(
    ReferenceLayer Layer,
    bool DryRun,
    bool Committed,
    Guid? ImportBatchId,
    int FeatureCount,
    int NewFeatures,
    int SupersededVersions,
    IReadOnlyList<ImportIssue> Errors);

public sealed record ReferenceLayerFeatureCollection(
    string Type,
    ReferenceLayer Layer,
    DateOnly AsOf,
    IReadOnlyList<ReferenceLayerFeature> Features,
    bool Truncated,
    int Limit);

public sealed record ReferenceLayerFeature(
    string Type,
    Guid Id,
    JsonElement Geometry,
    ReferenceLayerFeatureProperties Properties);

/// <param name="Key">psgcCode, zone code, or road code — the layer's business key.</param>
public sealed record ReferenceLayerFeatureProperties(
    string Key,
    string? Name,
    DateOnly EffectiveDate,
    DateOnly? EndDate,
    string Source,
    string? SourceReference);
