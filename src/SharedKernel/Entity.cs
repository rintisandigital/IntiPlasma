namespace SharedKernel;

public abstract class Entity
{
    protected Entity(Guid id)
    {
        Id = id;
    }

    // Required by EF Core for materialization.
    protected Entity()
    {
    }

    public Guid Id { get; protected init; }
}
