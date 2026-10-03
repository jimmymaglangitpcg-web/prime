using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Offices;
using Prime.Domain.Entities.Offices;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Forms;

/// <summary>
/// The letterhead a form prints (docs/analysis/province-wide-operation.md §3.6):
/// the office whose jurisdiction covers the record's municipality today, else
/// the provincial office. A record with no municipality (an Ownership Record
/// Card is kept by owner) prints the issuing user's office, else the provincial
/// office. Once an office is found, the letterhead is that office's own fields
/// only; the <c>Lgu:</c> settings are the fallback when no office applies (a
/// deployment without offices, and the frozen treasury forms, CLAUDE.md §0).
/// The province's name stays a setting; the legislature is the office's,
/// else the setting.
/// </summary>
public static class FormLetterhead
{
    /// <summary>Treasury forms keep the single <c>Lgu:</c> letterhead: offices are not given to frozen code (§6).</summary>
    private static readonly HashSet<FormSubjectType> Treasury = [FormSubjectType.TaxBill, FormSubjectType.StatementOfAccount, FormSubjectType.Payment];

    /// <returns>The <c>lgu</c> snapshot key (unchanged shape, so earlier templates print the office) and the <c>office</c> key, null when none applies.</returns>
    public static async Task<(JsonObject Lgu, JsonObject? Office)> BuildAsync(IApplicationDbContext db, IOfficeContext officeContext, LguOptions settings,
        FormSubjectType subjectType, Guid subjectId, DateOnly asOf, CancellationToken ct)
    {
        var office = Treasury.Contains(subjectType) ? null : await ResolveAsync(db, officeContext, subjectType, subjectId, asOf, ct);
        var lgu = office is null
            ? FormData.ToJson(new
            {
                name = settings.Name, office = settings.Office, province = settings.Province, address = settings.Address,
                contact = (string?)null, sanggunianName = settings.SanggunianName,
            })
            : FormData.ToJson(new
            {
                name = office.LguName, office = office.Name, province = settings.Province, address = office.Address,
                // The office's Sanggunian (records-and-forms.md Q8), else the deployment's.
                contact = office.Contact, sanggunianName = office.SanggunianName ?? settings.SanggunianName,
            });
        var officeJson = office is null ? null : FormData.ToJson(new
        {
            code = office.Code, name = office.Name, kind = office.Kind, lguName = office.LguName, headPosition = office.HeadPosition,
            address = office.Address, contact = office.Contact, sanggunianName = office.SanggunianName ?? settings.SanggunianName,
        });
        return (lgu, officeJson);
    }

    /// <summary>The office whose letterhead the record prints; null when PRIME has no active office to use.</summary>
    public static async Task<Office?> ResolveAsync(IApplicationDbContext db, IOfficeContext officeContext, FormSubjectType subjectType, Guid subjectId,
        DateOnly asOf, CancellationToken ct)
    {
        var municipalityId = await MunicipalityAsync(db, subjectType, subjectId, ct);
        Office? office = null;
        if (municipalityId is { } m)
        {
            office = await db.OfficeJurisdictions.AsNoTracking().InForce(asOf).Where(j => j.MunicipalityId == m)
                .Select(j => j.Office!).FirstOrDefaultAsync(ct);
        }
        else if ((await officeContext.GetAsync(ct)).OfficeId is { } own)
        {
            office = await db.Offices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == own, ct);
        }
        if (office is { Status: RecordStatus.Active })
        {
            return office;
        }
        return await db.Offices.AsNoTracking().FirstOrDefaultAsync(x => x.Kind == OfficeKind.Provincial && x.Status == RecordStatus.Active, ct);
    }

    /// <summary>The record's municipality (its jurisdiction, §3.3); null for a record kept province-wide.</summary>
    private static async Task<Guid?> MunicipalityAsync(IApplicationDbContext db, FormSubjectType type, Guid id, CancellationToken ct) => type switch
    {
        FormSubjectType.TaxDeclaration or FormSubjectType.Faas =>
            await db.TaxDeclarations.Where(x => x.Id == id).Select(x => (Guid?)x.Property!.MunicipalityId).FirstOrDefaultAsync(ct),
        FormSubjectType.Assessment =>
            await db.Assessments.Where(x => x.Id == id).Select(x => (Guid?)x.Property!.MunicipalityId).FirstOrDefaultAsync(ct),
        FormSubjectType.NoticeOfAssessment =>
            await db.NoticesOfAssessment.Where(x => x.Id == id).Select(x => (Guid?)x.Property!.MunicipalityId).FirstOrDefaultAsync(ct),
        FormSubjectType.Register =>
            await db.RegisterRuns.Where(x => x.Id == id).Select(x => x.Barangay == null ? null : (Guid?)x.Barangay.MunicipalityId).FirstOrDefaultAsync(ct),
        FormSubjectType.SwornStatement =>
            await db.SwornStatements.Where(x => x.Id == id).Select(x => (Guid?)x.MunicipalityId).FirstOrDefaultAsync(ct),
        FormSubjectType.NoticeOfCancellation =>
            await db.NoticesOfCancellation.Where(x => x.Id == id).Select(x => (Guid?)x.Property!.MunicipalityId).FirstOrDefaultAsync(ct),
        FormSubjectType.DiscoverySummons =>
            await db.DiscoverySummonses.Where(x => x.Id == id).Select(x => (Guid?)x.PropertyTransaction!.Property!.MunicipalityId).FirstOrDefaultAsync(ct),
        FormSubjectType.MarketDataReport =>
            await db.MarketDataReportRuns.Where(x => x.Id == id).Select(x => (Guid?)x.MunicipalityId).FirstOrDefaultAsync(ct),
        // A revision of one city/municipality carries its office's letterhead; one of several, the province's.
        FormSubjectType.GeneralRevision =>
            await db.GeneralRevisionScopes.Where(x => x.GeneralRevisionProgrammeId == id).CountAsync(ct) == 1
                ? await db.GeneralRevisionScopes.Where(x => x.GeneralRevisionProgrammeId == id).Select(x => (Guid?)x.MunicipalityId).FirstAsync(ct)
                : null,
        _ => null,
    };
}
