using System.Globalization;
using System.Text.Json;
using MobileApp.Core.Abstractions;
using MobileApp.Core.Api;
using MobileApp.Core.Contracts;
using MobileApp.Core.Local;
using MobileApp.Core.Production;

namespace MobileApp.Core.Sync;

/// <summary>
/// Sends the offline queue of the signed-in user (PLAN-MOBILE §3.6 steps 4–5, M-42). One run at a time; entries go
/// oldest date first, photos before the recording that refers to them, each with its own id so a resend after a lost
/// answer does not create duplicates.
/// <list type="bullet">
/// <item>accepted → removed from the queue (and its photos from the device);</item>
/// <item>no connection, server error or rate limit → stays Pending and is retried later (backoff), the run stops;</item>
/// <item>session ended → stays Pending until the user signs in again, the run stops;</item>
/// <item>rejected (400/403/404/409) → Failed with the reason; the user edits or deletes it. Other entries go on.</item>
/// </list>
/// </summary>
public sealed class SyncEngine : IDisposable
{
    public const string RecordingsPath = "production/daily-recordings";

    public const string AttachmentsPath = "attachments";

    public const string PhotoMissingCode = "Client.PhotoMissing";

    /// <summary>
    /// How often a started engine looks for entries that are due again.
    /// </summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan FirstRetry = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LastRetry = TimeSpan.FromMinutes(5);

    private readonly ApiClient _api;
    private readonly ProductionApi _production;
    private readonly LocalDb _db;
    private readonly PendingFiles _files;
    private readonly IClock _clock;
    private readonly IConnectivity _connectivity;
    private readonly Func<Guid?> _currentUserId;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _timer;

    /// <param name="currentUserId">The signed-in user, or null (nothing is sent then).</param>
    public SyncEngine(
        ApiClient api,
        ProductionApi production,
        LocalDb db,
        PendingFiles files,
        IClock clock,
        IConnectivity connectivity,
        Func<Guid?> currentUserId)
    {
        _api = api;
        _production = production;
        _db = db;
        _files = files;
        _clock = clock;
        _connectivity = connectivity;
        _currentUserId = currentUserId;
    }

    /// <summary>
    /// The queue changed (an entry was sent, failed or is being sent); may be raised on a background thread.
    /// </summary>
    public event EventHandler? Changed;

    public bool IsRunning => _gate.CurrentCount == 0;

    /// <summary>
    /// Delay before the next attempt after <paramref name="attempts"/> failed ones: 30 s, 1, 2, 4 then 5 minutes.
    /// </summary>
    public static TimeSpan Backoff(int attempts)
    {
        double seconds = FirstRetry.TotalSeconds * Math.Pow(2, Math.Max(attempts - 1, 0));

        return seconds >= LastRetry.TotalSeconds ? LastRetry : TimeSpan.FromSeconds(seconds);
    }

    /// <summary>
    /// Sends again when the connection comes back and checks every <see cref="Interval"/> for entries that are due.
    /// </summary>
    public void Start()
    {
        if (_timer is not null)
        {
            return;
        }

        _timer = new CancellationTokenSource();
        _connectivity.Changed += OnConnectivityChanged;
        _ = TickAsync(_timer.Token);
    }

    /// <summary>
    /// Sends what is due now.
    /// </summary>
    /// <param name="onlyId">Only this entry, even when its retry time has not come yet ("Kirim ulang", save).</param>
    public async Task<SyncRun> RunAsync(Guid? onlyId = null, CancellationToken cancellationToken = default)
    {
        if (_currentUserId() is not Guid userId || !_connectivity.IsOnline)
        {
            return SyncRun.Skipped;
        }

        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            return SyncRun.Skipped;
        }

        try
        {
            int sent = 0;
            int failed = 0;
            var cycles = new HashSet<Guid>();
            DateTime now = _clock.UtcNow;

            foreach (SyncItem item in await _db.GetQueueAsync(userId, cancellationToken))
            {
                if (onlyId is not null && item.Id != onlyId)
                {
                    continue;
                }

                // Failed entries wait for the user; Sending ones were cut off (app closed) and go again.
                bool due = item.Status == SyncStatus.Sending
                    || item.Status == SyncStatus.Pending && (onlyId is not null || item.NextAttemptAtUtc is null || item.NextAttemptAtUtc <= now);
                if (!due)
                {
                    continue;
                }

                Step step = await SendAsync(item, cancellationToken);
                if (step == Step.Sent)
                {
                    sent++;
                    cycles.Add(item.CycleId);
                }
                else if (step == Step.Failed)
                {
                    failed++;
                }
                else
                {
                    break;
                }
            }

            if (sent > 0)
            {
                // Population, stock and recorded dates changed on the server.
                await _production.GetFieldContextAsync(cancellationToken);
                foreach (Guid cycleId in cycles)
                {
                    await _production.GetRecordingsAsync(cycleId, cancellationToken);
                }
            }

            return new SyncRun(sent, failed);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_timer is not null)
        {
            _connectivity.Changed -= OnConnectivityChanged;
            _timer.Cancel();
            _timer.Dispose();
            _timer = null;
        }

        _gate.Dispose();
    }

    private async Task<Step> SendAsync(SyncItem item, CancellationToken cancellationToken)
    {
        await SaveAsync(item with { Status = SyncStatus.Sending }, cancellationToken);

        RecordingDraft? draft = JsonSerializer.Deserialize<RecordingDraft>(item.Payload, ApiClient.JsonOptions);
        if (draft is null)
        {
            return await FailAsync(item, ApiError.UnexpectedCode, "Data antrean rusak; hapus lalu input ulang.", cancellationToken);
        }

        foreach ((DraftPhoto photo, int number) in draft.Photos.Select((p, i) => (p, i + 1)).Where(p => !p.p.Uploaded))
        {
            byte[]? content = await _files.ReadAsync(photo.Id, cancellationToken);
            if (content is null)
            {
                return await FailAsync(item, PhotoMissingCode, "Foto tidak ditemukan di perangkat. Hapus foto itu lalu kirim ulang.", cancellationToken);
            }

            (string contentType, string extension) = ImageType(content);
            ApiResult<Attachment> upload = await _api.PostFileAsync<Attachment>(
                AttachmentsPath, content, FileName(draft, number, extension), contentType, photo.Id, cancellationToken);
            if (!upload.IsSuccess)
            {
                return await HandleErrorAsync(item, upload.Error!, cancellationToken);
            }

            draft = draft with { Photos = [.. draft.Photos.Select(p => p.Id == photo.Id ? p with { Uploaded = true } : p)] };
            item = item with { Payload = JsonSerializer.Serialize(draft, ApiClient.JsonOptions) };
            await SaveAsync(item, cancellationToken);
        }

        ApiResult<Guid> created = await _api.PostAsync<Guid>(RecordingsPath, draft.ToRequest(), draft.Id, cancellationToken);
        if (!created.IsSuccess)
        {
            return await HandleErrorAsync(item, created.Error!, cancellationToken);
        }

        await _db.DeleteQueueItemAsync(item.Id, cancellationToken);
        _files.DeleteAll(draft.Photos.Select(p => p.Id));
        Changed?.Invoke(this, EventArgs.Empty);

        return Step.Sent;
    }

    private async Task<Step> HandleErrorAsync(SyncItem item, ApiError error, CancellationToken cancellationToken)
    {
        if (error.IsSessionExpired)
        {
            await SaveAsync(item with { Status = SyncStatus.Pending }, cancellationToken);

            return Step.Stop;
        }

        if (error.IsNetwork || error.Status is >= 500 or 429)
        {
            int attempts = item.Attempts + 1;
            await SaveAsync(
                item with
                {
                    Status = SyncStatus.Pending,
                    Attempts = attempts,
                    NextAttemptAtUtc = _clock.UtcNow + Backoff(attempts),
                    ErrorCode = error.Code,
                    ErrorMessage = error.Message
                },
                cancellationToken);

            return Step.Stop;
        }

        string message = error.Code == "Cycles.NotFound"
            ? "Kandang sudah tidak menjadi tanggung jawab Anda atau siklusnya tidak ditemukan."
            : error.Message;

        return await FailAsync(item, error.Code, message, cancellationToken);
    }

    private async Task<Step> FailAsync(SyncItem item, string code, string message, CancellationToken cancellationToken)
    {
        await SaveAsync(
            item with { Status = SyncStatus.Failed, Attempts = item.Attempts + 1, ErrorCode = code, ErrorMessage = message },
            cancellationToken);

        return Step.Failed;
    }

    private async Task SaveAsync(SyncItem item, CancellationToken cancellationToken)
    {
        await _db.SaveQueueItemAsync(item with { UpdatedAtUtc = _clock.UtcNow }, cancellationToken);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Tells listeners (queue badge) that the queue changed outside a run, e.g. an entry was saved offline.
    /// </summary>
    public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// <see cref="RunAsync"/> for triggers that cannot handle failures (app resumed, signed in): never throws.
    /// </summary>
    public async Task TryRunAsync()
    {
        try
        {
            await RunAsync();
        }
#pragma warning disable CA1031 // A background run must never bring the app down; the entry stays in the queue.
        catch (Exception)
#pragma warning restore CA1031
        {
            // The next trigger tries again.
        }
    }

    /// <summary>
    /// Readable name of a photo in the attachment list, e.g. <c>recording-20261009-1.jpg</c>.
    /// </summary>
    internal static string FileName(RecordingDraft draft, int number, string extension) =>
        string.Create(CultureInfo.InvariantCulture, $"recording-{draft.Date:yyyyMMdd}-{number}{extension}");

    /// <summary>
    /// The photo picker writes JPEG; a PNG or WEBP from the gallery may stay as it is.
    /// </summary>
    internal static (string ContentType, string Extension) ImageType(byte[] content) => content switch
    {
        [0x89, 0x50, 0x4E, 0x47, ..] => ("image/png", ".png"),
        [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50, ..] => ("image/webp", ".webp"),
        _ => ("image/jpeg", ".jpg")
    };

    private void OnConnectivityChanged(object? sender, EventArgs e)
    {
        if (_connectivity.IsOnline)
        {
            _ = TryRunAsync();
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(Interval);

        try
        {
            do
            {
                await TryRunAsync();
            }
            while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            // Stopped.
        }
    }

    private enum Step
    {
        Sent,
        Failed,
        Stop
    }
}

/// <param name="Sent">Entries the server accepted in this run.</param>
/// <param name="Failed">Entries the server rejected in this run.</param>
public sealed record SyncRun(int Sent, int Failed)
{
    public static readonly SyncRun Skipped = new(0, 0);
}
