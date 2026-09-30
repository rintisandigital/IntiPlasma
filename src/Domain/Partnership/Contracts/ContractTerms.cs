using SharedKernel;

namespace Domain.Partnership.Contracts;

/// <summary>
/// The editable terms of a partnership contract, validated as a whole by <see cref="PartnershipContract"/>.
/// </summary>
public sealed record ContractTerms(
    string Name,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    decimal? PlasmaProfitSharePercent,
    Guid? IncomeTaxCodeId,
    string? Notes,
    IReadOnlyList<ContractTerms.InputPrice> InputPrices,
    IReadOnlyList<ContractTerms.LiveBirdPrice> LiveBirdPrices,
    IReadOnlyList<ContractTerms.Incentive> Incentives)
{
    /// <summary>
    /// Contract price of a sapronak item per base unit (e.g. per ekor DOC, per kg feed).
    /// </summary>
    public sealed record InputPrice(Guid ItemId, Money Price);

    /// <summary>
    /// Guaranteed buy-back price per kg for an average body weight range [Min, Max).
    /// </summary>
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
