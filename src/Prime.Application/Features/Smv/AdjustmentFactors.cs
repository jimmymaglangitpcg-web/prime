using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.DomainServices;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Smv;

/// <summary>One row of a factor's table (docs/analysis/valuation-foundation.md §4.4).</summary>
public sealed record AdjustmentFactorRowRequest(Guid? RoadTypeId, decimal? OverValue, decimal? UpToValue, int? DepthBand, decimal Percent);

/// <param name="Percent">Flat and Corner factors; other kinds take their percentages from <paramref name="Rows"/> (send 0).</param>
public sealed record CreateAdjustmentFactorRequest(
    Guid SmvId, string Code, string Name, decimal Percent, Guid? ClassificationId, string? Description,
    string LegalBasis, DateOnly EffectiveDate, string? Remarks,
    AdjustmentRuleKind RuleKind = AdjustmentRuleKind.Flat, DistanceReference? DistanceReference = null, decimal? StandardDepth = null,
    IReadOnlyList<AdjustmentFactorRowRequest>? Rows = null);

public sealed record AdjustmentFactorRowDto(int Sequence, Guid? RoadTypeId, string? RoadTypeName, decimal? OverValue, decimal? UpToValue, int? DepthBand, decimal Percent);

public sealed record AdjustmentFactorDto(
    Guid Id, Guid SmvId, string SmvOrdinanceNumber, string Code, string Name, decimal Percent, Guid? ClassificationId, string? ClassificationName,
    string? Description, string LegalBasis, DateOnly EffectiveDate, DateOnly? EndDate, WorkflowStatus Status,
    Guid? CreatedBy, DateTimeOffset CreatedAt, Guid? ApprovedBy, DateTimeOffset? ApprovedAt, string? Remarks,
    AdjustmentRuleKind RuleKind = AdjustmentRuleKind.Flat, DistanceReference? DistanceReference = null, decimal? StandardDepth = null,
    IReadOnlyList<AdjustmentFactorRowDto>? Rows = null);

public interface IAdjustmentFactorService
{
    Task<Result<AdjustmentFactorDto>> CreateAsync(CreateAdjustmentFactorRequest request, CancellationToken cancellationToken = default);
    Task<Result<AdjustmentFactorDto>> ApproveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<AdjustmentFactorDto>>> ListAsync(Guid? smvId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The SMV ordinance's market value adjustment factors (docs/analysis/mrpaao-forms-model.md
/// §8.3). A Draft is approved by a second user; approving a new version of the
/// same (SMV, code) ends the previous one. Every percentage is LGU data.
/// </summary>
public sealed class AdjustmentFactorService(IApplicationDbContext db, ICurrentUserService currentUser) : IAdjustmentFactorService
{
    public async Task<Result<AdjustmentFactorDto>> CreateAsync(CreateAdjustmentFactorRequest request, CancellationToken cancellationToken = default)
    {
        var code = request.Code?.Trim() ?? "";
        if (code.Length is 0 or > 20 || string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200
            || string.IsNullOrWhiteSpace(request.LegalBasis) || request.LegalBasis.Length > 500
            || request.Percent is <= -100m or > 1000m || request.EffectiveDate == default || request.Description?.Length > 1000)
        {
            return Fail("VALIDATION_FAILED",
                "code (max 20), name (max 200), legalBasis (max 500) and effectiveDate are required; percent must be above -100 and at most 1000.");
        }
        if (!await db.Smvs.AnyAsync(x => x.Id == request.SmvId, cancellationToken))
        {
            return Fail("SMV_NOT_FOUND", "No SMV was found with the given id.");
        }
        if (request.ClassificationId is { } c && !await db.Classifications.AnyAsync(x => x.Id == c, cancellationToken))
        {
            return Fail("CLASSIFICATION_NOT_FOUND", "The specified classification does not exist.");
        }
        var rows = request.Rows ?? [];
        if (RuleProblem(request, rows) is { } problem)
        {
            return Fail("VALIDATION_FAILED", problem);
        }
        var roadTypeIds = rows.Where(r => r.RoadTypeId is not null).Select(r => r.RoadTypeId!.Value).Distinct().ToList();
        if (roadTypeIds.Count > 0 && await db.RoadTypes.CountAsync(x => roadTypeIds.Contains(x.Id), cancellationToken) != roadTypeIds.Count)
        {
            return Fail("ROAD_TYPE_NOT_FOUND", "A road type in the factor's table does not exist.");
        }
        var factor = new AdjustmentFactor
        {
            SmvId = request.SmvId, Code = code, Name = request.Name.Trim(), Percent = request.Percent, ClassificationId = request.ClassificationId,
            Description = request.Description, LegalBasis = request.LegalBasis.Trim(), EffectiveDate = request.EffectiveDate, Remarks = request.Remarks,
            RuleKind = request.RuleKind, DistanceReference = request.DistanceReference, StandardDepth = request.StandardDepth,
            Rows = rows.Select((r, i) => new AdjustmentFactorRow
            {
                Sequence = i + 1, RoadTypeId = r.RoadTypeId, OverValue = r.OverValue, UpToValue = r.UpToValue, DepthBand = r.DepthBand, Percent = r.Percent,
            }).ToList(),
        };
        db.AdjustmentFactors.Add(factor);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(await MapAsync(factor.Id, cancellationToken));
    }

    public async Task<Result<AdjustmentFactorDto>> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var factor = await db.AdjustmentFactors.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (factor is null)
        {
            return Fail("ADJUSTMENT_FACTOR_NOT_FOUND", "No adjustment factor was found with the given id.");
        }
        if (await ConfigurationApproval.ApproveAsync(db, currentUser,
                db.AdjustmentFactors.Where(x => x.SmvId == factor.SmvId && x.Code == factor.Code), factor, "ADJUSTMENT_FACTOR", cancellationToken) is { } failure)
        {
            return Fail(failure.Code!, failure.Message!);
        }
        return Result.Success(await MapAsync(id, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<AdjustmentFactorDto>>> ListAsync(Guid? smvId, CancellationToken cancellationToken = default)
    {
        var query = Query();
        if (smvId is { } s)
        {
            query = query.Where(x => x.SmvId == s);
        }
        var rows = await query.OrderBy(x => x.Code).ThenByDescending(x => x.EffectiveDate).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<AdjustmentFactorDto>>(rows.Select(ToDto).ToList());
    }

    /// <summary>
    /// Each rule kind's shape (docs/analysis/valuation-foundation.md §4.4): Flat and Corner use the
    /// factor's percent and no rows; ByRoadType one row per road type; ByDistance a reference and
    /// non-overlapping bands; Depth the standard depth and one row per band.
    /// </summary>
    public static string? RuleProblem(CreateAdjustmentFactorRequest r, IReadOnlyList<AdjustmentFactorRowRequest> rows)
    {
        if (!Enum.IsDefined(r.RuleKind) || r.DistanceReference is { } d && !Enum.IsDefined(d))
        {
            return "ruleKind or distanceReference is not a known value.";
        }
        if (rows.Any(x => x.Percent is <= -100m or > 1000m))
        {
            return "A row percent must be above -100 and at most 1000.";
        }
        if (r.RuleKind != AdjustmentRuleKind.ByDistance && r.DistanceReference is not null)
        {
            return "distanceReference applies to ByDistance factors only.";
        }
        if (r.RuleKind != AdjustmentRuleKind.Depth && r.StandardDepth is not null)
        {
            return "standardDepth applies to Depth factors only.";
        }
        switch (r.RuleKind)
        {
            case AdjustmentRuleKind.Flat or AdjustmentRuleKind.Corner:
                return rows.Count > 0 ? $"A {r.RuleKind} factor uses its percent and has no rows." : null;
            case AdjustmentRuleKind.ByRoadType:
                if (rows.Count == 0 || rows.Any(x => x.RoadTypeId is null || x.OverValue is not null || x.UpToValue is not null || x.DepthBand is not null))
                {
                    return "A ByRoadType factor has rows of a road type and its percent only.";
                }
                return rows.Select(x => x.RoadTypeId).Distinct().Count() != rows.Count ? "A road type appears twice in the factor's table." : null;
            case AdjustmentRuleKind.ByDistance:
                if (r.DistanceReference is null)
                {
                    return "A ByDistance factor names what the distance is measured to (distanceReference).";
                }
                if (rows.Count == 0 || rows.Any(x => x.RoadTypeId is not null || x.DepthBand is not null || x.OverValue < 0
                        || x.OverValue is { } o && x.UpToValue is { } u && u <= o))
                {
                    return "A ByDistance factor has rows of a distance band (over, up to; km) and its percent only.";
                }
                for (var i = 0; i < rows.Count; i++)
                {
                    for (var j = i + 1; j < rows.Count; j++)
                    {
                        if (AdjustmentRules.BandsOverlap(rows[i].OverValue, rows[i].UpToValue, rows[j].OverValue, rows[j].UpToValue))
                        {
                            return $"Distance bands {i + 1} and {j + 1} overlap.";
                        }
                    }
                }
                return null;
            case AdjustmentRuleKind.Depth:
                if (r.StandardDepth is not > 0)
                {
                    return "A Depth factor gives the standard depth (m) its bands start beyond.";
                }
                if (rows.Count == 0 || rows.Any(x => x.DepthBand is not >= 1 || x.RoadTypeId is not null || x.OverValue is not null || x.UpToValue is not null))
                {
                    return "A Depth factor has rows of a depth band (1, 2, …) and its percent only.";
                }
                return rows.Select(x => x.DepthBand).Distinct().Count() != rows.Count ? "A depth band appears twice in the factor's table." : null;
            default:
                return null;
        }
    }

    private IQueryable<AdjustmentFactor> Query() => db.AdjustmentFactors.AsNoTracking().Include(x => x.Smv).Include(x => x.Classification)
        .Include(x => x.Rows).ThenInclude(r => r.RoadType);

    private async Task<AdjustmentFactorDto> MapAsync(Guid id, CancellationToken ct) => ToDto(await Query().SingleAsync(x => x.Id == id, ct));

    private static AdjustmentFactorDto ToDto(AdjustmentFactor x) => new(
        x.Id, x.SmvId, x.Smv!.Reference, x.Code, x.Name, x.Percent, x.ClassificationId, x.Classification?.Name, x.Description,
        x.LegalBasis, x.EffectiveDate, x.EndDate, x.Status, x.CreatedBy, x.CreatedAt, x.ApprovedBy, x.ApprovedAt, x.Remarks,
        x.RuleKind, x.DistanceReference, x.StandardDepth,
        x.Rows.OrderBy(r => r.Sequence).Select(r => new AdjustmentFactorRowDto(r.Sequence, r.RoadTypeId, r.RoadType?.Name, r.OverValue, r.UpToValue, r.DepthBand, r.Percent))
            .ToList());

    private static Result<AdjustmentFactorDto> Fail(string code, string message) => Result.Failure<AdjustmentFactorDto>(code, message);
}
