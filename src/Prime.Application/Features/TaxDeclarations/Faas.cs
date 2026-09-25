using Microsoft.EntityFrameworkCore;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Numbering;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.TaxDeclarations;

/// <summary>Where a FAAS gets its number (docs/analysis/mrpaao-forms-model.md §6.1).</summary>
public enum FaasNumberSource
{
    /// <summary>The FAAS number is its Tax Declaration's number — FAAS = ARP = TD (MRPAAO p.156, 159).</summary>
    TaxDeclaration = 0,
    /// <summary>A separate <c>Faas</c> numbering scheme, assigned when the assessment is approved.</summary>
    Own = 1,
}

/// <summary>
/// How PRIME keeps the manual's FAAS: a Tax Declaration together with the
/// assessment it declares. Each setting is a policy choice taken from the
/// superseded MRPAAO (DOMAIN VERIFICATION REQUIRED against the LAM).
/// </summary>
public sealed class FaasOptions
{
    public const string SectionName = "Faas";

    public FaasNumberSource NumberSource { get; set; } = FaasNumberSource.TaxDeclaration;

    /// <summary>When true, a TD cannot be approved without the assessment it declares. Off for legacy data.</summary>
    public bool RequireAssessmentOnTd { get; set; }

    /// <summary>
    /// When true, posting an assessment for a unit that has a current TD
    /// prepares a Draft TD declaring it (MRPAAO p.156: a TD is prepared on
    /// general revision and on every change of value).
    /// </summary>
    public bool PrepareTdOnPosting { get; set; } = true;
}

/// <summary>Links between Tax Declarations and the assessments they declare.</summary>
internal static class FaasTaxDeclarations
{
    /// <summary>
    /// The assessment a TD declares by default: the RPU's latest approved or
    /// posted assessment effective on or before the TD's effectivity — for a
    /// transfer, the assessment already in force.
    /// </summary>
    public static Task<Guid?> AssessmentInForceAsync(IApplicationDbContext db, Guid rpuId, DateOnly asOf, CancellationToken ct) =>
        db.Assessments
            .Where(x => x.RpuId == rpuId && x.EffectiveDate <= asOf
                && (x.Status == WorkflowStatus.Approved || x.Status == WorkflowStatus.Posted))
            .OrderByDescending(x => x.EffectiveDate).ThenByDescending(x => x.CreatedAt)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);

    /// <summary>Why <paramref name="assessmentId"/> cannot be declared by a TD of <paramref name="rpuId"/>, or null.</summary>
    public static async Task<string?> AssessmentProblemAsync(IApplicationDbContext db, Guid rpuId, Guid assessmentId, CancellationToken ct)
    {
        var a = await db.Assessments.Where(x => x.Id == assessmentId).Select(x => new { x.RpuId, x.Status }).FirstOrDefaultAsync(ct);
        if (a is null || a.RpuId != rpuId)
        {
            return "The assessment must belong to the TD's RPU.";
        }
        return a.Status is WorkflowStatus.Approved or WorkflowStatus.Posted
            ? null
            : $"Only an approved or posted assessment can be declared (this one is {a.Status}).";
    }

    /// <summary>
    /// After an assessment is posted: a Draft TD that declares it, replacing
    /// the unit's current TD, with the classification and actual use the
    /// assessment was levelled under. Returns why none was prepared, or null.
    /// Nothing is prepared for a unit without a current TD (its first TD is
    /// declared by hand), when one already declares this assessment, or when
    /// no TD numbering scheme is in force (a TD cannot exist unnumbered).
    /// </summary>
    public static async Task<string?> PrepareForPostedAsync(IApplicationDbContext db, INumberingService numbering, Assessment assessment,
        DateOnly today, CancellationToken ct)
    {
        var current = await db.TaxDeclarations.FirstOrDefaultAsync(x => x.RpuId == assessment.RpuId && x.Status == WorkflowStatus.Approved, ct);
        if (current is null)
        {
            return "The unit has no current Tax Declaration.";
        }
        if (current.AssessmentId == assessment.Id
            || await db.TaxDeclarations.AnyAsync(x => x.AssessmentId == assessment.Id
                && (x.Status == WorkflowStatus.Draft || x.Status == WorkflowStatus.PendingReview || x.Status == WorkflowStatus.Approved), ct))
        {
            return "A Tax Declaration already declares this assessment.";
        }
        var level = await db.AssessmentLevels.Where(x => x.Id == assessment.AssessmentLevelId)
            .Select(x => new { x.ClassificationId, x.ActualUseId }).FirstAsync(ct);
        var context = await NumberContexts.ForPropertyAsync(db, assessment.PropertyId, assessment.EffectiveDate.Year, ct);
        var number = await numbering.GenerateIfConfiguredAsync(NumberedDocumentKind.TaxDeclaration, context, today, ct);
        if (number.IsFailure)
        {
            return number.Message;
        }
        if (number.Value is null)
        {
            return "No Tax Declaration numbering scheme is in force.";
        }
        db.TaxDeclarations.Add(new TaxDeclaration
        {
            RpuId = assessment.RpuId,
            PropertyId = assessment.PropertyId,
            TaxDeclarationNumber = number.Value,
            RevisionNumber = current.RevisionNumber + 1,
            EffectivityDate = assessment.EffectiveDate,
            Taxability = current.Taxability,
            ClassificationId = level.ClassificationId,
            ActualUseId = level.ActualUseId,
            SubClassificationId = current.ClassificationId == level.ClassificationId ? current.SubClassificationId : null,
            AssessmentYear = assessment.AssessmentYear,
            PreviousTaxDeclarationId = current.Id,
            AssessmentId = assessment.Id,
            Remarks = $"Prepared on posting of the {assessment.AssessmentYear} assessment effective {assessment.EffectiveDate:yyyy-MM-dd}.",
            Status = WorkflowStatus.Draft,
        });
        return null;
    }
}
