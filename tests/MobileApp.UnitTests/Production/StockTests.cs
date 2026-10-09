using System.Text.Json;
using MobileApp.Core.Api;
using MobileApp.Core.Contracts;
using MobileApp.Core.Local;
using MobileApp.Core.Production;
using MobileApp.Core.Sync;

namespace MobileApp.UnitTests.Production;

/// <summary>
/// Stok ayam harian on the device (PLAN-MOBILE M3): checks, one queued entry per range, merging with the server list,
/// copying the previous report and the sending order recording → stock.
/// </summary>
public sealed class StockTests : IDisposable
{
    private static readonly Guid User = Guid.Parse("0199c3a0-0000-7000-8000-0000000000cc");
    private static readonly DateOnly Today = new(2026, 10, 9);

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"intiplasma-stock-{Guid.NewGuid():N}.db3");
    private readonly string _filesPath = Path.Combine(Path.GetTempPath(), $"intiplasma-stock-files-{Guid.NewGuid():N}");
    private readonly TestClock _clock = new();
    private readonly TestConnectivity _connectivity = new() { IsOnline = false };
    private readonly List<IDisposable> _resources = [];
    private readonly List<LiveBirdStockEntry> _server = [];

    [Fact]
    public void Check_Should_KeepTheAverageInTheRange()
    {
        RecordingCheck check = StockRules.Check(Sample.Context(), Draft(Sample.Medium, 100, 190m), [], [], Today);

        check.ErrorFor(StockRules.Figures).ShouldBe("Rata-rata 1,900 kg di luar rentang 1,4 – 1,8 kg.");
    }

    [Fact]
    public void Check_Should_CountOtherRanges_AndQueuedDepletion_AgainstThePopulation()
    {
        // 1.000 birds, 10 dead in a recording still in the queue, 600 already reported in the large range.
        StockRow large = new(Guid.NewGuid(), Guid.NewGuid(), Today, Sample.Large, "L", 600, 1_200m, null, RecordingState.Sent, null);
        RecordingDraft queued = Sample.Draft() with { Mortality = 10, Culling = 0 };

        RecordingCheck tooMany = StockRules.Check(Sample.Context(), Draft(Sample.Medium, 391, 600m), [large], [queued], Today);
        RecordingCheck fits = StockRules.Check(Sample.Context(), Draft(Sample.Medium, 390, 600m), [large], [queued], Today);

        tooMany.ErrorFor(StockRules.Figures).ShouldBe("Total ekor semua rentang (991) melebihi populasi berjalan (990).");
        fits.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Check_Should_IgnoreTheRowOfTheSameRange_BecauseItIsReplaced()
    {
        StockRow sameRange = new(Guid.NewGuid(), Guid.NewGuid(), Today, Sample.Medium, "M", 900, 1_400m, null, RecordingState.Sent, null);

        StockRules.Check(Sample.Context(), Draft(Sample.Medium, 1_000, 1_600m), [sameRange], [], Today).IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Save_Should_ReplaceTheQueuedEntryOfTheSameRange()
    {
        StockService service = Create();
        StockDraft first = Draft(Sample.Medium, 100, 160m);

        await service.SaveAsync(first);
        await service.SaveAsync(Draft(Sample.Medium, 200, 320m));
        await service.SaveAsync(Draft(Sample.Large, 50, 100m));

        IReadOnlyList<LocalStock> queued = await service.GetLocalListAsync(Sample.CycleId);
        queued.Count.ShouldBe(2);
        LocalStock medium = queued.Single(q => q.Draft.WeightRangeId == Sample.Medium);
        medium.Draft.Id.ShouldBe(first.Id);
        medium.Draft.Birds.ShouldBe(200);
    }

    [Fact]
    public async Task Rows_Should_ShowTheQueuedValue_InPlaceOfTheServerEntry()
    {
        var serverId = Guid.NewGuid();
        _server.Add(Entry(serverId, Today, Sample.Medium, 100, 160m));
        _server.Add(Entry(Guid.NewGuid(), Today.AddDays(-1), Sample.Medium, 120, 180m));
        StockService service = Create();
        await service.SaveAsync(Draft(Sample.Medium, 150, 240m));

        _connectivity.IsOnline = true;
        StockList list = await service.GetCycleRowsAsync(Sample.CycleId);

        StockRow today = list.On(Today).ShouldHaveSingleItem();
        today.Birds.ShouldBe(150);
        today.ServerId.ShouldBe(serverId);
        today.State.ShouldBe(RecordingState.Pending);
        (DateOnly? previous, IReadOnlyList<StockRow> rows) = StockService.Previous(list, Today);
        previous.ShouldBe(Today.AddDays(-1));
        rows.ShouldHaveSingleItem().Birds.ShouldBe(120);
    }

    [Fact]
    public void SendingOrder_Should_PutTheRecordingBeforeTheStockOfTheSameDate()
    {
        SyncItem stockToday = Item(SyncKinds.LiveBirdStock, Today, minute: 1);
        SyncItem recordingToday = Item(SyncKinds.Recording, Today, minute: 2);
        SyncItem stockYesterday = Item(SyncKinds.LiveBirdStock, Today.AddDays(-1), minute: 3);

        SyncEngine.SendingOrder([stockToday, recordingToday, stockYesterday])
            .ShouldBe([stockYesterday, recordingToday, stockToday]);
    }

    public void Dispose()
    {
        foreach (IDisposable resource in _resources)
        {
            resource.Dispose();
        }

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
    }

    private static StockDraft Draft(Guid range, int birds, decimal weightKg) => new()
    {
        CycleId = Sample.CycleId,
        CoopName = "Kandang Sukamaju",
        Date = Today,
        WeightRangeId = range,
        WeightRangeCode = range == Sample.Medium ? "M" : "L",
        Birds = birds,
        WeightKg = weightKg
    };

    private static LiveBirdStockEntry Entry(Guid id, DateOnly date, Guid range, int birds, decimal weightKg) => new()
    {
        Id = id,
        CycleId = Sample.CycleId,
        Date = date,
        WeightRangeId = range,
        WeightRangeCode = "M",
        Birds = birds,
        WeightKg = weightKg
    };

    private static SyncItem Item(string kind, DateOnly date, int minute) => new()
    {
        Id = Guid.NewGuid(),
        Kind = kind,
        Date = date,
        Payload = JsonSerializer.Serialize(new { }),
        CreatedAtUtc = new DateTime(2026, 10, 9, 1, minute, 0, DateTimeKind.Utc)
    };

    private StockService Create()
    {
        var api = new FakeApi(Respond);
        _resources.Add(api);
        var db = new LocalDb(_dbPath);
        _resources.Add(db);
        var files = new PendingFiles(_filesPath);
        var production = new ProductionApi(new ApiCache(api.Client, db), api.Client);
        var engine = new SyncEngine(api.Client, production, db, files, _clock, _connectivity, () => User);
        _resources.Add(engine);
        var recordings = new RecordingService(production, db, engine, files, _clock, _connectivity, () => User);

        return new StockService(production, db, engine, recordings, _clock, _connectivity, () => User);
    }

    private HttpResponseMessage Respond(HttpRequestMessage request, string? body)
    {
        if (!_connectivity.IsOnline)
        {
            return StubHttpHandler.Offline(request, body);
        }

        return StubHttpHandler.Json(_server);
    }
}
