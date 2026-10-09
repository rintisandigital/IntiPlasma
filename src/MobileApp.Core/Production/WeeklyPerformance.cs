using MobileApp.Core.Contracts;

namespace MobileApp.Core.Production;

/// <summary>
/// A week of a cycle (PLAN-MOBILE M-68). Week n covers age 7n−6 to 7n (week 1 = age 1–7; a recording on the
/// chick-in day, age 0, counts in week 1).
/// </summary>
/// <param name="Days">Recorded days in the week.</param>
/// <param name="IsComplete">False for the last week while its age 7n is not recorded yet ("berjalan").</param>
/// <param name="HasLocal">The week contains recordings that are not sent yet.</param>
/// <param name="FeedKg">Feed of the week.</param>
/// <param name="FeedGramPerBird">Feed of the week per bird alive at the start of the week.</param>
/// <param name="Depleted">Deaths and culls of the week.</param>
/// <param name="DepletedPercent">Of the population at the start of the week.</param>
/// <param name="BodyWeightGram">Body weight at the end of the week (carried forward from the last weighing).</param>
/// <param name="BodyWeightGainGram">Gain over the previous week; null for the first week or without weights.</param>
/// <param name="Cumulative">Cumulative figures on the last recorded day of the week.</param>
public sealed record WeekPerformance(
    int Week,
    int FromAge,
    int ToAge,
    int Days,
    bool IsComplete,
    bool HasLocal,
    decimal FeedKg,
    decimal FeedGramPerBird,
    int Depleted,
    decimal DepletedPercent,
    decimal? BodyWeightGram,
    decimal? BodyWeightGainGram,
    PerformanceFigures Cumulative)
{
    public static int WeekOf(int ageDays) => ageDays <= 0 ? 1 : (ageDays + 6) / 7;
}

public static class WeeklyPerformance
{
    /// <summary>
    /// Groups the chart days (date order) per week: flows (feed, deaths and culls) are summed, positions (body
    /// weight, depletion, FCR, IP, population) come from the last day of the week.
    /// </summary>
    public static IReadOnlyList<WeekPerformance> Aggregate(IReadOnlyList<PerformancePoint> days, int initialPopulation)
    {
        var weeks = new List<WeekPerformance>();
        int populationAtStart = initialPopulation;
        decimal? previousBodyWeight = null;

        List<IGrouping<int, PerformancePoint>> groups = [.. days.GroupBy(d => WeekPerformance.WeekOf(d.AgeDays))];

        for (int i = 0; i < groups.Count; i++)
        {
            IGrouping<int, PerformancePoint> group = groups[i];
            PerformancePoint last = group.Last();
            int week = group.Key;
            int depleted = group.Sum(d => d.Mortality + d.Culling);
            decimal feedKg = group.Sum(d => d.FeedKg);
            decimal? bodyWeight = last.Cumulative.AverageWeightKg * 1000m;

            weeks.Add(new WeekPerformance(
                week,
                week * 7 - 6,
                week * 7,
                group.Count(),
                i < groups.Count - 1 || last.AgeDays >= week * 7,
                group.Any(d => d.IsLocal),
                feedKg,
                populationAtStart > 0 ? Round(feedKg * 1000m / populationAtStart) : 0,
                depleted,
                populationAtStart > 0 ? Round(depleted * 100m / populationAtStart) : 0,
                bodyWeight,
                bodyWeight is not null && previousBodyWeight is not null ? bodyWeight - previousBodyWeight : null,
                last.Cumulative));

            populationAtStart = last.Cumulative.Population;
            previousBodyWeight = bodyWeight ?? previousBodyWeight;
        }

        return weeks;
    }

    private static decimal Round(decimal value) => decimal.Round(value, 3, MidpointRounding.AwayFromZero);
}
