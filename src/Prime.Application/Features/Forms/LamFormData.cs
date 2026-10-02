using Microsoft.EntityFrameworkCore;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Properties;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Forms;

/// <summary>
/// Shared pieces of the <c>lam</c> object the form data providers add for the LAM versions of the forms
/// (docs/analysis/records-and-forms.md §4.2). The MRPAAO templates keep reading the fields they read before; the
/// LAM templates (loaded as content, CLAUDE.md §118) read <c>lam</c>. Values are recorded ones; nothing is invented.
/// </summary>
internal static class LamFormData
{
    /// <summary>"Titled" when a title number is recorded, else "Untitled" (Q4: derived, no field of its own).</summary>
    public static string RegistrationType(string? titleNumber) => string.IsNullOrWhiteSpace(titleNumber) ? "Untitled" : "Titled";

    /// <summary>
    /// The parties declared on <paramref name="asOf"/> (the unit's own, else the property's) with TIN, contact, email
    /// and — only when <see cref="FormsOptions.PrintOwnerSex"/> is on — sex.
    /// </summary>
    public static async Task<List<LamParty>> PartiesAsync(IApplicationDbContext db, Guid propertyId, Guid? rpuId, DateOnly asOf, bool printSex,
        CancellationToken ct)
    {
        var rows = await PropertyParties.ProjectAsync(await PropertyParties.ScopeAsync(db, propertyId, rpuId,
            x => x.StartDate <= asOf && (x.EndDate == null || x.EndDate > asOf), ct), ct);
        var ids = rows.Select(o => o.TaxpayerId).OfType<Guid>().ToList();
        var details = await db.Taxpayers.Where(t => ids.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => new { t.Tin, t.ContactNumber, t.Email, t.Sex }, ct);
        return rows.Select(o =>
        {
            var d = o.TaxpayerId is { } id ? details.GetValueOrDefault(id) : null;
            return new LamParty(o.TaxpayerDisplayName, o.Role.ToString(), PropertyParties.RoleLabel(o.Role),
                o.Role is PropertyPartyRole.Owner or PropertyPartyRole.UnknownOwner, o.OwnershipPercentage, o.Address,
                d?.Tin, d?.ContactNumber, d?.Email, printSex ? d?.Sex?.ToString() : null);
        }).ToList();
    }

    /// <summary>The back-tax period an assessment values (valuation-foundation.md §4.8), null when it is not one.</summary>
    public static async Task<object?> BackTaxPeriodAsync(IApplicationDbContext db, Guid? assessmentId, CancellationToken ct) =>
        assessmentId is not { } id ? null
            : await db.BackTaxPeriods.Where(p => p.AssessmentId == id).Select(p => new { startDate = p.StartDate, endDate = p.EndDate })
                .FirstOrDefaultAsync(ct);

    /// <summary>The distinct ordinances of the assessment levels an assessment applied, for the TD's tax-ordinance citation (Q8).</summary>
    public static async Task<List<object>> AssessmentLevelOrdinancesAsync(IApplicationDbContext db, Guid? assessmentId, CancellationToken ct)
    {
        if (assessmentId is not { } id)
        {
            return [];
        }
        var rows = await db.AssessmentLines.Where(l => l.AssessmentId == id)
            .Select(l => new { l.AssessmentLevel!.OrdinanceNumber, l.AssessmentLevel.OrdinanceDate }).Distinct().ToListAsync(ct);
        return rows.Where(r => !string.IsNullOrWhiteSpace(r.OrdinanceNumber)).OrderBy(r => r.OrdinanceDate).ThenBy(r => r.OrdinanceNumber)
            .Select(r => (object)new { number = r.OrdinanceNumber, date = r.OrdinanceDate }).ToList();
    }

    /// <summary>
    /// The PIN a property had before its current one (the LAM TMCR's "previous PIN"): its latest PIN retired by
    /// <paramref name="asOf"/>; else, for a property a subdivision or consolidation produced, the PINs of the
    /// properties it came from. Null when none is recorded.
    /// </summary>
    public static async Task<string?> PreviousPinAsync(IApplicationDbContext db, IClock clock, Guid propertyId, DateOnly asOf, CancellationToken ct)
    {
        var retired = (await db.PinAssignments.AsNoTracking().Where(x => x.PropertyId == propertyId && x.RetiredAt != null).ToListAsync(ct))
            .Where(x => clock.LocalDate(x.RetiredAt!.Value) <= asOf).MaxBy(x => x.RetiredAt);
        if (retired is not null)
        {
            return retired.Pin;
        }
        var sources = await db.PropertyTransactionProperties.AsNoTracking()
            .Where(r => r.PropertyId == propertyId && r.Role == TransactionPropertyRole.Result)
            .Select(r => r.PropertyTransactionId).ToListAsync(ct);
        if (sources.Count == 0)
        {
            return null;
        }
        var from = await db.PropertyTransactions.AsNoTracking().Where(t => sources.Contains(t.Id) && t.Status == WorkflowStatus.Posted)
            .Select(t => t.PropertyId)
            .Concat(db.PropertyTransactionProperties.Where(r => sources.Contains(r.PropertyTransactionId) && r.Role == TransactionPropertyRole.Source)
                .Select(r => r.PropertyId))
            .Distinct().ToListAsync(ct);
        from.Remove(propertyId);
        if (from.Count == 0)
        {
            return null;
        }
        var pins = await db.Properties.Where(p => from.Contains(p.Id)).Select(p => p.PropertyIdentificationNumber).OrderBy(p => p).ToListAsync(ct);
        return string.Join("; ", pins);
    }

    /// <summary>The kind of property as the LAM TD names it.</summary>
    public static string Kind(RpuType type) => type switch
    {
        RpuType.Land => "Land",
        RpuType.Building => "Building",
        RpuType.Machinery => "Machinery",
        _ => "Others",
    };
}

/// <param name="sex">Null unless <see cref="FormsOptions.PrintOwnerSex"/> is on.</param>
internal sealed record LamParty(string name, string role, string roleLabel, bool isOwner, decimal sharePercent, string? address, string? tin,
    string? contactNumber, string? email, string? sex);
