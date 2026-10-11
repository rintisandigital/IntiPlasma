using Application.BreedStandards;
using Domain.MasterData.BreedStandards;

namespace Application.Production;

/// <summary>
/// The breed standard of a cycle in the performance response (PLAN-MOBILE M-74): the full curve for the chart line
/// and the thresholds, so the mobile app can compare offline recordings the same way.
/// </summary>
public sealed record PerformanceStandardResponse(
    Guid Id,
    string Code,
    string Name,
    string? Strain,
    bool IsDefault,
    bool IsFrozen,
    StandardThresholds Thresholds,
    IReadOnlyList<StandardPoint> Rows);

/// <summary>
/// The latest figures against the standard (M-74 s.d. M-76). Body weight and FCR come from the last weighing — a
/// body weight carried forward to later days would show a false shortfall against the growing standard — while
/// depletion and feed come from the last recording.
/// </summary>
public sealed record StandardStatus(
    int? WeighingAgeDays,
    decimal? BodyWeightGram,
    decimal? StandardBodyWeightGram,
    decimal? BodyWeightPercent,
    decimal? Fcr,
    decimal? StandardFcr,
    decimal? FcrDeviation,
    int AgeDays,
    decimal DepletionPercent,
    decimal? StandardDepletionPercent,
    decimal? DepletionPoints,
    decimal? CumulativeFeedGram,
    decimal? StandardCumulativeFeedGram,
    decimal? CumulativeFeedPercent,
    bool BodyWeightBelow,
    bool FcrAbove,
    bool DepletionAbove)
{
    public bool BelowStandard => BodyWeightBelow || FcrAbove || DepletionAbove;
}

/// <summary>
/// Compares the daily performance with a breed standard, shared by <c>cycles/{id}/performance</c> and the mobile
/// dashboard so both judge a cycle the same way.
/// </summary>
internal static class StandardComparer
{
    /// <summary>
    /// Adds the standard and the deviation to each day. Body weight and FCR deviate only on days with a weighing;
    /// cumulative feed per bird is feed kg × 1000 / birds placed, as in the mobile chart (M-75).
    /// </summary>
    public static List<DailyPerformance> Annotate(
        IEnumerable<DailyPerformance> days,
        BreedStandardSnapshot? standard,
        int initialPopulation) =>
        [
            .. days.Select(day =>
            {
                StandardValue? value = standard?.ValueAt(day.AgeDays);
                if (value is null)
                {
                    return day;
                }

                bool weighed = day.AverageBodyWeightGram is not null;

                var deviation = StandardDeviation.Calculate(
                    value,
                    weighed ? day.AverageBodyWeightGram : null,
                    weighed ? day.Cumulative.Fcr : null,
                    day.Cumulative.DepletionPercent,
                    FeedGramPerBird(day.Cumulative.FeedKg, initialPopulation));

                return day with { Standard = value, Deviation = deviation };
            })
        ];

    public static StandardStatus? Latest(
        IReadOnlyList<DailyPerformance> days,
        BreedStandardSnapshot? standard,
        int initialPopulation,
        StandardThresholds thresholds)
    {
        if (standard is null || days.Count == 0)
        {
            return null;
        }

        DailyPerformance last = days[^1];
        DailyPerformance? weighing = days.LastOrDefault(d => d.AverageBodyWeightGram is not null);

        StandardValue? atWeighing = weighing is null ? null : standard.ValueAt(weighing.AgeDays);
        StandardValue? atLast = standard.ValueAt(last.AgeDays);

        decimal? feedGram = FeedGramPerBird(last.Cumulative.FeedKg, initialPopulation);

        StandardDeviation growth = atWeighing is null
            ? new StandardDeviation(null, null, null, null)
            : StandardDeviation.Calculate(atWeighing, weighing!.AverageBodyWeightGram, weighing.Cumulative.Fcr, null, null);

        StandardDeviation flow = atLast is null
            ? new StandardDeviation(null, null, null, null)
            : StandardDeviation.Calculate(atLast, null, null, last.Cumulative.DepletionPercent, feedGram);

        return new StandardStatus(
            weighing?.AgeDays,
            weighing?.AverageBodyWeightGram,
            atWeighing?.BodyWeightGram,
            growth.BodyWeightPercent,
            weighing?.Cumulative.Fcr,
            atWeighing?.Fcr,
            growth.Fcr,
            last.AgeDays,
            last.Cumulative.DepletionPercent,
            atLast?.DepletionPercent,
            flow.DepletionPoints,
            feedGram,
            atLast?.CumulativeFeedGram,
            flow.CumulativeFeedPercent,
            growth.IsBodyWeightBelow(thresholds),
            growth.IsFcrAbove(thresholds),
            flow.IsDepletionAbove(thresholds));
    }

    public static PerformanceStandardResponse? ToResponse(EffectiveStandard? standard, StandardThresholds thresholds) =>
        standard is null
            ? null
            : new PerformanceStandardResponse(
                standard.Snapshot.Id,
                standard.Snapshot.Code,
                standard.Snapshot.Name,
                standard.Snapshot.Strain,
                standard.IsDefault,
                standard.IsFrozen,
                thresholds,
                standard.Snapshot.Rows);

    private static decimal? FeedGramPerBird(decimal feedKg, int initialPopulation) =>
        initialPopulation > 0 ? decimal.Round(feedKg * 1000m / initialPopulation, 1, MidpointRounding.AwayFromZero) : null;
}
