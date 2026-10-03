using Application.Abstractions.Messaging;
using Application.Access.Menus;
using SharedKernel;

namespace Web.App.Infrastructure.Authorization;

/// <summary>
/// Synchronizes <see cref="MenuCatalog"/> into the database at startup. Retries until it succeeds (e.g. the
/// database is not migrated yet), so Web.App never fails to start because of it.
/// </summary>
internal sealed partial class MenuCatalogSyncService(IServiceScopeFactory scopeFactory, ILogger<MenuCatalogSyncService> logger)
    : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int changes = await SyncAsync(scopeFactory, stoppingToken);
                LogSynced(logger, changes);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogSyncFailed(logger, ex);
            }

            await Task.Delay(RetryDelay, stoppingToken);
        }
    }

    /// <summary>
    /// Runs the synchronization once (also used by tests after migrating a fresh database).
    /// </summary>
    public static async Task<int> SyncAsync(IServiceScopeFactory scopeFactory, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ICommandHandler<SyncMenuCatalogCommand, int> handler =
            scope.ServiceProvider.GetRequiredService<ICommandHandler<SyncMenuCatalogCommand, int>>();

        Result<int> result = await handler.Handle(new SyncMenuCatalogCommand(MenuCatalog.Definitions), cancellationToken);

        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException($"Menu catalog synchronization failed: {result.Error.Description}");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Menu catalog synchronized ({Changes} changes)")]
    private static partial void LogSynced(ILogger logger, int changes);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Menu catalog synchronization failed; retrying in 30 seconds")]
    private static partial void LogSyncFailed(ILogger logger, Exception exception);
}
