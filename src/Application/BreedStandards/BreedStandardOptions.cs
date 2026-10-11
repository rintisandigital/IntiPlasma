using Domain.MasterData.BreedStandards;

namespace Application.BreedStandards;

/// <summary>
/// Limits beyond which a cycle is below its breed standard (PLAN-MOBILE M-76), bound from <c>Production:Standard</c>.
/// </summary>
public sealed class BreedStandardOptions
{
    public const string SectionName = "Production:Standard";

    /// <summary>
    /// Body weight more than this many percent under the standard.
    /// </summary>
    public decimal BodyWeightBelowPercent { get; set; } = StandardThresholds.Default.BodyWeightBelowPercent;

    /// <summary>
    /// FCR more than this above the standard.
    /// </summary>
    public decimal FcrAbove { get; set; } = StandardThresholds.Default.FcrAbove;

    /// <summary>
    /// Depletion more than this many percentage points above the standard.
    /// </summary>
    public decimal DepletionAbovePoints { get; set; } = StandardThresholds.Default.DepletionAbovePoints;

    public StandardThresholds Thresholds => new(BodyWeightBelowPercent, FcrAbove, DepletionAbovePoints);
}
