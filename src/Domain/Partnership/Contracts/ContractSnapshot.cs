using SharedKernel;

namespace Domain.Partnership.Contracts;

/// <summary>
/// Immutable copy of the contract terms stored on a production cycle, so the settlement is always
/// calculated with the terms that applied when the cycle started.
/// </summary>
public sealed record ContractSnapshot(
    Guid ContractId,
    string ContractCode,
    ContractScheme Scheme,
    decimal? PlasmaProfitSharePercent,
    Guid? IncomeTaxCodeId,
    IReadOnlyList<ContractSnapshot.InputPrice> InputPrices,
    IReadOnlyList<ContractSnapshot.LiveBirdPrice> LiveBirdPrices,
    IReadOnlyList<ContractSnapshot.Incentive> Incentives)
{
    public sealed record InputPrice(Guid ItemId, Money Price);

    public sealed record LiveBirdPrice(decimal MinWeightKg, decimal MaxWeightKg, Money PricePerKg);

    public sealed record Incentive(
        string Name,
        IncentiveKind Kind,
        IncentiveMetric Metric,
        decimal? RangeFrom,
        decimal? RangeTo,
        Money Amount,
        IncentiveBasis Basis);
}
