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

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_path);
    }
}
