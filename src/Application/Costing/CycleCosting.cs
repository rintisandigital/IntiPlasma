using Application.Abstractions.Data;
using Application.Sales;
using Domain.Costing.PlasmaSettlements;
using Domain.Inventory.Stock;
using Domain.MasterData.Items;
using Domain.Partnership.Cycles;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Costing;

/// <summary>
/// Cost (HPP) of a production cycle, from the coop warehouse's stock card: the sapronak consumed by the cycle at
/// moving average cost (DOC placed at chick-in, feed and OVK used in daily recordings, net of revisions).
/// Sapronak still lying in the coop is not part of the cost yet (it is returned or used later).
/// </summary>
internal static class CycleCosting
{
    private const int CostPerKgDecimals = 2;

    private static readonly StockMovementType[] ConsumptionTypes =
        [StockMovementType.ChickIn, StockMovementType.Usage, StockMovementType.UsageReversal];

    /// <summary>
    /// Sapronak consumed by the cycle per item, in base units, with its cost.
    /// </summary>
    public static async Task<List<ConsumedInput>> ConsumedInputsAsync(
        IApplicationDbContext context,
        Guid cycleId,
        CancellationToken cancellationToken)
    {
        var entries = await context.StockLedgerEntries.AsNoTracking()
            .Where(e => e.CycleId == cycleId && ConsumptionTypes.Contains(e.Type))
            .Select(e => new { e.ItemId, e.Quantity, Value = e.Value.Amount })
            .ToListAsync(cancellationToken);

        var itemIds = entries.Select(e => e.ItemId).Distinct().ToList();
        var items = await context.Items.AsNoTracking()
            .Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.Code, i.Category })
            .ToListAsync(cancellationToken);

        // Stock leaving the coop is negative on the stock card; consumption is its opposite.
        return
        [
            .. entries
                .GroupBy(e => e.ItemId)
                .Select(g =>
                {
                    var item = items.Single(i => i.Id == g.Key);
                    return new ConsumedInput(item.Id, item.Code, item.Category, -g.Sum(e => e.Quantity), new Money(-g.Sum(e => e.Value)));
                })
                .OrderBy(i => i.Category)
                .ThenBy(i => i.ItemCode)
        ];
    }

    /// <summary>
    /// Running cost per kg of live weight: consumed cost / (harvested kg + current population × latest body weight).
    /// Used as the estimated HPP when a sales invoice is posted.
    /// </summary>
    public static async Task<decimal> CostPerKgAsync(
        IApplicationDbContext context,
        ProductionCycle cycle,
        CancellationToken cancellationToken)
    {
        List<ConsumedInput> inputs = await ConsumedInputsAsync(context, cycle.Id, cancellationToken);
        decimal cost = inputs.Sum(i => i.Cost.Amount);

        decimal liveKg = await EstimatedLiveWeightKgAsync(context, cycle, cancellationToken);

        return liveKg > 0 ? decimal.Round(cost / liveKg, CostPerKgDecimals, MidpointRounding.AwayFromZero) : 0m;
    }

    /// <summary>
    /// Harvested kg plus the birds still in the coop at their latest weighed body weight (or, if never weighed,
    /// the average harvest weight).
    /// </summary>
    public static async Task<decimal> EstimatedLiveWeightKgAsync(
        IApplicationDbContext context,
        ProductionCycle cycle,
        CancellationToken cancellationToken)
    {
        int population = Math.Max(cycle.CurrentPopulation, 0);
        if (population == 0)
        {
            return cycle.HarvestedWeightKg;
        }

        decimal? latestBodyWeightGram = await context.DailyRecordings.AsNoTracking()
            .Where(r => r.CycleId == cycle.Id && r.AverageBodyWeightGram != null)
            .OrderByDescending(r => r.Date)
            .Select(r => r.AverageBodyWeightGram)
            .FirstOrDefaultAsync(cancellationToken);

        decimal? weightPerBird = latestBodyWeightGram / 1000m
            ?? (cycle.HarvestedBirds > 0 ? cycle.HarvestedWeightKg / cycle.HarvestedBirds : null);

        return cycle.HarvestedWeightKg + population * (weightPerBird ?? 0m);
    }

    /// <summary>
    /// Estimated cost of goods sold already journaled on posted sales invoices of the cycle.
    /// </summary>
    public static async Task<decimal> RecognizedCostAsync(
        IApplicationDbContext context,
        Guid cycleId,
        CancellationToken cancellationToken)
    {
        List<decimal> costs = await context.SalesInvoices.AsNoTracking()
            .Where(i => SalesSupport.PostedInvoiceStatuses.Contains(i.Status))
            .SelectMany(i => i.Lines)
            .Where(l => l.CycleId == cycleId)
            .Select(l => l.CostAmount.Amount)
            .ToListAsync(cancellationToken);

        return costs.Sum();
    }

    /// <summary>
    /// Final cost of a cycle being closed (all leftovers returned), with the difference to the recognized estimate.
    /// </summary>
    public static async Task<CycleCostSummary> SummarizeAsync(
        IApplicationDbContext context,
        ProductionCycle cycle,
        CancellationToken cancellationToken)
    {
        List<ConsumedInput> inputs = await ConsumedInputsAsync(context, cycle.Id, cancellationToken);
        decimal recognized = await RecognizedCostAsync(context, cycle.Id, cancellationToken);

        decimal CostOf(ItemCategory category) => inputs.Where(i => i.Category == category).Sum(i => i.Cost.Amount);

        return CycleCostSummary.Calculate(
            CostOf(ItemCategory.Doc),
            CostOf(ItemCategory.Feed),
            CostOf(ItemCategory.Ovk),
            cycle.HarvestedBirds,
            cycle.HarvestedWeightKg,
            recognized);
    }
}
