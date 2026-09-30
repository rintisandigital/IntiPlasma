namespace SharedKernel;

/// <summary>
/// Consistency boundary of the domain. Only aggregate roots are loaded and saved directly,
/// and only aggregate roots raise domain events (they are persisted to the outbox on save).
/// Every aggregate root is auditable; the audit columns are filled in by an EF Core interceptor.
/// </summary>
public abstract class AggregateRoot : Entity, IAuditable
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(Guid id)
        : base(id)
    {
    }

    protected AggregateRoot()
    {
    }

#pragma warning disable S1144 // Private setters are used by EF Core and the audit interceptor.
    public DateTime CreatedAtUtc { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTime? ModifiedAtUtc { get; private set; }

    public Guid? ModifiedBy { get; private set; }
#pragma warning restore S1144

    public IReadOnlyList<IDomainEvent> DomainEvents => [.. _domainEvents];

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    protected void Raise(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }
}
