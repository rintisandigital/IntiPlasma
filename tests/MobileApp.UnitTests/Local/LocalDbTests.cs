using MobileApp.Core.Local;

namespace MobileApp.UnitTests.Local;

public sealed class LocalDbTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"intiplasma-mobile-{Guid.NewGuid():N}.db3");

    [Fact]
    public async Task InitializeAsync_Should_ApplyEveryMigrationOnce()
    {
        // Arrange
        using var db = new LocalDb(_path);

        // Act
        await db.InitializeAsync();
        await db.InitializeAsync();

        using var reopened = new LocalDb(_path);
        await reopened.InitializeAsync();

        // Assert
        (await reopened.GetSchemaVersionAsync()).ShouldBe(LocalDb.Migrations.Count);
    }

    [Fact]
    public async Task Values_Should_BeStoredAndReplaced()
    {
        // Arrange
        using var db = new LocalDb(_path);

        // Act
        await db.SetValueAsync("current_user", "first");
        await db.SetValueAsync("current_user", "second");

        // Assert
        (await db.GetValueAsync("current_user")).ShouldBe("second");
        (await db.GetValueAsync("missing")).ShouldBeNull();
    }

    [Fact]
    public async Task ClearUserDataAsync_Should_RemoveTheSessionValues()
    {
        // Arrange
        using var db = new LocalDb(_path);
        await db.SetValueAsync("current_user", "someone");

        // Act
        await db.ClearUserDataAsync();

        // Assert
        (await db.GetValueAsync("current_user")).ShouldBeNull();
    }

    [Fact]
    public async Task QueueItems_Should_RoundTrip_InSendingOrder_AndSurviveLogout()
    {
        // Arrange
        using var db = new LocalDb(_path);
        var user = Guid.NewGuid();
        var now = new DateTime(2026, 10, 9, 3, 0, 0, DateTimeKind.Utc);
        SyncItem later = Item(user, new DateOnly(2026, 10, 9), now);
        SyncItem earlier = Item(user, new DateOnly(2026, 10, 8), now) with
        {
            Status = SyncStatus.Failed,
            Attempts = 2,
            NextAttemptAtUtc = now.AddMinutes(1),
            ErrorCode = "Stock.Insufficient",
            ErrorMessage = "Stok tidak cukup"
        };

        // Act
        await db.SaveQueueItemAsync(later);
        await db.SaveQueueItemAsync(earlier);
        await db.SaveQueueItemAsync(Item(Guid.NewGuid(), new DateOnly(2026, 10, 7), now));
        await db.SaveQueueItemAsync(later with { Payload = "{\"mortality\":3}" });
        await db.ClearUserDataAsync();

        // Assert
        IReadOnlyList<SyncItem> queue = await db.GetQueueAsync(user);
        queue.Select(i => i.Id).ShouldBe([earlier.Id, later.Id]);
        queue[0].ShouldBe(earlier);
        queue[1].Payload.ShouldBe("{\"mortality\":3}");
        (await db.CountQueueOfOtherUsersAsync(user)).ShouldBe(1);

        await db.DeleteQueueItemAsync(later.Id);
        (await db.GetQueueItemAsync(later.Id)).ShouldBeNull();
    }

    private static SyncItem Item(Guid userId, DateOnly date, DateTime now) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = userId,
        CycleId = Guid.NewGuid(),
        Date = date,
        Payload = "{}",
        Status = SyncStatus.Pending,
        CreatedAtUtc = now,
        UpdatedAtUtc = now
    };

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_path);
    }
}
