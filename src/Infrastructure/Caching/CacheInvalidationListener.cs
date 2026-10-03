using System.Collections.Concurrent;
using Application.Abstractions.Authorization;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure.Caching;

/// <summary>
/// Listens on the PostgreSQL <c>cache_invalidation</c> channel and removes the announced keys/tags from this
/// process's cache. Runs in every host (Web.Api and Web.App), so an access change made in one process is
/// visible in the others within seconds.
/// </summary>
internal sealed partial class CacheInvalidationListener(
    NpgsqlDataSource dataSource,
    HybridCache cache,
    CacheInvalidationStatus status,
    ILogger<CacheInvalidationListener> logger) : BackgroundService
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    private readonly ConcurrentQueue<string> _pending = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ListenAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                status.Listening = false;
                LogListenerFailed(logger, ex);

                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }
    }

    private async Task ListenAsync(CancellationToken stoppingToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(stoppingToken);

        connection.Notification += (_, args) => _pending.Enqueue(args.Payload);

        await using (var command = new NpgsqlCommand($"LISTEN {CacheInvalidationMessage.Channel}", connection))
        {
            await command.ExecuteNonQueryAsync(stoppingToken);
        }

        // Notifications sent while this process was not listening are lost: drop every access-related entry.
        await cache.RemoveByTagAsync(PermissionCacheKeys.Tag, stoppingToken);

        status.Listening = true;

        while (!stoppingToken.IsCancellationRequested)
        {
            await connection.WaitAsync(stoppingToken);

            while (_pending.TryDequeue(out string? payload))
            {
                await ApplyAsync(payload, stoppingToken);
            }
        }
    }

    private async Task ApplyAsync(string payload, CancellationToken cancellationToken)
    {
        if (!CacheInvalidationMessage.TryParse(payload, out bool isTag, out string value))
        {
            return;
        }

        if (isTag)
        {
            await cache.RemoveByTagAsync(value, cancellationToken);
        }
        else
        {
            await cache.RemoveAsync(value, cancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cache invalidation listener disconnected; reconnecting")]
    private static partial void LogListenerFailed(ILogger logger, Exception exception);
}
