using MobileApp.Core.Api;
using MobileApp.Core.Contracts;
using MobileApp.Core.Local;
using MobileApp.Core.Production;
using MobileApp.Core.Sync;

namespace MobileApp.UnitTests.Production;

/// <summary>
/// Saving recordings on the device and listing them with the server's (PLAN-MOBILE M2, M-43).
/// </summary>
public sealed class RecordingServiceTests : IDisposable
{
    private static readonly Guid User = Guid.Parse("0199c3a0-0000-7000-8000-0000000000bb");

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"intiplasma-rec-{Guid.NewGuid():N}.db3");
    private readonly string _filesPath = Path.Combine(Path.GetTempPath(), $"intiplasma-rec-files-{Guid.NewGuid():N}");
    private readonly TestClock _clock = new();
    private readonly TestConnectivity _connectivity = new();
    private readonly List<IDisposable> _resources = [];
    private readonly List<DailyRecording> _serverRecordings = [];

    [Fact]
    public async Task SaveOffline_Should_QueueTheEntry_AndListItAsNotSent()
    {
        // Arrange
        RecordingService service = Create();
        _connectivity.IsOnline = false;
        RecordingDraft draft = Sample.Draft();

        // Act
        SaveOutcome outcome = await service.SaveAsync(draft);

        // Assert
        outcome.State.ShouldBe(SaveState.Queued);
        LocalRecording local = (await service.GetLocalListAsync(Sample.CycleId)).ShouldHaveSingleItem();
        local.State.ShouldBe(RecordingState.Pending);
        local.Draft.Id.ShouldBe(draft.Id);
        local.Draft.Date.ShouldBe(draft.Date);
        local.Draft.Usages.ShouldBe(draft.Usages);
    }

    [Fact]
    public async Task SaveOnline_Should_SendRightAway()
    {
        // Arrange
        RecordingService service = Create();

        // Act
        SaveOutcome outcome = await service.SaveAsync(Sample.Draft());

        // Assert
        outcome.State.ShouldBe(SaveState.Sent);
        (await service.GetLocalListAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Edit_Should_KeepTheIdAndReplaceTheValues()
    {
        // Arrange
        RecordingService service = Create();
        _connectivity.IsOnline = false;
        RecordingDraft draft = Sample.Draft();
        await service.SaveAsync(draft);

        // Act
        await service.SaveAsync(draft with { Mortality = 7 });

        // Assert
        LocalRecording local = (await service.GetLocalListAsync()).ShouldHaveSingleItem();
        local.Draft.Id.ShouldBe(draft.Id);
        local.Draft.Mortality.ShouldBe(7);
    }

    [Fact]
    public async Task Delete_Should_RemoveTheEntryAndItsPhotos()
    {
        // Arrange
        RecordingService service = Create();
        _connectivity.IsOnline = false;
        var photo = Guid.CreateVersion7();
        await File.WriteAllBytesAsync(service.Files.Prepare(photo), [1, 2, 3]);
        RecordingDraft draft = Sample.Draft() with { Photos = [new DraftPhoto(photo)] };
        await service.SaveAsync(draft);

        // Act
        bool deleted = await service.DeleteAsync(draft.Id);

        // Assert
        deleted.ShouldBeTrue();
        (await service.GetLocalListAsync()).ShouldBeEmpty();
        service.Files.Exists(photo).ShouldBeFalse();
    }

    [Fact]
    public async Task CycleList_Should_MergeServerAndLocalEntries_NewestFirst()
    {
        // Arrange
        RecordingService service = Create();
        _serverRecordings.Add(new DailyRecording
        {
            Id = Guid.NewGuid(),
            CycleId = Sample.CycleId,
            Date = new DateOnly(2026, 10, 8),
            AgeDays = 7,
            Mortality = 1,
            FeedKg = 40
        });
        await service.GetContextAsync(refresh: true);
        _connectivity.IsOnline = false;
        await service.SaveAsync(Sample.Draft());
        _connectivity.IsOnline = true;

        // Act
        RecordingList list = await service.GetCycleRecordingsAsync(Sample.CycleId);

        // Assert
        list.Rows.Select(r => (r.Date.Day, r.State)).ShouldBe([(9, RecordingState.Pending), (8, RecordingState.Sent)]);
        RecordingRow local = list.Rows[0];
        local.AgeDays.ShouldBe(8);
        local.FeedKg.ShouldBe(50m);
        local.IsLocal.ShouldBeTrue();
    }

    [Fact]
    public async Task Check_Should_CountTheQueuedEntries()
    {
        // Arrange
        RecordingService service = Create();
        _connectivity.IsOnline = false;
        await service.SaveAsync(Sample.Draft());

        // Act
        RecordingCheck check = await service.CheckAsync(Sample.Context(), Sample.Draft());

        // Assert
        check.ErrorFor(RecordingRules.Date).ShouldNotBeNull();
    }

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

    private RecordingService Create()
    {
        var api = new FakeApi(Respond);
        _resources.Add(api);
        var db = new LocalDb(_dbPath);
        _resources.Add(db);
        var files = new PendingFiles(_filesPath);
        var production = new ProductionApi(new ApiCache(api.Client, db));
        var engine = new SyncEngine(api.Client, production, db, files, _clock, _connectivity, () => User);
        _resources.Add(engine);

        return new RecordingService(production, db, engine, files, _clock, _connectivity, () => User);
    }

    private HttpResponseMessage Respond(HttpRequestMessage request, string? body)
    {
        if (!_connectivity.IsOnline)
        {
            return StubHttpHandler.Offline(request, body);
        }

        string path = request.RequestUri!.AbsolutePath;

        if (path.EndsWith("/field-context", StringComparison.Ordinal))
        {
            return StubHttpHandler.Json(Sample.Context());
        }

        if (path.EndsWith("/daily-recordings", StringComparison.Ordinal) && request.Method == HttpMethod.Post)
        {
            return StubHttpHandler.Json(Guid.Parse(request.Headers.GetValues(ApiClient.IdempotencyKeyHeader).Single()));
        }

        return StubHttpHandler.Json(_serverRecordings);
    }
}
