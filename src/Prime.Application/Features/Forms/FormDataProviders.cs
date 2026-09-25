using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Appraisal;
using Prime.Application.Features.Properties;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Forms;

/// <summary>
/// What a form shows for one record. <see cref="IssueBlocker"/> explains why
/// it may be previewed but not issued (e.g. an unposted bill); null when it
/// can be issued.
/// </summary>
public sealed record FormSubjectData(string? DocumentNumber, JsonObject Data, string? IssueBlocker);

/// <summary>
/// Builds the data snapshot for one <see cref="FormSubjectType"/>
/// (docs/FORMS-REVISION-PLAN.md §4.1). The snapshot is the form's only
/// input: templates never query the database, so any template version — the
/// provisional one now, the LAM's later — can be rendered from it. Fields the
/// LAM needs that are not here are added to the provider, not the template.
/// </summary>
public interface IFormDataProvider
{
    FormSubjectType SubjectType { get; }
    Task<FormSubjectData?> BuildAsync(Guid subjectId, CancellationToken cancellationToken);
}

internal static class FormData
{
    // Enums as names, as the API writes them, so templates compare against readable values.
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public static JsonObject ToJson(object value) => (JsonObject)JsonSerializer.SerializeToNode(value, Options)!;

    /// <summary>Current parties in whose name the property is declared (LGC §§204–205), named as everywhere else in PRIME.</summary>
    public static async Task<List<object>> CurrentOwnersAsync(IApplicationDbContext db, Guid propertyId, CancellationToken ct) =>
        (await PropertyParties.ProjectAsync(db.PropertyTaxpayers.Where(x => x.PropertyId == propertyId && x.IsCurrent), ct))
        .Select(o => (object)new
        {
            name = o.TaxpayerDisplayName,
            role = o.Role.ToString(),
            roleLabel = PropertyParties.RoleLabel(o.Role),
            isOwner = o.Role == PropertyPartyRole.Owner,
            sharePercent = o.OwnershipPercentage,
            address = o.Address,
        })
        .ToList();

    public static async Task<object?> PropertyAsync(IApplicationDbContext db, Guid propertyId, CancellationToken ct) =>
        await db.Properties.Where(p => p.Id == propertyId).Select(p => new
        {
            pin = p.PropertyIdentificationNumber,
            street = p.Street,
            sitio = p.Sitio,
            lotNumber = p.LotNumber,
            blockNumber = p.BlockNumber,
            surveyNumber = p.SurveyNumber,
            titleNumber = p.TitleNumber,
            taxMapNumber = p.TaxMapNumber,
            barangay = p.Barangay!.Name,
            municipality = p.Municipality!.Name,
            province = p.Province!.Name,
        }).FirstOrDefaultAsync(ct);
}

/// <summary>Tax bill (docs/BILLING.md §4). Only a posted bill can be issued.</summary>
public sealed class TaxBillFormDataProvider(IApplicationDbContext db) : IFormDataProvider
{
    public FormSubjectType SubjectType => FormSubjectType.TaxBill;

    public async Task<FormSubjectData?> BuildAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var bill = await db.TaxBills.Include(x => x.Rpu).Include(x => x.TaxDeclaration)
            .Include(x => x.TaxTypes).ThenInclude(t => t.TaxType)
            .Include(x => x.Details).ThenInclude(d => d.TaxType)
            .AsNoTracking().FirstOrDefaultAsync(x => x.Id == subjectId, cancellationToken);
        if (bill is null)
        {
            return null;
        }

        var data = FormData.ToJson(new
        {
            bill = new
            {
                billNumber = bill.BillNumber,
                taxYear = bill.TaxYear,
                asOfDate = bill.AsOfDate,
                rulesAsOfDate = bill.RulesAsOfDate,
                status = bill.Status.ToString(),
                assessedValue = bill.AssessedValue,
                total = bill.Details.Sum(d => d.Amount),
                notes = bill.Notes,
                rpuNumber = bill.Rpu!.RpuNumber,
                taxDeclarationNumber = bill.TaxDeclaration!.TaxDeclarationNumber,
            },
            property = await FormData.PropertyAsync(db, bill.PropertyId, cancellationToken),
            owners = await FormData.CurrentOwnersAsync(db, bill.PropertyId, cancellationToken),
            taxTypes = bill.TaxTypes.OrderBy(t => t.TaxType!.SortOrder).ThenBy(t => t.TaxType!.Code).Select(t => new
            {
                code = t.TaxType!.Code, name = t.TaxType.Name, ratePercent = t.RatePercent,
                computedAnnualTax = t.ComputedAnnualTax, capLimit = t.CapLimit, annualTax = t.AnnualTax,
            }),
            lines = bill.Details.OrderBy(d => d.LineNumber).Select(d => new
            {
                installment = d.InstallmentSequence, dueDate = d.DueDate, taxType = d.TaxType!.Code,
                component = d.Component.ToString(), explanation = d.Explanation, amount = d.Amount,
            }),
        });
        var blocker = bill.Status == WorkflowStatus.Posted ? null : $"Only a posted bill can be issued (this one is {bill.Status}); preview it instead.";
        return new FormSubjectData(bill.BillNumber, data, blocker);
    }
}

/// <summary>
/// Tax Declaration. Shows the latest posted assessment of its RPU and the
/// signatories frozen in that assessment's approval records (§4.5).
/// </summary>
public sealed class TaxDeclarationFormDataProvider(IApplicationDbContext db) : IFormDataProvider
{
    public FormSubjectType SubjectType => FormSubjectType.TaxDeclaration;

    public async Task<FormSubjectData?> BuildAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var td = await db.TaxDeclarations.Include(x => x.Rpu).Include(x => x.Classification).Include(x => x.ActualUse)
            .Include(x => x.PreviousTaxDeclaration)
            .Include(x => x.Annotations).ThenInclude(a => a.AnnotationType)
            .AsNoTracking().FirstOrDefaultAsync(x => x.Id == subjectId, cancellationToken);
        if (td is null)
        {
            return null;
        }

        var assessment = await db.Assessments.AsNoTracking()
            .Where(x => x.RpuId == td.RpuId && x.Status == WorkflowStatus.Posted)
            .OrderByDescending(x => x.EffectiveDate).ThenByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var signatories = new List<object>();
        if (assessment is not null)
        {
            var records = await db.ApprovalRecords.AsNoTracking()
                .Where(x => x.SubjectType == ApprovalSubjectType.Assessment && x.SubjectId == assessment.Id)
                .OrderBy(x => x.StepSequence).ToListAsync(cancellationToken);
            signatories.AddRange(records.Select(r => (object)new { label = r.Label, name = r.SignatoryName, position = r.SignatoryPosition, signedAt = r.SignedAt }));
            if (records.Count == 0 && assessment.ApprovedBy is { } approver)
            {
                var name = await db.AppUsers.Where(u => u.Id == approver).Select(u => u.DisplayName).FirstOrDefaultAsync(cancellationToken);
                signatories.Add(new { label = "Approved by", name = name ?? "(unknown user)", position = (string?)null, signedAt = assessment.ApprovedAt });
            }
        }

        var data = FormData.ToJson(new
        {
            td = new
            {
                number = td.TaxDeclarationNumber,
                revisionNumber = td.RevisionNumber,
                effectivityDate = td.EffectivityDate,
                assessmentYear = td.AssessmentYear,
                taxability = td.Taxability.ToString(),
                status = td.Status.ToString(),
                classification = td.Classification!.Name,
                actualUse = td.ActualUse!.Name,
                previousNumber = td.PreviousTaxDeclaration?.TaxDeclarationNumber,
                remarks = td.Remarks,
                cancelledAt = td.CancelledAt,
                cancellationReason = td.CancellationReason,
                supersededByNumber = td.SupersededByTaxDeclarationId is { } supersededBy
                    ? await db.TaxDeclarations.Where(x => x.Id == supersededBy).Select(x => x.TaxDeclarationNumber).FirstOrDefaultAsync(cancellationToken)
                    : null,
            },
            annotations = td.Annotations.OrderBy(a => a.EffectiveDate).ThenBy(a => a.CreatedAt).Select(a => new
            {
                type = a.AnnotationType!.Name, text = a.Text, referenceNumber = a.ReferenceNumber, referenceDate = a.ReferenceDate,
                effectiveDate = a.EffectiveDate, lifted = a.LiftedAt != null, liftedAt = a.LiftedAt, liftReason = a.LiftReason,
            }),
            rpu = new { number = td.Rpu!.RpuNumber, type = td.Rpu.RpuType.ToString() },
            property = await FormData.PropertyAsync(db, td.PropertyId, cancellationToken),
            owners = await FormData.CurrentOwnersAsync(db, td.PropertyId, cancellationToken),
            assessment = assessment is null ? null : new
            {
                year = assessment.AssessmentYear,
                effectiveDate = assessment.EffectiveDate,
                marketValue = assessment.MarketValue,
                assessmentLevelPercent = assessment.AssessmentPercentage,
                assessedValue = assessment.AssessedValue,
            },
            signatories,
        });
        // A cancelled TD stays printable — certified copies of historical records (LGC §472(b)(9));
        // the form marks it CANCELLED. A rejected or voided one never became a declaration.
        var blocker = td.Status is WorkflowStatus.Rejected or WorkflowStatus.Voided
            ? $"A {td.Status} Tax Declaration cannot be issued."
            : null;
        return new FormSubjectData(td.TaxDeclarationNumber, data, blocker);
    }
}

/// <summary>Notice of Assessment (LGC §223). Only an issued or served notice can be issued as a form; a draft is previewed.</summary>
public sealed class NoticeFormDataProvider(IApplicationDbContext db) : IFormDataProvider
{
    public FormSubjectType SubjectType => FormSubjectType.NoticeOfAssessment;

    public async Task<FormSubjectData?> BuildAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var n = await db.NoticesOfAssessment.AsNoTracking().FirstOrDefaultAsync(x => x.Id == subjectId, cancellationToken);
        if (n is null)
        {
            return null;
        }
        var tdNumber = n.TaxDeclarationId is { } tdId
            ? await db.TaxDeclarations.Where(x => x.Id == tdId).Select(x => x.TaxDeclarationNumber).FirstOrDefaultAsync(cancellationToken)
            : null;
        var rpuNumber = await db.RealPropertyUnits.Where(x => x.Id == n.RpuId).Select(x => x.RpuNumber).FirstAsync(cancellationToken);
        var data = FormData.ToJson(new
        {
            notice = new
            {
                number = n.NoticeNumber,
                reason = n.Reason.ToString(),
                reasonLabel = n.Reason switch
                {
                    NoticeReason.FirstAssessment => "assessed for the first time",
                    NoticeReason.AssessmentIncreased => "increased",
                    _ => "decreased",
                },
                previousAssessedValue = n.PreviousAssessedValue,
                assessedValue = n.AssessedValue,
                marketValue = n.MarketValue,
                assessmentYear = n.AssessmentYear,
                assessmentEffectiveDate = n.AssessmentEffectiveDate,
                addresseeNames = n.AddresseeNames,
                addresseeAddress = n.AddresseeAddress,
                appealPeriodDays = n.AppealPeriodDays,
                status = n.Status.ToString(),
                issuedAt = n.IssuedAt,
                taxDeclarationNumber = tdNumber,
                rpuNumber,
            },
            property = await FormData.PropertyAsync(db, n.PropertyId, cancellationToken),
        });
        var blocker = n.Status is NoticeStatus.Issued or NoticeStatus.Served
            ? null
            : $"Only an issued notice can be printed as issued (this one is {n.Status}); preview it instead.";
        return new FormSubjectData(n.NoticeNumber, data, blocker);
    }
}

/// <summary>
/// FAAS: the appraisal record of one assessment (docs/FORMS-REVISION-PLAN.md
/// A7), from the same read model the API serves. Issuable once the
/// assessment is approved — the appraisal is then complete and signed.
/// </summary>
public sealed class AppraisalRecordFormDataProvider(IAppraisalRecordService appraisals) : IFormDataProvider
{
    public FormSubjectType SubjectType => FormSubjectType.Assessment;

    public async Task<FormSubjectData?> BuildAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var record = await appraisals.GetAsync(subjectId, cancellationToken);
        if (record.IsFailure)
        {
            return null;
        }
        var r = record.Value;
        var blocker = r.Status is WorkflowStatus.Approved or WorkflowStatus.Posted
            ? null
            : $"Only an approved or posted assessment's appraisal record can be issued (this one is {r.Status}); preview it instead.";
        return new FormSubjectData(r.FaasNumber, FormData.ToJson(new { appraisal = r }), blocker);
    }
}
