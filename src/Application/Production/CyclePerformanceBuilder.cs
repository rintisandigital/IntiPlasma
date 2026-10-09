using Domain.Partnership.Cycles;

namespace Application.Production;

/// <summary>
/// One recorded day as input for <see cref="CyclePerformanceBuilder"/>.
/// </summary>
internal sealed record RecordedDay(
    DateOnly Date,
    int AgeDays,
    int Mortality,
    int Culling,
    decimal FeedKg,
    decimal? AverageBodyWeightGram);

/// <summary>
/// A harvest as input for <see cref="CyclePerformanceBuilder"/>.
/// </summary>
internal sealed record HarvestedBatch(DateOnly Date, int Birds, decimal WeightKg);

/// <summary>
/// Accumulates a cycle's recordings into daily cumulative performance, shared by <c>cycles/{id}/performance</c> and
/// the mobile dashboard so both report the same figures (PLAN-MOBILE M-60).
/// </summary>
internal static class CyclePerformanceBuilder
{
    /// <summary>
    /// Accumulates the recordings (ordered by date) day by day; the body weight carries forward from the last
    /// weighing and a harvest counts from its date on.
    /// </summary>
    public static List<DailyPerformance> BuildDays(
        int initialPopulation,
        IEnumerable<RecordedDay> recordings,
        IReadOnlyCollection<HarvestedBatch> harvests)
    {
        var days = new List<DailyPerformance>();
        int mortality = 0;
        int culling = 0;
        decimal feedKg = 0;
        decimal? lastBodyWeightGram = null;

        foreach (RecordedDay day in recordings)
        {
            mortality += day.Mortality;
            culling += day.Culling;
            feedKg += day.FeedKg;
            lastBodyWeightGram = day.AverageBodyWeightGram ?? lastBodyWeightGram;

            var harvestedToDate = harvests.Where(h => h.Date <= day.Date).ToList();

            var cumulative = CyclePerformance.Calculate(
                initialPopulation,
                mortality,
                culling,
                harvestedToDate.Sum(h => h.Birds),
                harvestedToDate.Sum(h => h.WeightKg),
                feedKg,
                lastBodyWeightGram / 1000m,
                day.AgeDays);

            days.Add(new DailyPerformance(
                day.Date, day.AgeDays, day.Mortality, day.Culling, day.FeedKg, day.AverageBodyWeightGram, cumulative));
        }

        return days;
    }

    /// <summary>
    /// The figures after the last recording, or the starting position when nothing is recorded yet.
    /// </summary>
    public static CyclePerformance Current(int initialPopulation, IReadOnlyList<DailyPerformance> days) =>
        days.Count > 0
            ? days[^1].Cumulative
            : CyclePerformance.Calculate(initialPopulation, 0, 0, 0, 0, 0, null, null);
}
