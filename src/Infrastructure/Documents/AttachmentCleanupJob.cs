using Application.Abstractions.Storage;
using Dapper;
using Domain.Documents.Attachments;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using SharedKernel;

namespace Infrastructure.Documents;

/// <summary>
/// Purges orphaned attachments: uploaded but never attached to a document, or released by their last owner,
/// longer than <see cref="FileStorageOptions.OrphanRetentionHours"/> ago. Row and file are removed permanently.
/// </summary>
internal sealed partial class AttachmentCleanupJob(
    IServiceScopeFactory scopeFactory,
    NpgsqlDataSource dataSource,
    IFileStorage fileStorage,
    IDateTimeProvider dateTimeProvider,
    IOptions<FileStorageOptions> options,
    ILogger<AttachmentCleanupJob> logger) : BackgroundService
{
    /// <summary>
    /// Transaction-level advisory lock: with several Web.App replicas only one purges at a time.
    /// </summary>
    private const long AdvisoryLockKey = 0x4950_434C_4541_4E; // "IPCLEAN"

    private readonly FileStorageOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.CleanupIntervalMinutes));

        do
        {
            try
            {
                int purged = await PurgeExclusiveAsync(stoppingToken);
                if (purged > 0)
                {
                    LogPurged(logger, purged);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogCleanupFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task<int> PurgeExclusiveAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection lockConnection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction lockTransaction = await lockConnection.BeginTransactionAsync(cancellationToken);

        bool acquired = await lockConnection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT pg_try_advisory_xact_lock(@Key)",
            new { Key = AdvisoryLockKey },
            lockTransaction,
            cancellationToken: cancellationToken));

        // Another replica is purging; the lock is released when lockTransaction ends.
        return acquired ? await PurgeAsync(cancellationToken) : 0;
    }

    internal async Task<int> PurgeAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        DateTime cutoff = dateTimeProvider.UtcNow.AddHours(-_options.OrphanRetentionHours);

        var candidates = await context.Attachments
            .AsNoTracking()
            .Where(a => a.Status == AttachmentStatus.Temporary && (a.UnlinkedAtUtc ?? a.CreatedAtUtc) < cutoff)
            .OrderBy(a => a.CreatedAtUtc)
            .Select(a => new { a.Id, a.StoragePath })
            .Take(_options.CleanupBatchSize)
            .ToListAsync(cancellationToken);

        int purged = 0;

        foreach (var candidate in candidates)
        {
            // Re-checked in the DELETE itself: the attachment may have been linked in the meantime.
            int deleted = await context.Attachments
                .Where(a => a.Id == candidate.Id && a.Status == AttachmentStatus.Temporary)
                .ExecuteDeleteAsync(cancellationToken);

            if (deleted == 0)
            {
                continue;
            }

            try
            {
                await fileStorage.DeleteAsync(candidate.StoragePath, cancellationToken);
            }
            catch (IOException ex)
            {
                LogFileDeleteFailed(logger, ex, candidate.Id, candidate.StoragePath);
            }

            purged++;
        }

        return purged;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Purged {Count} orphaned attachments")]
    private static partial void LogPurged(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Attachment cleanup failed")]
    private static partial void LogCleanupFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "File of purged attachment {AttachmentId} could not be removed from {StoragePath}")]
    private static partial void LogFileDeleteFailed(ILogger logger, Exception exception, Guid attachmentId, string storagePath);
}
