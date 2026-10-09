using MobileApp.Core.Contracts;
using MobileApp.Core.Formatting;

namespace MobileApp.Core.Production;

/// <summary>
/// Stok ayam harian for one weight range, entered on the device (PLAN-MOBILE M-22 s.d. M-27). Kept in the sync queue
/// until the server took it; the server changes the entry of the same cycle, date and range, so a resend is harmless.
/// </summary>
public sealed record StockDraft
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid CycleId { get; init; }

    /// <summary>
    /// Shown in the queue while offline.
    /// </summary>
    public string CycleNumber { get; init; } = string.Empty;

    /// <summary>
    /// Shown in the queue while offline.
    /// </summary>
    public string CoopName { get; init; } = string.Empty;

    public DateOnly Date { get; init; }

    public Guid WeightRangeId { get; init; }

    /// <summary>
    /// Shown in the queue while offline.
    /// </summary>
    public string WeightRangeCode { get; init; } = string.Empty;

    public int Birds { get; init; }

    public decimal WeightKg { get; init; }

    public string? Notes { get; init; }

    public decimal AverageWeightKg => Birds <= 0 ? 0 : Math.Round(WeightKg / Birds, 3);

    public bool SameKey(StockDraft other) =>
        other.CycleId == CycleId && other.Date == Date && other.WeightRangeId == WeightRangeId;

    public UpsertLiveBirdStockRequest ToRequest() => new(
        Id,
        CycleId,
        Date,
        WeightRangeId,
        Birds,
        WeightKg,
        string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim());
}

/// <summary>
/// One weight range of a cycle on a date: what the server has, changed by what waits in the queue.
/// </summary>
/// <param name="ServerId">Id of the server entry, if any (needed to delete it).</param>
public sealed record StockRow(
    Guid Id,
    Guid? ServerId,
    DateOnly Date,
    Guid WeightRangeId,
    string WeightRangeCode,
    int Birds,
    decimal WeightKg,
    string? Notes,
    RecordingState State,
    string? Error)
{
    public decimal AverageWeightKg => Birds <= 0 ? 0 : Math.Round(WeightKg / Birds, 3);

    public bool IsLocal => State != RecordingState.Sent;
}

/// <summary>
/// The rules checked on the device before a stock entry enters the queue (M-24, M-27, M-50); the server checks again.
/// </summary>
public static class StockRules
{
    public const string Cycle = nameof(StockDraft.CycleId);
    public const string Date = nameof(StockDraft.Date);
    public const string Range = nameof(StockDraft.WeightRangeId);
    public const string Figures = nameof(StockDraft.Birds);

    /// <param name="dayRows">The rows of the same cycle and date (server and queue), the draft's own range included.</param>
    /// <param name="pendingRecordings">The user's recordings not yet sent (their depletion lowers the population).</param>
    public static RecordingCheck Check(
        FieldContext context,
        StockDraft draft,
        IReadOnlyList<StockRow> dayRows,
        IReadOnlyList<RecordingDraft> pendingRecordings,
        DateOnly today)
    {
        var check = new RecordingCheck();

        FieldCycle? cycle = context.FindCycle(draft.CycleId);
        if (cycle is null)
        {
            check.Errors[Cycle] = "Pilih kandang dengan siklus yang sedang berjalan.";

            return check;
        }

        if (draft.Date > today)
        {
            check.Errors[Date] = "Tanggal tidak boleh melewati hari ini.";
        }
        else if (draft.Date < cycle.ChickInDate)
        {
            check.Errors[Date] = $"Tanggal tidak boleh sebelum chick-in ({IdFormat.Date(cycle.ChickInDate)}).";
        }

        WeightRange? range = context.FindRange(draft.WeightRangeId);
        if (range is null)
        {
            check.Errors[Range] = "Pilih rentang bobot.";

            return check;
        }

        if (draft.Birds <= 0 || draft.WeightKg <= 0)
        {
            check.Errors[Figures] = "Isi ekoran dan tonase (lebih dari 0).";

            return check;
        }

        if (!range.Contains(draft.AverageWeightKg))
        {
            check.Errors[Figures] =
                $"Rata-rata {IdFormat.Number(draft.AverageWeightKg, 3)} kg di luar rentang {range.Name}.";

            return check;
        }

        int population = RecordingRules.AvailablePopulation(cycle, pendingRecordings);
        int total = dayRows.Where(r => r.WeightRangeId != draft.WeightRangeId).Sum(r => r.Birds) + draft.Birds;
        if (total > population)
        {
            check.Errors[Figures] =
                $"Total ekor semua rentang ({IdFormat.Number(total)}) melebihi populasi berjalan ({IdFormat.Number(population)}).";
        }

        return check;
    }
}
