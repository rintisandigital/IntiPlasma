namespace Domain.MasterData.BreedStandards;

/// <summary>
/// Limits beyond which a cycle is below standard (M-76), configured in <c>Production:Standard</c>.
/// </summary>
/// <param name="BodyWeightBelowPercent">Body weight more than this many percent under the standard.</param>
/// <param name="FcrAbove">FCR more than this above the standard.</param>
/// <param name="DepletionAbovePoints">Depletion more than this many percentage points above the standard.</param>
public sealed record StandardThresholds(decimal BodyWeightBelowPercent, decimal FcrAbove, decimal DepletionAbovePoints)
{
    public static readonly StandardThresholds Default = new(5m, 0.05m, 1m);
}

/// <summary>
/// Actual figures against the standard (M-75):
/// <list type="bullet">
/// <item>body weight and cumulative feed per bird: (actual − standard) / standard × 100</item>
/// <item>FCR: actual − standard</item>
/// <item>depletion: actual − standard, in percentage points</item>
/// </list>
/// A figure is null when the actual or the standard value is missing.
/// </summary>
public sealed record StandardDeviation(
    decimal? BodyWeightPercent,
    decimal? Fcr,
    decimal? DepletionPoints,
    decimal? CumulativeFeedPercent)
{
    private const int Decimals = 3;

    public static StandardDeviation Calculate(
        StandardValue standard,
        decimal? bodyWeightGram,
        decimal? fcr,
        decimal? depletionPercent,
        decimal? cumulativeFeedGram) =>
        new(
            Percent(bodyWeightGram, standard.BodyWeightGram),
            Difference(fcr, standard.Fcr),
            Difference(depletionPercent, standard.DepletionPercent),
            Percent(cumulativeFeedGram, standard.CumulativeFeedGram));

    public bool IsBodyWeightBelow(StandardThresholds thresholds) => BodyWeightPercent < -thresholds.BodyWeightBelowPercent;

    public bool IsFcrAbove(StandardThresholds thresholds) => Fcr > thresholds.FcrAbove;

    public bool IsDepletionAbove(StandardThresholds thresholds) => DepletionPoints > thresholds.DepletionAbovePoints;

    /// <summary>
    /// Any of body weight, FCR or depletion is beyond its threshold; feed intake alone does not count.
    /// </summary>
    public bool IsBelowStandard(StandardThresholds thresholds) =>
        IsBodyWeightBelow(thresholds) || IsFcrAbove(thresholds) || IsDepletionAbove(thresholds);

    private static decimal? Percent(decimal? actual, decimal? standard) =>
        actual is null || standard is not > 0
            ? null
            : decimal.Round((actual.Value - standard.Value) * 100m / standard.Value, Decimals, MidpointRounding.AwayFromZero);

    private static decimal? Difference(decimal? actual, decimal? standard) =>
        actual is null || standard is null
            ? null
            : decimal.Round(actual.Value - standard.Value, Decimals, MidpointRounding.AwayFromZero);
}
