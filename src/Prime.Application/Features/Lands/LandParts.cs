using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Lands;

/// <param name="ValuationClassificationId">Prices the strip when it differs from its own classification (valuation-foundation.md §4.3); null: its own.</param>
/// <param name="DepthBand">The strip's depth band beyond the standard depth, for a depth factor (valuation-foundation.md §4.4); null: none.</param>
public sealed record AddLandStripRequest(Guid ClassificationId, Guid? SubClassificationId, Guid ActualUseId, Guid? ZoneId, decimal Area,
    Guid? ValuationClassificationId = null, Guid? ValuationSubClassificationId = null, int? DepthBand = null);

/// <param name="SeparateRpuId">An <c>OtherImprovement</c> RPU on this land that owns the improvement apart from the land (§4.4); null: the land's own.</param>
public sealed record AddLandImprovementRequest(
    Guid ImprovementKindId, decimal Quantity, bool? IsProductive, Guid? ClassificationId, Guid? ActualUseId, string? Description,
    Guid? SeparateRpuId = null);

/// <summary>
/// What the land's adjustment factors read (valuation-foundation.md §4.4). Changed with a reason,
/// kept in the audit trail; valuations already made keep the values they used.
/// </summary>
public sealed record UpdateLandAppraisalInputsRequest(
    Guid? RoadTypeId, decimal? RoadFrontage, bool IsCornerLot, decimal? DistanceToAllWeatherRoadKm, decimal? DistanceToPoblacionKm,
    bool IsSubdivisionLot, string Reason);

public sealed record AddLandAdjustmentRequest(string FactorCode, Guid? LandStripId, string? Remarks);

public sealed record LandStripDto(
    Guid Id, int Sequence, Guid ClassificationId, string ClassificationName, Guid? SubClassificationId, string? SubClassificationName,
    Guid ActualUseId, string ActualUseName, Guid? ZoneId, string? ZoneName, decimal Area,
    Guid? ValuationClassificationId = null, string? ValuationClassificationName = null,
    Guid? ValuationSubClassificationId = null, string? ValuationSubClassificationName = null, int? DepthBand = null);

public sealed record LandImprovementDto(
    Guid Id, int Sequence, Guid ImprovementKindId, string ImprovementKindName, decimal Quantity, bool? IsProductive,
    Guid? ClassificationId, string? ClassificationName, Guid? ActualUseId, string? ActualUseName, string? Description,
    Guid? SeparateRpuId = null, string? SeparateRpuNumber = null);

public sealed record LandAdjustmentDto(Guid Id, string FactorCode, Guid? LandStripId, int? StripSequence, string? Remarks);

/// <summary>
/// The land's appraisal rows (docs/analysis/mrpaao-forms-model.md §8.3):
/// strips, improvements and adjustments. Rows are added, never edited in
/// place; valuations already made keep the values they used.
/// </summary>
internal static class LandParts
{
    /// <summary>Keeps <see cref="Land"/>'s area and principal classification in step with its strips.</summary>
    public static void MirrorPrincipal(Land land)
    {
        if (land.Strips.Count == 0)
        {
            return;
        }
        var principal = land.Strips.OrderByDescending(x => x.Area).ThenBy(x => x.Sequence).First();
        land.Area = land.Strips.Sum(x => x.Area);
        land.ClassificationId = principal.ClassificationId;
        land.SubClassificationId = principal.SubClassificationId;
        land.ActualUseId = principal.ActualUseId;
    }

    public static async Task<string?> StripProblemAsync(IApplicationDbContext db, AddLandStripRequest r, CancellationToken ct)
    {
        if (r.Area <= 0)
        {
            return "The strip's area must be greater than zero.";
        }
        if (!await db.Classifications.AnyAsync(x => x.Id == r.ClassificationId, ct))
        {
            return "The specified classification does not exist.";
        }
        if (!await db.ActualUses.AnyAsync(x => x.Id == r.ActualUseId, ct))
        {
            return "The specified actual use does not exist.";
        }
        if (r.SubClassificationId is { } sub && !await db.SubClassifications.AnyAsync(x => x.Id == sub, ct))
        {
            return "The specified sub-classification does not exist.";
        }
        if (r.ZoneId is { } zone && !await db.Zones.AnyAsync(x => x.Id == zone, ct))
        {
            return "The specified zone does not exist.";
        }
        if (r.ValuationClassificationId is { } priced && !await db.Classifications.AnyAsync(x => x.Id == priced, ct))
        {
            return "The specified valuation classification does not exist.";
        }
        if (r.ValuationSubClassificationId is { } pricedSub && !await db.SubClassifications.AnyAsync(x => x.Id == pricedSub, ct))
        {
            return "The specified valuation sub-classification does not exist.";
        }
        if (r.DepthBand is < 1 or > 50)
        {
            return "The depth band must be 1 or more (the standard strip has none).";
        }
        return null;
    }

    public static LandStripDto ToDto(LandStrip x) => new(x.Id, x.Sequence, x.ClassificationId, x.Classification!.Name, x.SubClassificationId,
        x.SubClassification?.Name, x.ActualUseId, x.ActualUse!.Name, x.ZoneId, x.Zone?.Name, x.Area,
        x.ValuationClassificationId, x.ValuationClassification?.Name, x.ValuationSubClassificationId, x.ValuationSubClassification?.Name, x.DepthBand);

    public static LandImprovementDto ToDto(LandImprovement x, IReadOnlyDictionary<Guid, string> rpuNumbers) => new(x.Id, x.Sequence, x.ImprovementKindId,
        x.ImprovementKind!.Name, x.Quantity, x.IsProductive, x.ClassificationId, x.Classification?.Name, x.ActualUseId, x.ActualUse?.Name, x.Description,
        x.SeparateRpuId, x.SeparateRpuId is { } r ? rpuNumbers.GetValueOrDefault(r) : null);

    public static LandAdjustmentDto ToDto(LandAdjustment x, IEnumerable<LandStrip> strips) =>
        new(x.Id, x.FactorCode, x.LandStripId, strips.FirstOrDefault(s => s.Id == x.LandStripId)?.Sequence, x.Remarks);

    public static Result<T> Fail<T>(string code, string message) => Result.Failure<T>(code, message);
}
