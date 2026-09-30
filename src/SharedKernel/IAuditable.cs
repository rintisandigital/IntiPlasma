namespace SharedKernel;

/// <summary>
/// Audit columns are populated automatically when the entity is saved.
/// </summary>
public interface IAuditable
{
    DateTime CreatedAtUtc { get; }

    Guid? CreatedBy { get; }

    DateTime? ModifiedAtUtc { get; }

    Guid? ModifiedBy { get; }
}
