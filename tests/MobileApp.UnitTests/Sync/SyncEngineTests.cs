using System.Net;
using System.Text.Json;
using MobileApp.Core.Api;
using MobileApp.Core.Contracts;
using MobileApp.Core.Local;
using MobileApp.Core.Production;
using MobileApp.Core.Sync;
using MobileApp.UnitTests.Production;

namespace MobileApp.UnitTests.Sync;

/// <summary>
/// The offline queue (PLAN-MOBILE §3.6, M-42, M-43): order, ids, retries and how server answers are classified.
/// </summary>
public sealed class SyncEngineTests : IDisposable
{
    private static readonly Guid User = Guid.Parse("0199c3a0-0000-7000-8000-0000000000aa");

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"intiplasma-sync-{Guid.NewGuid():N}.db3");
    private readonly string _filesPath = Path.Combine(Path.GetTempPath(), $"intiplasma-files-{Guid.NewGuid():N}");
    private readonly TestClock _clock = new();
    private readonly TestConnectivity _connectivity = new();
    private readonly List<IDisposable> _resources = [];

    /// <summary>
    /// Answer of the server to <c>POST production/daily-recordings</c>, per recording id.
    /// </summary>
    private readonly Dictionary<Guid, Func<HttpResponseMessage>> _recordingAnswers = [];

    private bool _online = true;

    [Fact]
    public async Task Run_Should_UploadPhotosFirst_ThenTheRecording_WithTheClientIds()
    {
        // Arrange
        (SyncEngine engine, LocalDb db, PendingFiles files, FakeApi api) = Create();
        var photo = Guid.CreateVersion7();
        await File.WriteAllBytesAsync(files.Prepare(photo), [0xFF, 0xD8, 0xFF]);
        RecordingDraft draft = Sample.Draft() with { Photos = [new DraftPhoto(photo)] };
        await EnqueueAsync(db, draft);

        // Act
        SyncRun run = await engine.RunAsync();

        // Assert
        run.ShouldBe(new SyncRun(1, 0));
        var posts = api.Api.Requests.Where(r => r.Method == HttpMethod.Post).ToList();
        posts.Select(r => r.Path).ShouldBe(["/api/v1/attachments", "/api/v1/production/daily-recordings"]);
        posts[0].Body!.ShouldContain(photo.ToString());
        posts[1].IdempotencyKey.ShouldBe(draft.Id.ToString());
        posts[1].Body!.ShouldContain($"\"id\":\"{draft.Id}\"");
        posts[1].Body!.ShouldContain($"\"documents\":[\"{photo}\"]");

        (await db.GetQueueAsync(User)).ShouldBeEmpty();
        files.Exists(photo).ShouldBeFalse();
        // The field context is reloaded after a successful send.
        api.Api.Requests.ShouldContain(r => r.Path == "/api/v1/mobile/field-context");
    }

    [Fact]
    public async Task Run_Should_SendOldestDateFirst()
    {
        // Arrange
        (SyncEngine engine, LocalDb db, _, FakeApi api) = Create();
        RecordingDraft later = Sample.Draft();
        RecordingDraft earlier = Sample.Draft() with { Date = new DateOnly(2026, 10, 6) };
        await EnqueueAsync(db, later);
        await EnqueueAsync(db, earlier);

        // Act
        await engine.RunAsync();

        // Assert
        api.Api.Requests.Where(r => r.Method == HttpMethod.Post).Select(r => r.IdempotencyKey)
            .ShouldBe([earlier.Id.ToString(), later.Id.ToString()]);
    }

    [Fact]
    public async Task NetworkFailure_Should_KeepTheEntryPending_UntilItsRetryTime()
    {
        // Arrange
        (SyncEngine engine, LocalDb db, _, FakeApi api) = Create();
        RecordingDraft draft = Sample.Draft();
        await EnqueueAsync(db, draft);
        _online = false;

        // Act
        await engine.RunAsync();

        // Assert
        SyncItem item = (await db.GetQueueItemAsync(draft.Id))!;
        item.Status.ShouldBe(SyncStatus.Pending);
        item.Attempts.ShouldBe(1);
        item.NextAttemptAtUtc.ShouldBe(_clock.UtcNow.AddSeconds(30));

        // Not due yet: nothing is sent even though the server is back.
        _online = true;
        int before = api.Api.Requests.Count;
        (await engine.RunAsync()).Sent.ShouldBe(0);
        api.Api.Requests.Count.ShouldBe(before);

        // Due after the backoff.
        _clock.UtcNow = _clock.UtcNow.AddSeconds(31);
        (await engine.RunAsync()).Sent.ShouldBe(1);
    }

    [Fact]
    public async Task Rejection_Should_MarkTheEntryFailed_AndGoOnWithTheNextOne()
    {
        // Arrange
        (SyncEngine engine, LocalDb db, _, _) = Create();
        RecordingDraft rejected = Sample.Draft() with { Date = new DateOnly(2026, 10, 6) };
        RecordingDraft next = Sample.Draft();
        _recordingAnswers[rejected.Id] = () => StubHttpHandler.Problem(
            HttpStatusCode.Conflict, "DailyRecordings.AlreadyRecorded", "The cycle already has a recording for 2026-10-06");
        await EnqueueAsync(db, rejected);
        await EnqueueAsync(db, next);

        // Act
        SyncRun run = await engine.RunAsync();

        // Assert
        run.ShouldBe(new SyncRun(1, 1));
        SyncItem item = (await db.GetQueueAsync(User)).ShouldHaveSingleItem();
        item.Id.ShouldBe(rejected.Id);
        item.Status.ShouldBe(SyncStatus.Failed);
        item.ErrorCode.ShouldBe("DailyRecordings.AlreadyRecorded");
        item.ErrorMessage.ShouldBe("Recording untuk tanggal ini sudah ada. Ubah tanggalnya atau hapus entri ini.");

        // A failed entry waits for the user.
        (await engine.RunAsync()).ShouldBe(new SyncRun(0, 0));
    }

    [Fact]
    public async Task CycleNoLongerInScope_Should_ExplainTheReassignment()
    {
        // Arrange
        (SyncEngine engine, LocalDb db, _, _) = Create();
        RecordingDraft draft = Sample.Draft();
        _recordingAnswers[draft.Id] = () => StubHttpHandler.Problem(HttpStatusCode.NotFound, "Cycles.NotFound", "Not found");
        await EnqueueAsync(db, draft);

        // Act
        await engine.RunAsync();

        // Assert
        (await db.GetQueueItemAsync(draft.Id))!.ErrorMessage
            .ShouldBe("Kandang sudah tidak menjadi tanggung jawab Anda atau siklusnya tidak ditemukan.");
    }

    [Fact]
    public async Task UploadedPhoto_Should_NotBeSentAgain_WhenTheRecordingIsRetried()
    {
        // Arrange: the photo goes up, then the server fails on the recording.
        (SyncEngine engine, LocalDb db, PendingFiles files, FakeApi api) = Create();
        var photo = Guid.CreateVersion7();
        await File.WriteAllBytesAsync(files.Prepare(photo), [0xFF, 0xD8, 0xFF]);
        RecordingDraft draft = Sample.Draft() with { Photos = [new DraftPhoto(photo)] };
        _recordingAnswers[draft.Id] = () => StubHttpHandler.Problem(HttpStatusCode.ServiceUnavailable, "Server.Failure", "Down");
        await EnqueueAsync(db, draft);
        await engine.RunAsync();

        // Act
        _recordingAnswers.Remove(draft.Id);
        await engine.RunAsync(draft.Id);

        // Assert
        api.Api.Requests.Count(r => r.Path == "/api/v1/attachments").ShouldBe(1);
        api.Api.Requests.Count(r => r.Method == HttpMethod.Post && r.Path == "/api/v1/production/daily-recordings").ShouldBe(2);
        (await db.GetQueueAsync(User)).ShouldBeEmpty();
    }

    [Fact]
    public async Task EntriesOfAnotherUser_Should_BeHeld()
    {
        // Arrange
        (SyncEngine engine, LocalDb db, _, FakeApi api) = Create();
        await EnqueueAsync(db, Sample.Draft(), userId: Guid.NewGuid());

        // Act
        SyncRun run = await engine.RunAsync();

        // Assert
        run.Sent.ShouldBe(0);
        api.Api.Requests.ShouldBeEmpty();
        (await db.CountQueueOfOtherUsersAsync(User)).ShouldBe(1);
    }

    [Fact]
    public async Task Offline_Should_NotTryAtAll()
    {
        (SyncEngine engine, LocalDb db, _, FakeApi api) = Create();
        await EnqueueAsync(db, Sample.Draft());
        _connectivity.IsOnline = false;

        (await engine.RunAsync()).ShouldBe(SyncRun.Skipped);
        api.Api.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(1, 30)]
    [InlineData(2, 60)]
    [InlineData(4, 240)]
    [InlineData(5, 300)]
    [InlineData(20, 300)]
    public void Backoff_Should_DoubleUpToFiveMinutes(int attempts, int seconds) =>
        SyncEngine.Backoff(attempts).ShouldBe(TimeSpan.FromSeconds(seconds));

    public void Dispose()
    {
        foreach (IDisposable resource in _resources)
        {
            resource.Dispose();
        }

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
        if (Directory.Exists(_filesPath))
        {
            Directory.Delete(_filesPath, recursive: true);
        }
    }

    private (SyncEngine Engine, LocalDb Db, PendingFiles Files, FakeApi Api) Create()
    {
        var api = new FakeApi(Respond);
        _resources.Add(api);
        var db = new LocalDb(_dbPath);
        _resources.Add(db);
        var files = new PendingFiles(_filesPath);
        var production = new ProductionApi(new ApiCache(api.Client, db), api.Client);
        var engine = new SyncEngine(api.Client, production, db, files, _clock, _connectivity, () => User);
        _resources.Add(engine);

        return (engine, db, files, api);
    }

    private async Task EnqueueAsync(LocalDb db, RecordingDraft draft, Guid? userId = null) =>
        await db.SaveQueueItemAsync(new SyncItem
        {
            Id = draft.Id,
            UserId = userId ?? User,
            CycleId = draft.CycleId,
            Date = draft.Date,
            Payload = JsonSerializer.Serialize(draft, ApiClient.JsonOptions),
            Status = SyncStatus.Pending,
            CreatedAtUtc = _clock.UtcNow,
            UpdatedAtUtc = _clock.UtcNow
        });

    private HttpResponseMessage Respond(HttpRequestMessage request, string? body)
    {
        if (!_online)
        {
            return StubHttpHandler.Offline(request, body);
        }

        string path = request.RequestUri!.AbsolutePath;

        if (path.EndsWith("/attachments", StringComparison.Ordinal))
        {
            return StubHttpHandler.Json(new { id = Guid.NewGuid(), fileName = "photo.jpg", contentType = "image/jpeg" });
        }

        if (path.EndsWith("/daily-recordings", StringComparison.Ordinal) && request.Method == HttpMethod.Post)
        {
            var id = Guid.Parse(request.Headers.GetValues(ApiClient.IdempotencyKeyHeader).Single());

            if (_recordingAnswers.TryGetValue(id, out Func<HttpResponseMessage>? answer))
            {
                return answer();
            }

            return StubHttpHandler.Json(id);
        }

        if (path.EndsWith("/field-context", StringComparison.Ordinal))
        {
            return StubHttpHandler.Json(Sample.Context());
        }

        return StubHttpHandler.Json(Array.Empty<DailyRecording>());
    }
}
