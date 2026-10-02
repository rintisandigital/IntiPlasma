using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using SharedKernel;

namespace Infrastructure.Outbox;

/// <summary>
/// Reports the outbox backlog of the host that runs the processor: degraded when a pending message waits
/// longer than <see cref="StaleAfter"/> (the processor is stuck or stopped) or when dead letters exist.
/// </summary>
internal sealed class OutboxHealthCheck(NpgsqlDataSource dataSource, IDateTimeProvider dateTimeProvider) : IHealthCheck
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);

        const string sql =
            """
            SELECT
                count(*) FILTER (WHERE processed_on_utc IS NULL) AS Pending,
                min(occurred_on_utc) FILTER (WHERE processed_on_utc IS NULL) AS OldestPendingUtc,
                count(*) FILTER (WHERE processed_on_utc IS NOT NULL AND error IS NOT NULL) AS DeadLetters
            FROM infrastructure.outbox_messages
            """;

        OutboxBacklog backlog = await connection.QuerySingleAsync<OutboxBacklog>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        var data = new Dictionary<string, object>
        {
            ["pending"] = backlog.Pending,
            ["deadLetters"] = backlog.DeadLetters
        };

        if (backlog.OldestPendingUtc is { } oldest && dateTimeProvider.UtcNow - oldest > StaleAfter)
        {
            return HealthCheckResult.Degraded($"Outbox messages have been pending since {oldest:O}", data: data);
        }

        return backlog.DeadLetters > 0
            ? HealthCheckResult.Degraded("Outbox has dead-lettered messages (see system/failed-events)", data: data)
            : HealthCheckResult.Healthy(data: data);
    }

    internal sealed class OutboxBacklog
    {
        public long Pending { get; set; }

        public DateTime? OldestPendingUtc { get; set; }

        public long DeadLetters { get; set; }
    }
}
