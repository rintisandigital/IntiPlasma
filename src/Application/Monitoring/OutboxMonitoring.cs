using System.Data.Common;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using SharedKernel;

namespace Application.Monitoring;

/// <summary>
/// Domain events that failed after all retries (dead letters), e.g. an automatic journal rejected because a mapping
/// is missing or the period is closed. They block closing the period until retried successfully. Events still being
/// retried automatically (error set, not processed yet) are not failed yet: they count as pending.
/// </summary>
public sealed record GetFailedEventsQuery : IQuery<IReadOnlyList<FailedEventResponse>>;

/// <summary>
/// Puts failed events (all, or one) back in the outbox queue after the cause has been fixed. Returns how many.
/// </summary>
public sealed record RetryFailedEventsCommand(Guid? EventId) : ICommand<int>;

public sealed record FailedEventResponse(Guid Id, string Type, DateTime OccurredOnUtc, int Attempts, string Error);

internal sealed class GetFailedEventsQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetFailedEventsQuery, IReadOnlyList<FailedEventResponse>>
{
    private const int ErrorPreviewLength = 1000;

    public async Task<Result<IReadOnlyList<FailedEventResponse>>> Handle(GetFailedEventsQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        IEnumerable<FailedEventResponse> rows = await connection.QueryAsync<FailedEventResponse>(new CommandDefinition(
            $"""
            SELECT id AS Id, regexp_replace(type, '^.*\.', '') AS Type, occurred_on_utc AS OccurredOnUtc,
                   attempts AS Attempts, left(error, {ErrorPreviewLength}) AS Error
            FROM infrastructure.outbox_messages
            WHERE processed_on_utc IS NOT NULL AND error IS NOT NULL
            ORDER BY occurred_on_utc
            """,
            cancellationToken: cancellationToken));

        return rows.ToList();
    }
}

internal sealed class RetryFailedEventsCommandHandler(IDbConnectionFactory dbConnectionFactory)
    : ICommandHandler<RetryFailedEventsCommand, int>
{
    public async Task<Result<int>> Handle(RetryFailedEventsCommand command, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        int count = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE infrastructure.outbox_messages
            SET processed_on_utc = NULL, attempts = 0, error = NULL
            WHERE processed_on_utc IS NOT NULL AND error IS NOT NULL AND (@EventId::uuid IS NULL OR id = @EventId)
            """,
            new { command.EventId },
            cancellationToken: cancellationToken));

        return count;
    }
}
