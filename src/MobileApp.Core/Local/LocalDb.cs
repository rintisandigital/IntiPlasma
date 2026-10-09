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
        """
    ];

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
    /// Removes everything that belongs to the signed-in user (on logout or when another user signs in).
    /// </summary>
    public async Task ClearUserDataAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, null, "DELETE FROM session; DELETE FROM cache_entries;", cancellationToken);
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
