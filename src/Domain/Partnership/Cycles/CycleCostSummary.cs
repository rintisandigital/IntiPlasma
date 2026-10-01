namespace Domain.Partnership.Cycles;

/// <summary>
/// Final cost of a closed cycle (HPP), frozen at closing: the sapronak consumed in the coop at moving average cost
/// (DOC placed, feed and OVK used). Equals the cycle's ayam dalam proses, because leftovers must be returned before
/// closing. <see cref="Adjustment"/> is the difference with the estimated cost already recognized on the sales invoices,
/// journaled at closing so the cycle's ayam dalam proses ends at zero.
/// </summary>
public sealed record CycleCostSummary(
    decimal DocCost,
    decimal FeedCost,
    decimal OvkCost,
    decimal TotalCost,
    int HarvestedBirds,
    decimal HarvestedWeightKg,
    decimal? CostPerKg,
    decimal? CostPerBird,
    decimal RecognizedCost,
    decimal Adjustment)
{
    private const int UnitCostDecimals = 2;

    public static CycleCostSummary Calculate(
        decimal docCost,
        decimal feedCost,
        decimal ovkCost,
        int harvestedBirds,
        decimal harvestedWeightKg,
        decimal recognizedCost)
    {
        decimal total = docCost + feedCost + ovkCost;

        return new CycleCostSummary(
            docCost,
            feedCost,
            ovkCost,
            total,
            harvestedBirds,
            harvestedWeightKg,
            harvestedWeightKg > 0 ? decimal.Round(total / harvestedWeightKg, UnitCostDecimals, MidpointRounding.AwayFromZero) : null,
            harvestedBirds > 0 ? decimal.Round(total / harvestedBirds, UnitCostDecimals, MidpointRounding.AwayFromZero) : null,
            recognizedCost,
            total - recognizedCost);
    }
}
