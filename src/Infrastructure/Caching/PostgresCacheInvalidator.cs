using Application.Abstractions.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure.Caching;

/// <summary>
/// Removes the entry from this process's cache, then broadcasts the removal with <c>pg_notify</c> so every
/// other process (see <see cref="CacheInvalidationListener"/>) removes it too.
/// </summary>
internal sealed partial class PostgresCacheInvalidator(
    HybridCache cache,
    NpgsqlDataSource dataSource,
    ILogger<PostgresCacheInvalidator> logger) : ICacheInvalidator
{
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await cache.RemoveAsync(key, cancellationToken);

        await NotifyAsync(CacheInvalidationMessage.ForKey(key), cancellationToken);
    }

    public async Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        await cache.RemoveByTagAsync(tag, cancellationToken);

        await NotifyAsync(CacheInvalidationMessage.ForTag(tag), cancellationToken);
    }

    private async Task NotifyAsync(string payload, CancellationToken cancellationToken)
    {
        try
        {
            await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT pg_notify(@Channel, @Payload)", connection);
            command.Parameters.AddWithValue("Channel", CacheInvalidationMessage.Channel);
            command.Parameters.AddWithValue("Payload", payload);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (NpgsqlException ex)
        {
            // The change is already saved; other processes fall back to the cache expiration.
            LogNotifyFailed(logger, ex, payload);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cache invalidation {Payload} could not be broadcast")]
    private static partial void LogNotifyFailed(ILogger logger, Exception exception, string payload);
}
