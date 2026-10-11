using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.ReportConfiguration;

/// <summary>The reports that take a row map.</summary>
public static class ReportRowMapCodes
{
    /// <summary>The quarterly report on real property assessments (reporting.md §10, Q15).</summary>
    public const string Qrrpa = "QRRPA";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Qrrpa };
}

/// <summary>The groups of a QRRPA's rows: each FAAS part falls in one (reporting.md §10, Q15).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReportRowSection
{
    /// <summary>Taxable parts of units without a restriction, by classification (and actual use).</summary>
    Taxable,
    /// <summary>Exempt parts, by the exemption type of the part.</summary>
    Exempt,
    /// <summary>Taxable parts of units under a restriction (a TD annotation of a listed type), by restriction group and classification.</summary>
    Restricted,
    /// <summary>Idle lands: listed, left empty until the idle-land designation exists (Q7, Q17).</summary>
    IdleLand,
}

/// <summary>A group of properties with restrictions: the annotation types (codes) that put a unit in it.</summary>
public sealed record RestrictionGroupSpec(string Code, string Label, IReadOnlyList<string> AnnotationTypes);

/// <summary>
/// One row of the report. A part falls in the first row of its section (and restriction group) whose lists all match:
/// classification codes, actual-use codes, exemption-type codes; an empty list matches anything. A row marked
/// <see cref="Others"/> takes what no other row of its section and group takes. <see cref="SplitsBuildings"/> marks the
/// row whose building market values are split at the threshold (Q16).
/// </summary>
public sealed record ReportRowSpec(
    ReportRowSection Section, string Code, string Label, string? Restriction = null, IReadOnlyList<string>? Classifications = null,
    IReadOnlyList<string>? ActualUses = null, IReadOnlyList<string>? ExemptionTypes = null, bool SplitsBuildings = false, bool Others = false);

public sealed record ReportRowMapDefinition(IReadOnlyList<RestrictionGroupSpec>? Restrictions, IReadOnlyList<ReportRowSpec> Rows)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static ReportRowMapDefinition Parse(string json) =>
        JsonSerializer.Deserialize<ReportRowMapDefinition>(json, Json) ?? throw new JsonException("The definition is empty.");

    public string ToJson() => JsonSerializer.Serialize(this, Json);
}

public sealed record CreateReportRowMapRequest(
    string Code, string Name, JsonElement Definition, string LegalBasis, DateOnly EffectiveDate, string? Remarks);

public sealed record ReportRowMapDto(
    Guid Id, string Code, string Name, ReportRowMapDefinition Definition, string LegalBasis, DateOnly EffectiveDate, DateOnly? EndDate,
    WorkflowStatus Status, string? Remarks, Guid? CreatedBy, Guid? ApprovedBy, DateTimeOffset? ApprovedAt);

public sealed class CreateReportRowMapRequestValidator : AbstractValidator<CreateReportRowMapRequest>
{
    public CreateReportRowMapRequestValidator()
    {
        RuleFor(x => x.Code).Must(c => ReportRowMapCodes.All.Contains(c)).WithMessage($"code must be one of: {string.Join(", ", ReportRowMapCodes.All)}.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LegalBasis).NotEmpty().MaximumLength(500);
        RuleFor(x => x.EffectiveDate).NotEqual(default(DateOnly)).WithMessage("effectiveDate is required.");
        RuleFor(x => x.Remarks).MaximumLength(1000);
    }
}

public interface IReportRowMapService
{
    Task<Result<ReportRowMapDto>> CreateAsync(CreateReportRowMapRequest request, CancellationToken cancellationToken = default);
    Task<Result<ReportRowMapDto>> ApproveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ReportRowMapDto>>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>The approved map of the report in force on <paramref name="on"/>; null if none.</summary>
    Task<ReportRowMapDto?> InForceAsync(string code, DateOnly on, CancellationToken cancellationToken = default);

    /// <summary>
    /// Why a definition cannot be used: its structure, then codes that do not exist in PRIME. Empty when valid.
    /// <paramref name="pending"/> accepts codes a content pack adds with the map (the catalogue's name, the code).
    /// </summary>
    Task<IReadOnlyList<string>> CheckAsync(ReportRowMapDefinition definition, Func<string, string, bool>? pending = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Report row maps (docs/analysis/reporting.md §10, Q15): the QRRPA's rows as configuration, checked against PRIME's
/// classifications, actual uses, exemption types and annotation types, approved by a second user. A new version takes
/// over from its effective date. Province-wide: set by an office whose jurisdiction is the whole province.
/// </summary>
public sealed class ReportRowMapService(
    IApplicationDbContext db,
    IValidator<CreateReportRowMapRequest> validator,
    ICurrentUserService currentUser,
    IJurisdiction jurisdiction) : IReportRowMapService
{
    private const int MaxRows = 300;

    public async Task<Result<ReportRowMapDto>> CreateAsync(CreateReportRowMapRequest r, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(r, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<ReportRowMapDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }
        if (jurisdiction.Restricted)
        {
            return Result.Failure<ReportRowMapDto>(JurisdictionErrors.Code, "A report's rows are set by an office whose jurisdiction is the whole province.");
        }
        ReportRowMapDefinition definition;
        try
        {
            definition = ReportRowMapDefinition.Parse(r.Definition.GetRawText());
        }
        catch (JsonException ex)
        {
            return Result.Failure<ReportRowMapDto>("VALIDATION_FAILED", $"The definition is not a valid row map: {ex.Message}");
        }
        var problems = await CheckAsync(definition, null, cancellationToken);
        if (problems.Count > 0)
        {
            return Result.Failure<ReportRowMapDto>("VALIDATION_FAILED", string.Join("; ", problems.Take(20)));
        }
        var map = new ReportRowMap
        {
            Code = r.Code, Name = r.Name.Trim(), Definition = definition.ToJson(), LegalBasis = r.LegalBasis.Trim(), EffectiveDate = r.EffectiveDate,
            Remarks = string.IsNullOrWhiteSpace(r.Remarks) ? null : r.Remarks.Trim(),
        };
        db.ReportRowMaps.Add(map);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(map));
    }

    public async Task<Result<ReportRowMapDto>> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var map = await db.ReportRowMaps.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (map is null)
        {
            return Result.Failure<ReportRowMapDto>("REPORT_ROW_MAP_NOT_FOUND", "No report row map was found with the given id.");
        }
        if (jurisdiction.Restricted)
        {
            return Result.Failure<ReportRowMapDto>(JurisdictionErrors.Code, "A report's rows are approved by an office whose jurisdiction is the whole province.");
        }
        if (await ConfigurationApproval.ApproveAsync(db, currentUser, db.ReportRowMaps.Where(x => x.Code == map.Code), map, "REPORT_ROW_MAP", cancellationToken)
            is { } failure)
        {
            return Result.Failure<ReportRowMapDto>(failure.Code!, failure.Message!);
        }
        return Result.Success(ToDto(map));
    }

    public async Task<Result<IReadOnlyList<ReportRowMapDto>>> ListAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.ReportRowMaps.AsNoTracking().OrderBy(x => x.Code).ThenByDescending(x => x.EffectiveDate).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<ReportRowMapDto>>(rows.Select(ToDto).ToList());
    }

    public async Task<ReportRowMapDto?> InForceAsync(string code, DateOnly on, CancellationToken cancellationToken = default) =>
        await db.ReportRowMaps.AsNoTracking().InForce(on).Where(x => x.Code == code).OrderByDescending(x => x.EffectiveDate)
            .FirstOrDefaultAsync(cancellationToken) is { } map ? ToDto(map) : null;

    public async Task<IReadOnlyList<string>> CheckAsync(ReportRowMapDefinition d, Func<string, string, bool>? pending = null,
        CancellationToken cancellationToken = default)
    {
        var problems = new List<string>();
        var rows = d.Rows ?? [];
        var groups = d.Restrictions ?? [];
        if (rows.Count == 0 || rows.Count > MaxRows)
        {
            problems.Add($"A map has 1 to {MaxRows} rows.");
        }
        Duplicates(groups.Select(g => g.Code), "restriction group", problems);
        Duplicates(rows.Select(r => r.Code), "row", problems);
        foreach (var g in groups)
        {
            if (string.IsNullOrWhiteSpace(g.Code) || string.IsNullOrWhiteSpace(g.Label) || (g.AnnotationTypes?.Count ?? 0) == 0)
            {
                problems.Add($"Restriction group \"{g.Code}\" needs a code, a label and at least one annotation type.");
            }
        }
        Duplicates(groups.SelectMany(g => g.AnnotationTypes ?? []), "restriction annotation type", problems);
        foreach (var r in rows)
        {
            var name = $"Row \"{r.Code}\"";
            if (string.IsNullOrWhiteSpace(r.Code) || r.Code.Length > 50 || string.IsNullOrWhiteSpace(r.Label) || r.Label.Length > 200)
            {
                problems.Add($"{name} needs a code (at most 50) and a label (at most 200).");
            }
            if (!Enum.IsDefined(r.Section))
            {
                problems.Add($"{name} has an unknown section.");
            }
            if ((r.Section == ReportRowSection.Restricted) != (r.Restriction is not null))
            {
                problems.Add($"{name}: a row names a restriction group if, and only if, it is in the Restricted section.");
            }
            else if (r.Restriction is { } group && groups.All(g => g.Code != group))
            {
                problems.Add($"{name} names restriction group \"{group}\", which the map does not define.");
            }
            if (r.Section != ReportRowSection.Exempt && (r.ExemptionTypes?.Count ?? 0) > 0)
            {
                problems.Add($"{name}: only an Exempt row lists exemption types.");
            }
            if (r.Others && ((r.Classifications?.Count ?? 0) + (r.ActualUses?.Count ?? 0) + (r.ExemptionTypes?.Count ?? 0)) > 0)
            {
                problems.Add($"{name}: an \"others\" row lists no codes.");
            }
        }
        foreach (var dup in rows.Where(r => r.Others).GroupBy(r => (r.Section, r.Restriction)).Where(g => g.Count() > 1))
        {
            problems.Add($"Section {dup.Key.Section}{(dup.Key.Restriction is { } x ? $" ({x})" : "")} has more than one \"others\" row.");
        }
        if (problems.Count > 0)
        {
            return problems;
        }

        bool Pending(string catalogue, string code) => pending?.Invoke(catalogue, code) ?? false;
        await Unknown(rows.SelectMany(r => r.Classifications ?? []).Where(c => !Pending("classifications", c)), db.Classifications.Select(x => x.Code),
            "classification", problems, cancellationToken);
        await Unknown(rows.SelectMany(r => r.ActualUses ?? []).Where(c => !Pending("actual-uses", c)), db.ActualUses.Select(x => x.Code),
            "actual use", problems, cancellationToken);
        await Unknown(rows.SelectMany(r => r.ExemptionTypes ?? []).Where(c => !Pending("exemption-types", c)), db.ExemptionTypes.Select(x => x.Code),
            "exemption type", problems, cancellationToken);
        await Unknown(groups.SelectMany(g => g.AnnotationTypes ?? []).Where(c => !Pending("annotation-types", c)), db.AnnotationTypes.Select(x => x.Code),
            "annotation type", problems, cancellationToken);
        return problems;
    }

    private static void Duplicates(IEnumerable<string> codes, string what, List<string> problems)
    {
        foreach (var code in codes.GroupBy(c => c, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key))
        {
            problems.Add($"The {what} code \"{code}\" appears more than once.");
        }
    }

    private static async Task Unknown(IEnumerable<string> codes, IQueryable<string> known, string what, List<string> problems, CancellationToken ct)
    {
        var wanted = codes.Distinct(StringComparer.Ordinal).ToList();
        if (wanted.Count == 0)
        {
            return;
        }
        var found = await known.Where(c => wanted.Contains(c)).Distinct().ToListAsync(ct);
        problems.AddRange(wanted.Except(found, StringComparer.Ordinal).Select(c => $"No {what} has the code \"{c}\"."));
    }

    private static ReportRowMapDto ToDto(ReportRowMap x) => new(
        x.Id, x.Code, x.Name, ReportRowMapDefinition.Parse(x.Definition), x.LegalBasis, x.EffectiveDate, x.EndDate, x.Status, x.Remarks,
        x.CreatedBy, x.ApprovedBy, x.ApprovedAt);
}
