namespace Domain.Partnership.Cycles;

/// <summary>
/// Broiler performance figures of a cycle, calculated one way for the daily read model and the closing summary.
/// <list type="bullet">
/// <item>Deplesi % = (mati + culling) / populasi awal × 100</item>
/// <item>FCR = pakan terpakai (kg) / bobot hidup (kg); bobot hidup = populasi × BW + bobot yang sudah dipanen</item>
/// <item>ADG = BW (gram) / umur (hari)</item>
/// <item>IP = (daya hidup % × BW kg) / (FCR × umur) × 100, dengan daya hidup % = 100 − deplesi %</item>
/// </list>
/// </summary>
public sealed record CyclePerformance(
    int InitialPopulation,
    int Mortality,
    int Culling,
    int HarvestedBirds,
    decimal HarvestedWeightKg,
    int Population,
    decimal FeedKg,
    decimal DepletionPercent,
    decimal? AverageWeightKg,
    decimal? LiveWeightKg,
    decimal? Fcr,
    decimal? AgeDays,
    decimal? AdgGram,
    decimal? Ip)
{
    private const int Decimals = 3;

    /// <param name="averageWeightKg">Current body weight per bird (while growing) or average harvest weight (at closing).</param>
    /// <param name="ageDays">Current age, or the bird-weighted average harvest age at closing.</param>
    public static CyclePerformance Calculate(
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

        return new CyclePerformance(
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

    private static decimal Round(decimal value) => decimal.Round(value, Decimals, MidpointRounding.AwayFromZero);

    private static decimal? Round(decimal? value) => value is null ? null : Round(value.Value);
}
