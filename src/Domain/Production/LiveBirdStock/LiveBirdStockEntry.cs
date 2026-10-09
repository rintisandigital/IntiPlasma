using Domain.MasterData.WeightRanges;
using Domain.Partnership.Cycles;
using SharedKernel;

namespace Domain.Production.LiveBirdStock;

/// <summary>
/// Stok ayam harian (PLAN-MOBILE M-22 s.d. M-27): the PPL's estimate of the live birds ready for sale in one coop on
/// one date within one weight range — birds (ekoran) and their total weight (tonase, kg). Information for Sales only:
/// no stock movement, no journal, no approval. One entry per cycle, date and weight range; entering the same
/// combination again changes that entry.
/// </summary>
public sealed class LiveBirdStockEntry : AggregateRoot
{
    private LiveBirdStockEntry(Guid id, ProductionCycle cycle, DateOnly date, WeightRange weightRange)
        : base(id)
    {
        CycleId = cycle.Id;
        BranchId = cycle.BranchId;
        CoopId = cycle.CoopId;
        Date = date;
        AgeDays = cycle.AgeOn(date);
        WeightRangeId = weightRange.Id;
    }

    private LiveBirdStockEntry()
    {
    }

    public Guid CycleId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid CoopId { get; private set; }
    public DateOnly Date { get; private set; }

    /// <summary>
    /// Days since chick-in on <see cref="Date"/>.
    /// </summary>
    public int AgeDays { get; private set; }

    public Guid WeightRangeId { get; private set; }

    /// <summary>
    /// Ekoran: number of birds in the range.
    /// </summary>
    public int Birds { get; private set; }

    /// <summary>
    /// Tonase: total live weight of those birds in kg.
    /// </summary>
    public decimal WeightKg { get; private set; }

    public string? Notes { get; private set; }

    public decimal AverageWeightKg => Birds == 0 ? 0 : Math.Round(WeightKg / Birds, WeightRange.WeightDecimals);

    /// <param name="id">Client-generated id (offline mobile entry), or null for a new one.</param>
    public static Result<LiveBirdStockEntry> Create(
        Guid? id,
        ProductionCycle cycle,
        DateOnly date,
        WeightRange weightRange,
        int birds,
        decimal weightKg,
        string? notes)
    {
        Result recordable = cycle.EnsureRecordable(date);
        if (recordable.IsFailure)
        {
            return Result.Failure<LiveBirdStockEntry>(recordable.Error);
        }

        var entry = new LiveBirdStockEntry(id ?? Guid.CreateVersion7(), cycle, date, weightRange);

        Result applied = entry.Apply(weightRange, birds, weightKg, notes);

        return applied.IsSuccess ? entry : Result.Failure<LiveBirdStockEntry>(applied.Error);
    }

    /// <summary>
    /// New figures for the same cycle, date and weight range.
    /// </summary>
    public Result Update(ProductionCycle cycle, WeightRange weightRange, int birds, decimal weightKg, string? notes)
    {
        if (cycle.Id != CycleId || weightRange.Id != WeightRangeId)
        {
            return Result.Failure(LiveBirdStockErrors.KeyMismatch);
        }

        Result recordable = cycle.EnsureRecordable(Date);

        return recordable.IsFailure ? recordable : Apply(weightRange, birds, weightKg, notes);
    }

    private Result Apply(WeightRange weightRange, int birds, decimal weightKg, string? notes)
    {
        if (!weightRange.IsActive)
        {
            return Result.Failure(WeightRangeErrors.Inactive(weightRange.Code));
        }

        if (birds <= 0 || weightKg <= 0)
        {
            return Result.Failure(LiveBirdStockErrors.InvalidQuantity);
        }

        decimal average = Math.Round(weightKg / birds, WeightRange.WeightDecimals);
        if (!weightRange.Contains(average))
        {
            return Result.Failure(LiveBirdStockErrors.AverageOutsideRange(average, weightRange.Code));
        }

        Birds = birds;
        WeightKg = weightKg;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        return Result.Success();
    }
}
