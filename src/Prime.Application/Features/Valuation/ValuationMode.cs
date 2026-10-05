namespace Prime.Application.Features.Valuation;

/// <summary>
/// How the one valuation engine runs (docs/analysis/smv-preparation-general-revision.md §4.3, Q8).
/// By default it prices from the approved SMV in force and stores the valuation. With
/// <see cref="ProposedSmvId"/> it prices from that SMV's rows and tables whatever their status, and
/// with <see cref="ComputeOnly"/> it returns the lines and totals without storing anything or
/// changing the valued records. A valuation under a proposed SMV is always compute-only: it can
/// never be assessed or posted.
/// </summary>
public sealed record ValuationMode(Guid? ProposedSmvId = null, bool ComputeOnly = false)
{
    public static readonly ValuationMode Stored = new();

    /// <summary>Values under <paramref name="smvId"/> without storing (a simulation, valuation testing).</summary>
    public static ValuationMode Proposed(Guid smvId) => new(smvId, true);
}

/// <summary>An SMV row's unit value, as the engine selects it.</summary>
public sealed record SmvRateDto(Guid SmvScheduleId, Guid SmvId, string Unit, decimal UnitValue);
