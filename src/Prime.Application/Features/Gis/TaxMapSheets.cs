using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Converters;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.PropertyIdentification;

namespace Prime.Application.Features.Gis;

/// <summary>
/// The tax map book's sheets (MRPAAO Ch. II §2 C.4, C.8; docs/analysis/property-identification.md §3.7):
/// a section's tax map, a barangay's section index map, and a municipality's or
/// district's barangay index map.
/// </summary>
public enum TaxMapSheetKind
{
    /// <summary>One tax map section with its parcels (the property identification map).</summary>
    TaxMap,
    /// <summary>A barangay's sections with their index numbers (Figure 8).</summary>
    SectionIndex,
    /// <summary>A municipality's or city district's barangays with their index numbers (Figure 6).</summary>
    BarangayIndex,
}

/// <summary>One line of the sheet's heading, e.g. ("Barangay", "San Jose", "0013").</summary>
public sealed record TaxMapSheetHeading(string Label, string? Name, string? IndexNumber);

/// <param name="Role">"area": the sheet's own boundary (drawn as its frame); "unit": a labelled section or barangay.</param>
public sealed record TaxMapSheetFeatureProperties(string Label, string? Name, string Role, string Source, string? SourceReference);

public sealed record TaxMapSheetFeature(string Type, Guid Id, JsonElement Geometry, TaxMapSheetFeatureProperties Properties);

/// <param name="Extent">WGS84 [minLon, minLat, maxLon, maxLat] to fit the sheet to; null when nothing on it has a shape.</param>
/// <param name="Missing">What the sheet cannot show (units without a boundary or an index number), for the print page to warn about.</param>
public sealed record TaxMapSheetDto(
    TaxMapSheetKind Kind,
    string Title,
    DateOnly AsOf,
    IReadOnlyList<TaxMapSheetHeading> Heading,
    double[]? Extent,
    IReadOnlyList<TaxMapSheetFeature> Features,
    IReadOnlyList<string> Missing);

public interface ITaxMapSheetService
{
    Task<Result<TaxMapSheetDto>> TaxMapAsync(Guid sectionId, DateOnly? asOf, CancellationToken cancellationToken = default);
    Task<Result<TaxMapSheetDto>> SectionIndexAsync(Guid barangayId, DateOnly? asOf, CancellationToken cancellationToken = default);
    Task<Result<TaxMapSheetDto>> BarangayIndexAsync(Guid municipalityId, Guid? districtId, DateOnly? asOf, CancellationToken cancellationToken = default);
}

/// <summary>
/// Builds the sheets from the effective-dated boundary layers and the assessor's index
/// numbers. Nothing is drawn that PRIME has not recorded: a unit without a boundary
/// is listed under <see cref="TaxMapSheetDto.Missing"/>. The standard sheet size and
/// map symbols are DOMAIN VERIFICATION REQUIRED (the print page uses A4 landscape).
/// </summary>
public sealed class TaxMapSheetService(IApplicationDbContext db, IClock clock) : ITaxMapSheetService
{
    private static readonly JsonSerializerOptions GeoJsonOptions = new() { Converters = { new GeoJsonConverterFactory() } };

    private sealed record Shape(Guid Id, string Label, string? Name, string Role, string Source, string? SourceReference, Geometry Geometry);

    public async Task<Result<TaxMapSheetDto>> TaxMapAsync(Guid sectionId, DateOnly? asOf, CancellationToken cancellationToken = default)
    {
        var date = asOf ?? clock.Today;
        var section = await db.TaxMapSections.AsNoTracking().Where(x => x.Id == sectionId)
            .Select(x => new { x.Id, x.BarangayId, x.IndexNumber, x.RetiredOn }).SingleOrDefaultAsync(cancellationToken);
        if (section is null)
        {
            return Result.Failure<TaxMapSheetDto>("TAX_MAP_SECTION_NOT_FOUND", "The specified tax map section does not exist.");
        }
        var shapes = await db.SectionBoundaries.AsNoTracking()
            .Where(v => v.SectionId == sectionId && v.EffectiveDate <= date && (v.EndDate == null || v.EndDate > date))
            .Select(v => new Shape(v.Id, section.IndexNumber, null, "area", v.Source, v.SourceReference, v.Geometry)).ToListAsync(cancellationToken);
        var missing = new List<string>();
        Envelope? extent = null;
        if (shapes.Count == 0)
        {
            missing.Add($"Section {section.IndexNumber} has no boundary as of {date:yyyy-MM-dd}; the sheet is fitted to its parcels.");
            // Fallback: the parcels mapped in the section.
            var parcels = await db.Parcels.AsNoTracking().Where(p => p.SectionId == sectionId && p.Geometry != null).Select(p => p.Geometry!).ToListAsync(cancellationToken);
            extent = Envelope(parcels);
        }
        if (section.RetiredOn is { } retired && retired <= date)
        {
            missing.Add($"Section {section.IndexNumber} was retired on {retired:yyyy-MM-dd}.");
        }
        // Areas in dispute on the sheet, hatched, with the PINs of the section's parcels they touch (LAM Bk II pp.50–52).
        var sheetArea = shapes.Count > 0 ? shapes[0].Geometry
            : extent is { } e ? new GeometryFactory(new PrecisionModel(), Prime.Domain.Common.SpatialReference.StorageSrid).ToGeometry(e) : null;
        if (sheetArea is not null)
        {
            var disputes = await db.DisputedAreas.AsNoTracking()
                .Where(v => v.EffectiveDate <= date && (v.EndDate == null || v.EndDate > date) && v.Geometry.Intersects(sheetArea))
                .ToListAsync(cancellationToken);
            foreach (var d in disputes)
            {
                var pins = await db.Parcels.AsNoTracking().Where(p => p.SectionId == sectionId && p.Geometry != null && p.Geometry.Intersects(d.Geometry))
                    .Select(p => p.Property!.PropertyIdentificationNumber).Distinct().OrderBy(x => x).ToListAsync(cancellationToken);
                shapes.Add(new Shape(d.Id, d.Code, pins.Count == 0 ? d.Name : $"{d.Name} — PIN {string.Join(", ", pins)}".TrimStart(' ', '—'),
                    "disputed", d.Source, d.SourceReference, d.Geometry));
            }
        }
        var heading = await HeadingAsync(section.BarangayId, section.IndexNumber, cancellationToken);
        return Result.Success(Sheet(TaxMapSheetKind.TaxMap, "Tax Map", date, heading, shapes, missing, extent));
    }

    public async Task<Result<TaxMapSheetDto>> SectionIndexAsync(Guid barangayId, DateOnly? asOf, CancellationToken cancellationToken = default)
    {
        var date = asOf ?? clock.Today;
        if (!await db.Barangays.AnyAsync(x => x.Id == barangayId, cancellationToken))
        {
            return Result.Failure<TaxMapSheetDto>("BARANGAY_NOT_FOUND", "The specified barangay does not exist.");
        }
        var sections = await db.TaxMapSections.AsNoTracking()
            .Where(x => x.BarangayId == barangayId && (x.RetiredOn == null || x.RetiredOn > date))
            .OrderBy(x => x.IndexNumber).Select(x => new { x.Id, x.IndexNumber }).ToListAsync(cancellationToken);
        var ids = sections.Select(x => x.Id).ToList();
        var shapes = await db.SectionBoundaries.AsNoTracking()
            .Where(v => ids.Contains(v.SectionId) && v.EffectiveDate <= date && (v.EndDate == null || v.EndDate > date))
            .Select(v => new Shape(v.Id, v.Section!.IndexNumber, null, "unit", v.Source, v.SourceReference, v.Geometry)).ToListAsync(cancellationToken);
        shapes.AddRange(await db.BarangayBoundaries.AsNoTracking()
            .Where(v => v.BarangayId == barangayId && v.EffectiveDate <= date && (v.EndDate == null || v.EndDate > date))
            .Select(v => new Shape(v.Id, v.Barangay!.PinIndexNumber ?? v.Barangay.Name, v.Barangay.Name, "area", v.Source, v.SourceReference, v.Geometry))
            .ToListAsync(cancellationToken));

        var missing = new List<string>();
        if (sections.Count == 0)
        {
            missing.Add("The barangay has no tax map sections in use on this date.");
        }
        var drawn = shapes.Where(x => x.Role == "unit").Select(x => x.Label).ToHashSet();
        var undrawn = sections.Where(x => !drawn.Contains(x.IndexNumber)).Select(x => x.IndexNumber).ToList();
        if (undrawn.Count > 0)
        {
            missing.Add($"No boundary as of {date:yyyy-MM-dd} for section(s) {string.Join(", ", undrawn)}.");
        }
        if (shapes.All(x => x.Role != "area"))
        {
            missing.Add($"The barangay has no boundary as of {date:yyyy-MM-dd}.");
        }
        var heading = await HeadingAsync(barangayId, null, cancellationToken);
        return Result.Success(Sheet(TaxMapSheetKind.SectionIndex, "Section Index Map", date, heading, shapes, missing, null));
    }

    public async Task<Result<TaxMapSheetDto>> BarangayIndexAsync(Guid municipalityId, Guid? districtId, DateOnly? asOf, CancellationToken cancellationToken = default)
    {
        var date = asOf ?? clock.Today;
        var municipality = await db.Municipalities.AsNoTracking().Include(x => x.Province).SingleOrDefaultAsync(x => x.Id == municipalityId, cancellationToken);
        if (municipality is null)
        {
            return Result.Failure<TaxMapSheetDto>("MUNICIPALITY_NOT_FOUND", "The specified city or municipality does not exist.");
        }
        var district = districtId is { } d ? await db.CityDistricts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == d && x.MunicipalityId == municipalityId, cancellationToken) : null;
        if (districtId is not null && district is null)
        {
            return Result.Failure<TaxMapSheetDto>("CITY_DISTRICT_NOT_FOUND", "The specified district does not exist in this city or municipality.");
        }
        var barangays = await db.Barangays.AsNoTracking()
            .Where(x => x.MunicipalityId == municipalityId && (districtId == null || x.CityDistrictId == districtId) && (x.RetiredOn == null || x.RetiredOn > date))
            .OrderBy(x => x.PinIndexNumber).ThenBy(x => x.Name).Select(x => new { x.Id, x.Name, x.PinIndexNumber }).ToListAsync(cancellationToken);
        var ids = barangays.Select(x => x.Id).ToList();
        var shapes = await db.BarangayBoundaries.AsNoTracking()
            .Where(v => ids.Contains(v.BarangayId) && v.EffectiveDate <= date && (v.EndDate == null || v.EndDate > date))
            .Select(v => new Shape(v.Id, v.Barangay!.PinIndexNumber ?? "—", v.Barangay.Name, "unit", v.Source, v.SourceReference, v.Geometry))
            .ToListAsync(cancellationToken);

        var missing = new List<string>();
        var drawn = await db.BarangayBoundaries.Where(v => ids.Contains(v.BarangayId) && v.EffectiveDate <= date && (v.EndDate == null || v.EndDate > date))
            .Select(v => v.BarangayId).ToListAsync(cancellationToken);
        var undrawn = barangays.Where(x => !drawn.Contains(x.Id)).Select(x => x.Name).ToList();
        if (undrawn.Count > 0)
        {
            missing.Add($"No boundary as of {date:yyyy-MM-dd} for {undrawn.Count} barangay(s): {string.Join(", ", undrawn.Take(20))}{(undrawn.Count > 20 ? " …" : "")}.");
        }
        var unnumbered = barangays.Count(x => x.PinIndexNumber is null);
        if (unnumbered > 0)
        {
            missing.Add($"{unnumbered} barangay(s) have no index number yet (labelled \"—\").");
        }
        var cityHeads = municipality.PinIndexNumber is { Length: 3 };
        List<TaxMapSheetHeading> heading =
        [
            cityHeads
                ? new("City/Municipality", municipality.Name, municipality.PinIndexNumber)
                : new("Province", municipality.Province?.Name, municipality.Province?.PinIndexNumber),
        ];
        if (!cityHeads)
        {
            heading.Add(new("Municipality", municipality.Name, municipality.PinIndexNumber));
        }
        if (district is not null)
        {
            heading.Add(new("District", district.Name, district.IndexNumber));
        }
        return Result.Success(Sheet(TaxMapSheetKind.BarangayIndex, "Barangay Index Map", date, heading, shapes, missing, null));
    }

    /// <summary>Province or city, municipality or district, barangay and (for a tax map) section, with their index numbers.</summary>
    private async Task<List<TaxMapSheetHeading>> HeadingAsync(Guid barangayId, string? sectionIndex, CancellationToken ct)
    {
        var b = await db.Barangays.AsNoTracking().Where(x => x.Id == barangayId).Select(x => new
        {
            x.Name, District = x.CityDistrict!.Name, Municipality = x.Municipality!.Name, MunicipalityIndex = x.Municipality.PinIndexNumber,
            Province = x.Municipality.Province!.Name,
        }).SingleAsync(ct);
        var index = await PinContexts.ForBarangayAsync(db, barangayId, clock.Today.Year, sectionIndex, ct);
        var cityHeads = b.MunicipalityIndex is { Length: 3 };
        List<TaxMapSheetHeading> heading =
        [
            new(cityHeads ? "City/Municipality" : "Province", cityHeads ? b.Municipality : b.Province, index.LguIndex),
        ];
        if (b.District is not null || !cityHeads)
        {
            heading.Add(new(b.District is not null ? "District" : "Municipality", b.District ?? b.Municipality, index.MunicipalityIndex));
        }
        heading.Add(new("Barangay", b.Name, index.BarangayIndex));
        if (sectionIndex is not null)
        {
            heading.Add(new("Section", null, sectionIndex));
        }
        return heading;
    }

    private static TaxMapSheetDto Sheet(TaxMapSheetKind kind, string title, DateOnly date, List<TaxMapSheetHeading> heading, List<Shape> shapes, List<string> missing, Envelope? fallback)
    {
        // A disputed area may reach beyond the sheet; the sheet is fitted to its own boundaries.
        var extent = Envelope(shapes.Where(x => x.Role != "disputed").Select(x => x.Geometry)) ?? fallback;
        var features = shapes.OrderBy(x => x.Role).ThenBy(x => x.Label, StringComparer.Ordinal).Select(x => new TaxMapSheetFeature("Feature", x.Id,
            JsonSerializer.SerializeToElement(x.Geometry, GeoJsonOptions),
            new TaxMapSheetFeatureProperties(x.Label, x.Name, x.Role, x.Source, x.SourceReference))).ToList();
        return new TaxMapSheetDto(kind, title, date, heading,
            extent is null ? null : [extent.MinX, extent.MinY, extent.MaxX, extent.MaxY], features, missing);
    }

    private static Envelope? Envelope(IEnumerable<Geometry> geometries)
    {
        Envelope? result = null;
        foreach (var g in geometries)
        {
            result ??= new Envelope();
            result.ExpandToInclude(g.EnvelopeInternal);
        }
        return result;
    }
}
