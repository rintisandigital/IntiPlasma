using MobileApp.Core.Contracts;

namespace MobileApp.Core.Production;

/// <summary>
/// A day of a cycle's chart: the server's daily performance, or a recording still in the queue (<see cref="IsLocal"/>)
/// calculated on the device.
/// </summary>
public sealed record PerformancePoint(
    DateOnly Date,
    int AgeDays,
    int Mortality,
    int Culling,
    decimal FeedKg,
    decimal? AverageBodyWeightGram,
    PerformanceFigures Cumulative,
    bool IsLocal);

/// <summary>
/// The broiler formulas of the server (<c>Domain.Partnership.Cycles.CyclePerformance</c> and the daily accumulation of
/// <c>cycles/{id}/performance</c>), ported so recordings that are not sent yet show in the charts with the same
/// figures (PLAN-MOBILE M-65). Keep both in step: the unit tests use the numbers of the server tests.
/// </summary>
public static class PerformanceCalculator
{
    private const int Decimals = 3;

    /// <param name="averageWeightKg">Current body weight per bird.</param>
    /// <param name="ageDays">Current age in days.</param>
    public static PerformanceFigures Calculate(
        int initialPopulation,
        int mortality,
        int culling,
        int harvestedBirds,
        decimal harvestedWeightKg,
        decimal feedKg,
        decimal? averageWeightKg,
        decimal? ageDays)
    {
        int population = initialPopulation - mortality - culling - harvestedBirds;

        decimal depletion = initialPopulation == 0 ? 0 : (mortality + culling) * 100m / initialPopulation;

        decimal? liveWeight = averageWeightKg is null ? null : population * averageWeightKg.Value + harvestedWeightKg;

        decimal? fcr = liveWeight is > 0 ? feedKg / liveWeight : null;

        decimal? adg = averageWeightKg is not null && ageDays is > 0 ? averageWeightKg * 1000m / ageDays : null;

        decimal? ip = fcr is > 0 && ageDays is > 0 && averageWeightKg is not null
            ? (100m - depletion) * averageWeightKg / (fcr * ageDays) * 100m
            : null;

        return new PerformanceFigures(
            initialPopulation,
            mortality,
            culling,
            harvestedBirds,
            harvestedWeightKg,
            population,
            feedKg,
            Round(depletion),
            Round(averageWeightKg),
            Round(liveWeight),
            Round(fcr),
            Round(ageDays),
            Round(adg),
            Round(ip));
    }

    /// <summary>
    /// The chart days of a cycle: the server days plus the queued recordings of the cycle (dates the server does not
    /// have yet), accumulated again in date order when there are any.
    /// </summary>
    /// <param name="context">Converts the queued usages to feed kg; without it their feed counts as 0.</param>
    public static IReadOnlyList<PerformancePoint> WithLocal(
        CyclePerformanceReport report,
        IEnumerable<RecordingDraft> queued,
        FieldContext? context)
    {
        var serverDates = report.Days.Select(d => d.Date).ToHashSet();
        List<RecordingDraft> local =
        [
            .. queued.Where(d => d.CycleId == report.CycleId && !serverDates.Contains(d.Date) && report.ChickInDate is not null)
        ];

        if (local.Count == 0)
        {
            return
            [
                .. report.Days.Select(d => new PerformancePoint(
                    d.Date, d.AgeDays, d.Mortality, d.Culling, d.FeedKg, d.AverageBodyWeightGram, d.Cumulative, false))
            ];
        }

        DateOnly chickIn = report.ChickInDate!.Value;
        IEnumerable<Day> days = report.Days
            .Select(d => new Day(d.Date, d.AgeDays, d.Mortality, d.Culling, d.FeedKg, d.AverageBodyWeightGram, false))
            .Concat(local.Select(d => new Day(
                d.Date, d.Date.DayNumber - chickIn.DayNumber, d.Mortality, d.Culling, d.FeedKg(context), d.AverageBodyWeightGram, true)))
            .OrderBy(d => d.Date);

        int initialPopulation = report.Current.InitialPopulation;
        var points = new List<PerformancePoint>();
        int mortality = 0;
        int culling = 0;
        decimal feedKg = 0;
        decimal? lastBodyWeightGram = null;

        foreach (Day day in days)
        {
            mortality += day.Mortality;
            culling += day.Culling;
            feedKg += day.FeedKg;
            lastBodyWeightGram = day.AverageBodyWeightGram ?? lastBodyWeightGram;

            List<CycleHarvest> harvested = [.. report.Harvests.Where(h => h.Date <= day.Date)];

            PerformanceFigures cumulative = Calculate(
                initialPopulation,
                mortality,
                culling,
                harvested.Sum(h => h.Birds),
                harvested.Sum(h => h.WeightKg),
                feedKg,
                lastBodyWeightGram / 1000m,
                day.AgeDays);

            points.Add(new PerformancePoint(
                day.Date, day.AgeDays, day.Mortality, day.Culling, day.FeedKg, day.AverageBodyWeightGram, cumulative, day.IsLocal));
        }

        return points;
    }

    private sealed record Day(
        DateOnly Date,
        int AgeDays,
        int Mortality,
        int Culling,
        decimal FeedKg,
        decimal? AverageBodyWeightGram,
        bool IsLocal);

    private static decimal Round(decimal value) => decimal.Round(value, Decimals, MidpointRounding.AwayFromZero);

    private static decimal? Round(decimal? value) => value is null ? null : Round(value.Value);
}
