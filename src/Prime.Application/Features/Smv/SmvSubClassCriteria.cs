using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Smv;

public sealed record SmvSubClassCriterionInput(Guid ClassificationId, Guid SubClassificationId, int Sequence, string Criteria);
public sealed record SetSmvSubClassCriteriaRequest(IReadOnlyList<SmvSubClassCriterionInput> Criteria);
public sealed record SmvSubClassCriterionDto(Guid Id, Guid ClassificationId, string ClassificationName, Guid SubClassificationId, string SubClassificationName,
    int Sequence, string Criteria);

public interface ISmvSubClassCriteriaService
{
    Task<Result<IReadOnlyList<SmvSubClassCriterionDto>>> ListAsync(Guid smvId, CancellationToken cancellationToken = default);
    /// <summary>Replaces the SMV's criteria; only while the SMV is a draft.</summary>
    Task<Result<IReadOnlyList<SmvSubClassCriterionDto>>> SetAsync(Guid smvId, SetSmvSubClassCriteriaRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Sub-class criteria of an SMV (SMV Form 1; docs/analysis/smv-preparation-general-revision.md §4.2). The province's text, entered
/// with the draft SMV by the provincial office (Q2); fixed with the SMV once approved.
/// </summary>
public sealed class SmvSubClassCriteriaService(IApplicationDbContext db, IJurisdiction jurisdiction) : ISmvSubClassCriteriaService
{
    public async Task<Result<IReadOnlyList<SmvSubClassCriterionDto>>> ListAsync(Guid smvId, CancellationToken cancellationToken = default) =>
        await db.Smvs.AnyAsync(x => x.Id == smvId, cancellationToken)
            ? Result.Success(await QueryAsync(smvId, cancellationToken))
            : Result.Failure<IReadOnlyList<SmvSubClassCriterionDto>>("SMV_NOT_FOUND", "No SMV was found with the given id.");

    public async Task<Result<IReadOnlyList<SmvSubClassCriterionDto>>> SetAsync(Guid smvId, SetSmvSubClassCriteriaRequest r, CancellationToken cancellationToken = default)
    {
        var ct = cancellationToken;
        var input = (r.Criteria ?? []).ToList();
        if (input.Any(c => string.IsNullOrWhiteSpace(c.Criteria) || c.Criteria.Length > 4000 || c.Sequence < 1))
        {
            return Fail("VALIDATION_FAILED", "Each sub-class needs its criteria (max 4000) and an order of 1 or more.");
        }
        if (input.GroupBy(c => (c.ClassificationId, c.SubClassificationId)).Any(g => g.Count() > 1))
        {
            return Fail("SUB_CLASS_CRITERIA_REPEATED", "A sub-class of a class is listed twice.");
        }
        if (jurisdiction.Restricted)
        {
            return Fail(SmvPreparationService.ForbiddenCode, "The SMV is prepared by the Provincial Assessor's Office; municipal offices see it only.");
        }
        var smv = await db.Smvs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == smvId, ct);
        if (smv is null)
        {
            return Fail("SMV_NOT_FOUND", "No SMV was found with the given id.");
        }
        if (smv.Status != WorkflowStatus.Draft)
        {
            return Fail("SMV_NOT_DRAFT", "The criteria are set while the SMV is a draft; they are fixed once it is approved.");
        }
        var classes = input.Select(c => c.ClassificationId).Distinct().ToList();
        var subs = input.Select(c => c.SubClassificationId).Distinct().ToList();
        if (await db.Classifications.CountAsync(x => classes.Contains(x.Id), ct) != classes.Count
            || await db.SubClassifications.CountAsync(x => subs.Contains(x.Id), ct) != subs.Count)
        {
            return Fail("CLASSIFICATION_NOT_FOUND", "A class or sub-class does not exist.");
        }
        // Working text of a draft SMV: replaced as a whole.
        db.SmvSubClassCriteria.RemoveRange(await db.SmvSubClassCriteria.Where(x => x.SmvId == smvId).ToListAsync(ct));
        db.SmvSubClassCriteria.AddRange(input.Select(c => new SmvSubClassCriterion
        {
            SmvId = smvId, ClassificationId = c.ClassificationId, SubClassificationId = c.SubClassificationId, Sequence = c.Sequence, Criteria = c.Criteria.Trim(),
        }));
        await db.SaveChangesAsync(ct);
        return Result.Success(await QueryAsync(smvId, ct));
    }

    private async Task<IReadOnlyList<SmvSubClassCriterionDto>> QueryAsync(Guid smvId, CancellationToken ct) =>
        await db.SmvSubClassCriteria.AsNoTracking().Where(x => x.SmvId == smvId)
            .OrderBy(x => x.Classification!.Name).ThenBy(x => x.Sequence)
            .Select(x => new SmvSubClassCriterionDto(x.Id, x.ClassificationId, x.Classification!.Name, x.SubClassificationId, x.SubClassification!.Name,
                x.Sequence, x.Criteria))
            .ToListAsync(ct);

    private static Result<IReadOnlyList<SmvSubClassCriterionDto>> Fail(string code, string message) =>
        Result.Failure<IReadOnlyList<SmvSubClassCriterionDto>>(code, message);
}
