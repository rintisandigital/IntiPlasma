namespace Domain.MasterData.BreedStandards;

/// <summary>
/// One age of a breed standard (PLAN-MOBILE M-69): the target figures of the strain at the end of that day of age.
/// </summary>
/// <param name="BodyWeightGram">Body weight per bird (g).</param>
/// <param name="Fcr">Cumulative feed conversion ratio.</param>
/// <param name="DepletionPercent">Cumulative mortality + culling (% of the birds placed).</param>
/// <param name="DailyFeedGram">Feed intake of the day (g per bird).</param>
/// <param name="CumulativeFeedGram">Feed intake since placement (g per bird).</param>
public sealed record StandardPoint(
    int AgeDays,
    decimal BodyWeightGram,
    decimal? Fcr,
    decimal? DepletionPercent,
    decimal? DailyFeedGram,
    decimal? CumulativeFeedGram);

/// <summary>
/// The standard figures at one age, interpolated between the rows of the standard (M-70); a figure is null when the
/// standard does not cover the age for it.
/// </summary>
public sealed record StandardValue(
    int AgeDays,
    decimal? BodyWeightGram,
    decimal? Fcr,
    decimal? DepletionPercent,
    decimal? CumulativeFeedGram);
