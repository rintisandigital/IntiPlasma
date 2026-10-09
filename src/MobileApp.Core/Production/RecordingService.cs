using System.Text.Json;
using MobileApp.Core.Abstractions;
using MobileApp.Core.Api;
using MobileApp.Core.Contracts;
using MobileApp.Core.Local;
using MobileApp.Core.Sync;

namespace MobileApp.Core.Production;

/// <summary>
/// Daily recording on the device (PLAN-MOBILE M2): saving into the offline queue (sent right away when online),
/// editing or deleting entries that did not reach the server yet (M-43), and lists that show server and local
/// entries together.
/// </summary>
/// <param name="currentUserId">The signed-in user, or null.</param>
public sealed class RecordingService(
    ProductionApi production,
    LocalDb db,
    SyncEngine sync,
    PendingFiles files,
    IClock clock,
    IConnectivity connectivity,
    Func<Guid?> currentUserId)
{
    public PendingFiles Files => files;

    public DateOnly Today => clock.Today;

    /// <summary>
    /// The field context: from the server when <paramref name="refresh"/> (or nothing is stored yet), otherwise the
    /// stored copy.
    /// </summary>
    public async Task<CachedResult<FieldContext>> GetContextAsync(bool refresh, CancellationToken cancellationToken = default)
    {
        if (!refresh && await production.ReadFieldContextAsync(cancellationToken) is { } stored)
        {
            return stored;
        }

        return await production.GetFieldContextAsync(cancellationToken);
    }

    /// <summary>
    /// The signed-in user's entries that are not on the server yet, oldest date first.
    /// </summary>
    public async Task<IReadOnlyList<LocalRecording>> GetLocalListAsync(Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        if (currentUserId() is not Guid userId)
        {
            return [];
        }

        IReadOnlyList<SyncItem> queue = await db.GetQueueAsync(userId, cancellationToken);

        return
        [
            .. queue
                .Where(i => i.Kind == SyncKinds.Recording && (cycleId is null || i.CycleId == cycleId))
                .Select(i => new LocalRecording(i, Read(i)))
        ];
    }

    public async Task<LocalRecording?> FindLocalAsync(Guid id, CancellationToken cancellationToken = default)
    {
        SyncItem? item = await db.GetQueueItemAsync(id, cancellationToken);

        return item is null || item.UserId != currentUserId() ? null : new LocalRecording(item, Read(item));
    }

    /// <summary>
    /// Entries of other users kept on this device (sent when they sign in again).
    /// </summary>
    public async Task<int> CountHeldForOtherUsersAsync(CancellationToken cancellationToken = default) =>
        currentUserId() is Guid userId ? await db.CountQueueOfOtherUsersAsync(userId, cancellationToken) : 0;

    public async Task<RecordingCheck> CheckAsync(FieldContext context, RecordingDraft draft, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<LocalRecording> local = await GetLocalListAsync(cycleId: null, cancellationToken);

        return RecordingRules.Check(context, draft, [.. local.Select(l => l.Draft)], clock.Today);
    }

    /// <summary>
    /// Puts a new or edited draft in the queue and, when online, sends it right away.
    /// </summary>
    public async Task<SaveOutcome> SaveAsync(RecordingDraft draft, CancellationToken cancellationToken = default)
    {
        if (currentUserId() is not Guid userId)
        {
            return new SaveOutcome(SaveState.Rejected, "Sesi berakhir. Silakan masuk kembali.");
        }

        SyncItem? existing = await db.GetQueueItemAsync(draft.Id, cancellationToken);
        if (existing?.Status == SyncStatus.Sending)
        {
            return new SaveOutcome(SaveState.Rejected, "Data sedang dikirim. Tunggu sebentar lalu coba lagi.");
        }

        DateTime now = clock.UtcNow;
        await db.SaveQueueItemAsync(
            new SyncItem
            {
                Id = draft.Id,
                UserId = userId,
                Kind = SyncKinds.Recording,
                CycleId = draft.CycleId,
                Date = draft.Date,
                Payload = JsonSerializer.Serialize(draft, ApiClient.JsonOptions),
                Status = SyncStatus.Pending,
                CreatedAtUtc = existing?.CreatedAtUtc ?? now,
                UpdatedAtUtc = now
            },
            cancellationToken);

        return await SendNowAsync(draft.Id, cancellationToken);
    }

    /// <summary>
    /// "Kirim ulang": a failed or waiting entry goes again right now.
    /// </summary>
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
    /// Removes an entry that has not reached the server, with its photos.
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        LocalRecording? local = await FindLocalAsync(id, cancellationToken);
        if (local is null || local.Item.Status == SyncStatus.Sending)
        {
            return false;
        }

        await db.DeleteQueueItemAsync(id, cancellationToken);
        files.DeleteAll(local.Draft.Photos.Select(p => p.Id));
        sync.NotifyChanged();

        return true;
    }

    /// <summary>
    /// The recordings of a cycle: the server list (cached when offline) merged with the local entries.
    /// </summary>
    /// <param name="refresh">False: use the stored server list (after a queue change; a sync run refreshes it).</param>
    public async Task<RecordingList> GetCycleRecordingsAsync(
        Guid cycleId,
        bool refresh = true,
        CancellationToken cancellationToken = default)
    {
        CachedResult<List<DailyRecording>>? stored = refresh ? null : await production.ReadRecordingsAsync(cycleId, cancellationToken);
        CachedResult<List<DailyRecording>> server = stored is null
            ? await production.GetRecordingsAsync(cycleId, cancellationToken)
            : CachedResult<List<DailyRecording>>.Fresh(stored.Value);
        IReadOnlyList<LocalRecording> local = await GetLocalListAsync(cycleId, cancellationToken);
        FieldContext? context = (await production.ReadFieldContextAsync(cancellationToken))?.Value;

        return Merge(server, local, context);
    }

    internal static RecordingList Merge(
        CachedResult<List<DailyRecording>> server,
        IReadOnlyList<LocalRecording> local,
        FieldContext? context)
    {
        FieldCycle? cycle = local.Count == 0 ? null : context?.FindCycle(local[0].Draft.CycleId);

        IEnumerable<RecordingRow> serverRows = server.IsSuccess
            ? server.Value.Select(r => new RecordingRow(
                r.Id, r.Date, r.AgeDays, r.Mortality, r.Culling, r.FeedKg, r.AverageBodyWeightGram, RecordingState.Sent, null))
            : [];

        IEnumerable<RecordingRow> localRows = local.Select(l => new RecordingRow(
            l.Draft.Id,
            l.Draft.Date,
            cycle?.AgeOn(l.Draft.Date),
            l.Draft.Mortality,
            l.Draft.Culling,
            l.Draft.FeedKg(context),
            l.Draft.AverageBodyWeightGram,
            l.State,
            l.Item.ErrorMessage));

        return new RecordingList(
            [.. serverRows.Concat(localRows).OrderByDescending(r => r.Date)],
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

    private static RecordingDraft Read(SyncItem item) =>
        JsonSerializer.Deserialize<RecordingDraft>(item.Payload, ApiClient.JsonOptions)
        ?? new RecordingDraft { Id = item.Id, CycleId = item.CycleId, Date = item.Date };
}

public enum RecordingState
{
    /// <summary>
    /// On the server.
    /// </summary>
    Sent,

    /// <summary>
    /// Waiting in the queue ("Belum terkirim").
    /// </summary>
    Pending,

    Sending,

    /// <summary>
    /// Rejected by the server ("Gagal").
    /// </summary>
    Failed
}

/// <summary>
/// A recording in the sync queue.
/// </summary>
public sealed record LocalRecording(SyncItem Item, RecordingDraft Draft)
{
    public RecordingState State => Item.Status switch
    {
        SyncStatus.Sending => RecordingState.Sending,
        SyncStatus.Failed => RecordingState.Failed,
        _ => RecordingState.Pending
    };
}

/// <param name="AgeDays">Null for a local entry when the cycle is not in the stored field context.</param>
/// <param name="Error">Why a local entry failed or is waiting.</param>
public sealed record RecordingRow(
    Guid Id,
    DateOnly Date,
    int? AgeDays,
    int Mortality,
    int Culling,
    decimal FeedKg,
    decimal? AverageBodyWeightGram,
    RecordingState State,
    string? Error)
{
    public bool IsLocal => State != RecordingState.Sent;
}

/// <param name="ServerError">Set when the server list could not be loaded (nothing cached either).</param>
/// <param name="CachedAtUtc">Set when the server list came from the cache.</param>
public sealed record RecordingList(IReadOnlyList<RecordingRow> Rows, ApiError? ServerError, DateTime? CachedAtUtc);

public enum SaveState
{
    /// <summary>
    /// Accepted by the server.
    /// </summary>
    Sent,

    /// <summary>
    /// In the queue, sent when the connection allows.
    /// </summary>
    Queued,

    /// <summary>
    /// Rejected by the server; stays in the queue as Failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Not saved.
    /// </summary>
    Rejected
}

public sealed record SaveOutcome(SaveState State, string? Message);
