using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Approvals;
using Prime.Application.Features.Forms;
using Prime.Application.Features.Numbering;
using Prime.Application.Features.Offices;
using Prime.Application.Features.Transactions;
using Prime.Domain.Common;
using Prime.Domain.Enums;

namespace Prime.Application.Features.ContentPacks;

/// <summary>
/// A configuration version a pack would create (step C3). <see cref="Request"/>
/// is the create request of the owning service; <see cref="Item"/> is the
/// 1-based position in the JSON catalogue.
/// </summary>
/// <param name="Action">Created for a new record or version; Changed when an existing record is updated in place (an office's details).</param>
public sealed record PlannedVersion(string Kind, string Key, string Name, int Item, object Request, IReadOnlyList<ContentFieldChangeDto> Changes, string Source,
    ContentImportAction Action = ContentImportAction.Created);

/// <summary>An office's details to update (step LP-1).</summary>
public sealed record PackOfficeUpdate(Guid OfficeId, UpdateOfficeRequest Request);

/// <summary>A jurisdiction draft; the office and municipality are resolved at import, when both exist (step LP-1).</summary>
public sealed record PackOfficeJurisdiction(string OfficeCode, string MunicipalityPsgcCode, DateOnly EffectiveDate, string LegalBasis, string? Remarks);

public sealed record VersionedPreview(List<ContentIssueDto> Issues, List<PlannedVersion> Versions, List<string> Referenced, int Items, int Unchanged);

/// <summary>
/// Step C3 of the LGU content pack (docs/analysis/lgu-content-pack.md §3.3):
/// transaction types, numbering schemes, approval chains and forms. Each item
/// is checked with the same validator as its admin screen and, when it differs
/// from both the version in force and any pending draft, becomes a new
/// <b>Draft</b> version created through the owning service, under the
/// importer's name. The existing approval step then applies unchanged: a
/// second user approves it (CLAUDE.md §46) and it takes effect only then.
/// Treasury kinds are refused (CLAUDE.md §0: frozen).
/// </summary>
public sealed class ContentPackVersionedContent(
    IApplicationDbContext db,
    IValidator<CreateTransactionTypeRequest> typeValidator,
    IValidator<CreateNumberingSchemeRequest> schemeValidator,
    IValidator<CreateApprovalChainRequest> chainValidator,
    IValidator<CreateFormDefinitionRequest> formValidator,
    IValidator<CreateOfficeRequest> officeValidator,
    ITransactionService transactions,
    INumberingService numbering,
    IApprovalChainService chains,
    IFormService forms,
    IOfficeService offices)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    private static readonly HashSet<NumberedDocumentKind> TreasuryNumbering =
        [NumberedDocumentKind.TaxBill, NumberedDocumentKind.OfficialReceipt, NumberedDocumentKind.PaymentTransaction, NumberedDocumentKind.Remittance];

    private static readonly HashSet<FormSubjectType> TreasuryForms = [FormSubjectType.TaxBill, FormSubjectType.StatementOfAccount, FormSubjectType.Payment];

    /// <summary>Authorities PRIME installs itself; a pack supplies official layouts.</summary>
    private static readonly HashSet<FormAuthority> BuiltInAuthorities = [FormAuthority.PrimeProvisional, FormAuthority.Mrpaao];

    // --- Catalogue item shapes (camelCase JSON; unknown members are errors) ---

    private sealed record TypeItem(string? Code, string? Name, string? Kind, int? Rank, string? Description, string? LegalBasis, string? EffectiveDate,
        string? Remarks, List<RequirementItem>? Requirements, string? Source);

    private sealed record RequirementItem(string? Code, string? Label, bool? Mandatory, string? LegalBasis);

    private sealed record SchemeItem(string? AppliesTo, string? Name, string? Pattern, string? ValidationRegex, bool? AllowManualEntry, string? LegalBasis,
        string? EffectiveDate, string? Remarks, string? Source);

    private sealed record ChainItem(string? SubjectType, string? Name, string? Office, string? LegalBasis, string? EffectiveDate, string? Remarks,
        List<StepItem>? Steps, string? Source);

    private sealed record StepItem(string? StepCode, string? Label, string? SignatoryPosition);

    private sealed record FormItem(string? Code, string? Title, string? SubjectType, string? Authority, string? Template, string? LegalBasis,
        string? SourceReference, string? EffectiveDate, string? Remarks, string? Source);

    private sealed record OfficeItem(string? Code, string? Name, string? Kind, string? HeadPosition, string? Address, string? Contact,
        List<string>? Municipalities, string? EffectiveDate, string? LegalBasis, string? Remarks, string? Source);

    /// <param name="pendingMunicipalities">PSGC codes of municipalities the same pack adds; offices may cover them.</param>
    public async Task<VersionedPreview> PreviewAsync(string kind, string path, byte[] bytes, string? fileSource,
        Func<string, Task<Result<ContentFileRead>>> readFile, CancellationToken ct, IReadOnlySet<string>? pendingMunicipalities = null)
    {
        var result = new VersionedPreview([], [], [], 0, 0);
        try
        {
            return kind switch
            {
                ContentFileKinds.TransactionTypes => await TypesAsync(Parse<TypeItem>(bytes), fileSource, result, ct),
                ContentFileKinds.NumberingSchemes => await SchemesAsync(Parse<SchemeItem>(bytes), fileSource, result, ct),
                ContentFileKinds.ApprovalChains => await ChainsAsync(Parse<ChainItem>(bytes), fileSource, result, ct),
                ContentFileKinds.Forms => await FormsAsync(Parse<FormItem>(bytes), fileSource, readFile, result, ct),
                ContentFileKinds.Offices => await OfficesAsync(Parse<OfficeItem>(bytes), fileSource, pendingMunicipalities ?? new HashSet<string>(), result, ct),
                _ => throw new InvalidOperationException($"Not a versioned kind: {kind}"),
            };
        }
        catch (JsonException ex)
        {
            result.Issues.Add(Error("JSON_INVALID", $"{path} is not a valid {kind} catalogue: {ex.Message}", ex.LineNumber is { } l ? (int)l + 1 : null));
            return result;
        }
    }

    /// <summary>Creates the planned version through its service; the new record's entity name and id.</summary>
    public async Task<Result<(string EntityType, Guid Id)>> CreateAsync(PlannedVersion version, CancellationToken ct) => version.Request switch
    {
        CreateTransactionTypeRequest r => Map("TransactionType", await transactions.CreateTypeAsync(r, ct), x => x.Id),
        CreateNumberingSchemeRequest r => Map("NumberingScheme", await numbering.CreateAsync(r, ct), x => x.Id),
        CreateApprovalChainRequest r => Map("ApprovalChain", await chains.CreateAsync(r, ct), x => x.Id),
        CreateFormDefinitionRequest r => Map("FormDefinition", await forms.CreateDefinitionAsync(r, ct), x => x.Id),
        CreateOfficeRequest r => Map("Office", await offices.CreateAsync(r, ct), x => x.Id),
        PackOfficeUpdate r => Map("Office", await offices.UpdateAsync(r.OfficeId, r.Request, ct), x => x.Id),
        PackOfficeJurisdiction r => await CreateJurisdictionAsync(r, ct),
        _ => throw new InvalidOperationException("Unknown planned version."),
    };

    /// <summary>Resolves the office and municipality by code now that the pack's offices and geography exist.</summary>
    private async Task<Result<(string, Guid)>> CreateJurisdictionAsync(PackOfficeJurisdiction r, CancellationToken ct)
    {
        var officeId = await db.Offices.Where(o => o.Code == r.OfficeCode).Select(o => (Guid?)o.Id).FirstOrDefaultAsync(ct);
        var municipalityId = await db.Municipalities.Where(m => m.PsgcCode == r.MunicipalityPsgcCode).Select(m => (Guid?)m.Id).FirstOrDefaultAsync(ct);
        if (officeId is null || municipalityId is null)
        {
            return Result.Failure<(string, Guid)>("CONTENT_PACK_IMPORT_FAILED", $"Office {r.OfficeCode} or municipality {r.MunicipalityPsgcCode} was not found at import.");
        }
        return Map("OfficeJurisdiction",
            await offices.CreateJurisdictionAsync(new CreateOfficeJurisdictionRequest(officeId.Value, municipalityId.Value, r.EffectiveDate, r.LegalBasis, r.Remarks), ct),
            x => x.Id);
    }

    private static Result<(string, Guid)> Map<T>(string type, Result<T> result, Func<T, Guid> id) =>
        result.IsSuccess ? Result.Success((type, id(result.Value))) : Result.Failure<(string, Guid)>(result.Code!, result.Message!);

    // --- Transaction types (key: code) ---

    private async Task<VersionedPreview> TypesAsync(List<TypeItem> items, string? fileSource, VersionedPreview result, CancellationToken ct)
    {
        var existing = await db.TransactionTypes.AsNoTracking().Include(x => x.Requirements).ToListAsync(ct);
        var unchanged = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < items.Count; i++)
        {
            var (n, x) = (i + 1, items[i]);
            if (!Common(result, n, x.Source, fileSource, x.EffectiveDate, out var source, out var effective)
                || !TryEnum<PropertyTransactionKind>(result, n, "kind", x.Kind, out var kindValue))
            {
                continue;
            }
            var requirements = (x.Requirements ?? []).Select((r, j) => new TransactionRequirementRequest(j + 1, Trim(r.Code) ?? "", Trim(r.Label) ?? "",
                r.Mandatory ?? true, Trim(r.LegalBasis))).ToList();
            var request = new CreateTransactionTypeRequest(Trim(x.LegalBasis) ?? "", effective, Trim(x.Remarks), Trim(x.Code) ?? "", Trim(x.Name) ?? "",
                kindValue, x.Rank, Trim(x.Description), requirements);
            if (!await ValidAsync(typeValidator, request, result, n, ct) || !Unique(result, seen, request.Code, n, "code"))
            {
                continue;
            }
            var scope = existing.Where(e => e.Code == request.Code).ToList();
            var current = Current(scope);
            var changes = new List<ContentFieldChangeDto>();
            Diff(changes, "name", current?.Name, request.Name);
            Diff(changes, "kind", current?.Kind.ToString(), request.Kind.ToString());
            Diff(changes, "rank", current?.Rank?.ToString(CultureInfo.InvariantCulture), request.Rank?.ToString(CultureInfo.InvariantCulture));
            Diff(changes, "description", current?.Description, request.Description);
            Diff(changes, "legalBasis", current?.LegalBasis, request.LegalBasis);
            Diff(changes, "requirements", current is null ? null : Requirements(current.Requirements.OrderBy(r => r.Sequence)
                .Select(r => (r.Code, r.Label, r.IsMandatory, r.LegalBasis))), Requirements(requirements.Select(r => (r.Code, r.Label, r.IsMandatory, r.LegalBasis))));
            if (Same(scope, s => Equal(s.Name, request.Name) && s.Kind == request.Kind && s.Rank == request.Rank && Equal(s.Description, request.Description)
                    && Equal(s.LegalBasis, request.LegalBasis)
                    && Requirements(s.Requirements.OrderBy(r => r.Sequence).Select(r => (r.Code, r.Label, r.IsMandatory, r.LegalBasis)))
                        == Requirements(requirements.Select(r => (r.Code, r.Label, r.IsMandatory, r.LegalBasis)))))
            {
                unchanged++;
                continue;
            }
            if (Plan(result, scope, n, effective))
            {
                result.Versions.Add(new PlannedVersion(ContentFileKinds.TransactionTypes, request.Code, request.Name, n, request, changes, source));
            }
        }
        return result with { Items = items.Count, Unchanged = unchanged };
    }

    // --- Numbering schemes (key: the document kind) ---

    private async Task<VersionedPreview> SchemesAsync(List<SchemeItem> items, string? fileSource, VersionedPreview result, CancellationToken ct)
    {
        var existing = await db.NumberingSchemes.AsNoTracking().ToListAsync(ct);
        var unchanged = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < items.Count; i++)
        {
            var (n, x) = (i + 1, items[i]);
            if (!Common(result, n, x.Source, fileSource, x.EffectiveDate, out var source, out var effective)
                || !TryEnum<NumberedDocumentKind>(result, n, "appliesTo", x.AppliesTo, out var appliesTo))
            {
                continue;
            }
            if (TreasuryNumbering.Contains(appliesTo))
            {
                result.Issues.Add(Error("TREASURY_FROZEN", $"Item {n}: numbering for {appliesTo} belongs to the frozen treasury scope (CLAUDE.md §0).", null, $"[{n}].appliesTo"));
                continue;
            }
            var request = new CreateNumberingSchemeRequest(Trim(x.LegalBasis) ?? "", effective, Trim(x.Remarks), appliesTo, Trim(x.Name) ?? "",
                Trim(x.Pattern) ?? "", Trim(x.ValidationRegex), x.AllowManualEntry ?? false);
            if (!await ValidAsync(schemeValidator, request, result, n, ct) || !Unique(result, seen, appliesTo.ToString(), n, "appliesTo"))
            {
                continue;
            }
            var scope = existing.Where(e => e.AppliesTo == appliesTo).ToList();
            var current = Current(scope);
            var changes = new List<ContentFieldChangeDto>();
            Diff(changes, "name", current?.Name, request.Name);
            Diff(changes, "pattern", current?.Pattern, request.Pattern);
            Diff(changes, "validationRegex", current?.ValidationRegex, request.ValidationRegex);
            Diff(changes, "allowManualEntry", current is null ? null : Bool(current.AllowManualEntry), Bool(request.AllowManualEntry));
            Diff(changes, "legalBasis", current?.LegalBasis, request.LegalBasis);
            if (Same(scope, s => Equal(s.Name, request.Name) && s.Pattern == request.Pattern && Equal(s.ValidationRegex, request.ValidationRegex)
                    && s.AllowManualEntry == request.AllowManualEntry && Equal(s.LegalBasis, request.LegalBasis)))
            {
                unchanged++;
                continue;
            }
            if (Plan(result, scope, n, effective))
            {
                result.Versions.Add(new PlannedVersion(ContentFileKinds.NumberingSchemes, appliesTo.ToString(), request.Name, n, request, changes, source));
            }
        }
        return result with { Items = items.Count, Unchanged = unchanged };
    }

    // --- Approval chains (key: the subject; offices come with step LP) ---

    private async Task<VersionedPreview> ChainsAsync(List<ChainItem> items, string? fileSource, VersionedPreview result, CancellationToken ct)
    {
        var existing = await db.ApprovalChains.AsNoTracking().Include(x => x.Steps).ToListAsync(ct);
        var unchanged = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < items.Count; i++)
        {
            var (n, x) = (i + 1, items[i]);
            if (!Common(result, n, x.Source, fileSource, x.EffectiveDate, out var source, out var effective)
                || !TryEnum<ApprovalSubjectType>(result, n, "subjectType", x.SubjectType, out var subject))
            {
                continue;
            }
            if (Trim(x.Office) is not null)
            {
                result.Issues.Add(Error("OFFICE_NOT_YET_SUPPORTED",
                    $"Item {n}: approval chains per office arrive with step LP-4 (docs/analysis/province-wide-operation.md §3.4); remove 'office' for now.",
                    null, $"[{n}].office"));
                continue;
            }
            var steps = (x.Steps ?? []).Select((s, j) => new ApprovalStepRequest(j + 1, Trim(s.StepCode) ?? "", Trim(s.Label) ?? "", Trim(s.SignatoryPosition))).ToList();
            var request = new CreateApprovalChainRequest(Trim(x.LegalBasis) ?? "", effective, Trim(x.Remarks), subject, Trim(x.Name) ?? "", steps);
            if (!await ValidAsync(chainValidator, request, result, n, ct) || !Unique(result, seen, subject.ToString(), n, "subjectType"))
            {
                continue;
            }
            var scope = existing.Where(e => e.SubjectType == subject).ToList();
            var current = Current(scope);
            var wanted = Steps(steps.Select(s => (s.StepCode, s.Label, s.SignatoryPosition)));
            var changes = new List<ContentFieldChangeDto>();
            Diff(changes, "name", current?.Name, request.Name);
            Diff(changes, "steps", current is null ? null : Steps(current.Steps.OrderBy(s => s.Sequence).Select(s => (s.StepCode, s.Label, s.SignatoryPosition))), wanted);
            Diff(changes, "legalBasis", current?.LegalBasis, request.LegalBasis);
            if (Same(scope, s => Equal(s.Name, request.Name) && Equal(s.LegalBasis, request.LegalBasis)
                    && Steps(s.Steps.OrderBy(t => t.Sequence).Select(t => (t.StepCode, t.Label, t.SignatoryPosition))) == wanted))
            {
                unchanged++;
                continue;
            }
            if (Plan(result, scope, n, effective))
            {
                result.Versions.Add(new PlannedVersion(ContentFileKinds.ApprovalChains, subject.ToString(), request.Name, n, request, changes, source));
            }
        }
        return result with { Items = items.Count, Unchanged = unchanged };
    }

    // --- Forms (key: form code; the template is a pack file) ---

    private async Task<VersionedPreview> FormsAsync(List<FormItem> items, string? fileSource, Func<string, Task<Result<ContentFileRead>>> readFile,
        VersionedPreview result, CancellationToken ct)
    {
        var existing = await db.FormDefinitions.AsNoTracking().ToListAsync(ct);
        var unchanged = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < items.Count; i++)
        {
            var (n, x) = (i + 1, items[i]);
            if (!Common(result, n, x.Source, fileSource, x.EffectiveDate, out var source, out var effective)
                || !TryEnum<FormSubjectType>(result, n, "subjectType", x.SubjectType, out var subject)
                || !TryEnum<FormAuthority>(result, n, "authority", x.Authority, out var authority))
            {
                continue;
            }
            if (TreasuryForms.Contains(subject))
            {
                result.Issues.Add(Error("TREASURY_FROZEN", $"Item {n}: forms for {subject} belong to the frozen treasury scope (CLAUDE.md §0).", null, $"[{n}].subjectType"));
                continue;
            }
            if (BuiltInAuthorities.Contains(authority))
            {
                result.Issues.Add(Error("AUTHORITY_BUILT_IN",
                    $"Item {n}: {authority} layouts are installed by PRIME itself; a pack supplies Lam, Blgf, LguOrdinance or Other forms.", null, $"[{n}].authority"));
                continue;
            }
            if (ContentPackService.PathProblem(x.Template) is { } problem)
            {
                result.Issues.Add(Error("TEMPLATE_PATH", $"Item {n}: template {problem}", null, $"[{n}].template"));
                continue;
            }
            var read = await readFile(x.Template!.Trim());
            if (read.IsFailure || read.Value.Content is not { } templateBytes)
            {
                result.Issues.Add(Error("TEMPLATE_UNREADABLE", $"Item {n}: {(read.IsFailure ? read.Message : read.Value.Error)}", null, $"[{n}].template"));
                continue;
            }
            result.Referenced.Add($"{x.Template.Trim()}:{Convert.ToHexStringLower(SHA256.HashData(templateBytes))}");
            var body = Encoding.UTF8.GetString(templateBytes).TrimStart('﻿');
            var request = new CreateFormDefinitionRequest(Trim(x.LegalBasis) ?? "", effective, Trim(x.Remarks), Trim(x.Code) ?? "", Trim(x.Title) ?? "",
                subject, authority, Trim(x.SourceReference), body);
            if (!await ValidAsync(formValidator, request, result, n, ct) || !Unique(result, seen, request.Code, n, "code"))
            {
                continue;
            }
            var scope = existing.Where(e => e.Code == request.Code).ToList();
            if (scope.FirstOrDefault(e => e.SubjectType != subject) is { } other)
            {
                result.Issues.Add(Error("FORM_SUBJECT_MISMATCH", $"Item {n}: form {request.Code} renders {other.SubjectType}; a new version must render the same kind of record.", null, $"[{n}].subjectType"));
                continue;
            }
            var current = Current(scope);
            var changes = new List<ContentFieldChangeDto>();
            Diff(changes, "title", current?.Title, request.Title);
            Diff(changes, "authority", current?.Authority.ToString(), request.Authority.ToString());
            Diff(changes, "sourceReference", current?.SourceReference, request.SourceReference);
            Diff(changes, "legalBasis", current?.LegalBasis, request.LegalBasis);
            Diff(changes, "template", current is null ? null : Hash(current.TemplateBody), Hash(body));
            if (Same(scope, s => Equal(s.Title, request.Title) && s.Authority == request.Authority && Equal(s.SourceReference, request.SourceReference)
                    && Equal(s.LegalBasis, request.LegalBasis) && s.TemplateBody == body))
            {
                unchanged++;
                continue;
            }
            if (Plan(result, scope, n, effective))
            {
                result.Versions.Add(new PlannedVersion(ContentFileKinds.Forms, request.Code, request.Title, n, request, changes, source));
            }
        }
        return result with { Items = items.Count, Unchanged = unchanged };
    }

    // --- Offices (key: office code; step LP-1, docs/analysis/province-wide-operation.md §3.1) ---

    /// <summary>
    /// An office record is created or its details updated on import. Each
    /// municipality it covers becomes a Draft jurisdiction a second user
    /// approves, unless the office already covers it (or a draft says so).
    /// </summary>
    private async Task<VersionedPreview> OfficesAsync(List<OfficeItem> items, string? fileSource, IReadOnlySet<string> pendingMunicipalities,
        VersionedPreview result, CancellationToken ct)
    {
        var existing = await db.Offices.AsNoTracking().ToListAsync(ct);
        var byCode = existing.ToDictionary(o => o.Code, StringComparer.Ordinal);
        var jurisdictions = await db.OfficeJurisdictions.AsNoTracking().Include(j => j.Municipality).ToListAsync(ct);
        var known = await db.Municipalities.AsNoTracking().Select(m => m.PsgcCode).ToListAsync(ct);
        var municipalities = known.Concat(pendingMunicipalities).ToHashSet(StringComparer.Ordinal);
        var unchanged = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var covered = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < items.Count; i++)
        {
            var (n, x) = (i + 1, items[i]);
            var source = Trim(x.Source) ?? fileSource ?? string.Empty;
            if (source.Length is 0 or > 500)
            {
                result.Issues.Add(Error("SOURCE_MISSING", $"Item {n}: cite a source (at most 500 characters) on the item or its file.", null, $"[{n}].source"));
                continue;
            }
            if (!TryEnum<OfficeKind>(result, n, "kind", x.Kind, out var kind))
            {
                continue;
            }
            var request = new CreateOfficeRequest(Trim(x.Code) ?? "", Trim(x.Name) ?? "", kind, Trim(x.HeadPosition), Trim(x.Address), Trim(x.Contact));
            if (!await ValidAsync(officeValidator, request, result, n, ct) || !Unique(result, seen, request.Code, n, "code"))
            {
                continue;
            }

            var planned = result.Versions.Count;
            byCode.TryGetValue(request.Code, out var office);
            if (office is not null && office.Kind != kind)
            {
                result.Issues.Add(Error("OFFICE_KIND_MISMATCH", $"Item {n}: office {request.Code} is {office.Kind} in PRIME; its kind cannot change.", null, $"[{n}].kind"));
                continue;
            }
            if (office is null && kind == OfficeKind.Provincial && existing.FirstOrDefault(o => o.Kind == OfficeKind.Provincial) is { } other)
            {
                result.Issues.Add(Error("OFFICE_PROVINCIAL_DUPLICATE",
                    $"Item {n}: the province already has its provincial office ({other.Code}); there is exactly one.", null, $"[{n}].code"));
                continue;
            }
            var list = (x.Municipalities ?? []).Select(m => m.Trim()).ToList();
            if (kind == OfficeKind.Provincial && list.Count > 0)
            {
                result.Issues.Add(Error("OFFICE_NOT_MUNICIPAL", $"Item {n}: the provincial office covers the whole province; list no municipalities.", null, $"[{n}].municipalities"));
                continue;
            }
            DateOnly effective = default;
            if (list.Count > 0 && !DateOnly.TryParseExact(Trim(x.EffectiveDate), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out effective))
            {
                result.Issues.Add(Error("DATE_INVALID", $"Item {n}: effectiveDate (yyyy-MM-dd) is required when the office lists municipalities; got '{x.EffectiveDate}'.",
                    null, $"[{n}].effectiveDate"));
                continue;
            }

            if (office is null)
            {
                result.Versions.Add(new PlannedVersion(ContentFileKinds.Offices, request.Code, request.Name, n, request,
                    [new ContentFieldChangeDto("name", null, request.Name), new ContentFieldChangeDto("kind", null, kind.ToString())], source));
            }
            else
            {
                var changes = new List<ContentFieldChangeDto>();
                Diff(changes, "name", office.Name, request.Name);
                Diff(changes, "headPosition", office.HeadPosition, request.HeadPosition);
                Diff(changes, "address", office.Address, request.Address);
                Diff(changes, "contact", office.Contact, request.Contact);
                if (changes.Count > 0)
                {
                    var update = new UpdateOfficeRequest(request.Name, request.HeadPosition, request.Address, request.Contact, office.Status);
                    result.Versions.Add(new PlannedVersion(ContentFileKinds.Offices, request.Code, request.Name, n, new PackOfficeUpdate(office.Id, update),
                        changes, source, ContentImportAction.Changed));
                }
            }

            foreach (var psgc in list)
            {
                if (!municipalities.Contains(psgc))
                {
                    result.Issues.Add(Error("PARENT_NOT_FOUND", $"Item {n}: municipality {psgc} is neither in the pack nor in PRIME.", null, $"[{n}].municipalities"));
                    continue;
                }
                if (covered.TryGetValue(psgc, out var firstItem))
                {
                    result.Issues.Add(Error("DUPLICATE_KEY", $"Item {n}: municipality {psgc} is also listed under item {firstItem}; one office covers a municipality.",
                        null, $"[{n}].municipalities"));
                    continue;
                }
                covered[psgc] = n;
                var scope = jurisdictions.Where(j => j.Municipality!.PsgcCode == psgc).ToList();
                if (office is not null && Same(scope, j => j.OfficeId == office.Id))
                {
                    continue; // already covered by this office, or a draft says so
                }
                if (!Plan(result, scope, n, effective))
                {
                    continue;
                }
                var from = Current(scope) is { } current ? existing.FirstOrDefault(o => o.Id == current.OfficeId)?.Code : null;
                result.Versions.Add(new PlannedVersion(ContentFileKinds.Offices, $"{request.Code} {psgc}", $"{request.Name}: {psgc}", n,
                    new PackOfficeJurisdiction(request.Code, psgc, effective, Trim(x.LegalBasis) ?? source, Trim(x.Remarks)),
                    [new ContentFieldChangeDto("office", from, request.Code), new ContentFieldChangeDto("effectiveDate", null, effective.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))],
                    source));
            }
            if (result.Versions.Count == planned)
            {
                unchanged++;
            }
        }
        return result with { Items = items.Count, Unchanged = unchanged };
    }

    // --- Shared rules ---

    private static List<T> Parse<T>(byte[] bytes) => JsonSerializer.Deserialize<List<T>>(ContentPackService.WithoutBom(bytes), Json)
        ?? throw new JsonException("The catalogue is empty; it must be a JSON array.");

    /// <summary>Source citation and effective date, required for every item.</summary>
    private static bool Common(VersionedPreview result, int n, string? itemSource, string? fileSource, string? effectiveText, out string source, out DateOnly effective)
    {
        source = Trim(itemSource) ?? fileSource ?? string.Empty;
        effective = default;
        if (source.Length == 0)
        {
            result.Issues.Add(Error("SOURCE_MISSING", $"Item {n}: neither the item nor its file cites a source.", null, $"[{n}].source"));
            return false;
        }
        if (source.Length > 500)
        {
            result.Issues.Add(Error("TOO_LONG", $"Item {n}: the source is longer than 500 characters.", null, $"[{n}].source"));
            return false;
        }
        if (!DateOnly.TryParseExact(Trim(effectiveText), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out effective))
        {
            result.Issues.Add(Error("DATE_INVALID", $"Item {n}: effectiveDate must be a date written yyyy-MM-dd; got '{effectiveText}'.", null, $"[{n}].effectiveDate"));
            return false;
        }
        return true;
    }

    private static bool TryEnum<T>(VersionedPreview result, int n, string field, string? text, out T value) where T : struct, Enum
    {
        value = default;
        var t = Trim(text);
        if (t is null || int.TryParse(t, out _) || !Enum.TryParse(t, ignoreCase: true, out value) || !Enum.IsDefined(value))
        {
            result.Issues.Add(Error("VALUE_INVALID", $"Item {n}: {field} '{text}' is not one of {string.Join(", ", Enum.GetNames<T>())}.", null, $"[{n}].{field}"));
            return false;
        }
        return true;
    }

    private static async Task<bool> ValidAsync<T>(IValidator<T> validator, T request, VersionedPreview result, int n, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        foreach (var e in validation.Errors)
        {
            result.Issues.Add(Error("VALIDATION_FAILED", $"Item {n}: {e.ErrorMessage}", null, $"[{n}].{JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName)}"));
        }
        return validation.IsValid;
    }

    private static bool Unique(VersionedPreview result, HashSet<string> seen, string key, int n, string field)
    {
        if (seen.Add(key))
        {
            return true;
        }
        result.Issues.Add(Error("DUPLICATE_KEY", $"Item {n}: {field} {key} appears more than once in the catalogue.", null, $"[{n}].{field}"));
        return false;
    }

    /// <summary>The version in force today, else the latest approved one.</summary>
    private static T? Current<T>(List<T> scope) where T : EffectiveDatedConfiguration =>
        scope.Where(x => x.Status == WorkflowStatus.Approved).OrderByDescending(x => x.EffectiveDate).FirstOrDefault();

    /// <summary>
    /// Unchanged when a version still in play has the same content: approved and not
    /// ended, or a Draft awaiting approval (so a second import does not stack drafts).
    /// </summary>
    private static bool Same<T>(List<T> scope, Func<T, bool> sameContent) where T : EffectiveDatedConfiguration =>
        scope.Any(x => (x.Status == WorkflowStatus.Draft || x.Status == WorkflowStatus.Approved && x.EndDate is null) && sameContent(x));

    /// <summary>A new version must start after every approved one, or it could never be approved.</summary>
    private static bool Plan<T>(VersionedPreview result, List<T> scope, int n, DateOnly effective) where T : EffectiveDatedConfiguration
    {
        if (scope.Where(x => x.Status == WorkflowStatus.Approved).OrderByDescending(x => x.EffectiveDate).FirstOrDefault() is { } latest
            && latest.EffectiveDate >= effective)
        {
            result.Issues.Add(Error("EFFECTIVE_DATE_CONFLICT",
                $"Item {n}: an approved version already starts on {latest.EffectiveDate:yyyy-MM-dd}; the new version must start after it.", null, $"[{n}].effectiveDate"));
            return false;
        }
        if (scope.Any(x => x.Status == WorkflowStatus.Draft))
        {
            result.Issues.Add(Warning("DRAFT_PENDING", $"Item {n}: another draft version is already waiting for approval; this adds a second one."));
        }
        return true;
    }

    private static void Diff(List<ContentFieldChangeDto> changes, string field, string? from, string? to)
    {
        if (!Equal(from, to))
        {
            changes.Add(new ContentFieldChangeDto(field, from, to));
        }
    }

    private static bool Equal(string? a, string? b) => string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.Ordinal);

    private static string Requirements(IEnumerable<(string Code, string Label, bool Mandatory, string? LegalBasis)> items) =>
        string.Join(" | ", items.Select(r => $"{r.Code}: {r.Label}{(r.Mandatory ? "" : " (optional)")}{(r.LegalBasis is null ? "" : $" [{r.LegalBasis}]")}"));

    private static string Steps(IEnumerable<(string Code, string Label, string? Position)> steps) =>
        string.Join(" → ", steps.Select(s => $"{s.Code} {s.Label}{(s.Position is null ? "" : $" ({s.Position})")}"));

    private static string Hash(string text) => $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12]}";

    private static string Bool(bool value) => value ? "true" : "false";

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ContentIssueDto Error(string code, string message, int? line = null, string? field = null) =>
        new(ContentIssueSeverity.Error, code, message, line, field);

    private static ContentIssueDto Warning(string code, string message) => new(ContentIssueSeverity.Warning, code, message, null, null);
}
