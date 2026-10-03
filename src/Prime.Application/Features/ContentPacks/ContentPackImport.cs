using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Features.Gis.ReferenceLayers;
using Prime.Domain.Entities.Content;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Application.Features.ContentPacks;

/// <summary>
/// Step C2 (docs/analysis/lgu-content-pack.md §3.2–§3.4): IMPORT → AUDIT for
/// geography and lookups. The import re-runs the preview and applies exactly
/// its plan, so it can never disagree with what the user saw; the files must
/// still hash to the previewed fingerprint. Everything happens in one
/// transaction. Each record created or changed gets a
/// <see cref="ContentImportItem"/> carrying its citation, file and line, and
/// the audit log records the pack as the reason (CLAUDE.md §48).
/// </summary>
public sealed partial class ContentPackService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<ContentImportResultDto>> ImportAsync(string pack, ImportContentPackRequest request, CancellationToken cancellationToken = default)
    {
        if (currentUser.AppUserId is not { } userId)
        {
            return Result.Failure<ContentImportResultDto>("IMPORTER_UNKNOWN", "An import is recorded under the user who confirms it; no signed-in user was found.");
        }
        if (string.IsNullOrWhiteSpace(request.Fingerprint))
        {
            return Result.Failure<ContentImportResultDto>("VALIDATION_FAILED", "Preview the pack first and send the fingerprint the preview returned.");
        }

        var built = await BuildAsync(pack, cancellationToken);
        if (built.IsFailure)
        {
            return Result.Failure<ContentImportResultDto>(built.Code!, built.Message!);
        }
        var preview = built.Value.Preview;
        if (preview.Fingerprint != request.Fingerprint.Trim())
        {
            return Result.Failure<ContentImportResultDto>("CONTENT_PACK_CONFLICT", "The pack's files changed since it was previewed. Preview it again, then import.");
        }
        if (!preview.CanImport)
        {
            return Result.Failure<ContentImportResultDto>("CONTENT_PACK_INVALID", $"The pack has {preview.ErrorCount} error(s). Fix them, preview again, then import.");
        }

        var files = built.Value.Files.Where(f => f.Entry.Supported && (f.Table is not null || f.Versions.Count > 0 || f.Geo is not null)).ToList();
        if (files.All(f => f.Plan.Count == 0 && f.Versions.Count == 0 && (f.Geo is null || f.New + f.Changed == 0)))
        {
            return Result.Success(new ContentImportResultDto(false, "PRIME already matches this pack; nothing was imported or recorded.", null));
        }

        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        currentUser.Reason = $"Content pack {pack} {preview.Version}";
        var items = new List<ContentImportItem>();

        await ApplyGeographyAsync(files, items, cancellationToken);
        var partsAdded = new Dictionary<string, StructuralPart>(StringComparer.Ordinal);
        // Parts before materials, which point to them; otherwise manifest order.
        foreach (var work in files.Where(f => f.Entry.Kind == ContentFileKinds.Lookup)
                     .OrderBy(f => f.Entry.Lookup == "structural-materials" ? 1 : 0))
        {
            await ApplyLookupAsync(work, partsAdded, items, cancellationToken);
        }

        // Versioned configuration: each planned version is created as a Draft by its own service (step C3).
        foreach (var work in files.Where(f => f.Versions.Count > 0))
        {
            foreach (var version in work.Versions)
            {
                var created = await versioned.CreateAsync(version, cancellationToken);
                if (created.IsFailure)
                {
                    // Nothing is kept: the transaction is rolled back when disposed uncommitted.
                    return Result.Failure<ContentImportResultDto>("CONTENT_PACK_IMPORT_FAILED",
                        $"{work.Entry.Path} item {version.Item} ({version.Key}): {created.Message} Nothing was imported.");
                }
                items.Add(new ContentImportItem
                {
                    Sequence = items.Count + 1,
                    EntityType = created.Value.EntityType,
                    EntityId = created.Value.Id,
                    Key = version.Key,
                    Action = version.Action,
                    ChangesJson = JsonSerializer.Serialize(version.Changes, Json),
                    Source = version.Source,
                    FilePath = work.Entry.Path,
                    Line = version.Item,
                });
            }
        }

        // Map layers last, once the barangays, zones and road types they refer to exist (step C5).
        // The layer import joins this transaction; each new boundary or road version gets an item.
        foreach (var work in files.Where(f => f.Geo is not null && f.New + f.Changed > 0))
        {
            var entry = work.Entry;
            var layer = await layers.ImportAsync(entry.Layer!.Value,
                new ImportReferenceLayerRequest(entry.EffectiveDate!.Value, entry.Source!, null, work.Geo!.Value),
                dryRun: false, new ReferenceLayerImportOptions(SkipUnchanged: true), cancellationToken);
            if (layer.IsFailure || !layer.Value.Committed)
            {
                var why = layer.IsFailure ? layer.Message : layer.Value.Errors.FirstOrDefault()?.Message;
                return Result.Failure<ContentImportResultDto>("CONTENT_PACK_IMPORT_FAILED", $"{entry.Path}: {why} Nothing was imported.");
            }
            var date = entry.EffectiveDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            foreach (var feature in layer.Value.Features ?? [])
            {
                items.Add(new ContentImportItem
                {
                    Sequence = items.Count + 1,
                    EntityType = feature.EntityType,
                    EntityId = feature.VersionId!.Value,
                    Key = feature.Key,
                    Action = ContentImportAction.Created,
                    ChangesJson = JsonSerializer.Serialize(new[]
                    {
                        new ContentFieldChangeDto("geometry", feature.Supersedes ? "version in force" : null, $"new version effective {date}"),
                    }, Json),
                    Source = entry.Source!,
                    FilePath = entry.Path,
                    Line = feature.FeatureIndex + 1,
                });
            }
        }

        var record = new ContentImport
        {
            Pack = pack,
            PackVersion = preview.Version!,
            Description = preview.Description,
            ManifestSha256 = preview.ManifestSha256!,
            Fingerprint = preview.Fingerprint!,
            ImportedAt = DateTimeOffset.UtcNow,
            ImportedBy = userId,
            CreatedCount = items.Count(i => i.Action == ContentImportAction.Created),
            ChangedCount = items.Count(i => i.Action == ContentImportAction.Changed),
            FilesJson = JsonSerializer.Serialize(preview.Files.Select(f =>
                new ContentImportFileDto(f.Kind, f.Lookup, f.Path, f.Sha256, f.Source, f.New, f.Changed, f.Unchanged, f.Layer)), Json),
            WarningsJson = JsonSerializer.Serialize(preview.Issues.Concat(preview.Files.SelectMany(f => f.Issues)), Json),
            Items = items,
        };
        db.ContentImports.Add(record);
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return Result.Success(new ContentImportResultDto(true,
            $"Imported {pack} {preview.Version}: {record.CreatedCount} created, {record.ChangedCount} changed.", await MapImportAsync(record.Id, cancellationToken)));
    }

    // --- Geography ---

    private async Task ApplyGeographyAsync(List<FileWork> files, List<ContentImportItem> items, CancellationToken ct)
    {
        var prov = files.FirstOrDefault(f => f.Entry.Kind == ContentFileKinds.Provinces);
        var mun = files.FirstOrDefault(f => f.Entry.Kind == ContentFileKinds.Municipalities);
        var brgy = files.FirstOrDefault(f => f.Entry.Kind == ContentFileKinds.Barangays);
        if (prov is null && mun is null && brgy is null)
        {
            return;
        }

        var provinceKeys = Keys(prov).Concat(Parents(mun, "province_psgc")).ToHashSet(StringComparer.Ordinal);
        var municipalityKeys = Keys(mun).Concat(Parents(brgy, "municipality_psgc")).ToHashSet(StringComparer.Ordinal);
        var barangayKeys = Keys(brgy).ToHashSet(StringComparer.Ordinal);
        var provinces = await db.Provinces.Where(x => provinceKeys.Contains(x.PsgcCode)).ToDictionaryAsync(x => x.PsgcCode, StringComparer.Ordinal, ct);
        var municipalities = await db.Municipalities.Where(x => municipalityKeys.Contains(x.PsgcCode)).ToDictionaryAsync(x => x.PsgcCode, StringComparer.Ordinal, ct);
        var barangays = await db.Barangays.Where(x => barangayKeys.Contains(x.PsgcCode)).ToDictionaryAsync(x => x.PsgcCode, StringComparer.Ordinal, ct);

        // A changing index number is cleared first, so numbers can move between records
        // (A 0001 → 0002, B 0002 → 0003) without tripping the unique indexes mid-save.
        var cleared = false;
        foreach (var plan in Changing(prov))
        {
            provinces[plan.Key].PinIndexNumber = null;
            cleared = true;
        }
        foreach (var plan in Changing(mun))
        {
            municipalities[plan.Key].PinIndexNumber = null;
            cleared = true;
        }
        foreach (var plan in Changing(brgy))
        {
            barangays[plan.Key].PinIndexNumber = null;
            cleared = true;
        }
        if (cleared)
        {
            await db.SaveChangesAsync(ct);
        }

        foreach (var plan in prov?.Plan ?? [])
        {
            var (name, index) = (plan.Row.Get("name")!, plan.Row.Get("index_number"));
            if (plan.IsNew)
            {
                var created = new Province { PsgcCode = plan.Key, Name = name, PinIndexNumber = index };
                db.Provinces.Add(created);
                provinces[plan.Key] = created;
            }
            else
            {
                var p = provinces[plan.Key];
                p.Name = name;
                p.PinIndexNumber = index ?? p.PinIndexNumber;
            }
            items.Add(Item(items, nameof(Province), provinces[plan.Key].Id, plan, prov!));
        }

        foreach (var plan in mun?.Plan ?? [])
        {
            var (name, index, isCity) = (plan.Row.Get("name")!, plan.Row.Get("index_number"), ParseBool(plan.Row.Get("is_city")));
            if (plan.IsNew)
            {
                var created = new Municipality
                {
                    Province = provinces[plan.Row.Get("province_psgc")!], PsgcCode = plan.Key, Name = name, IsCity = isCity ?? false, PinIndexNumber = index,
                };
                db.Municipalities.Add(created);
                municipalities[plan.Key] = created;
            }
            else
            {
                var m = municipalities[plan.Key];
                m.Name = name;
                m.IsCity = isCity ?? m.IsCity;
                m.PinIndexNumber = index ?? m.PinIndexNumber;
            }
            items.Add(Item(items, nameof(Municipality), municipalities[plan.Key].Id, plan, mun!));
        }

        foreach (var plan in brgy?.Plan ?? [])
        {
            var (name, index) = (plan.Row.Get("name")!, plan.Row.Get("index_number"));
            if (plan.IsNew)
            {
                var created = new Barangay { Municipality = municipalities[plan.Row.Get("municipality_psgc")!], PsgcCode = plan.Key, Name = name, PinIndexNumber = index };
                db.Barangays.Add(created);
                barangays[plan.Key] = created;
            }
            else
            {
                var b = barangays[plan.Key];
                b.Name = name;
                b.PinIndexNumber = index ?? b.PinIndexNumber;
            }
            items.Add(Item(items, nameof(Barangay), barangays[plan.Key].Id, plan, brgy!));
        }
        await db.SaveChangesAsync(ct);

        static IEnumerable<string> Keys(FileWork? work) => work?.Plan.Select(p => p.Key) ?? [];
        static IEnumerable<string> Parents(FileWork? work, string column) => work?.Plan.Select(p => p.Row.Get(column)).OfType<string>() ?? [];
        static IEnumerable<PlannedRow> Changing(FileWork? work) =>
            work?.Plan.Where(p => !p.IsNew && p.Changes.Any(c => c.Field == "index_number" && c.From is not null)) ?? [];
    }

    // --- Lookups ---

    private async Task ApplyLookupAsync(FileWork work, Dictionary<string, StructuralPart> partsAdded, List<ContentImportItem> items, CancellationToken ct)
    {
        if (work.Plan.Count == 0)
        {
            return;
        }
        var spec = Lookups[work.Entry.Lookup!];
        var keys = work.Plan.Where(p => !p.IsNew).Select(p => p.Key).ToList();
        var existing = await spec.Query(db).Where(x => keys.Contains(x.Code)).ToDictionaryAsync(x => x.Code, StringComparer.Ordinal, ct);

        foreach (var plan in work.Plan)
        {
            var row = plan.Row;
            var description = row.Get("description");
            var sortOrder = row.Get("sort_order") is { } sort ? int.Parse(sort, NumberStyles.Integer, CultureInfo.InvariantCulture) : (int?)null;
            var isActive = ParseBool(row.Get("is_active"));
            var carriesOver = ParseBool(row.Get("carries_over"));
            var blocksCancellation = ParseBool(row.Get("blocks_cancellation"));
            LookupEntity entity;
            if (plan.IsNew)
            {
                entity = spec.Add(db);
                entity.Code = plan.Key;
                entity.Name = row.Get("name")!;
                entity.Description = description;
                entity.SortOrder = sortOrder ?? 0;
                entity.IsActive = isActive ?? true;
                switch (entity)
                {
                    case StructuralPart part:
                        partsAdded[plan.Key] = part;
                        break;
                    case AnnotationType type:
                        type.CarriesOver = carriesOver ?? true;
                        type.BlocksCancellation = blocksCancellation ?? false;
                        break;
                    case StructuralMaterial material:
                        var partCode = row.Get("part_code")!;
                        material.StructuralPart = partsAdded.GetValueOrDefault(partCode)
                            ?? await db.StructuralParts.SingleAsync(x => x.Code == partCode, ct);
                        break;
                }
            }
            else
            {
                entity = existing[plan.Key];
                entity.Name = row.Get("name")!;
                entity.Description = description ?? entity.Description;
                entity.SortOrder = sortOrder ?? entity.SortOrder;
                entity.IsActive = isActive ?? entity.IsActive;
                if (entity is AnnotationType type)
                {
                    type.CarriesOver = carriesOver ?? type.CarriesOver;
                    type.BlocksCancellation = blocksCancellation ?? type.BlocksCancellation;
                }
            }
            items.Add(Item(items, entity.GetType().Name, entity.Id, plan, work));
        }
        await db.SaveChangesAsync(ct);
    }

    private static ContentImportItem Item(List<ContentImportItem> items, string entityType, Guid entityId, PlannedRow plan, FileWork work) => new()
    {
        Sequence = items.Count + 1,
        EntityType = entityType,
        EntityId = entityId,
        Key = plan.Key,
        Action = plan.IsNew ? ContentImportAction.Created : ContentImportAction.Changed,
        ChangesJson = JsonSerializer.Serialize(plan.Changes, Json),
        Source = plan.Row.Get("source") ?? work.Entry.Source!,
        FilePath = work.Entry.Path,
        Line = plan.Row.Line,
    };

    private static bool? ParseBool(string? text) => text?.ToLowerInvariant() switch
    {
        null => null,
        "true" or "yes" or "y" or "1" => true,
        _ => false, // validated by the preview: only false/no/n/0 remain
    };

    // --- History ---

    public async Task<Result<PagedResult<ContentImportDto>>> ListImportsAsync(string? pack, PagedRequest request, CancellationToken cancellationToken = default)
    {
        var query = db.ContentImports.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(pack))
        {
            query = query.Where(x => x.Pack == pack.Trim());
        }
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => x.ImportedAt).Skip((Math.Max(request.Page, 1) - 1) * request.PageSize).Take(request.PageSize)
            .ToListAsync(cancellationToken);
        var names = await UserNamesAsync(rows.Select(r => r.ImportedBy), cancellationToken);
        return Result.Success(new PagedResult<ContentImportDto>
        {
            Items = rows.Select(r => ToDto(r, names.GetValueOrDefault(r.ImportedBy))).ToList(), TotalCount = total, Page = Math.Max(request.Page, 1), PageSize = request.PageSize,
        });
    }

    public async Task<Result<ContentImportDto>> GetImportAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.ContentImports.AnyAsync(x => x.Id == id, cancellationToken)
            ? Result.Success(await MapImportAsync(id, cancellationToken))
            : Result.Failure<ContentImportDto>("CONTENT_IMPORT_NOT_FOUND", "No content import was found with the given id.");

    public async Task<Result<PagedResult<ContentImportItemDto>>> ListImportItemsAsync(Guid id, PagedRequest request, CancellationToken cancellationToken = default)
    {
        if (!await db.ContentImports.AnyAsync(x => x.Id == id, cancellationToken))
        {
            return Result.Failure<PagedResult<ContentImportItemDto>>("CONTENT_IMPORT_NOT_FOUND", "No content import was found with the given id.");
        }
        var query = db.ContentImportItems.AsNoTracking().Where(x => x.ContentImportId == id);
        var page = Math.Max(request.Page, 1);
        var rows = await query.OrderBy(x => x.Sequence).Skip((page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(cancellationToken);
        return Result.Success(new PagedResult<ContentImportItemDto>
        {
            Items = rows.Select(x => new ContentImportItemDto(x.Sequence, x.EntityType, x.EntityId, x.Key, x.Action,
                JsonSerializer.Deserialize<List<ContentFieldChangeDto>>(x.ChangesJson, Json) ?? [], x.Source, x.FilePath, x.Line)).ToList(),
            TotalCount = await query.CountAsync(cancellationToken),
            Page = page,
            PageSize = request.PageSize,
        });
    }

    private async Task<ContentImportDto> MapImportAsync(Guid id, CancellationToken ct)
    {
        var record = await db.ContentImports.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        return ToDto(record, (await UserNamesAsync([record.ImportedBy], ct)).GetValueOrDefault(record.ImportedBy));
    }

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await db.AppUsers.AsNoTracking().Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
    }

    private static ContentImportDto ToDto(ContentImport r, string? importedByName) => new(
        r.Id, r.Pack, r.PackVersion, r.Description, r.ManifestSha256, r.Fingerprint, r.ImportedAt, r.ImportedBy, importedByName,
        r.CreatedCount, r.ChangedCount,
        JsonSerializer.Deserialize<List<ContentImportFileDto>>(r.FilesJson, Json) ?? [],
        JsonSerializer.Deserialize<List<ContentIssueDto>>(r.WarningsJson, Json) ?? []);
}
