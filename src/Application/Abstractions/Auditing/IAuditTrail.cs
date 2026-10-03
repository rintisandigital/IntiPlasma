using Domain.Auditing;

namespace Application.Abstractions.Auditing;

/// <summary>
/// Appends entries to the audit trail (W10). <see cref="Record"/> only adds the entry to the unit of work; it is
/// stored by the next <c>SaveChangesAsync</c>, so an entry written by a handler commits with its change.
/// The signed-in user, IP address and application are filled in by the implementation.
/// </summary>
public interface IAuditTrail
{
    void Record(AuditEntry entry);
}

public sealed record AuditEntry(AuditCategory Category, string Action, string Summary)
{
    public string? EntityType { get; init; }

    public Guid? EntityId { get; init; }

    /// <summary>
    /// Serialized to JSON; properties whose name contains "password" are masked.
    /// </summary>
    public object? Details { get; init; }

    /// <summary>
    /// The user the entry is about when nobody is signed in yet (sign-in events); otherwise the signed-in user.
    /// </summary>
    public Guid? UserId { get; init; }

    public string? UserEmail { get; init; }
}

/// <summary>
/// Marks a command whose successful execution is recorded in the audit trail (category Access) by
/// <c>AuditDecorator</c>: the command values become the details, the entity id is the created id or the first
/// <see cref="Guid"/> property of the command.
/// </summary>
public interface IAuditedCommand
{
    /// <summary>
    /// The kind of record the command changes, e.g. <c>User</c> or <c>MenuAccessProfile</c>.
    /// </summary>
    string AuditEntityType { get; }
}
