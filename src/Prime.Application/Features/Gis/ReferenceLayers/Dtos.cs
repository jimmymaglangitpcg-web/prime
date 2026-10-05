using System.Text.Json;

namespace Prime.Application.Features.Gis.ReferenceLayers;

/// <summary>Reference layers served beside parcels (docs/GIS.md §3). Route values: barangays, zones, roads, sections.</summary>
public enum ReferenceLayer
{
    Barangays,
    Zones,
    Roads,
    /// <summary>Tax map section boundaries (docs/analysis/property-identification.md §3.7).</summary>
    Sections,
    /// <summary>Areas in dispute, hatched on tax maps (LAM Bk II pp.50–52; docs/analysis/identification-numbering.md §4.3). Keyed by "code".</summary>
    DisputedAreas,
    /// <summary>Sub-market areas of the land value map (smv-preparation-general-revision.md §4.4). Keyed by "code".</summary>
    SubMarketAreas,
}

/// <summary>
/// A GeoJSON FeatureCollection (WGS84, RFC 7946) to import as a new version
/// of each feature it contains, effective from <see cref="EffectiveDate"/>.
/// Per-layer feature properties (docs/GIS.md §3):
/// barangays → "psgcCode"; zones → "zoneCode"; roads → "code", optional
/// "name" and "roadTypeCode"; sections → "psgcCode" (the barangay's) and
/// "section" (its 3-digit index number).
/// </summary>
public sealed record ImportReferenceLayerRequest(
    DateOnly EffectiveDate,
    string Source,
    string? SourceReference,
    JsonElement FeatureCollection);

/// <summary>How a content pack calls the import (docs/analysis/lgu-content-pack.md, step C5).</summary>
/// <param name="SkipUnchanged">
/// A feature whose version in force already has the same shape (for roads also
/// the same name and road type), and started on or before the effective date,
/// is counted as unchanged and not versioned again, so re-importing a pack
/// changes nothing.
/// </param>
/// <param name="PendingKeys">
/// Barangay PSGC codes or zone codes that the same pack creates before its map
/// layers. A dry run treats them as existing; a commit ignores the set, since
/// they must exist by then.
/// </param>
/// <param name="PendingRoadTypeCodes">Road-type codes the same pack creates; as <paramref name="PendingKeys"/>.</param>
public sealed record ReferenceLayerImportOptions(
    bool SkipUnchanged = false,
    IReadOnlySet<string>? PendingKeys = null,
    IReadOnlySet<string>? PendingRoadTypeCodes = null)
{
    public static readonly ReferenceLayerImportOptions Default = new();
}

/// <param name="FeatureIndex">Zero-based index in the submitted features array; null for collection-level issues.</param>
public sealed record ImportIssue(int? FeatureIndex, string Code, string Message);

/// <summary>A feature that gets (or, in a dry run, would get) a new version.</summary>
/// <param name="Supersedes">True when it ends the version in force; false for a key with no version yet.</param>
/// <param name="EntityType">BarangayBoundary, ZoneBoundary, RoadSegment or SectionBoundary.</param>
/// <param name="VersionId">The new version's id; null in a dry run.</param>
public sealed record ImportedLayerFeature(int FeatureIndex, string Key, bool Supersedes, string EntityType, Guid? VersionId);

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
    IReadOnlyList<ImportIssue> Errors,
    int UnchangedFeatures = 0,
    IReadOnlyList<ImportedLayerFeature>? Features = null);

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

/// <param name="Key">psgcCode, zone code, road code, or "psgcCode/section" — the layer's business key.</param>
public sealed record ReferenceLayerFeatureProperties(
    string Key,
    string? Name,
    DateOnly EffectiveDate,
    DateOnly? EndDate,
    string Source,
    string? SourceReference);
