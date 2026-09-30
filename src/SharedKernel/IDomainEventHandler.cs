namespace SharedKernel;

/// <summary>
/// Handles a domain event published from the outbox. Delivery is at-least-once,
/// so handlers must be idempotent (use <see cref="IDomainEvent.Id"/> as the idempotency key).
/// </summary>
public interface IDomainEventHandler<in T> where T : IDomainEvent
{
    Task Handle(T domainEvent, CancellationToken cancellationToken);
}
