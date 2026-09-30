namespace SharedKernel;

/// <summary>
/// Soft-deleted rows are filtered out by a global query filter; a hard delete is converted into a soft delete on save.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; }

    DateTime? DeletedAtUtc { get; }

    Guid? DeletedBy { get; }
}
