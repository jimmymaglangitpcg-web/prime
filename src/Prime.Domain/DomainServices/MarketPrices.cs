namespace Prime.Domain.DomainServices;

/// <summary>
/// Unit prices of a market transaction (LAM 2025 Annex IV-B, IV-F: the sales value divided by the area;
/// docs/analysis/smv-preparation-general-revision.md §4.1). A land price is told apart only when the land was conveyed
/// alone or the part of the consideration for it is stated; a building price likewise. Rounded to centavos, half away
/// from zero. Nothing is estimated.
/// </summary>
public static class MarketPrices
{
    public static (decimal? LandUnitPrice, decimal? BuildingUnitPrice) Compute(
        bool conveysLand, bool conveysBuilding, decimal consideration, decimal? landConsideration, decimal? landArea, decimal? buildingFloorArea)
    {
        decimal? land = null, building = null;
        if (conveysLand && landArea > 0)
        {
            var basis = conveysBuilding ? landConsideration : consideration;
            if (basis is { } b)
            {
                land = Round(b / landArea.Value);
            }
        }
        if (conveysBuilding && buildingFloorArea > 0)
        {
            var basis = !conveysLand ? consideration : landConsideration is { } l ? consideration - l : (decimal?)null;
            if (basis is { } b)
            {
                building = Round(b / buildingFloorArea.Value);
            }
        }
        return (land, building);
    }

    /// <summary>
    /// Lowest, median and highest of a set of unit prices (LAM 2025 Book I p.25, Annex I-R). The median of an even
    /// count is the mean of the two middle values, rounded to centavos. Null for an empty set.
    /// </summary>
    public static (decimal Lowest, decimal Median, decimal Highest)? Spread(IEnumerable<decimal> unitPrices)
    {
        var sorted = unitPrices.Order().ToList();
        if (sorted.Count == 0)
        {
            return null;
        }
        var mid = sorted.Count / 2;
        var median = sorted.Count % 2 == 1 ? sorted[mid] : Round((sorted[mid - 1] + sorted[mid]) / 2);
        return (sorted[0], median, sorted[^1]);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
