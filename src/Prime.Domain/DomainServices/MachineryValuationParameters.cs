namespace Prime.Domain.DomainServices;

/// <summary>
/// Configured inputs to machinery valuation that come from law rather than
/// from the machine itself. Supplied by the caller (from configuration) so
/// the calculator holds no legal constant of its own.
/// </summary>
/// <param name="MinimumRemainingValuePercent">
/// LGC §225 proviso: the remaining value is "not less than twenty percent
/// (20%) of such original, replacement, or reproduction cost for so long as
/// the machinery is useful and in operation". Percent, 0–100.
/// </param>
public sealed record MachineryValuationParameters(decimal MinimumRemainingValuePercent);
