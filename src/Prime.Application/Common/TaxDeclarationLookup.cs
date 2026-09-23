using Microsoft.EntityFrameworkCore;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities;

namespace Prime.Application.Common;

/// <summary>
/// "The current Tax Declaration for an RPU" — needed wherever a Building or
/// Machinery must be classified for a lookup keyed by Classification/ActualUse
/// (SMV schedules, assessment levels), since neither entity carries those
/// fields itself (unlike Land, which does). Extracted here because both
/// <c>Prime.Application.Features.Valuation.ValuationService</c> and
/// <c>Prime.Application.Features.Assessments.AssessmentService</c> need the
/// identical query — a genuine shared concern, not premature abstraction.
/// </summary>
public static class TaxDeclarationLookup
{
    public static Task<TaxDeclaration?> GetCurrentAsync(IApplicationDbContext db, Guid rpuId, CancellationToken cancellationToken = default) =>
        db.TaxDeclarations
            .Where(x => x.RpuId == rpuId)
            .OrderByDescending(x => x.RevisionNumber)
            .FirstOrDefaultAsync(cancellationToken);
}
