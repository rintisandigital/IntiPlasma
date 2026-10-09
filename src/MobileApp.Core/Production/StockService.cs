using System.Text.Json;
using MobileApp.Core.Abstractions;
using MobileApp.Core.Api;
using MobileApp.Core.Contracts;
using MobileApp.Core.Local;
using MobileApp.Core.Sync;

namespace MobileApp.Core.Production;

/// <summary>
/// Stok ayam harian on the device (PLAN-MOBILE M3): saving into the offline queue (sent right away when online; the
/// same cycle, date and range replaces the queued entry, M-24), deleting recent server entries online (M-49), copying
/// the previous report and lists that merge server and queue.
/// </summary>
/// <param name="currentUserId">The signed-in user, or null.</param>
public sealed class StockService(
    ProductionApi production,
    LocalDb db,
    SyncEngine sync,
    RecordingService recordings,
    IClock clock,
    IConnectivity connectivity,
    Func<Guid?> currentUserId)
{
    public DateOnly Today => clock.Today;

    public bool IsOnline => connectivity.IsOnline;

    /// <summary>
    /// The user's stock entries not yet on the server, oldest date first.
    /// </summary>
    public async Task<IReadOnlyList<LocalStock>> GetLocalListAsync(Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        if (currentUserId() is not Guid userId)
        {
            return [];
        }

        IReadOnlyList<SyncItem> queue = await db.GetQueueAsync(userId, cancellationToken);

        return
        [
            .. queue
                .Where(i => i.Kind == SyncKinds.LiveBirdStock && (cycleId is null || i.CycleId == cycleId))
                .Select(i => new LocalStock(i, Read(i)))
        ];
    }

    public async Task<LocalStock?> FindLocalAsync(Guid id, CancellationToken cancellationToken = default)
    {
        SyncItem? item = await db.GetQueueItemAsync(id, cancellationToken);

        return item is null || item.Kind != SyncKinds.LiveBirdStock || item.UserId != currentUserId()
            ? null
            : new LocalStock(item, Read(item));
    }

    /// <summary>
    /// Every date of a cycle: the server entries (stored copy when offline) with the queued ones in their place.
    /// </summary>
    /// <param name="refresh">False: use the stored server list (after a queue change).</param>
    public async Task<StockList> GetCycleRowsAsync(Guid cycleId, bool refresh = true, CancellationToken cancellationToken = default)
    {
        CachedResult<List<LiveBirdStockEntry>>? stored = refresh ? null : await production.ReadCycleStockAsync(cycleId, cancellationToken);
        CachedResult<List<LiveBirdStockEntry>> server = stored is null
            ? await production.GetCycleStockAsync(cycleId, cancellationToken)
            : CachedResult<List<LiveBirdStockEntry>>.Fresh(stored.Value);

        IReadOnlyList<LocalStock> local = await GetLocalListAsync(cycleId, cancellationToken);

        return Merge(server, local);
    }

    /// <summary>
    /// The latest date before <paramref name="date"/> with entries, and its rows ("Salin dari kemarin").
    /// </summary>
    public static (DateOnly? Date, IReadOnlyList<StockRow> Rows) Previous(StockList list, DateOnly date)
    {
        DateOnly? previous = list.Rows.Where(r => r.Date < date).Select(r => (DateOnly?)r.Date).Max();

        return (previous, previous is null ? [] : [.. list.Rows.Where(r => r.Date == previous)]);
    }

    public async Task<RecordingCheck> CheckAsync(
        FieldContext context,
        StockDraft draft,
        IReadOnlyList<StockRow> dayRows,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<LocalRecording> pending = await recordings.GetLocalListAsync(draft.CycleId, cancellationToken);

        return StockRules.Check(context, draft, dayRows, [.. pending.Select(p => p.Draft)], clock.Today);
    }

    /// <summary>
    /// Puts the entry in the queue (replacing a queued entry of the same range) and sends it when online.
    /// </summary>
    public async Task<SaveOutcome> SaveAsync(StockDraft draft, CancellationToken cancellationToken = default)
    {
        if (currentUserId() is not Guid userId)
        {
            return new SaveOutcome(SaveState.Rejected, "Sesi berakhir. Silakan masuk kembali.");
        }

        IReadOnlyList<LocalStock> queued = await GetLocalListAsync(draft.CycleId, cancellationToken);
        LocalStock? same = queued.FirstOrDefault(q => q.Draft.SameKey(draft));
        if (same?.Item.Status == SyncStatus.Sending)
        {
            return new SaveOutcome(SaveState.Rejected, "Data sedang dikirim. Tunggu sebentar lalu coba lagi.");
        }

        if (same is not null)
        {
            draft = draft with { Id = same.Draft.Id };
        }

        DateTime now = clock.UtcNow;
        await db.SaveQueueItemAsync(
            new SyncItem
            {
                Id = draft.Id,
                UserId = userId,
                Kind = SyncKinds.LiveBirdStock,
                CycleId = draft.CycleId,
                Date = draft.Date,
                Payload = JsonSerializer.Serialize(draft, ApiClient.JsonOptions),
                Status = SyncStatus.Pending,
                CreatedAtUtc = same?.Item.CreatedAtUtc ?? now,
                UpdatedAtUtc = now
            },
            cancellationToken);

        return await SendNowAsync(draft.Id, cancellationToken);
    }

    public async Task<SaveOutcome> RetryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        SyncItem? item = await db.GetQueueItemAsync(id, cancellationToken);
        if (item is null)
        {
            return new SaveOutcome(SaveState.Sent, null);
        }

        if (item.Status != SyncStatus.Sending)
        {
            await db.SaveQueueItemAsync(item with { Status = SyncStatus.Pending, NextAttemptAtUtc = null, UpdatedAtUtc = clock.UtcNow }, cancellationToken);
        }

        return await SendNowAsync(id, cancellationToken);
    }

    /// <summary>
    /// Removes an entry that has not reached the server.
    /// </summary>
    public async Task<bool> DeleteLocalAsync(Guid id, CancellationToken cancellationToken = default)
    {
        LocalStock? local = await FindLocalAsync(id, cancellationToken);
        if (local is null || local.Item.Status == SyncStatus.Sending)
        {
            return false;
        }

        await db.DeleteQueueItemAsync(id, cancellationToken);
        sync.NotifyChanged();

        return true;
    }

    /// <summary>
    /// Deletes a server entry of today or yesterday (online only).
    /// </summary>
    public async Task<ApiResult> DeleteServerAsync(Guid entryId, Guid cycleId, CancellationToken cancellationToken = default)
    {
        ApiResult result = await production.DeleteStockAsync(entryId, cancellationToken);
        if (result.IsSuccess)
        {
            await production.GetCycleStockAsync(cycleId, cancellationToken);
            await production.GetFieldContextAsync(cancellationToken);
            sync.NotifyChanged();
        }

        return result;
    }

    internal static StockList Merge(CachedResult<List<LiveBirdStockEntry>> server, IReadOnlyList<LocalStock> local)
    {
        List<StockRow> rows = server.IsSuccess
            ? [
                .. server.Value.Select(e => new StockRow(
                    e.Id, e.Id, e.Date, e.WeightRangeId, e.WeightRangeCode, e.Birds, e.WeightKg, e.Notes, RecordingState.Sent, null))
            ]
            : [];

        foreach (LocalStock entry in local)
        {
            StockDraft d = entry.Draft;
            StockRow? replaced = rows.Find(r => r.Date == d.Date && r.WeightRangeId == d.WeightRangeId);
            if (replaced is not null)
            {
                rows.Remove(replaced);
            }

            rows.Add(new StockRow(
                d.Id, replaced?.ServerId, d.Date, d.WeightRangeId, d.WeightRangeCode, d.Birds, d.WeightKg, d.Notes,
                entry.State, entry.Item.ErrorMessage));
        }

        return new StockList(
            [.. rows.OrderByDescending(r => r.Date).ThenBy(r => r.WeightRangeCode, StringComparer.Ordinal)],
            server.IsSuccess ? null : server.Error,
            server.IsSuccess ? server.CachedAtUtc : null);
    }

    private async Task<SaveOutcome> SendNowAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!connectivity.IsOnline)
        {
            sync.NotifyChanged();

            return new SaveOutcome(SaveState.Queued, null);
        }

        await sync.RunAsync(id, cancellationToken);

        SyncItem? after = await db.GetQueueItemAsync(id, cancellationToken);

        return after switch
        {
            null => new SaveOutcome(SaveState.Sent, null),
            { Status: SyncStatus.Failed } => new SaveOutcome(SaveState.Failed, after.ErrorMessage),
            _ => new SaveOutcome(SaveState.Queued, after.ErrorMessage)
        };
    }

    private static StockDraft Read(SyncItem item) =>
        JsonSerializer.Deserialize<StockDraft>(item.Payload, ApiClient.JsonOptions)
        ?? new StockDraft { Id = item.Id, CycleId = item.CycleId, Date = item.Date };
}

/// <summary>
/// A stock entry in the sync queue.
/// </summary>
public sealed record LocalStock(SyncItem Item, StockDraft Draft)
{
    public RecordingState State => Item.Status switch
    {
        SyncStatus.Sending => RecordingState.Sending,
        SyncStatus.Failed => RecordingState.Failed,
        _ => RecordingState.Pending
    };
}

/// <param name="ServerError">Set when the server list could not be loaded (nothing stored either).</param>
/// <param name="CachedAtUtc">Set when the server list came from the stored copy.</param>
public sealed record StockList(IReadOnlyList<StockRow> Rows, ApiError? ServerError, DateTime? CachedAtUtc)
{
    public IReadOnlyList<StockRow> On(DateOnly date) => [.. Rows.Where(r => r.Date == date)];
}
