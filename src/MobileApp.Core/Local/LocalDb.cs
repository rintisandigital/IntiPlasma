using System.Globalization;
using Microsoft.Data.Sqlite;

namespace MobileApp.Core.Local;

/// <summary>
/// The app's SQLite database (PLAN-MOBILE §3.5). The schema version lives in <c>PRAGMA user_version</c>; each
/// migration runs once, in order, inside a transaction. Later phases append migrations (sync queue, field context…);
/// existing ones are never edited.
/// </summary>
public sealed class LocalDb(string databasePath) : IDisposable
{
    /// <summary>
    /// Migration n brings the schema from version n to n + 1.
    /// </summary>
    internal static readonly IReadOnlyList<string> Migrations =
    [
        // v1 (M0): signed-in user and cached API responses.
        """
        CREATE TABLE session (
            key TEXT NOT NULL PRIMARY KEY,
            value TEXT NOT NULL,
            updated_at TEXT NOT NULL
        );

        CREATE TABLE cache_entries (
            key TEXT NOT NULL PRIMARY KEY,
            json TEXT NOT NULL,
            fetched_at TEXT NOT NULL
        );
        """,

        // v2 (M2): offline entries waiting to be sent (PLAN-MOBILE §3.6). A row is deleted once the server took it,
        // so the table also serves as the list of "Belum terkirim" entries.
        """
        CREATE TABLE sync_queue (
            id TEXT NOT NULL PRIMARY KEY,
            user_id TEXT NOT NULL,
            kind TEXT NOT NULL,
            cycle_id TEXT NOT NULL,
            date TEXT NOT NULL,
            payload TEXT NOT NULL,
            status TEXT NOT NULL,
            attempts INTEGER NOT NULL,
            next_attempt_at TEXT NULL,
            error_code TEXT NULL,
            error_message TEXT NULL,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        );

        CREATE INDEX ix_sync_queue_user ON sync_queue (user_id, date, created_at);
        """
    ];

    private const string QueueSelect =
        """
        SELECT id, user_id, kind, cycle_id, date, payload, status, attempts, next_attempt_at, error_code, error_message,
               created_at, updated_at
        FROM sync_queue
        """;

    private readonly SemaphoreSlim _initialization = new(1, 1);
    private bool _initialized;

    public string DatabasePath => databasePath;

    public void Dispose() => _initialization.Dispose();

    /// <summary>
    /// Creates the file if needed and applies pending migrations. Safe to call more than once.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _initialization.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using SqliteConnection connection = await OpenAsync(cancellationToken);

            long version = await ScalarAsync<long>(connection, "PRAGMA user_version;", cancellationToken);

            for (int next = (int)version; next < Migrations.Count; next++)
            {
                await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

                await ExecuteAsync(connection, transaction, Migrations[next], cancellationToken);
                await ExecuteAsync(
                    connection,
                    transaction,
                    string.Create(CultureInfo.InvariantCulture, $"PRAGMA user_version = {next + 1};"),
                    cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }

            _initialized = true;
        }
        finally
        {
            _initialization.Release();
        }
    }

    public async Task<int> GetSchemaVersionAsync(CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);

        return (int)await ScalarAsync<long>(connection, "PRAGMA user_version;", cancellationToken);
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM session WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);

        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    public async Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO session (key, value, updated_at) VALUES ($key, $value, $now)
            ON CONFLICT (key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Removes the session and the cached responses (on logout or when another user signs in). The sync queue
    /// stays: its entries are sent once their owner signs in again (PLAN-MOBILE §3.3).
    /// </summary>
    public async Task ClearUserDataAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, null, "DELETE FROM session; DELETE FROM cache_entries;", cancellationToken);
    }

    /// <summary>
    /// A cached API response (read-only cache, PLAN-MOBILE §3.5), or null.
    /// </summary>
    public async Task<CacheEntry?> GetCacheAsync(string key, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT json, fetched_at FROM cache_entries WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new CacheEntry(
            reader.GetString(0),
            DateTime.Parse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    }

    public async Task SetCacheAsync(string key, string json, DateTime fetchedAtUtc, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO cache_entries (key, json, fetched_at) VALUES ($key, $json, $fetchedAt)
            ON CONFLICT (key) DO UPDATE SET json = excluded.json, fetched_at = excluded.fetched_at;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$json", json);
        command.Parameters.AddWithValue("$fetchedAt", fetchedAtUtc.ToString("O", CultureInfo.InvariantCulture));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RemoveCacheAsync(string key, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM cache_entries WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Removes cached API responses only (settings page "Hapus cache").
    /// </summary>
    public async Task ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, null, "DELETE FROM cache_entries;", cancellationToken);
    }

    public async Task SaveQueueItemAsync(SyncItem item, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO sync_queue (id, user_id, kind, cycle_id, date, payload, status, attempts, next_attempt_at,
                                    error_code, error_message, created_at, updated_at)
            VALUES ($id, $userId, $kind, $cycleId, $date, $payload, $status, $attempts, $nextAttemptAt,
                    $errorCode, $errorMessage, $createdAt, $updatedAt)
            ON CONFLICT (id) DO UPDATE SET
                cycle_id = excluded.cycle_id, date = excluded.date, payload = excluded.payload, status = excluded.status,
                attempts = excluded.attempts, next_attempt_at = excluded.next_attempt_at, error_code = excluded.error_code,
                error_message = excluded.error_message, updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$id", item.Id.ToString());
        command.Parameters.AddWithValue("$userId", item.UserId.ToString());
        command.Parameters.AddWithValue("$kind", item.Kind);
        command.Parameters.AddWithValue("$cycleId", item.CycleId.ToString());
        command.Parameters.AddWithValue("$date", item.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$payload", item.Payload);
        command.Parameters.AddWithValue("$status", item.Status.ToString());
        command.Parameters.AddWithValue("$attempts", item.Attempts);
        command.Parameters.AddWithValue("$nextAttemptAt", (object?)Text(item.NextAttemptAtUtc) ?? DBNull.Value);
        command.Parameters.AddWithValue("$errorCode", (object?)item.ErrorCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$errorMessage", (object?)item.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAt", Text(item.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAt", Text(item.UpdatedAtUtc));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<SyncItem?> GetQueueItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = QueueSelect + " WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString());

        return (await ReadQueueAsync(command, cancellationToken)).SingleOrDefault();
    }

    /// <summary>
    /// The queue of one user, oldest entry date first (the order in which it is sent).
    /// </summary>
    public async Task<IReadOnlyList<SyncItem>> GetQueueAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = QueueSelect + " WHERE user_id = $userId ORDER BY date, created_at;";
        command.Parameters.AddWithValue("$userId", userId.ToString());

        return await ReadQueueAsync(command, cancellationToken);
    }

    /// <summary>
    /// Entries of other users on this device, held until their owner signs in again.
    /// </summary>
    public async Task<int> CountQueueOfOtherUsersAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sync_queue WHERE user_id <> $userId;";
        command.Parameters.AddWithValue("$userId", userId.ToString());

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    public async Task DeleteQueueItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM sync_queue WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString());

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<List<SyncItem>> ReadQueueAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        var items = new List<SyncItem>();

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new SyncItem
            {
                Id = Guid.Parse(reader.GetString(0)),
                UserId = Guid.Parse(reader.GetString(1)),
                Kind = reader.GetString(2),
                CycleId = Guid.Parse(reader.GetString(3)),
                Date = DateOnly.ParseExact(reader.GetString(4), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                Payload = reader.GetString(5),
                Status = Enum.Parse<SyncStatus>(reader.GetString(6)),
                Attempts = reader.GetInt32(7),
                NextAttemptAtUtc = reader.GetValue(8) is string next ? Time(next) : null,
                ErrorCode = reader.GetValue(9) as string,
                ErrorMessage = reader.GetValue(10) as string,
                CreatedAtUtc = Time(reader.GetString(11)),
                UpdatedAtUtc = Time(reader.GetString(12))
            });
        }

        return items;
    }

    private static string Text(DateTime utc) => utc.ToString("O", CultureInfo.InvariantCulture);

    private static string? Text(DateTime? utc) => utc?.ToString("O", CultureInfo.InvariantCulture);

    private static DateTime Time(string text) => DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString());

        await connection.OpenAsync(cancellationToken);

        return connection;
    }

    // The SQL passed to the two helpers below is always a constant of this class, never user input.
#pragma warning disable CA2100
    private static async Task ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<T> ScalarAsync<T>(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;

        object? value = await command.ExecuteScalarAsync(cancellationToken);

        return (T)Convert.ChangeType(value!, typeof(T), CultureInfo.InvariantCulture);
    }
#pragma warning restore CA2100
}

public enum SyncStatus
{
    /// <summary>
    /// Waiting to be sent (also after a network failure, until <see cref="SyncItem.NextAttemptAtUtc"/>).
    /// </summary>
    Pending,

    /// <summary>
    /// Being sent right now.
    /// </summary>
    Sending,

    /// <summary>
    /// Rejected by the server; the user edits it and sends it again, or deletes it.
    /// </summary>
    Failed
}

/// <summary>
/// An entry of the offline queue. <see cref="Id"/> is the id of the document sent to the server.
/// </summary>
public sealed record SyncItem
{
    public Guid Id { get; init; }

    /// <summary>
    /// Owner: only sent while this user is signed in.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// One of <see cref="SyncKinds"/>.
    /// </summary>
    public string Kind { get; init; } = SyncKinds.Recording;

    public Guid CycleId { get; init; }

    public DateOnly Date { get; init; }

    /// <summary>
    /// The entry as JSON (e.g. a recording draft with its photos).
    /// </summary>
    public string Payload { get; init; } = "{}";

    public SyncStatus Status { get; init; }

    public int Attempts { get; init; }

    public DateTime? NextAttemptAtUtc { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }
}

public static class SyncKinds
{
    public const string Recording = "Recording";

    public const string LiveBirdStock = "LiveBirdStock";
}

/// <param name="Json">The response body as stored (camelCase JSON).</param>
/// <param name="FetchedAtUtc">When the response was received from the server.</param>
public sealed record CacheEntry(string Json, DateTime FetchedAtUtc);
