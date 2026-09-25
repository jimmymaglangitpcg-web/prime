using Microsoft.EntityFrameworkCore;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Common;

/// <summary>
/// The one rule for which assessment an assessment changes, shared by the
/// Notice of Assessment (LGC §223) and the appraisal record, so both state
/// the same "previous value".
/// </summary>
public static class AssessmentHistory
{
    /// <summary>The assessment it names as previous, else the latest earlier posted assessment of the same RPU; null for a first assessment.</summary>
    public static async Task<Assessment?> PreviousAsync(IApplicationDbContext db, Assessment assessment, CancellationToken ct) =>
        assessment.PreviousAssessmentId is { } previousId
            ? await db.Assessments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == previousId, ct)
            : await db.Assessments.AsNoTracking()
                .Where(x => x.RpuId == assessment.RpuId && x.Id != assessment.Id && x.Status == WorkflowStatus.Posted
                    && x.EffectiveDate < assessment.EffectiveDate)
                .OrderByDescending(x => x.EffectiveDate).ThenByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
}
