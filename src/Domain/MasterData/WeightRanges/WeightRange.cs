using Domain.Common;
using SharedKernel;

namespace Domain.MasterData.WeightRanges;

/// <summary>
/// Rentang bobot (PLAN-MOBILE M-23, M-46): the average live weight bands the PPL report the birds ready for sale
/// in (Stok Ayam Harian, information for Sales). Global for all branches, maintained in the WebApp. A band is
/// [<see cref="MinWeightKg"/>, <see cref="MaxWeightKg"/>) in kg; an open side is null. Active bands never overlap.
/// Bands are deactivated, never deleted.
/// </summary>
public sealed class WeightRange : AggregateRoot
{
    public const int WeightDecimals = 3;

    private WeightRange(Guid id, string code, string name, decimal? minWeightKg, decimal? maxWeightKg, int sortOrder)
        : base(id)
    {
        Code = code;
        Name = name;
        MinWeightKg = minWeightKg;
        MaxWeightKg = maxWeightKg;
        SortOrder = sortOrder;
        IsActive = true;
    }

    private WeightRange()
    {
    }

    public string Code { get; private set; }
    public string Name { get; private set; }

    /// <summary>
    /// Lower bound in kg, inclusive; null = no lower bound.
    /// </summary>
    public decimal? MinWeightKg { get; private set; }

    /// <summary>
    /// Upper bound in kg, exclusive; null = no upper bound.
    /// </summary>
    public decimal? MaxWeightKg { get; private set; }

    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    public static Result<WeightRange> Create(string code, string name, decimal? minWeightKg, decimal? maxWeightKg, int sortOrder)
    {
        Result bounds = ValidateBounds(minWeightKg, maxWeightKg);
        if (bounds.IsFailure)
        {
            return Result.Failure<WeightRange>(bounds.Error);
        }

        return new WeightRange(Guid.CreateVersion7(), Codes.Normalize(code), name.Trim(), minWeightKg, maxWeightKg, sortOrder);
    }

    public void Update(string name, int sortOrder, bool isActive)
    {
        Name = name.Trim();
        SortOrder = sortOrder;
        IsActive = isActive;
    }

    /// <summary>
    /// Changes the bounds; the caller makes sure no stock entry uses the band yet (M-46).
    /// </summary>
    public Result SetBounds(decimal? minWeightKg, decimal? maxWeightKg)
    {
        Result bounds = ValidateBounds(minWeightKg, maxWeightKg);
        if (bounds.IsFailure)
        {
            return bounds;
        }

        MinWeightKg = minWeightKg;
        MaxWeightKg = maxWeightKg;

        return Result.Success();
    }

    public bool HasBounds(decimal? minWeightKg, decimal? maxWeightKg) => MinWeightKg == minWeightKg && MaxWeightKg == maxWeightKg;

    /// <summary>
    /// The average weight (kg) falls in the band.
    /// </summary>
    public bool Contains(decimal averageWeightKg) =>
        (MinWeightKg is not { } min || averageWeightKg >= min) && (MaxWeightKg is not { } max || averageWeightKg < max);

    public bool Overlaps(WeightRange other) =>
        (MinWeightKg is not { } min || other.MaxWeightKg is not { } otherMax || min < otherMax)
        && (other.MinWeightKg is not { } otherMin || MaxWeightKg is not { } max || otherMin < max);

    private static Result ValidateBounds(decimal? minWeightKg, decimal? maxWeightKg)
    {
        if (minWeightKg is null && maxWeightKg is null)
        {
            return Result.Failure(WeightRangeErrors.InvalidBounds);
        }

        if (minWeightKg < 0 || maxWeightKg <= 0 || minWeightKg >= maxWeightKg)
        {
            return Result.Failure(WeightRangeErrors.InvalidBounds);
        }

        return Result.Success();
    }
}
