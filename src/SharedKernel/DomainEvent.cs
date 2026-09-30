namespace SharedKernel;

/// <summary>
/// Base record for domain events. <see cref="Id"/> is stable across outbox retries, so consumers
/// can use it as an idempotency key (e.g. the source key of an automatically generated journal).
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
