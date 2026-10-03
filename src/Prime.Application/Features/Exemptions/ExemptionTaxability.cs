using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Exemptions;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Exemptions;

/// <summary>
/// Taxability per assessment line (docs/analysis/assessment-listing-exemptions.md §4.1, step L3-1b): the single rule
/// that marks a line taxable or exempt, used when an assessment is calculated, when it is made (finally approved), and
/// when an exemption decided later reassesses the unit.
/// </summary>
public static class ExemptionTaxability
{
    /// <summary>
    /// An approved exemption is in force on <paramref name="on"/> from its effective date to its expiry (inclusive),
    /// and until the day it ended (exclusive).
    /// </summary>
    public static IQueryable<PropertyExemption> InForce(this IQueryable<PropertyExemption> query, DateOnly on) =>
        query.Where(x => (x.Status == ExemptionStatus.Approved || x.Status == ExemptionStatus.Ended)
            && x.EffectiveDate <= on && (x.ExpiryDate == null || x.ExpiryDate >= on) && (x.EndedOn == null || x.EndedOn > on));

    /// <summary>
    /// Marks each line of a unit's assessment effective <paramref name="on"/>:
    /// <list type="bullet">
    /// <item>exempt under the exemption in force for its actual use (a part's own exemption before one of the whole
    /// unit), unless the line's assessed value is over the exemption type's ceiling (Q14): then taxable, with the
    /// reason;</item>
    /// <item>taxable otherwise: an unproven or undecided claim does not exempt (LGC §206);</item>
    /// <item>a unit with no exemption records at all whose current TD is declared exempt keeps that, with a note to
    /// record the exemption: declarations made before PRIME kept exemption records are not silently made taxable
    /// (Q15).</item>
    /// </list>
    /// Returns whether any line's taxability changed.
    /// </summary>
    public static async Task<bool> MarkAsync(IApplicationDbContext db, Guid rpuId, IReadOnlyList<AssessmentLine> lines, DateOnly on, CancellationToken ct)
    {
        var exemptions = await db.PropertyExemptions.AsNoTracking().Include(x => x.ExemptionType)
            .Where(x => x.RpuId == rpuId).InForce(on)
            .OrderBy(x => x.EffectiveDate).ThenBy(x => x.DecidedAt).ToListAsync(ct);
        string? declaredExempt = null;
        if (exemptions.Count == 0 && !await db.PropertyExemptions.AnyAsync(x => x.RpuId == rpuId, ct))
        {
            declaredExempt = await db.TaxDeclarations
                .Where(x => x.RpuId == rpuId && x.Status == WorkflowStatus.Approved && x.Taxability == Taxability.Exempt)
                .Select(x => x.TaxDeclarationNumber).FirstOrDefaultAsync(ct);
        }

        var changed = false;
        foreach (var line in lines)
        {
            var (taxability, exemptionId, note) = (Taxability.Taxable, (Guid?)null, (string?)null);
            var exemption = exemptions.FirstOrDefault(x => x.ActualUseId == line.ActualUseId) ?? exemptions.FirstOrDefault(x => x.ActualUseId == null);
            if (exemption is not null)
            {
                exemptionId = exemption.Id;
                if (await CeilingAsync(db, exemption.ExemptionType!, on, ct) is { } ceiling && line.AssessedValue > ceiling)
                {
                    note = $"Taxable: the assessed value {line.AssessedValue:#,0.00} is over the {ceiling:#,0.00} ceiling of exemption {exemption.ExemptionType!.Code}.";
                }
                else
                {
                    taxability = Taxability.Exempt;
                }
            }
            else if (declaredExempt is not null)
            {
                taxability = Taxability.Exempt;
                note = $"Exempt as declared on TD {declaredExempt}, before exemption records were kept; record the exemption.";
            }
            changed |= line.Taxability != taxability;
            (line.Taxability, line.PropertyExemptionId, line.TaxabilityNote) = (taxability, exemptionId, note);
        }
        return changed;
    }

    /// <summary>A TD's taxability from its lines (Q2); null for no lines.</summary>
    public static Taxability? Summary(IEnumerable<Taxability> lines)
    {
        var set = lines.ToHashSet();
        return set.Count == 0 ? null
            : set.Contains(Taxability.Exempt) ? (set.Contains(Taxability.Taxable) ? Taxability.PartlyExempt : Taxability.Exempt)
            : Taxability.Taxable;
    }

    /// <summary>The taxability of a TD declaring <paramref name="assessmentId"/>, from that assessment's lines; null if it has none.</summary>
    public static async Task<Taxability?> OfAssessmentAsync(IApplicationDbContext db, Guid assessmentId, CancellationToken ct) =>
        Summary(await db.AssessmentLines.Where(x => x.AssessmentId == assessmentId).Select(x => x.Taxability).ToListAsync(ct));

    /// <summary>The ceiling of the exemption type's version in force on the date, else of the version the claim was made under.</summary>
    private static async Task<decimal?> CeilingAsync(IApplicationDbContext db, ExemptionType type, DateOnly on, CancellationToken ct) =>
        await db.ExemptionTypes.AsNoTracking().InForce(on).Where(x => x.Code == type.Code).FirstOrDefaultAsync(ct) is { } current
            ? current.AssessedValueCeiling
            : type.AssessedValueCeiling;
}
