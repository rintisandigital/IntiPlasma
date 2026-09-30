namespace Infrastructure.Outbox;

internal sealed class OutboxMessage
{
    public Guid Id { get; init; }

    /// <summary>
    /// Full CLR type name of the domain event, resolved through <see cref="DomainEventTypes"/>.
    /// </summary>
    public string Type { get; init; }

    public string Content { get; init; }

    public DateTime OccurredOnUtc { get; init; }

    public DateTime? ProcessedOnUtc { get; init; }

    public int Attempts { get; init; }

    public string? Error { get; init; }
}
