namespace MobileApp.Core.Production;

/// <summary>
/// The metrics of the production chart (PLAN-MOBILE M-64).
/// </summary>
public enum ChartMetric
{
    BodyWeight,
    Depletion,
    Fcr,
    Feed,
    Mortality,
    Ip
}

/// <summary>
/// A cycle to chart: its days (server + queued) and the label of its line when cycles are compared.
/// </summary>
public sealed record CycleSeries(Guid CycleId, string Label, int InitialPopulation, IReadOnlyList<PerformancePoint> Days)
{
    public IReadOnlyList<WeekPerformance> Weeks { get; } = WeeklyPerformance.Aggregate(Days, InitialPopulation);
}

/// <summary>
/// What <c>chart-interop.js</c> draws with Chart.js: one x axis, datasets on the left (<c>y</c>) or right (<c>y1</c>)
/// axis.
/// </summary>
public sealed record ChartSpec(
    IReadOnlyList<string> Labels,
    IReadOnlyList<ChartDataset> Datasets,
    string XTitle,
    string YTitle,
    string? Y1Title);

/// <param name="Type"><c>line</c> or <c>bar</c>.</param>
/// <param name="Axis"><c>y</c> or <c>y1</c>.</param>
/// <param name="LocalFrom">Index of the first value from a recording that is not sent yet: drawn dashed from there on.</param>
/// <param name="Stack">Bars with the same stack are stacked.</param>
public sealed record ChartDataset(
    string Label,
    IReadOnlyList<decimal?> Data,
    string Type,
    string Axis,
    string Color,
    int? LocalFrom,
    string? Stack = null);

/// <summary>
/// Builds the chart of a metric for one cycle (detail series) or for up to three cycles (one line each, M-66), per
/// day of age or per week (M-68).
/// </summary>
public static class PerformanceCharts
{
    public const int MaxCompared = 3;

    public static readonly IReadOnlyList<string> CompareColors = ["#0984E3", "#E17055", "#00B894"];

    private const string Primary = "#0984E3";
    private const string Light = "#74B9FF";
    private const string Danger = "#D63031";
    private const string Warning = "#FDCB6E";

    public static string Title(ChartMetric metric) => metric switch
    {
        ChartMetric.BodyWeight => "BW",
        ChartMetric.Depletion => "Deplesi",
        ChartMetric.Fcr => "FCR",
        ChartMetric.Feed => "Pakan",
        ChartMetric.Mortality => "Mati",
        _ => "IP"
    };

    public static ChartSpec Build(ChartMetric metric, bool weekly, IReadOnlyList<CycleSeries> cycles)
    {
        if (weekly)
        {
            List<int> weeks = [.. cycles.SelectMany(c => c.Weeks).Select(w => w.Week).Distinct().Order()];
            Axis<WeekPerformance> axis = new([.. weeks.Select(w => $"M{w}")], "Minggu", c => c.Weeks, w => weeks.IndexOf(w.Week), w => w.HasLocal);

            if (cycles.Count == 1)
            {
                return WeeklySingle(metric, axis, cycles[0]);
            }

            string unit = metric == ChartMetric.Mortality ? "% deplesi per minggu" : CompareUnit(metric);

            return Compare(metric, axis, cycles, WeeklyValue, unit);
        }

        List<int> ages = [.. cycles.SelectMany(c => c.Days).Select(d => d.AgeDays).Distinct().Order()];
        Axis<PerformancePoint> days = new([.. ages.Select(a => a.ToString(System.Globalization.CultureInfo.InvariantCulture))], "Umur (hari)", c => c.Days, d => ages.IndexOf(d.AgeDays), d => d.IsLocal);

        return cycles.Count == 1 ? DailySingle(metric, days, cycles[0]) : Compare(metric, days, cycles, DailyValue, CompareUnit(metric));
    }

    private static ChartSpec DailySingle(ChartMetric metric, Axis<PerformancePoint> axis, CycleSeries cycle) => metric switch
    {
        ChartMetric.Feed => new ChartSpec(
            axis.Labels,
            [
                axis.Dataset(cycle, "Pakan harian (kg)", d => d.FeedKg, "bar", "y", Light),
                axis.Dataset(cycle, "Pakan kumulatif (kg)", d => d.Cumulative.FeedKg, "line", "y1", Primary)
            ],
            axis.XTitle,
            "kg/hari",
            "kg kumulatif"),
        ChartMetric.Mortality => new ChartSpec(
            axis.Labels,
            [
                axis.Dataset(cycle, "Mati", d => d.Mortality, "bar", "y", Danger, "deplesi"),
                axis.Dataset(cycle, "Culling", d => d.Culling, "bar", "y", Warning, "deplesi")
            ],
            axis.XTitle,
            "ekor",
            null),
        _ => new ChartSpec(
            axis.Labels,
            [axis.Dataset(cycle, SeriesLabel(metric), d => DailyValue(metric, d, cycle), "line", "y", Primary)],
            axis.XTitle,
            Unit(metric),
            null)
    };

    private static ChartSpec WeeklySingle(ChartMetric metric, Axis<WeekPerformance> axis, CycleSeries cycle) => metric switch
    {
        ChartMetric.Feed => new ChartSpec(
            axis.Labels,
            [
                axis.Dataset(cycle, "Pakan per minggu (kg)", w => w.FeedKg, "bar", "y", Light),
                axis.Dataset(cycle, "Pakan kumulatif (kg)", w => w.Cumulative.FeedKg, "line", "y1", Primary)
            ],
            axis.XTitle,
            "kg/minggu",
            "kg kumulatif"),
        ChartMetric.Mortality => new ChartSpec(
            axis.Labels,
            [
                axis.Dataset(cycle, "Mati + culling (ekor)", w => w.Depleted, "bar", "y", Danger),
                axis.Dataset(cycle, "Deplesi minggu (%)", w => w.DepletedPercent, "line", "y1", Primary)
            ],
            axis.XTitle,
            "ekor",
            "%"),
        ChartMetric.BodyWeight => new ChartSpec(
            axis.Labels,
            [
                axis.Dataset(cycle, "BW akhir minggu (g)", w => w.BodyWeightGram, "line", "y", Primary),
                axis.Dataset(cycle, "Pertambahan BW (g)", w => w.BodyWeightGainGram, "bar", "y", Light)
            ],
            axis.XTitle,
            "gram",
            null),
        _ => new ChartSpec(
            axis.Labels,
            [axis.Dataset(cycle, SeriesLabel(metric), w => WeeklyValue(metric, w, cycle), "line", "y", Primary)],
            axis.XTitle,
            Unit(metric),
            null)
    };

    private static ChartSpec Compare<T>(
        ChartMetric metric,
        Axis<T> axis,
        IReadOnlyList<CycleSeries> cycles,
        Func<ChartMetric, T, CycleSeries, decimal?> value,
        string unit) =>
        new(
            axis.Labels,
            [
                .. cycles.Take(MaxCompared).Select((c, i) =>
                    axis.Dataset(c, c.Label, p => value(metric, p, c), "line", "y", CompareColors[i]))
            ],
            axis.XTitle,
            unit,
            null);

    /// <summary>
    /// The value of a day for a single line; for comparisons feed is per bird and deaths include culls.
    /// </summary>
    private static decimal? DailyValue(ChartMetric metric, PerformancePoint day, CycleSeries cycle) => metric switch
    {
        ChartMetric.BodyWeight => day.AverageBodyWeightGram,
        ChartMetric.Depletion => day.Cumulative.DepletionPercent,
        ChartMetric.Fcr => day.Cumulative.Fcr,
        ChartMetric.Feed => PerBird(day.Cumulative.FeedKg, cycle.InitialPopulation),
        ChartMetric.Mortality => day.Mortality + day.Culling,
        _ => day.Cumulative.Ip
    };

    private static decimal? WeeklyValue(ChartMetric metric, WeekPerformance week, CycleSeries cycle) => metric switch
    {
        ChartMetric.BodyWeight => week.BodyWeightGram,
        ChartMetric.Depletion => week.Cumulative.DepletionPercent,
        ChartMetric.Fcr => week.Cumulative.Fcr,
        ChartMetric.Feed => PerBird(week.Cumulative.FeedKg, cycle.InitialPopulation),
        ChartMetric.Mortality => week.DepletedPercent,
        _ => week.Cumulative.Ip
    };

    private static decimal? PerBird(decimal feedKg, int initialPopulation) =>
        initialPopulation > 0 ? decimal.Round(feedKg * 1000m / initialPopulation, 1, MidpointRounding.AwayFromZero) : null;

    private static string SeriesLabel(ChartMetric metric) => metric switch
    {
        ChartMetric.BodyWeight => "BW (g)",
        ChartMetric.Depletion => "Deplesi kumulatif (%)",
        ChartMetric.Fcr => "FCR",
        _ => "IP"
    };

    private static string Unit(ChartMetric metric) => metric switch
    {
        ChartMetric.BodyWeight => "gram",
        ChartMetric.Depletion => "%",
        ChartMetric.Fcr => "FCR",
        _ => "IP"
    };

    private static string CompareUnit(ChartMetric metric) => metric switch
    {
        ChartMetric.Feed => "pakan kumulatif g/ekor",
        ChartMetric.Mortality => "mati + culling (ekor)",
        _ => Unit(metric)
    };

    /// <summary>
    /// The x axis (ages or weeks) and how to place a cycle's points on it.
    /// </summary>
    private sealed record Axis<T>(
        IReadOnlyList<string> Labels,
        string XTitle,
        Func<CycleSeries, IReadOnlyList<T>> Points,
        Func<T, int> IndexOf,
        Func<T, bool> IsLocal)
    {
        public ChartDataset Dataset(CycleSeries cycle, string label, Func<T, decimal?> value, string type, string axis, string color, string? stack = null)
        {
            decimal?[] data = new decimal?[Labels.Count];
            int? localFrom = null;

            foreach (T point in Points(cycle))
            {
                int index = IndexOf(point);
                data[index] = value(point);

                if (IsLocal(point) && (localFrom is null || index < localFrom))
                {
                    localFrom = index;
                }
            }

            return new ChartDataset(label, data, type, axis, color, localFrom, stack);
        }
    }
}
