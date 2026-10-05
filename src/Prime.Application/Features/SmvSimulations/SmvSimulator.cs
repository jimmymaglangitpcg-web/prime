using Prime.Application.Features.Assessments;
using Prime.Application.Features.Valuation;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.SmvSimulations;

/// <summary>One unit simulated: its computed valuation and assessment rows, or why it could not be valued or assessed.</summary>
public sealed record UnitSimulation(ValuationDto? Valuation, IReadOnlyList<AssessmentLine>? Lines, string? Failure)
{
    public decimal? MarketValue => Valuation?.ComputedMarketValue;
    public decimal? AssessedValue => Lines?.Sum(l => l.AssessedValue);
    public decimal? TaxableAssessedValue => Lines?.Where(l => l.Taxability == Taxability.Taxable).Sum(l => l.AssessedValue);
    /// <summary>The classification of the largest assessment row (the first of equals).</summary>
    public Guid? PrincipalClassificationId => Lines?.OrderByDescending(l => l.MarketValue).ThenBy(l => l.Sequence).FirstOrDefault()?.ClassificationId;
}

/// <summary>
/// Values one unit under a proposed SMV without storing anything, then assesses the computed rows with the assessment
/// service's one calculation (docs/analysis/smv-preparation-general-revision.md §4.3, Q8). Valued as for a general
/// revision: a building takes a new depreciation.
/// </summary>
public sealed class SmvSimulator(IValuationService valuation, IAssessmentService assessments)
{
    public async Task<UnitSimulation> SimulateAsync(Guid rpuId, Guid smvId, DateOnly asOf, CancellationToken ct)
    {
        var valued = await valuation.ComputeForRpuAsync(rpuId, ct, asOf, generalRevision: true, mode: ValuationMode.Proposed(smvId));
        if (valued.IsFailure)
        {
            return new UnitSimulation(null, null, valued.Message ?? valued.Code ?? "The unit could not be valued.");
        }
        var lines = (valued.Value.Lines ?? []).Select(l => new AssessableLine(l.ClassificationId, l.ActualUseId, l.MarketValue)).ToList();
        var assessed = await assessments.AssessLinesAsync(rpuId, valued.Value.SourceType, lines, asOf, ct);
        return assessed.IsFailure
            ? new UnitSimulation(valued.Value, null, assessed.Message ?? assessed.Code ?? "The unit could not be assessed.")
            : new UnitSimulation(valued.Value, assessed.Value, null);
    }
}
