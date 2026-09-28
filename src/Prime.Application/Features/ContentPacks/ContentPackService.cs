using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Application.Features.ContentPacks;

public interface IContentPackService
{
    Task<Result<IReadOnlyList<ContentPackInfo>>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Validates <paramref name="pack"/> and compares it with the database. Writes nothing.</summary>
    Task<Result<ContentPackPreviewDto>> PreviewAsync(string pack, CancellationToken cancellationToken = default);

    /// <summary>Stores an uploaded pack zip in the content root (step C4); preview it next.</summary>
    Task<Result<ContentPackInfo>> UploadAsync(Stream zip, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies <paramref name="pack"/> if it is valid and its files still hash to the
    /// previewed <see cref="ImportContentPackRequest.Fingerprint"/>. One transaction;
    /// provenance recorded per record. A pack that would change nothing is not recorded.
    /// </summary>
    Task<Result<ContentImportResultDto>> ImportAsync(string pack, ImportContentPackRequest request, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<ContentImportDto>>> ListImportsAsync(string? pack, PagedRequest request, CancellationToken cancellationToken = default);

    Task<Result<ContentImportDto>> GetImportAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<ContentImportItemDto>>> ListImportItemsAsync(Guid id, PagedRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Step C1 of the LGU content pack (docs/analysis/lgu-content-pack.md §3.2–§3.3):
/// READ → VALIDATE → PREVIEW for geography (provinces, municipalities,
/// barangays with their assessor's index numbers) and lookups. The preview
/// applies the same rules the index-number screens do (formats, uniqueness,
/// numbers locked once permanent PINs use them) so an import cannot fail on
/// them later. Blank optional cells mean "keep the current value"; nothing is
/// ever cleared or removed by a pack (decision Q4).
/// </summary>
public sealed partial class ContentPackService(IApplicationDbContext db, IContentPackSource source, ICurrentUserService currentUser,
    ContentPackVersionedContent versioned) : IContentPackService
{
    public const string ManifestFile = "manifest.json";
    public const int ChangeListLimit = 200;
    public const int MissingKeyLimit = 50;

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{0,63}$")] private static partial Regex PackName();
    [GeneratedRegex("^[0-9A-Za-z-]{1,20}$")] private static partial Regex PsgcAllowed();
    [GeneratedRegex("^[0-9]{9,10}$")] private static partial Regex PsgcTypical();
    [GeneratedRegex("^[0-9]{3}$")] private static partial Regex ThreeDigits();
    [GeneratedRegex("^[0-9]{2,3}$")] private static partial Regex TwoOrThreeDigits();
    [GeneratedRegex("^[0-9]{4}$")] private static partial Regex FourDigits();
    [GeneratedRegex(@"\p{C}")] private static partial Regex ControlCharacter();

    private interface ILookupSpec
    {
        string Name { get; }
        IQueryable<LookupEntity> Query(IApplicationDbContext d);
        /// <summary>A new, tracked row of this lookup.</summary>
        LookupEntity Add(IApplicationDbContext d);
    }

    private sealed class LookupSpec<T>(string name, Func<IApplicationDbContext, DbSet<T>> set) : ILookupSpec where T : LookupEntity, new()
    {
        public string Name => name;
        public IQueryable<LookupEntity> Query(IApplicationDbContext d) => set(d);
        public LookupEntity Add(IApplicationDbContext d) => set(d).Add(new T()).Entity;
    }

    private static ILookupSpec L<T>(string name, Func<IApplicationDbContext, DbSet<T>> set) where T : LookupEntity, new() => new LookupSpec<T>(name, set);

    /// <summary>The lookups a pack may carry, by manifest name (the <c>/api/reference</c> route names).</summary>
    private static readonly IReadOnlyDictionary<string, ILookupSpec> Lookups = new[]
    {
        L("zones", d => d.Zones),
        L("classifications", d => d.Classifications),
        L("actual-uses", d => d.ActualUses),
        L("sub-classifications", d => d.SubClassifications),
        L("ownership-types", d => d.OwnershipTypes),
        L("property-types", d => d.PropertyTypes),
        L("road-types", d => d.RoadTypes),
        L("conditions", d => d.Conditions),
        L("building-types", d => d.BuildingTypes),
        L("structural-types", d => d.StructuralTypes),
        L("building-component-types", d => d.BuildingComponentTypes),
        L("machinery-types", d => d.MachineryTypes),
        L("improvement-kinds", d => d.ImprovementKinds),
        L("title-types", d => d.TitleTypes),
        L("structural-parts", d => d.StructuralParts),
        L("structural-materials", d => d.StructuralMaterials),
        L("annotation-types", d => d.AnnotationTypes),
    }.ToDictionary(x => x.Name);

    public static IReadOnlyCollection<string> LookupNames => Lookups.Keys.ToList();

    public Task<Result<IReadOnlyList<ContentPackInfo>>> ListAsync(CancellationToken cancellationToken = default) =>
        source.ListAsync(cancellationToken);

    public Task<Result<ContentPackInfo>> UploadAsync(Stream zip, CancellationToken cancellationToken = default) =>
        source.SaveUploadAsync(zip, cancellationToken);

    /// <summary>Whether <paramref name="pack"/> is a valid pack (folder) name.</summary>
    public static bool IsPackName(string? pack) => pack is not null && PackName().IsMatch(pack);

    public async Task<Result<ContentPackPreviewDto>> PreviewAsync(string pack, CancellationToken cancellationToken = default)
    {
        var built = await BuildAsync(pack, cancellationToken);
        return built.IsFailure ? Result.Failure<ContentPackPreviewDto>(built.Code!, built.Message!) : Result.Success(built.Value.Preview);
    }

    /// <summary>The preview and, for the import, the full plan behind it.</summary>
    private sealed record Built(ContentPackPreviewDto Preview, List<FileWork> Files);

    private async Task<Result<Built>> BuildAsync(string pack, CancellationToken cancellationToken)
    {
        if (pack is null || !PackName().IsMatch(pack))
        {
            return Result.Failure<Built>("VALIDATION_FAILED",
                "A pack name is 1–64 characters: lower-case letters, digits, '-' or '_', starting with a letter or digit.");
        }

        var manifestRead = await source.ReadAsync(pack, ManifestFile, cancellationToken);
        if (manifestRead.IsFailure)
        {
            return Result.Failure<Built>(manifestRead.Code!, manifestRead.Message!);
        }

        var issues = new List<ContentIssueDto>();
        if (manifestRead.Value.Content is not { } manifestBytes)
        {
            issues.Add(Error("MANIFEST_MISSING", $"The pack has no readable {ManifestFile}: {manifestRead.Value.Error}"));
            return Result.Success(new Built(Finish(pack, null, null, null, issues, []), []));
        }

        var manifestSha = Sha256(manifestBytes);
        ContentPackManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ContentPackManifest>(WithoutBom(manifestBytes));
        }
        catch (JsonException ex)
        {
            issues.Add(Error("MANIFEST_INVALID", $"{ManifestFile} is not valid JSON: {ex.Message}", ex.LineNumber is { } l ? (int)l + 1 : null));
            return Result.Success(new Built(Finish(pack, null, null, manifestSha, issues, []), []));
        }
        if (manifest is null)
        {
            issues.Add(Error("MANIFEST_INVALID", $"{ManifestFile} is empty."));
            return Result.Success(new Built(Finish(pack, null, null, manifestSha, issues, []), []));
        }

        var entries = CheckManifest(pack, manifest, issues);
        var files = new List<FileWork>();
        foreach (var entry in entries)
        {
            var work = new FileWork(entry);
            files.Add(work);
            var read = await source.ReadAsync(pack, entry.Path, cancellationToken);
            if (read.IsFailure)
            {
                return Result.Failure<Built>(read.Code!, read.Message!);
            }
            if (read.Value.Content is not { } bytes)
            {
                work.Issues.Add(Error("FILE_UNREADABLE", read.Value.Error ?? "The file could not be read."));
                continue;
            }
            work.Sha256 = Sha256(bytes);
            if (!entry.Supported)
            {
                work.Issues.Add(Warning("NOT_YET_SUPPORTED",
                    $"Files of kind '{entry.Kind}' are checked for presence only; PRIME reads them from step {ContentFileKinds.Later[entry.Kind]}."));
                continue;
            }
            if (ContentFileKinds.Versioned.Contains(entry.Kind))
            {
                work.Bytes = bytes;
                continue;
            }
            var table = ContentPackCsv.Parse(bytes);
            if (table.Error is not null)
            {
                work.Issues.Add(Error("CSV_INVALID", table.Error, table.ErrorLine));
                continue;
            }
            work.Table = table;
        }

        await PreviewGeographyAsync(files, cancellationToken);
        foreach (var work in files.Where(f => f.Entry.Kind == ContentFileKinds.Lookup && f.Table is not null))
        {
            await PreviewLookupAsync(work, files, cancellationToken);
        }
        CheckRequiredPropertyTypes(files, await db.PropertyTypes.AsNoTracking().Select(x => x.Code).ToListAsync(cancellationToken));
        foreach (var work in files.Where(f => f.Bytes is not null))
        {
            await PreviewVersionedAsync(pack, work, cancellationToken);
        }

        return Result.Success(new Built(Finish(pack, manifest.Version, manifest.Description, manifestSha, issues, files), files));
    }

    // --- Manifest ---

    private sealed record PlannedRow(string Key, bool IsNew, IReadOnlyList<ContentFieldChangeDto> Changes, CsvRow Row);

    private sealed record ManifestEntry(string Kind, string Path, string? Lookup, string? Source, bool Supported);

    private sealed class FileWork(ManifestEntry entry)
    {
        public ManifestEntry Entry { get; } = entry;
        public string? Sha256 { get; set; }
        public CsvTable? Table { get; set; }
        public List<ContentIssueDto> Issues { get; } = [];
        public List<ContentChangeDto> Changes { get; } = [];
        /// <summary>Every row to create or change, uncapped: what an import applies.</summary>
        public List<PlannedRow> Plan { get; } = [];
        /// <summary>A JSON catalogue's raw content (versioned kinds).</summary>
        public byte[]? Bytes { get; set; }
        /// <summary>Items in a JSON catalogue.</summary>
        public int ItemCount { get; set; }
        /// <summary>New configuration versions to create (versioned kinds).</summary>
        public List<PlannedVersion> Versions { get; } = [];
        /// <summary>Files a catalogue refers to (form templates), as "path:sha256", part of the fingerprint.</summary>
        public List<string> Referenced { get; } = [];
        public int New { get; set; }
        public int Changed { get; set; }
        public int Unchanged { get; set; }
        public List<string> Missing { get; } = [];
        /// <summary>Rows that passed their own checks, by natural key (PSGC or code).</summary>
        public Dictionary<string, CsvRow> Valid { get; } = new(StringComparer.Ordinal);
    }

    private static List<ManifestEntry> CheckManifest(string pack, ContentPackManifest manifest, List<ContentIssueDto> issues)
    {
        if (manifest.SchemaVersion != 1)
        {
            issues.Add(Error("MANIFEST_SCHEMA", $"schemaVersion {manifest.SchemaVersion} is not supported; this PRIME reads schemaVersion 1."));
        }
        if (manifest.Pack != pack)
        {
            issues.Add(Error("MANIFEST_PACK_MISMATCH", $"The manifest names pack '{manifest.Pack}', but the folder is '{pack}'. They must match."));
        }
        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            issues.Add(Error("MANIFEST_VERSION", "The manifest has no version; every pack import records the version it applied."));
        }
        if (manifest.Files is not { Count: > 0 })
        {
            issues.Add(Error("MANIFEST_NO_FILES", "The manifest lists no files."));
            return [];
        }

        var entries = new List<ManifestEntry>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var singletons = new HashSet<string>();
        for (var i = 0; i < manifest.Files.Count; i++)
        {
            var f = manifest.Files[i];
            var at = $"files[{i}]";
            if (f is null)
            {
                issues.Add(Error("MANIFEST_PATH", $"{at}: the entry is empty.", field: at));
                continue;
            }
            var kind = f.Kind?.Trim().ToLowerInvariant();
            if (kind is null || !(ContentFileKinds.Supported.Contains(kind) || ContentFileKinds.Later.ContainsKey(kind)))
            {
                issues.Add(Error("MANIFEST_KIND", $"{at}: unknown kind '{f.Kind}'.", field: at));
                continue;
            }
            if (PathProblem(f.Path) is { } problem)
            {
                issues.Add(Error("MANIFEST_PATH", $"{at}: {problem}", field: at));
                continue;
            }
            var path = f.Path!.Trim();
            if (!paths.Add(path))
            {
                issues.Add(Error("MANIFEST_DUPLICATE_PATH", $"{at}: '{path}' is listed more than once.", field: at));
                continue;
            }
            string? lookup = null;
            if (kind == ContentFileKinds.Lookup)
            {
                lookup = f.Lookup?.Trim().ToLowerInvariant();
                if (lookup is null || !Lookups.ContainsKey(lookup))
                {
                    issues.Add(Error("MANIFEST_LOOKUP", $"{at}: unknown lookup '{f.Lookup}'. Known: {string.Join(", ", Lookups.Keys.Order())}.", field: at));
                    continue;
                }
            }
            if (ContentFileKinds.Supported.Contains(kind) && !singletons.Add(lookup is null ? kind : $"lookup:{lookup}"))
            {
                issues.Add(Error("MANIFEST_DUPLICATE_KIND", $"{at}: a pack carries one {(lookup ?? kind)} file.", field: at));
                continue;
            }
            if (string.IsNullOrWhiteSpace(f.Source))
            {
                issues.Add(Warning("SOURCE_MISSING", $"{at} ({path}) cites no source; each row must then give its own.", field: at));
            }
            entries.Add(new ManifestEntry(kind, path, lookup, Clean(f.Source), ContentFileKinds.Supported.Contains(kind)));
        }
        return entries;
    }

    /// <summary>Why <paramref name="path"/> may not name a pack file; null when it may. Public for tests.</summary>
    public static string? PathProblem(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "the path is missing.";
        }
        var p = path.Trim();
        if (p.Length > 260)
        {
            return "the path is longer than 260 characters.";
        }
        if (p.Contains('\\') || p.Contains(':') || p.StartsWith('/'))
        {
            return "use a relative path with forward slashes, e.g. geography/provinces.csv.";
        }
        if (p.Split('/').Any(s => s is "" or "." or ".."))
        {
            return "the path may not contain empty, '.' or '..' segments.";
        }
        if (ControlCharacter().IsMatch(p))
        {
            return "the path contains control characters.";
        }
        return null;
    }

    // --- Geography ---

    private async Task PreviewGeographyAsync(List<FileWork> files, CancellationToken ct)
    {
        var provFile = files.FirstOrDefault(f => f.Entry.Kind == ContentFileKinds.Provinces && f.Table is not null);
        var munFile = files.FirstOrDefault(f => f.Entry.Kind == ContentFileKinds.Municipalities && f.Table is not null);
        var brgyFile = files.FirstOrDefault(f => f.Entry.Kind == ContentFileKinds.Barangays && f.Table is not null);
        if (provFile is null && munFile is null && brgyFile is null)
        {
            return;
        }

        var provinces = await db.Provinces.AsNoTracking().ToListAsync(ct);
        var municipalities = await db.Municipalities.AsNoTracking().ToListAsync(ct);
        var provinceByPsgc = provinces.ToDictionary(x => x.PsgcCode, StringComparer.Ordinal);
        var provincePsgcById = provinces.ToDictionary(x => x.Id, x => x.PsgcCode);
        var municipalityByPsgc = municipalities.ToDictionary(x => x.PsgcCode, StringComparer.Ordinal);

        // Final index numbers after the pack: the database's, overridden by the pack's non-blank numbers.
        var provinceIndex = provinces.ToDictionary(x => x.PsgcCode, x => x.PinIndexNumber, StringComparer.Ordinal);
        var municipalityIndex = municipalities.ToDictionary(x => x.PsgcCode, x => (Province: provincePsgcById[x.ProvinceId], x.PinIndexNumber),
            StringComparer.Ordinal);

        if (provFile is not null)
        {
            if (Columns(provFile, ["psgc_code", "name"], ["index_number", "source"]))
            {
                foreach (var row in provFile.Table!.Rows)
                {
                    if (!CheckPsgc(provFile, row, "psgc_code", out var psgc) || !CheckName(provFile, row, out var name)
                        || !CheckIndex(provFile, row, ThreeDigits(), "A province index number has 3 digits.", out var index)
                        || !CheckSource(provFile, row))
                    {
                        continue;
                    }
                    provFile.Valid[psgc] = row;
                    provinceByPsgc.TryGetValue(psgc, out var existing);
                    var changes = new List<ContentFieldChangeDto>();
                    AddChange(changes, "name", existing?.Name, name);
                    AddChange(changes, "index_number", existing?.PinIndexNumber, index, keepWhenNull: true);
                    Tally(provFile, row, psgc, name, existing is null, changes);
                    if (index is not null)
                    {
                        provinceIndex[psgc] = index;
                    }
                    if (existing is { PinIndexNumber: { } current } && index is not null && index != current
                        && await db.PinAssignments.AnyAsync(a => a.Kind == PinKind.Permanent && a.Barangay!.Municipality!.ProvinceId == existing.Id, ct))
                    {
                        provFile.Issues.Add(Error("PIN_INDEX_LOCKED", $"Province {psgc}: index number {current} is part of permanent PINs already issued and cannot change.", row.Line, "index_number"));
                    }
                }
                provFile.Missing.AddRange(provinces.Where(p => !provFile.Valid.ContainsKey(p.PsgcCode) && !Listed(provFile, p.PsgcCode)).Select(p => $"{p.PsgcCode} {p.Name}"));
            }
        }

        var packProvinces = provFile?.Table?.Rows.Select(r => r.Get("psgc_code")).OfType<string>().ToHashSet(StringComparer.Ordinal) ?? [];
        if (munFile is not null && Columns(munFile, ["psgc_code", "province_psgc", "name"], ["is_city", "index_number", "source"]))
        {
            foreach (var row in munFile.Table!.Rows)
            {
                if (!CheckPsgc(munFile, row, "psgc_code", out var psgc) || !CheckName(munFile, row, out var name)
                    || !CheckIndex(munFile, row, TwoOrThreeDigits(),
                        "Enter 3 digits for a city with its own index number, or 2 digits for a municipality within its province.", out var index)
                    || !CheckBool(munFile, row, "is_city", out var isCity) || !CheckSource(munFile, row))
                {
                    continue;
                }
                var parent = row.Get("province_psgc");
                if (parent is null || !(packProvinces.Contains(parent) || provinceByPsgc.ContainsKey(parent)))
                {
                    munFile.Issues.Add(Error("PARENT_NOT_FOUND", $"Municipality {psgc}: province {parent} is neither in the pack nor in PRIME.", row.Line, "province_psgc"));
                    continue;
                }
                municipalityByPsgc.TryGetValue(psgc, out var existing);
                if (existing is not null && provincePsgcById[existing.ProvinceId] != parent)
                {
                    munFile.Issues.Add(Error("MOVE_NOT_SUPPORTED", $"Municipality {psgc} belongs to another province in PRIME; a pack cannot move it.", row.Line, "province_psgc"));
                    continue;
                }
                munFile.Valid[psgc] = row;
                var changes = new List<ContentFieldChangeDto>();
                AddChange(changes, "name", existing?.Name, name);
                AddChange(changes, "is_city", existing is null ? null : Bool(existing.IsCity), isCity is null ? (existing is null ? Bool(false) : null) : Bool(isCity.Value), keepWhenNull: true);
                AddChange(changes, "index_number", existing?.PinIndexNumber, index, keepWhenNull: true);
                Tally(munFile, row, psgc, name, existing is null, changes);
                municipalityIndex[psgc] = (parent, index ?? (existing?.PinIndexNumber));
                if (existing is { PinIndexNumber: { } current } && index is not null && index != current
                    && await db.PinAssignments.AnyAsync(a => a.Kind == PinKind.Permanent && a.Barangay!.MunicipalityId == existing.Id, ct))
                {
                    munFile.Issues.Add(Error("PIN_INDEX_LOCKED", $"Municipality {psgc}: index number {current} is part of permanent PINs already issued and cannot change.", row.Line, "index_number"));
                }
            }
            var scope = packProvinces.Concat(munFile.Valid.Values.Select(r => r.Get("province_psgc")!)).ToHashSet(StringComparer.Ordinal);
            munFile.Missing.AddRange(municipalities
                .Where(m => scope.Contains(provincePsgcById[m.ProvinceId]) && !munFile.Valid.ContainsKey(m.PsgcCode) && !Listed(munFile, m.PsgcCode))
                .Select(m => $"{m.PsgcCode} {m.Name}"));
        }

        // Provinces, cities and Metro Manila municipalities share one 3-digit number space;
        // a municipality's 2-digit number is unique within its province.
        var target = provFile ?? munFile;
        foreach (var group in target is null ? [] : provinceIndex.Where(x => x.Value is not null).Select(x => (Key: x.Key, Number: x.Value!, What: "province"))
                     .Concat(municipalityIndex.Where(x => x.Value.PinIndexNumber is { Length: 3 }).Select(x => (Key: x.Key, Number: x.Value.PinIndexNumber!, What: "city")))
                     .GroupBy(x => x.Number).Where(g => g.Count() > 1))
        {
            target!.Issues.Add(Error("PIN_INDEX_DUPLICATE", $"Index number {group.Key} would be used by {string.Join(", ", group.Select(x => $"{x.What} {x.Key}"))}."));
        }
        foreach (var group in target is null ? [] : municipalityIndex.Where(x => x.Value.PinIndexNumber is { Length: 2 })
                     .GroupBy(x => (x.Value.Province, x.Value.PinIndexNumber)).Where(g => g.Count() > 1))
        {
            (munFile ?? target)!.Issues.Add(Error("PIN_INDEX_DUPLICATE",
                $"Municipal index number {group.Key.PinIndexNumber} would be used twice in province {group.Key.Province}: {string.Join(", ", group.Select(x => x.Key))}."));
        }

        if (brgyFile is not null && Columns(brgyFile, ["psgc_code", "municipality_psgc", "name"], ["index_number", "source"]))
        {
            var packMunicipalities = munFile?.Table?.Rows.Select(r => r.Get("psgc_code")).OfType<string>().ToHashSet(StringComparer.Ordinal) ?? [];
            var parents = brgyFile.Table!.Rows.Select(r => r.Get("municipality_psgc")).OfType<string>()
                .Concat(packMunicipalities).Distinct().ToList();
            var parentIds = municipalities.Where(m => parents.Contains(m.PsgcCode)).Select(m => m.Id).ToList();
            var psgcs = brgyFile.Table.Rows.Select(r => r.Get("psgc_code")).OfType<string>().ToList();
            var barangays = await db.Barangays.AsNoTracking()
                .Where(b => parentIds.Contains(b.MunicipalityId) || psgcs.Contains(b.PsgcCode)).ToListAsync(ct);
            var barangayByPsgc = barangays.ToDictionary(x => x.PsgcCode, StringComparer.Ordinal);
            var municipalityPsgcById = municipalities.ToDictionary(m => m.Id, m => m.PsgcCode);
            // Final numbers per (municipality, district); a pack sets no districts, so its new barangays have none.
            var barangayIndex = barangays.ToDictionary(b => b.PsgcCode,
                b => (Municipality: municipalityPsgcById[b.MunicipalityId], b.CityDistrictId, b.PinIndexNumber), StringComparer.Ordinal);

            foreach (var row in brgyFile.Table.Rows)
            {
                if (!CheckPsgc(brgyFile, row, "psgc_code", out var psgc) || !CheckName(brgyFile, row, out var name)
                    || !CheckIndex(brgyFile, row, FourDigits(), "A barangay index number has 4 digits.", out var index) || !CheckSource(brgyFile, row))
                {
                    continue;
                }
                var parent = row.Get("municipality_psgc");
                if (parent is null || !(packMunicipalities.Contains(parent) || municipalityByPsgc.ContainsKey(parent)))
                {
                    brgyFile.Issues.Add(Error("PARENT_NOT_FOUND", $"Barangay {psgc}: city/municipality {parent} is neither in the pack nor in PRIME.", row.Line, "municipality_psgc"));
                    continue;
                }
                barangayByPsgc.TryGetValue(psgc, out var existing);
                if (existing is not null && municipalityPsgcById[existing.MunicipalityId] != parent)
                {
                    brgyFile.Issues.Add(Error("MOVE_NOT_SUPPORTED", $"Barangay {psgc} belongs to another city/municipality in PRIME; a pack cannot move it.", row.Line, "municipality_psgc"));
                    continue;
                }
                if (existing is { RetiredOn: not null } && index is not null && index != existing.PinIndexNumber)
                {
                    brgyFile.Issues.Add(Error("BARANGAY_RETIRED", $"Barangay {psgc} is retired; it keeps its index number {existing.PinIndexNumber}.", row.Line, "index_number"));
                    continue;
                }
                brgyFile.Valid[psgc] = row;
                var changes = new List<ContentFieldChangeDto>();
                AddChange(changes, "name", existing?.Name, name);
                AddChange(changes, "index_number", existing?.PinIndexNumber, index, keepWhenNull: true);
                Tally(brgyFile, row, psgc, name, existing is null, changes);
                barangayIndex[psgc] = (parent, existing?.CityDistrictId, index ?? existing?.PinIndexNumber);
                if (existing is { PinIndexNumber: { } current } && index is not null && index != current
                    && await db.PinAssignments.AnyAsync(a => a.Kind == PinKind.Permanent && a.BarangayId == existing.Id, ct))
                {
                    brgyFile.Issues.Add(Error("PIN_INDEX_LOCKED", $"Barangay {psgc}: index number {current} is part of permanent PINs already issued and cannot change.", row.Line, "index_number"));
                }
            }
            foreach (var group in barangayIndex.Where(x => x.Value.PinIndexNumber is not null)
                         .GroupBy(x => (x.Value.Municipality, x.Value.CityDistrictId, x.Value.PinIndexNumber)).Where(g => g.Count() > 1))
            {
                brgyFile.Issues.Add(Error("PIN_INDEX_DUPLICATE",
                    $"Barangay index number {group.Key.PinIndexNumber} would be used twice in city/municipality {group.Key.Municipality}: {string.Join(", ", group.Select(x => x.Key))}. Numbers are never reused, retired barangays included."));
            }
            var scope = parents.ToHashSet(StringComparer.Ordinal);
            brgyFile.Missing.AddRange(barangays
                .Where(b => b.RetiredOn is null && scope.Contains(municipalityPsgcById[b.MunicipalityId]) && !brgyFile.Valid.ContainsKey(b.PsgcCode) && !Listed(brgyFile, b.PsgcCode))
                .Select(b => $"{b.PsgcCode} {b.Name}"));
        }
    }

    // --- Lookups ---

    private async Task PreviewLookupAsync(FileWork work, List<FileWork> files, CancellationToken ct)
    {
        var isMaterial = work.Entry.Lookup == "structural-materials";
        string[] required = isMaterial ? ["code", "name", "part_code"] : ["code", "name"];
        if (!Columns(work, required, ["description", "sort_order", "is_active", "source"]))
        {
            return;
        }

        var existing = await Lookups[work.Entry.Lookup!].Query(db).AsNoTracking().ToListAsync(ct);
        var byCode = existing.ToDictionary(x => x.Code, StringComparer.Ordinal);
        var byUpper = existing.GroupBy(x => x.Code.ToUpperInvariant()).ToDictionary(g => g.Key, g => g.First().Code);

        Dictionary<Guid, string> partCodeById = [];
        HashSet<string> knownParts = new(StringComparer.Ordinal);
        if (isMaterial)
        {
            var parts = await db.StructuralParts.AsNoTracking().Select(p => new { p.Id, p.Code }).ToListAsync(ct);
            partCodeById = parts.ToDictionary(p => p.Id, p => p.Code);
            knownParts.UnionWith(parts.Select(p => p.Code));
            if (files.FirstOrDefault(f => f.Entry.Lookup == "structural-parts") is { Table: { } partTable })
            {
                knownParts.UnionWith(partTable.Rows.Select(r => r.Get("code")).OfType<string>());
            }
        }

        foreach (var row in work.Table!.Rows)
        {
            var code = row.Get("code");
            if (code is null)
            {
                work.Issues.Add(Error("REQUIRED", "The code is missing.", row.Line, "code"));
                continue;
            }
            if (code.Length > 50 || ControlCharacter().IsMatch(code))
            {
                work.Issues.Add(Error("CODE_INVALID", $"Code '{code}' is longer than 50 characters or contains control characters.", row.Line, "code"));
                continue;
            }
            if (work.Valid.ContainsKey(code) || Listed(work, code, before: row))
            {
                work.Issues.Add(Error("DUPLICATE_KEY", $"Code {code} appears more than once in the file.", row.Line, "code"));
                continue;
            }
            if (!CheckName(work, row, out var name) || !CheckLength(work, row, "description", 1000, out var description)
                || !CheckInt(work, row, "sort_order", out var sortOrder) || !CheckBool(work, row, "is_active", out var isActive) || !CheckSource(work, row))
            {
                continue;
            }
            byCode.TryGetValue(code, out var current);
            if (current is null && byUpper.TryGetValue(code.ToUpperInvariant(), out var similar))
            {
                work.Issues.Add(Warning("CODE_CASE", $"Code {code} differs only in letter case from existing code {similar}; codes are case-sensitive, so this adds a new entry.", row.Line, "code"));
            }

            var changes = new List<ContentFieldChangeDto>();
            if (isMaterial)
            {
                var part = row.Get("part_code")!;
                if (!knownParts.Contains(part))
                {
                    work.Issues.Add(Error("PARENT_NOT_FOUND", $"Material {code}: structural part {part} is neither in the pack nor in PRIME.", row.Line, "part_code"));
                    continue;
                }
                if (current is StructuralMaterial m && partCodeById.GetValueOrDefault(m.StructuralPartId) is { } currentPart && currentPart != part)
                {
                    work.Issues.Add(Error("MOVE_NOT_SUPPORTED",
                        $"Material {code} belongs to part {currentPart} in PRIME; a material's part cannot change. Material codes are unique across all parts.", row.Line, "part_code"));
                    continue;
                }
                AddChange(changes, "part_code", null, current is null ? part : null, keepWhenNull: true);
            }
            work.Valid[code] = row;
            AddChange(changes, "name", current?.Name, name);
            AddChange(changes, "description", current?.Description, description, keepWhenNull: true);
            AddChange(changes, "sort_order", current?.SortOrder.ToString(CultureInfo.InvariantCulture), sortOrder?.ToString(CultureInfo.InvariantCulture), keepWhenNull: true);
            AddChange(changes, "is_active", current is null ? null : Bool(current.IsActive), isActive is null ? (current is null ? Bool(true) : null) : Bool(isActive.Value), keepWhenNull: true);
            Tally(work, row, code, name, current is null, changes);
        }
        work.Missing.AddRange(existing.Where(x => !work.Valid.ContainsKey(x.Code) && !Listed(work, x.Code)).Select(x => $"{x.Code} {x.Name}"));
    }

    /// <summary>Step C3: transaction types, numbering schemes, approval chains and forms (<see cref="ContentPackVersionedContent"/>).</summary>
    private async Task PreviewVersionedAsync(string pack, FileWork work, CancellationToken ct)
    {
        var r = await versioned.PreviewAsync(work.Entry.Kind, work.Entry.Path, work.Bytes!, work.Entry.Source, p => source.ReadAsync(pack, p, ct), ct);
        work.Issues.AddRange(r.Issues);
        work.Versions.AddRange(r.Versions);
        work.Referenced.AddRange(r.Referenced);
        work.ItemCount = r.Items;
        work.New = r.Versions.Count;
        work.Unchanged = r.Unchanged;
        work.Changes.AddRange(r.Versions.Take(ChangeListLimit).Select(v => new ContentChangeDto(v.Key, v.Name, ContentChangeAction.New, v.Changes)));
    }

    /// <summary>PRIME's valuation keys off these property-type codes (<see cref="PropertyTypeCodes"/>); warn if a pack would leave one undefined.</summary>
    private static void CheckRequiredPropertyTypes(List<FileWork> files, IReadOnlyCollection<string> existing)
    {
        if (files.FirstOrDefault(f => f.Entry.Lookup == "property-types" && f.Table is not null) is not { } work)
        {
            return;
        }
        foreach (var code in new[] { PropertyTypeCodes.Land, PropertyTypeCodes.Building, PropertyTypeCodes.Machinery })
        {
            if (!work.Valid.ContainsKey(code) && !existing.Contains(code))
            {
                work.Issues.Add(Warning("PROPERTY_TYPE_REQUIRED", $"PRIME values land, buildings and machinery under property type code {code}; neither the pack nor PRIME defines it."));
            }
        }
    }

    // --- Row checks ---

    private static bool Columns(FileWork work, string[] required, string[] optional)
    {
        var columns = work.Table!.Columns;
        var missing = required.Where(c => !columns.Contains(c)).ToList();
        if (missing.Count > 0)
        {
            work.Issues.Add(Error("COLUMN_MISSING", $"Required column(s) missing: {string.Join(", ", missing)}.", 1));
            return false;
        }
        foreach (var extra in columns.Where(c => !required.Contains(c) && !optional.Contains(c)))
        {
            work.Issues.Add(Warning("COLUMN_UNKNOWN", $"Column '{extra}' is not read and will be ignored.", 1, extra));
        }
        return true;
    }

    private static bool CheckPsgc(FileWork work, CsvRow row, string column, out string psgc)
    {
        psgc = row.Get(column) ?? string.Empty;
        if (psgc.Length == 0)
        {
            work.Issues.Add(Error("REQUIRED", "The PSGC code is missing.", row.Line, column));
            return false;
        }
        if (!PsgcAllowed().IsMatch(psgc))
        {
            work.Issues.Add(Error("PSGC_INVALID", $"PSGC code '{psgc}' may hold only letters, digits and '-', up to 20 characters.", row.Line, column));
            return false;
        }
        if (work.Valid.ContainsKey(psgc) || Listed(work, psgc, before: row))
        {
            work.Issues.Add(Error("DUPLICATE_KEY", $"PSGC code {psgc} appears more than once in the file.", row.Line, column));
            return false;
        }
        if (!PsgcTypical().IsMatch(psgc))
        {
            work.Issues.Add(Warning("PSGC_FORMAT", $"PSGC code {psgc} is not 9 or 10 digits; check it against the PSA's list.", row.Line, column));
        }
        return true;
    }

    private static bool CheckName(FileWork work, CsvRow row, out string name)
    {
        name = row.Get("name") ?? string.Empty;
        if (name.Length == 0)
        {
            work.Issues.Add(Error("REQUIRED", "The name is missing.", row.Line, "name"));
            return false;
        }
        if (name.Length > 200)
        {
            work.Issues.Add(Error("TOO_LONG", "The name is longer than 200 characters.", row.Line, "name"));
            return false;
        }
        return true;
    }

    private static bool CheckIndex(FileWork work, CsvRow row, Regex format, string message, out string? index)
    {
        index = row.Get("index_number");
        if (index is not null && !format.IsMatch(index))
        {
            work.Issues.Add(Error("INDEX_INVALID", $"{message} Got '{index}'.", row.Line, "index_number"));
            return false;
        }
        return true;
    }

    private static bool CheckLength(FileWork work, CsvRow row, string column, int max, out string? value)
    {
        value = row.Get(column);
        if (value is { Length: var n } && n > max)
        {
            work.Issues.Add(Error("TOO_LONG", $"The {column} is longer than {max} characters.", row.Line, column));
            return false;
        }
        return true;
    }

    private static bool CheckInt(FileWork work, CsvRow row, string column, out int? value)
    {
        value = null;
        if (row.Get(column) is not { } text)
        {
            return true;
        }
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            work.Issues.Add(Error("NUMBER_INVALID", $"The {column} '{text}' is not a whole number.", row.Line, column));
            return false;
        }
        value = parsed;
        return true;
    }

    private static bool CheckBool(FileWork work, CsvRow row, string column, out bool? value)
    {
        value = null;
        if (row.Get(column) is not { } text)
        {
            return true;
        }
        value = text.ToLowerInvariant() switch
        {
            "true" or "yes" or "y" or "1" => true,
            "false" or "no" or "n" or "0" => false,
            _ => null,
        };
        if (value is null)
        {
            work.Issues.Add(Error("BOOLEAN_INVALID", $"The {column} '{text}' must be true/false, yes/no or 1/0.", row.Line, column));
            return false;
        }
        return true;
    }

    /// <summary>Each row needs a citation: its own <c>source</c> or the file's.</summary>
    private static bool CheckSource(FileWork work, CsvRow row)
    {
        var rowSource = row.Get("source");
        if (rowSource is { Length: > 500 })
        {
            work.Issues.Add(Error("TOO_LONG", "The source is longer than 500 characters.", row.Line, "source"));
            return false;
        }
        if (rowSource is null && work.Entry.Source is null)
        {
            work.Issues.Add(Error("SOURCE_MISSING", "Neither the row nor its file cites a source.", row.Line, "source"));
            return false;
        }
        return true;
    }

    /// <summary>Whether <paramref name="key"/> is on an earlier row of the file (or any row), valid or not; used for duplicate and missing checks.</summary>
    private static bool Listed(FileWork work, string key, CsvRow? before = null)
    {
        var column = work.Entry.Kind == ContentFileKinds.Lookup ? "code" : "psgc_code";
        return work.Table!.Rows.Any(r => (before is null || r.Line < before.Line) && r.Get(column) == key);
    }

    private static void AddChange(List<ContentFieldChangeDto> changes, string field, string? from, string? to, bool keepWhenNull = false)
    {
        if (keepWhenNull && to is null)
        {
            return;
        }
        if (!string.Equals(from, to, StringComparison.Ordinal))
        {
            changes.Add(new ContentFieldChangeDto(field, from, to));
        }
    }

    private static void Tally(FileWork work, CsvRow row, string key, string name, bool isNew, List<ContentFieldChangeDto> changes)
    {
        if (isNew)
        {
            work.New++;
        }
        else if (changes.Count > 0)
        {
            work.Changed++;
        }
        else
        {
            work.Unchanged++;
            return;
        }
        work.Plan.Add(new PlannedRow(key, isNew, changes, row));
        if (work.Changes.Count < ChangeListLimit)
        {
            work.Changes.Add(new ContentChangeDto(key, name, isNew ? ContentChangeAction.New : ContentChangeAction.Changed, changes));
        }
    }

    // --- Result ---

    private static ContentPackPreviewDto Finish(string pack, string? version, string? description, string? manifestSha,
        List<ContentIssueDto> issues, List<FileWork> files)
    {
        var fileDtos = files.Select(f => new ContentFilePreviewDto(
            f.Entry.Kind, f.Entry.Lookup, f.Entry.Path, f.Entry.Source, f.Sha256, f.Entry.Supported,
            f.Table?.Rows.Count ?? f.ItemCount, f.New, f.Changed, f.Unchanged, f.Missing.Count, f.Missing.Take(MissingKeyLimit).ToList(),
            f.Changes, f.Issues.OrderBy(i => i.Line ?? 0).ToList())).ToList();
        var all = issues.Concat(fileDtos.SelectMany(f => f.Issues)).ToList();
        var errors = all.Count(i => i.Severity == ContentIssueSeverity.Error);
        var fingerprint = manifestSha is null ? null
            : Sha256(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", files.SelectMany(f => f.Referenced.Prepend($"{f.Entry.Path}:{f.Sha256}")).Prepend(manifestSha))));
        return new ContentPackPreviewDto(pack, Clean(version), Clean(description), manifestSha, fingerprint, errors == 0 && files.Count > 0, errors,
            all.Count - errors, issues, fileDtos);
    }

    private static ContentIssueDto Error(string code, string message, int? line = null, string? field = null) =>
        new(ContentIssueSeverity.Error, code, message, line, field);

    private static ContentIssueDto Warning(string code, string message, int? line = null, string? field = null) =>
        new(ContentIssueSeverity.Warning, code, message, line, field);

    private static string Bool(bool value) => value ? "true" : "false";

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>JSON text without a UTF-8 byte-order mark, which Windows editors often write and the JSON reader refuses.</summary>
    public static ReadOnlySpan<byte> WithoutBom(byte[] bytes) =>
        bytes.AsSpan().StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]) ? bytes.AsSpan(3) : bytes;
}
