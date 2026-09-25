using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Appraisal;
using Prime.Application.Features.Billing.Bills;
using Prime.Application.Features.Properties;
using Prime.Application.Features.TaxDeclarations;
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

    /// <summary>
    /// Current parties in whose name the property — or, with <paramref name="rpuId"/>, that unit — is
    /// declared (LGC §§204–205), named as everywhere else in PRIME.
    /// </summary>
    public static async Task<List<object>> CurrentOwnersAsync(IApplicationDbContext db, Guid propertyId, Guid? rpuId, CancellationToken ct) =>
        (await PropertyParties.ProjectAsync(await PropertyParties.ScopeAsync(db, propertyId, rpuId, x => x.IsCurrent, ct), ct))
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
            titleType = p.TitleType == null ? null : p.TitleType.Name,
            titleDate = p.TitleDate,
            boundaryNorth = p.BoundaryNorth,
            boundaryEast = p.BoundaryEast,
            boundarySouth = p.BoundarySouth,
            boundaryWest = p.BoundaryWest,
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
            owners = await FormData.CurrentOwnersAsync(db, bill.PropertyId, bill.RpuId, cancellationToken),
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

        // The assessment the TD declares (the TD with it is the FAAS); for a TD that declares none, the
        // RPU's latest posted one, as before.
        var assessments = db.Assessments.AsNoTracking()
            .Include(x => x.Lines).ThenInclude(l => l.Classification)
            .Include(x => x.Lines).ThenInclude(l => l.ActualUse)
            .Include(x => x.Valuation).ThenInclude(v => v!.Smv)
            .Include(x => x.Valuation).ThenInclude(v => v!.Lines);
        var assessment = td.AssessmentId is { } declared
            ? await assessments.FirstOrDefaultAsync(x => x.Id == declared, cancellationToken)
            : await assessments.Where(x => x.RpuId == td.RpuId && x.Status == WorkflowStatus.Posted)
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
            owners = await FormData.CurrentOwnersAsync(db, td.PropertyId, td.RpuId, cancellationToken),
            assessment = assessment is null ? null : new
            {
                year = assessment.AssessmentYear,
                effectiveDate = assessment.EffectiveDate,
                marketValue = assessment.MarketValue,
                assessmentLevelPercent = assessment.AssessmentPercentage,
                assessedValue = assessment.AssessedValue,
                // The TD's rows: classification, area, market value, actual use, level, assessed value (MRPAAO Att. 4).
                lines = assessment.Lines.OrderBy(l => l.Sequence).Select(l => new
                {
                    classification = l.Classification!.Name, actualUse = l.ActualUse!.Name, marketValue = l.MarketValue,
                    assessmentLevelPercent = l.AssessmentPercentage, assessedValue = l.AssessedValue,
                    area = AreaOf(assessment, td, l.ClassificationId, l.ActualUseId),
                    areaUnit = assessment.Valuation?.Lines.FirstOrDefault(v => v.Quantity != null && v.Source != ValuationLineSource.LandImprovement)?.Unit,
                }),
                smvOrdinanceNumber = assessment.Valuation?.Smv?.OrdinanceNumber,
                smvOrdinanceDate = assessment.Valuation?.Smv?.OrdinanceDate,
            },
            // MRPAAO Att. 4 additions (docs/analysis/mrpaao-forms-model.md §13).
            mrpaao = new
            {
                // The unit's PIN with its postscript, parenthesised when the unit is owned apart from the land.
                pin = RealPropertyUnits.UnitPin.Compose(
                    await db.Properties.Where(p => p.Id == td.PropertyId).Select(p => p.PropertyIdentificationNumber).FirstAsync(cancellationToken),
                    td.Rpu.PinSuffix,
                    await db.PropertyTaxpayers.AnyAsync(x => x.RpuId == td.RpuId && x.IsCurrent
                        && (x.Role == PropertyPartyRole.Owner || x.Role == PropertyPartyRole.UnknownOwner), cancellationToken)),
                effectivityQuarter = (td.EffectivityDate.Month - 1) / 3 + 1,
                effectivityYear = td.EffectivityDate.Year,
                transactionCode = td.TransactionCode,
                kind = await KindAsync(td, cancellationToken),
                cancels = td.PreviousTaxDeclaration is { } previous ? new
                {
                    number = previous.TaxDeclarationNumber,
                    owner = string.Join("; ", (await PropertyParties.ProjectAsync(await PropertyParties.ScopeAsync(db, previous.PropertyId, previous.RpuId,
                            x => x.StartDate <= previous.EffectivityDate && (x.EndDate == null || x.EndDate > previous.EffectivityDate), cancellationToken), cancellationToken))
                        .Where(o => o.Role is PropertyPartyRole.Owner or PropertyPartyRole.UnknownOwner).Select(o => o.TaxpayerDisplayName)),
                    assessedValue = previous.AssessmentId is { } pa
                        ? await db.Assessments.Where(x => x.Id == pa).Select(x => (decimal?)x.AssessedValue).FirstOrDefaultAsync(cancellationToken)
                        : null,
                } : null,
                declaredOwners = await DeclaredPartiesAsync(td, cancellationToken),
            },
            signatories,
            // A TD issued under a transfer carries the BIR clearance on its back (MRPAAO Annex A).
            transferClearance = td.PropertyTransactionId is { } txId
                ? await db.TransferTaxClearances.AsNoTracking().Where(x => x.PropertyTransactionId == txId).Select(c => new
                {
                    carNumber = c.CarNumber, carDate = c.CarDate, transferorName = c.TransferorName, transferorTin = c.TransferorTin,
                    transfereeTin = c.TransfereeTin, capitalGainsTax = c.CapitalGainsTax, capitalGainsTaxReceipt = c.CapitalGainsTaxReceipt,
                    capitalGainsTaxDate = c.CapitalGainsTaxDate, documentaryStampTax = c.DocumentaryStampTax,
                    documentaryStampTaxReceipt = c.DocumentaryStampTaxReceipt, documentaryStampTaxDate = c.DocumentaryStampTaxDate,
                    transferTax = c.TransferTax, transferTaxReceipt = c.TransferTaxReceipt, transferTaxDate = c.TransferTaxDate,
                }).FirstOrDefaultAsync(cancellationToken)
                : null,
        });
        // A cancelled TD stays printable — certified copies of historical records (LGC §472(b)(9));
        // the form marks it CANCELLED. A rejected or voided one never became a declaration.
        var blocker = td.Status is WorkflowStatus.Rejected or WorkflowStatus.Voided
            ? $"A {td.Status} Tax Declaration cannot be issued."
            : null;
        return new FormSubjectData(td.TaxDeclarationNumber, data, blocker);
    }

    /// <summary>The parties declared on the TD's effectivity date (the unit's own, else the property's), with TIN and contact (MRPAAO Att. 4).</summary>
    private async Task<List<object>> DeclaredPartiesAsync(TaxDeclaration td, CancellationToken ct)
    {
        var rows = await PropertyParties.ProjectAsync(await PropertyParties.ScopeAsync(db, td.PropertyId, td.RpuId,
            x => x.StartDate <= td.EffectivityDate && (x.EndDate == null || x.EndDate > td.EffectivityDate), ct), ct);
        var ids = rows.Select(o => o.TaxpayerId).OfType<Guid>().ToList();
        var contact = await db.Taxpayers.Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => new { t.Tin, t.ContactNumber }, ct);
        return rows.Select(o => (object)new
        {
            name = o.TaxpayerDisplayName, role = o.Role.ToString(), roleLabel = PropertyParties.RoleLabel(o.Role), address = o.Address,
            sharePercent = o.OwnershipPercentage,
            tin = o.TaxpayerId is { } a ? contact.GetValueOrDefault(a)?.Tin : null,
            contactNumber = o.TaxpayerId is { } b ? contact.GetValueOrDefault(b)?.ContactNumber : null,
        }).ToList();
    }

    /// <summary>The area of the valuation lines under one assessment line's classification and use (land and buildings; none for machinery).</summary>
    private static decimal? AreaOf(Assessment assessment, TaxDeclaration td, Guid classificationId, Guid actualUseId)
    {
        var lines = assessment.Valuation?.Lines
            .Where(v => v.Source is ValuationLineSource.Land or ValuationLineSource.LandStrip or ValuationLineSource.Building or ValuationLineSource.BuildingUsePortion)
            .Where(v => (v.ClassificationId ?? td.ClassificationId) == classificationId && (v.ActualUseId ?? td.ActualUseId) == actualUseId)
            .ToList();
        return lines is { Count: > 0 } ? lines.Sum(v => v.Quantity ?? 0m) : null;
    }

    /// <summary>MRPAAO Att. 4 "Kind of Property Assessed": land, building (storeys, brief description), machinery (brief description) or others.</summary>
    private async Task<object> KindAsync(TaxDeclaration td, CancellationToken ct)
    {
        var type = td.Rpu!.RpuType;
        string? description = null;
        int? storeys = null;
        if (type == RpuType.Building)
        {
            var b = await db.Buildings.Where(x => x.RpuId == td.RpuId)
                .Select(x => new { x.NumberOfStoreys, Type = x.BuildingType!.Name, Structure = x.StructuralType!.Name }).FirstOrDefaultAsync(ct);
            storeys = b?.NumberOfStoreys;
            description = b is null ? null : $"{b.Type}, {b.Structure}";
        }
        else if (type == RpuType.Machinery)
        {
            var names = await db.MachineryUnits.Where(x => x.RpuId == td.RpuId).OrderBy(x => x.CreatedAt)
                .Select(x => x.MachineryType!.Name + (x.Brand != null ? " " + x.Brand : "")).ToListAsync(ct);
            description = names.Count == 0 ? null : string.Join("; ", names);
        }
        return new { type = type.ToString(), storeys, description };
    }
}

/// <summary>
/// Notice of Assessment (LGC §223). Only an issued or served notice can be
/// issued as a form; a draft is previewed. Its <c>items</c> are the MRPAAO
/// Att. 10 rows: ARPN, TDN, PIN, location, classification, MV, AV.
/// </summary>
public sealed class NoticeFormDataProvider(IApplicationDbContext db, IOptions<FaasOptions> faas) : IFormDataProvider
{
    public FormSubjectType SubjectType => FormSubjectType.NoticeOfAssessment;

    public async Task<FormSubjectData?> BuildAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var n = await db.NoticesOfAssessment.AsNoTracking().Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == subjectId, cancellationToken);
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
                    NoticeReason.AssessmentDecreased => "decreased",
                    NoticeReason.DeclaredOwnerChanged => "updated for a change of declared owner",
                    NoticeReason.OwnerAddressChanged => "updated for a change of the owner's address",
                    _ => "updated for a change of the property's location",
                },
                previousAssessedValue = n.PreviousAssessedValue,
                assessedValue = n.AssessedValue,
                marketValue = n.MarketValue,
                assessmentYear = n.AssessmentYear,
                assessmentEffectiveDate = n.AssessmentEffectiveDate,
                addresseeNames = n.AddresseeNames,
                addresseeAddress = n.AddresseeAddress,
                appealPeriodDays = n.AppealPeriodDays,
                appealDeadline = n.AppealDeadline,
                status = n.Status.ToString(),
                issuedAt = n.IssuedAt,
                taxDeclarationNumber = tdNumber,
                rpuNumber,
            },
            property = await FormData.PropertyAsync(db, n.PropertyId, cancellationToken),
            items = await ItemsAsync(n, cancellationToken),
        });
        var blocker = n.Status is NoticeStatus.Issued or NoticeStatus.Served
            ? null
            : $"Only an issued notice can be printed as issued (this one is {n.Status}); preview it instead.";
        return new FormSubjectData(n.NoticeNumber, data, blocker);
    }

    private async Task<List<object>> ItemsAsync(Domain.Entities.Notices.NoticeOfAssessment n, CancellationToken ct)
    {
        var rows = new List<object>();
        foreach (var i in n.Items.OrderBy(i => i.Sequence))
        {
            var property = await db.Properties.AsNoTracking().Where(p => p.Id == i.PropertyId)
                .Select(p => new { p.PropertyIdentificationNumber, p.Street, Barangay = p.Barangay!.Name, Municipality = p.Municipality!.Name, Province = p.Province!.Name })
                .FirstAsync(ct);
            var suffix = await db.RealPropertyUnits.Where(r => r.Id == i.RpuId).Select(r => r.PinSuffix).FirstAsync(ct);
            var td = i.TaxDeclarationId is { } tdId
                ? await db.TaxDeclarations.AsNoTracking().Where(x => x.Id == tdId).Select(x => new { x.TaxDeclarationNumber, x.AssessmentId }).FirstOrDefaultAsync(ct)
                : null;
            var faasNumber = await db.Assessments.Where(a => a.Id == i.AssessmentId).Select(a => a.FaasNumber).FirstOrDefaultAsync(ct);
            var classification = string.Join(", ", await db.AssessmentLines.Where(l => l.AssessmentId == i.AssessmentId).OrderBy(l => l.Sequence)
                .Select(l => l.Classification!.Name).ToListAsync(ct));
            rows.Add(new
            {
                sequence = i.Sequence,
                arpNumber = faas.Value.NumberSource == FaasNumberSource.TaxDeclaration ? td?.TaxDeclarationNumber : faasNumber,
                tdNumber = td?.TaxDeclarationNumber,
                pin = RealPropertyUnits.UnitPin.Compose(property.PropertyIdentificationNumber, suffix, false),
                location = string.Join(", ", new[] { property.Street, property.Barangay }.Where(x => !string.IsNullOrWhiteSpace(x))),
                municipality = property.Municipality,
                province = property.Province,
                classification,
                reason = i.Reason.ToString(),
                previousAssessedValue = i.PreviousAssessedValue,
                marketValue = i.MarketValue,
                assessedValue = i.AssessedValue,
                assessmentYear = i.AssessmentYear,
            });
        }
        return rows;
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

/// <summary>
/// Statement of account (CLAUDE.md §52): a property's posted bills, from the
/// same read model the API serves. Preview only: issuing is once per subject
/// (UX_IssuedForms_Definition_Subject_Valid), but a statement is a
/// point-in-time view that changes with every bill and, from Phase 9, every
/// payment — issuing one needs its own statement record first.
/// </summary>
public sealed class StatementOfAccountFormDataProvider(IApplicationDbContext db, IBillService bills) : IFormDataProvider
{
    public FormSubjectType SubjectType => FormSubjectType.StatementOfAccount;

    public async Task<FormSubjectData?> BuildAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var statement = await bills.GetStatementOfAccountAsync(subjectId, cancellationToken);
        if (statement.IsFailure)
        {
            return null;
        }
        var data = FormData.ToJson(new
        {
            statement = statement.Value,
            property = await FormData.PropertyAsync(db, subjectId, cancellationToken),
            owners = await FormData.CurrentOwnersAsync(db, subjectId, null, cancellationToken),
        });
        return new FormSubjectData(null, data,
            "A statement of account can only be previewed and printed: it changes with every bill and payment, and PRIME does not yet keep issued statements.");
    }
}
