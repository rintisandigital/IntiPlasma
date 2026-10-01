using System.Globalization;
using Domain.MasterData.Items;
using Domain.Partnership.Contracts;
using Domain.Partnership.Cycles;
using SharedKernel;

namespace Domain.Costing.PlasmaSettlements;

/// <summary>
/// Everything a settlement policy needs about a closed plasma cycle.
/// </summary>
/// <param name="Inputs">Sapronak consumed in the coop (DOC placed, feed and OVK used), per item, in base units.</param>
/// <param name="NetSales">Sales of the cycle's birds excluding VAT, minus credit notes (for profit sharing).</param>
/// <param name="CycleCost">The cycle's final cost at moving average (for profit sharing).</param>
public sealed record SettlementInput(
    ContractSnapshot Contract,
    CyclePerformance Performance,
    IReadOnlyList<HarvestFigures> Harvests,
    IReadOnlyList<ConsumedInput> Inputs,
    Money NetSales,
    Money CycleCost);

public sealed record HarvestFigures(DateOnly Date, int Birds, decimal WeightKg);

public sealed record ConsumedInput(Guid ItemId, string ItemCode, ItemCategory Category, decimal Quantity, Money Cost);

/// <param name="Amount">Effect on the plasma's income: positive adds (ayam, bonus), negative deducts (sapronak, denda).</param>
public sealed record SettlementLineInput(
    SettlementLineType Type,
    string Description,
    decimal? Quantity,
    Money? UnitPrice,
    Money Amount);

public enum SettlementLineType
{
    /// <summary>
    /// Ayam hidup at the guaranteed price (harga kontrak).
    /// </summary>
    LiveBirdValue = 1,

    /// <summary>
    /// Sapronak charged at the contract price (harga kontrak).
    /// </summary>
    InputCharge = 2,

    /// <summary>
    /// Plasma's share of the cycle profit (bagi hasil).
    /// </summary>
    ProfitShare = 3,

    /// <summary>
    /// Contract bonus / insentif.
    /// </summary>
    Bonus = 4,

    /// <summary>
    /// Contract potongan / denda.
    /// </summary>
    Penalty = 5
}

/// <summary>
/// How the plasma's income is calculated for a contract scheme.
/// </summary>
public interface ISettlementPolicy
{
    Result<IReadOnlyList<SettlementLineInput>> Calculate(SettlementInput input);
}

public static class SettlementPolicies
{
    public static ISettlementPolicy For(ContractScheme scheme) => scheme switch
    {
        ContractScheme.PriceContract => new PriceContractSettlementPolicy(),
        ContractScheme.ProfitSharing => new ProfitSharingSettlementPolicy(),
        _ => throw new ArgumentOutOfRangeException(nameof(scheme), scheme, null)
    };

    /// <summary>
    /// Contract bonus and penalty rules whose metric of the closed cycle falls within [RangeFrom, RangeTo].
    /// </summary>
    internal static IEnumerable<SettlementLineInput> Incentives(SettlementInput input)
    {
        CyclePerformance performance = input.Performance;

        foreach (ContractSnapshot.Incentive incentive in input.Contract.Incentives)
        {
            decimal? value = incentive.Metric switch
            {
                IncentiveMetric.Fcr => performance.Fcr,
                IncentiveMetric.Ip => performance.Ip,
                IncentiveMetric.Depletion => performance.DepletionPercent,
                IncentiveMetric.AverageWeight => performance.AverageWeightKg,
                _ => 0m
            };

            bool applies = value is not null &&
                           (incentive.RangeFrom is null || value >= incentive.RangeFrom) &&
                           (incentive.RangeTo is null || value <= incentive.RangeTo);

            if (!applies)
            {
                continue;
            }

            decimal? quantity = incentive.Basis switch
            {
                IncentiveBasis.PerKg => performance.HarvestedWeightKg,
                IncentiveBasis.PerBird => performance.HarvestedBirds,
                _ => null
            };

            Money amount = incentive.Amount * (quantity ?? 1m);
            bool bonus = incentive.Kind == IncentiveKind.Bonus;

            yield return new SettlementLineInput(
                bonus ? SettlementLineType.Bonus : SettlementLineType.Penalty,
                incentive.Metric == IncentiveMetric.None
                    ? incentive.Name
                    : string.Create(CultureInfo.InvariantCulture, $"{incentive.Name} ({incentive.Metric} {value:0.###})"),
                quantity,
                incentive.Amount,
                bonus ? amount : -amount);
        }
    }
}

/// <summary>
/// Sistem harga kontrak: every harvest (truck) at the guaranteed price of its average body weight range, minus the
/// sapronak consumed at the contract prices, plus bonuses and minus penalties of the contract.
/// </summary>
public sealed class PriceContractSettlementPolicy : ISettlementPolicy
{
    public Result<IReadOnlyList<SettlementLineInput>> Calculate(SettlementInput input)
    {
        var lines = new List<SettlementLineInput>();

        foreach (HarvestFigures harvest in input.Harvests.OrderBy(h => h.Date))
        {
            decimal averageWeight = decimal.Round(harvest.WeightKg / harvest.Birds, 3, MidpointRounding.AwayFromZero);

            ContractSnapshot.LiveBirdPrice? price = input.Contract.LiveBirdPrices
                .FirstOrDefault(p => averageWeight >= p.MinWeightKg && averageWeight < p.MaxWeightKg);

            if (price is null)
            {
                return Result.Failure<IReadOnlyList<SettlementLineInput>>(PlasmaSettlementErrors.NoLiveBirdPrice(averageWeight));
            }

            lines.Add(new SettlementLineInput(
                SettlementLineType.LiveBirdValue,
                string.Create(CultureInfo.InvariantCulture, $"Panen {harvest.Date:dd/MM/yyyy}: {harvest.Birds} ekor, BW {averageWeight:0.000} kg"),
                harvest.WeightKg,
                price.PricePerKg,
                price.PricePerKg * harvest.WeightKg));
        }

        foreach (ConsumedInput consumed in input.Inputs.Where(i => i.Quantity != 0).OrderBy(i => i.Category).ThenBy(i => i.ItemCode))
        {
            ContractSnapshot.InputPrice? price = input.Contract.InputPrices.FirstOrDefault(p => p.ItemId == consumed.ItemId);
            if (price is null)
            {
                return Result.Failure<IReadOnlyList<SettlementLineInput>>(PlasmaSettlementErrors.NoInputPrice(consumed.ItemCode));
            }

            lines.Add(new SettlementLineInput(
                SettlementLineType.InputCharge,
                $"{consumed.Category} {consumed.ItemCode}",
                consumed.Quantity,
                price.Price,
                -(price.Price * consumed.Quantity)));
        }

        lines.AddRange(SettlementPolicies.Incentives(input));

        return lines;
    }
}

/// <summary>
/// Sistem bagi hasil: the plasma's percentage of the cycle profit (net sales − cycle cost); a loss is borne by the
/// company (share = 0). Contract bonuses and penalties apply on top.
/// </summary>
public sealed class ProfitSharingSettlementPolicy : ISettlementPolicy
{
    public Result<IReadOnlyList<SettlementLineInput>> Calculate(SettlementInput input)
    {
        decimal percent = input.Contract.PlasmaProfitSharePercent ?? 0m;
        Money profit = input.NetSales - input.CycleCost;
        Money share = profit.IsNegative ? Money.Zero : profit * (percent / 100m);

        List<SettlementLineInput> lines =
        [
            new(
                SettlementLineType.ProfitShare,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Bagi hasil {percent:0.##}% × laba {profit} (penjualan {input.NetSales} − biaya {input.CycleCost})"),
                percent,
                profit,
                share),
            .. SettlementPolicies.Incentives(input)
        ];

        return lines;
    }
}
