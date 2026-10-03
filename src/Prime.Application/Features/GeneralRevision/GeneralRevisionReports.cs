using Microsoft.EntityFrameworkCore;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Forms;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Notices;
using Prime.Domain.Enums;

namespace Prime.Application.Features.GeneralRevision;

/// <summary>
/// The data of a general revision's completion report (GRI 19) and status report (LAM 2025 Book I p.25), both printed from the
/// programme (L6-6c): its references, counts of units by outcome, values before and after by classification (from the lines of
/// the assessments replaced and made), notices, register runs and units taken out. Recorded values only. Appeals are recorded
/// from step L7; until then the report says so. DOMAIN VERIFICATION REQUIRED: the status report's fields (LAM Annex I-S).
/// </summary>
public sealed class GeneralRevisionReportFormDataProvider(IApplicationDbContext db, IClock clock) : IFormDataProvider
{
    public FormSubjectType SubjectType => FormSubjectType.GeneralRevision;

    public async Task<FormSubjectData?> BuildAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var ct = cancellationToken;
        var p = await db.GeneralRevisionProgrammes.AsNoTracking().Include(x => x.Smv).Include(x => x.Scope).ThenInclude(s => s.Municipality)
            .ThenInclude(m => m!.Province).FirstOrDefaultAsync(x => x.Id == subjectId, ct);
        if (p is null)
        {
            return null;
        }
        var items = await db.GeneralRevisionItems.AsNoTracking().Where(x => x.GeneralRevisionProgrammeId == p.Id)
            .Select(x => new
            {
                x.Pin, x.RpuNumber, x.RpuType, x.Status, x.ExclusionReason, x.FailureReason, x.PreviousAssessmentId, x.AssessmentId, x.RpuId,
                AssessmentStatus = x.Assessment != null ? (WorkflowStatus?)x.Assessment.Status : null, x.MunicipalityId,
            }).ToListAsync(ct);
        var counted = items.Where(x => x.Status != GeneralRevisionItemStatus.Excluded).ToList();
        var posted = counted.Where(x => x.AssessmentStatus == WorkflowStatus.Posted).Select(x => x.AssessmentId!.Value).ToList();
        var previous = counted.Where(x => x.PreviousAssessmentId is not null && x.AssessmentStatus == WorkflowStatus.Posted)
            .Select(x => x.PreviousAssessmentId!.Value).ToList();

        // Values by classification: the replaced assessments' lines (before) and the new ones' (after), posted units only.
        async Task<Dictionary<string, (decimal Mv, decimal Av)>> ByClassAsync(List<Guid> ids) =>
            (await db.AssessmentLines.AsNoTracking().Where(l => ids.Contains(l.AssessmentId))
                .GroupBy(l => l.Classification!.Name).Select(g => new { g.Key, Mv = g.Sum(l => l.MarketValue), Av = g.Sum(l => l.AssessedValue) })
                .ToListAsync(ct)).ToDictionary(x => x.Key, x => (x.Mv, x.Av));
        var before = await ByClassAsync(previous);
        var after = await ByClassAsync(posted);
        // Each posted unit counted once, under its principal (largest) line's classification.
        var principal = (await db.AssessmentLines.AsNoTracking().Where(l => posted.Contains(l.AssessmentId))
                .Select(l => new { l.AssessmentId, l.MarketValue, l.Sequence, Class = l.Classification!.Name }).ToListAsync(ct))
            .GroupBy(l => l.AssessmentId).Select(g => g.OrderByDescending(l => l.MarketValue).ThenBy(l => l.Sequence).First().Class)
            .GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
        var classes = before.Keys.Union(after.Keys).Order(StringComparer.Ordinal).Select(c => new
        {
            classification = c, units = principal.GetValueOrDefault(c),
            marketValueBefore = before.TryGetValue(c, out var b) ? b.Mv : 0m, assessedValueBefore = before.TryGetValue(c, out var b2) ? b2.Av : 0m,
            marketValueAfter = after.TryGetValue(c, out var a) ? a.Mv : 0m, assessedValueAfter = after.TryGetValue(c, out var a2) ? a2.Av : 0m,
        }).ToList();

        var assessmentIds = counted.Where(x => x.AssessmentId is not null).Select(x => x.AssessmentId!.Value).ToList();
        var notices = await db.NoticesOfAssessment.AsNoTracking()
            .Where(n => n.Status != NoticeStatus.Cancelled && n.Items.Any(i => assessmentIds.Contains(i.AssessmentId)))
            .Select(n => new { n.Status, n.ReceivedDate }).ToListAsync(ct);
        var rpuIds = counted.Select(x => x.RpuId).ToList();
        var independent = await db.IndependentAppraisals.AsNoTracking().Where(a => rpuIds.Contains(a.RpuId) && a.IsCurrent)
            .Select(a => a.RpuId).Distinct().CountAsync(ct);
        var runs = await db.RegisterRuns.AsNoTracking().Where(r => r.GeneralRevisionProgrammeId == p.Id)
            .GroupBy(r => r.Kind).Select(g => new { g.Key, Count = g.Count(), Overridden = g.Count(r => r.RollGateOverrideReason != null) }).ToListAsync(ct);
        var declared = await db.TaxDeclarations.AsNoTracking().CountAsync(t => t.AssessmentId != null && posted.Contains(t.AssessmentId.Value)
            && t.Status == WorkflowStatus.Approved, ct);

        var data = FormData.ToJson(new
        {
            revision = new
            {
                year = p.RevisionYear, effectiveDate = p.EffectiveDate, smv = p.Smv!.Reference, officeOrder = p.OfficeOrderReference, ordinance = p.OrdinanceReference,
                description = p.Description, status = p.Status.ToString(), completedOn = p.CompletedAt is { } at ? clock.LocalDate(at) : (DateOnly?)null,
                scope = p.Scope.OrderBy(s => s.Municipality!.Name).Select(s => s.Municipality!.Name).ToList(),
                province = p.Scope.Select(s => s.Municipality!.Province?.Name).FirstOrDefault(),
            },
            units = new
            {
                compiled = items.Count, excluded = items.Count(x => x.Status == GeneralRevisionItemStatus.Excluded),
                failed = counted.Count(x => x.Status == GeneralRevisionItemStatus.Failed), pending = counted.Count(x => x.Status == GeneralRevisionItemStatus.Pending),
                posted = posted.Count, declared, independentlyAppraised = independent,
                byKind = counted.GroupBy(x => x.RpuType).OrderBy(g => g.Key).Select(g => new { kind = g.Key.ToString(), units = g.Count() }).ToList(),
            },
            byClassification = classes,
            totals = new
            {
                marketValueBefore = classes.Sum(c => c.marketValueBefore), assessedValueBefore = classes.Sum(c => c.assessedValueBefore),
                marketValueAfter = classes.Sum(c => c.marketValueAfter), assessedValueAfter = classes.Sum(c => c.assessedValueAfter),
            },
            notices = new
            {
                total = notices.Count, draft = notices.Count(n => n.Status == NoticeStatus.Draft), issued = notices.Count(n => n.Status == NoticeStatus.Issued),
                served = notices.Count(n => n.Status == NoticeStatus.Served), latestReceipt = notices.Max(n => n.ReceivedDate),
            },
            registers = runs.OrderBy(r => r.Key).Select(r => new { kind = r.Key.ToString(), runs = r.Count, overridden = r.Overridden }).ToList(),
            appeals = new { recorded = false, note = "Assessment appeals are recorded in PRIME from step L7; not counted here." },
            excludedUnits = items.Where(x => x.Status == GeneralRevisionItemStatus.Excluded).OrderBy(x => x.Pin).ThenBy(x => x.RpuNumber)
                .Select(x => new { pin = x.Pin, unit = x.RpuNumber, reason = x.ExclusionReason }).ToList(),
            failedUnits = counted.Where(x => x.Status == GeneralRevisionItemStatus.Failed).OrderBy(x => x.Pin).ThenBy(x => x.RpuNumber)
                .Select(x => new { pin = x.Pin, unit = x.RpuNumber, reason = x.FailureReason }).ToList(),
        });
        // Both reports are made on completion (GRI 19; Book I p.25): previewed before, issued (frozen) after.
        return new FormSubjectData(null, data,
            p.Status == GeneralRevisionStatus.Completed ? null : "The revision's reports are issued once it is completed; preview them until then.");
    }
}
