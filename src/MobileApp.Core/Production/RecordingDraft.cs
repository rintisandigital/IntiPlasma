using MobileApp.Core.Contracts;
using MobileApp.Core.Formatting;

namespace MobileApp.Core.Production;

/// <summary>
/// A daily recording entered on the device, stored in the sync queue until the server took it (PLAN-MOBILE §3.6).
/// <see cref="Id"/> is the recording id on the server (Guid v7 made by the app), so a resend is harmless.
/// </summary>
public sealed record RecordingDraft
{
    public const int MaxPhotos = 5;

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

    public int Mortality { get; init; }

    public int Culling { get; init; }

    public decimal? AverageBodyWeightGram { get; init; }

    public string? Notes { get; init; }

    public IReadOnlyList<UsageInput> Usages { get; init; } = [];

    public IReadOnlyList<DraftPhoto> Photos { get; init; } = [];

    public CreateDailyRecordingRequest ToRequest() => new(
        Id,
        CycleId,
        Date,
        Mortality,
        Culling,
        AverageBodyWeightGram,
        string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
        Usages,
        [.. Photos.Select(p => p.Id)]);

    /// <summary>
    /// Feed used in kg (base unit of feed), computed with the units of the field context.
    /// </summary>
    public decimal FeedKg(FieldContext? context) =>
        context is null
            ? 0
            : Usages.Sum(u => context.FindItem(u.ItemId) is { IsFeed: true } item ? item.ToBase(u.UomId, u.Quantity) ?? 0 : 0);

    /// <summary>
    /// Base quantity of one item used in this draft.
    /// </summary>
    public decimal BaseQuantityOf(FieldContext context, Guid itemId) =>
        Usages.Where(u => u.ItemId == itemId)
            .Sum(u => context.FindItem(itemId)?.ToBase(u.UomId, u.Quantity) ?? 0);
}

/// <param name="Id">Attachment id on the server; the file on the device is named after it.</param>
/// <param name="Uploaded">True once <c>POST attachments</c> succeeded.</param>
public sealed record DraftPhoto(Guid Id, bool Uploaded = false);

/// <summary>
/// Outcome of the checks of a draft: errors block saving, warnings only inform (PLAN-MOBILE §3.6 step 2).
/// </summary>
public sealed class RecordingCheck
{
    public Dictionary<string, string> Errors { get; } = new(StringComparer.Ordinal);

    public List<string> Warnings { get; } = [];

    public bool IsValid => Errors.Count == 0;

    public string? ErrorFor(string field) => Errors.GetValueOrDefault(field);
}

/// <summary>
/// The rules checked on the device before a recording enters the queue. The server checks again when it is sent;
/// stock is only a warning here because the cached stock may be out of date.
/// </summary>
public static class RecordingRules
{
    public const string Cycle = nameof(RecordingDraft.CycleId);
    public const string Date = nameof(RecordingDraft.Date);
    public const string Mortality = nameof(RecordingDraft.Mortality);
    public const string BodyWeight = nameof(RecordingDraft.AverageBodyWeightGram);
    public const string Usages = nameof(RecordingDraft.Usages);
    public const string Photos = nameof(RecordingDraft.Photos);

    /// <param name="others">The user's other local drafts (not yet on the server), any cycle.</param>
    public static RecordingCheck Check(
        FieldContext context,
        RecordingDraft draft,
        IReadOnlyList<RecordingDraft> others,
        DateOnly today)
    {
        var check = new RecordingCheck();

        FieldCycle? cycle = context.FindCycle(draft.CycleId);
        if (cycle is null)
        {
            check.Errors[Cycle] = "Pilih kandang dengan siklus yang sedang berjalan.";

            return check;
        }

        List<RecordingDraft> sameCycle = [.. others.Where(o => o.CycleId == cycle.Id && o.Id != draft.Id)];

        if (draft.Date > today)
        {
            check.Errors[Date] = "Tanggal tidak boleh melewati hari ini.";
        }
        else if (draft.Date < cycle.ChickInDate)
        {
            check.Errors[Date] = $"Tanggal tidak boleh sebelum chick-in ({IdFormat.Date(cycle.ChickInDate)}).";
        }
        else if (cycle.RecordedDates.Contains(draft.Date))
        {
            check.Errors[Date] = "Recording tanggal ini sudah ada di server.";
        }
        else if (sameCycle.Any(o => o.Date == draft.Date))
        {
            check.Errors[Date] = "Recording tanggal ini sudah ada di antrean (belum terkirim).";
        }

        int population = AvailablePopulation(cycle, sameCycle);
        if (draft.Mortality < 0 || draft.Culling < 0)
        {
            check.Errors[Mortality] = "Mati dan culling tidak boleh negatif.";
        }
        else if (draft.Mortality + draft.Culling > population)
        {
            check.Errors[Mortality] = $"Mati + culling melebihi populasi berjalan ({IdFormat.Number(population)} ekor).";
        }

        if (draft.AverageBodyWeightGram is { } bw && bw is <= 0 or >= 10_000)
        {
            check.Errors[BodyWeight] = "BW rata-rata harus antara 1 dan 9.999 gram.";
        }

        CheckUsages(context, cycle, draft, others, check);

        if (draft.Photos.Count > RecordingDraft.MaxPhotos)
        {
            check.Errors[Photos] = $"Maksimal {RecordingDraft.MaxPhotos} foto.";
        }

        return check;
    }

    /// <summary>
    /// Running population minus the depletion of the cycle's drafts that the server has not seen yet.
    /// </summary>
    public static int AvailablePopulation(FieldCycle cycle, IEnumerable<RecordingDraft> pendingOfCycle) =>
        cycle.CurrentPopulation - pendingOfCycle.Where(d => d.CycleId == cycle.Id).Sum(d => d.Mortality + d.Culling);

    /// <summary>
    /// Stock of the coop warehouse (base unit) minus what the pending drafts of the same coop will use.
    /// </summary>
    public static decimal AvailableStock(
        FieldContext context,
        FieldCycle cycle,
        Guid itemId,
        IEnumerable<RecordingDraft> pending) =>
        context.StockOf(cycle.WarehouseId, itemId)
        - pending.Where(d => d.CycleId == cycle.Id).Sum(d => d.BaseQuantityOf(context, itemId));

    /// <summary>
    /// Kalkulator sampel timbang: average body weight in grams of a weighed sample.
    /// </summary>
    public static decimal? AverageGram(decimal? sampleWeightKg, int? sampleBirds) =>
        sampleWeightKg is > 0 && sampleBirds is > 0
            ? Math.Round(sampleWeightKg.Value * 1000 / sampleBirds.Value, 1)
            : null;

    private static void CheckUsages(
        FieldContext context,
        FieldCycle cycle,
        RecordingDraft draft,
        IReadOnlyList<RecordingDraft> others,
        RecordingCheck check)
    {
        if (draft.Usages.Count == 0)
        {
            return;
        }

        if (cycle.WarehouseId is null)
        {
            check.Errors[Usages] = "Kandang ini belum memiliki gudang kandang; pemakaian tidak bisa dicatat.";

            return;
        }

        if (draft.Usages.GroupBy(u => u.ItemId).Any(g => g.Count() > 1))
        {
            check.Errors[Usages] = "Setiap item hanya boleh dicatat sekali.";

            return;
        }

        foreach (UsageInput usage in draft.Usages)
        {
            FieldItem? item = context.FindItem(usage.ItemId);
            if (item is null || usage.Quantity <= 0 || item.ToBase(usage.UomId, usage.Quantity) is not { } baseQuantity)
            {
                check.Errors[Usages] = "Lengkapi item, satuan, dan jumlah pemakaian (lebih dari 0).";

                return;
            }

            List<RecordingDraft> pending = [.. others.Where(o => o.Id != draft.Id)];
            decimal available = AvailableStock(context, cycle, item.Id, pending);
            if (baseQuantity > available)
            {
                string stock = IdFormat.Number(Math.Max(available, 0), 2);
                check.Warnings.Add(
                    $"Stok {item.Name} di gudang kandang tercatat {stock} {item.BaseUomCode}; server akan menolak bila stok tidak cukup.");
            }
        }
    }
}
