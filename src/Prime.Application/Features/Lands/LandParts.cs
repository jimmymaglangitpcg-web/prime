using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Lands;

public sealed record AddLandStripRequest(Guid ClassificationId, Guid? SubClassificationId, Guid ActualUseId, Guid? ZoneId, decimal Area);

public sealed record AddLandImprovementRequest(
    Guid ImprovementKindId, decimal Quantity, bool? IsProductive, Guid? ClassificationId, Guid? ActualUseId, string? Description);

public sealed record AddLandAdjustmentRequest(string FactorCode, Guid? LandStripId, string? Remarks);

public sealed record LandStripDto(
    Guid Id, int Sequence, Guid ClassificationId, string ClassificationName, Guid? SubClassificationId, string? SubClassificationName,
    Guid ActualUseId, string ActualUseName, Guid? ZoneId, string? ZoneName, decimal Area);

public sealed record LandImprovementDto(
    Guid Id, int Sequence, Guid ImprovementKindId, string ImprovementKindName, decimal Quantity, bool? IsProductive,
    Guid? ClassificationId, string? ClassificationName, Guid? ActualUseId, string? ActualUseName, string? Description);

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
        return null;
    }

    public static LandStripDto ToDto(LandStrip x) => new(x.Id, x.Sequence, x.ClassificationId, x.Classification!.Name, x.SubClassificationId,
        x.SubClassification?.Name, x.ActualUseId, x.ActualUse!.Name, x.ZoneId, x.Zone?.Name, x.Area);

    public static LandImprovementDto ToDto(LandImprovement x) => new(x.Id, x.Sequence, x.ImprovementKindId, x.ImprovementKind!.Name, x.Quantity,
        x.IsProductive, x.ClassificationId, x.Classification?.Name, x.ActualUseId, x.ActualUse?.Name, x.Description);

    public static LandAdjustmentDto ToDto(LandAdjustment x, IEnumerable<LandStrip> strips) =>
        new(x.Id, x.FactorCode, x.LandStripId, strips.FirstOrDefault(s => s.Id == x.LandStripId)?.Sequence, x.Remarks);

    public static Result<T> Fail<T>(string code, string message) => Result.Failure<T>(code, message);
}
