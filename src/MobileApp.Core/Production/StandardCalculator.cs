using MobileApp.Core.Contracts;

namespace MobileApp.Core.Production;

/// <summary>
/// The breed standard at one age, interpolated between the rows; a figure is null where the standard does not cover it.
/// </summary>
public sealed record StandardValue(
    int AgeDays,
    decimal? BodyWeightGram,
    decimal? Fcr,
    decimal? DepletionPercent,
    decimal? CumulativeFeedGram);

/// <summary>
/// Actual against the standard: body weight and feed in %, FCR as a difference, depletion in percentage points.
/// </summary>
public sealed record StandardDeviation(
    decimal? BodyWeightPercent,
    decimal? Fcr,
    decimal? DepletionPoints,
    decimal? CumulativeFeedPercent)
{
    public bool IsBodyWeightBelow(StandardThresholds thresholds) => BodyWeightPercent < -thresholds.BodyWeightBelowPercent;

    public bool IsFcrAbove(StandardThresholds thresholds) => Fcr > thresholds.FcrAbove;

    public bool IsDepletionAbove(StandardThresholds thresholds) => DepletionPoints > thresholds.DepletionAbovePoints;
}

/// <summary>
/// The breed standard comparison of the server (<c>Domain.MasterData.BreedStandards</c> and
/// <c>Application.Production.StandardComparer</c>), ported so queued recordings are judged on the device the same
/// way (PLAN-MOBILE M-74 s.d. M-76, M-78). Keep both in step: the unit tests use the numbers of the server tests.
/// </summary>
public static class StandardCalculator
{
    private const int Decimals = 3;

    /// <summary>
    /// Linear interpolation between the nearest rows that have the figure; nothing before the first or after the
    /// last row.
    /// </summary>
    public static StandardValue? ValueAt(PerformanceStandard standard, int ageDays)
    {
        IReadOnlyList<StandardRow> rows = standard.Rows;
        if (rows.Count == 0 || ageDays < rows[0].AgeDays || ageDays > rows[^1].AgeDays)
        {
            return null;
        }

        return new StandardValue(
            ageDays,
            Interpolate(rows, ageDays, r => r.BodyWeightGram),
            Interpolate(rows, ageDays, r => r.Fcr),
            Interpolate(rows, ageDays, r => r.DepletionPercent),
            Interpolate(rows, ageDays, r => r.CumulativeFeedGram));
    }

    public static StandardDeviation Deviation(
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

    /// <summary>
    /// The deviation of a chart day; body weight and FCR only on a weighing day (a carried-forward weight would show a
    /// false shortfall against the growing standard).
    /// </summary>
    public static StandardDeviation? DayDeviation(PerformanceStandard? standard, PerformancePoint day, int initialPopulation)
    {
        if (standard is null || ValueAt(standard, day.AgeDays) is not { } value)
        {
            return null;
        }

        bool weighed = day.AverageBodyWeightGram is not null;

        return Deviation(
            value,
            weighed ? day.AverageBodyWeightGram : null,
            weighed ? day.Cumulative.Fcr : null,
            day.Cumulative.DepletionPercent,
            FeedGramPerBird(day.Cumulative.FeedKg, initialPopulation));
    }

    /// <summary>
    /// The latest figures against the standard, queued recordings included.
    /// </summary>
    public static StandardStatus? Latest(IReadOnlyList<PerformancePoint> days, PerformanceStandard? standard, int initialPopulation)
    {
        if (standard is null || days.Count == 0)
        {
            return null;
        }

        StandardThresholds thresholds = standard.Thresholds;
        PerformancePoint last = days[^1];
        PerformancePoint? weighing = days.LastOrDefault(d => d.AverageBodyWeightGram is not null);

        StandardValue? atWeighing = weighing is null ? null : ValueAt(standard, weighing.AgeDays);
        StandardValue? atLast = ValueAt(standard, last.AgeDays);
        decimal? feedGram = FeedGramPerBird(last.Cumulative.FeedKg, initialPopulation);

        StandardDeviation growth = atWeighing is null
            ? new StandardDeviation(null, null, null, null)
            : Deviation(atWeighing, weighing!.AverageBodyWeightGram, weighing.Cumulative.Fcr, null, null);

        StandardDeviation flow = atLast is null
            ? new StandardDeviation(null, null, null, null)
            : Deviation(atLast, null, null, last.Cumulative.DepletionPercent, feedGram);

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

    /// <summary>
    /// Feed per bird placed (g), as in the charts.
    /// </summary>
    public static decimal? FeedGramPerBird(decimal feedKg, int initialPopulation) =>
        initialPopulation > 0 ? decimal.Round(feedKg * 1000m / initialPopulation, 1, MidpointRounding.AwayFromZero) : null;

    private static decimal? Interpolate(IReadOnlyList<StandardRow> rows, int ageDays, Func<StandardRow, decimal?> figure)
    {
        StandardRow? before = null;
        StandardRow? after = null;

        foreach (StandardRow row in rows)
        {
            if (figure(row) is null)
            {
                continue;
            }

            if (row.AgeDays <= ageDays)
            {
                before = row;
            }

            if (row.AgeDays >= ageDays)
            {
                after = row;
                break;
            }
        }

        if (before is null || after is null)
        {
            return null;
        }

        decimal from = figure(before)!.Value;
        if (before.AgeDays == after.AgeDays)
        {
            return from;
        }

        decimal to = figure(after)!.Value;

        return Round(from + (to - from) * (ageDays - before.AgeDays) / (after.AgeDays - before.AgeDays));
    }

    private static decimal? Percent(decimal? actual, decimal? standard) =>
        actual is null || standard is not > 0 ? null : Round((actual.Value - standard.Value) * 100m / standard.Value);

    private static decimal? Difference(decimal? actual, decimal? standard) =>
        actual is null || standard is null ? null : Round(actual.Value - standard.Value);

    private static decimal Round(decimal value) => decimal.Round(value, Decimals, MidpointRounding.AwayFromZero);
}
