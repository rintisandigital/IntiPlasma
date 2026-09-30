using System.Data.Common;
using Dapper;
using Infrastructure.DomainEvents;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using SharedKernel;

namespace Infrastructure.Outbox;

/// <summary>
/// Publishes outbox messages to their domain event handlers. Rows are locked with FOR UPDATE SKIP LOCKED,
/// so several application instances can run the processor concurrently.
/// </summary>
internal sealed partial class OutboxProcessor(
    NpgsqlDataSource dataSource,
    IDomainEventsDispatcher dispatcher,
    IDateTimeProvider dateTimeProvider,
    IOptions<OutboxOptions> options,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    private readonly OutboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.IntervalInSeconds));

        do
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogBatchFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using DbTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string selectSql =
            """
            SELECT id AS Id, type AS Type, content AS Content, attempts AS Attempts
            FROM infrastructure.outbox_messages
            WHERE processed_on_utc IS NULL
            ORDER BY occurred_on_utc
            LIMIT @BatchSize
            FOR UPDATE SKIP LOCKED
            """;

        var messages = (await connection.QueryAsync<OutboxMessageRow>(
            new CommandDefinition(selectSql, new { _options.BatchSize }, transaction, cancellationToken: cancellationToken)))
            .ToList();

        foreach (OutboxMessageRow message in messages)
        {
            await ProcessMessageAsync(connection, transaction, message, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task ProcessMessageAsync(
        NpgsqlConnection connection,
        DbTransaction transaction,
        OutboxMessageRow message,
        CancellationToken cancellationToken)
    {
        DateTime? processedOnUtc = dateTimeProvider.UtcNow;
        string? error = null;

        try
        {
            IDomainEvent domainEvent = DomainEventTypes.Deserialize(message.Type, message.Content);

            await dispatcher.DispatchAsync([domainEvent], cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogMessageFailed(logger, ex, message.Id, message.Type);

            error = ex.ToString();

            if (message.Attempts + 1 < _options.MaxAttempts)
            {
                processedOnUtc = null;
            }
        }

        const string updateSql =
            """
            UPDATE infrastructure.outbox_messages
            SET processed_on_utc = @ProcessedOnUtc, attempts = attempts + 1, error = @Error
            WHERE id = @Id
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            updateSql,
            new { message.Id, ProcessedOnUtc = processedOnUtc, Error = error },
            transaction,
            cancellationToken: cancellationToken));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to process outbox batch")]
    private static partial void LogBatchFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to process outbox message {MessageId} of type {MessageType}")]
    private static partial void LogMessageFailed(ILogger logger, Exception exception, Guid messageId, string messageType);

    private sealed record OutboxMessageRow(Guid Id, string Type, string Content, int Attempts);
}
