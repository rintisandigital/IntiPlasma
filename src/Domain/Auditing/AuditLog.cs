using SharedKernel;

namespace Domain.Auditing;

public enum AuditCategory
{
    /// <summary>
    /// Users, menu/branch access profiles, API roles, menus and branches.
    /// </summary>
    Access,

    /// <summary>
    /// Excel/PDF/CSV exports and printed documents.
    /// </summary>
    Export,

    /// <summary>
    /// Successful, failed and locked-out sign-ins (Web.App and Web.Api).
    /// </summary>
    SignIn
}

/// <summary>
/// One entry of the audit trail (W10): who did what, when and from where. Entries are only appended,
/// never changed or deleted by the application.
/// </summary>
public sealed class AuditLog : Entity
{
    public const int ActionMaxLength = 100;
    public const int EntityTypeMaxLength = 100;
    public const int SummaryMaxLength = 500;
    public const int UserEmailMaxLength = 256;
    public const int IpAddressMaxLength = 64;
    public const int SourceMaxLength = 50;

    private AuditLog(Guid id)
        : base(id)
    {
    }

    private AuditLog()
    {
    }

    public DateTime OccurredAtUtc { get; private set; }

    public AuditCategory Category { get; private set; }

    /// <summary>
    /// What happened, e.g. <c>SetUserAccess</c>, <c>Excel</c>, <c>SignInFailed</c>.
    /// </summary>
    public string Action { get; private set; }

    public Guid? UserId { get; private set; }

    public string? UserEmail { get; private set; }

    public string? EntityType { get; private set; }

    public Guid? EntityId { get; private set; }

    public string Summary { get; private set; }

    /// <summary>
    /// JSON with the values of the change (passwords removed) or the filters of an export.
    /// </summary>
    public string? Details { get; private set; }

    public string? IpAddress { get; private set; }

    /// <summary>
    /// The application that recorded the entry (Web.App or Web.Api).
    /// </summary>
    public string Source { get; private set; }

    public static AuditLog Create(
        DateTime occurredAtUtc,
        AuditCategory category,
        string action,
        string summary,
        string source,
        Guid? userId = null,
        string? userEmail = null,
        string? entityType = null,
        Guid? entityId = null,
        string? details = null,
        string? ipAddress = null) =>
        new(Guid.CreateVersion7())
        {
            OccurredAtUtc = occurredAtUtc,
            Category = category,
            Action = Truncate(action, ActionMaxLength),
            Summary = Truncate(summary, SummaryMaxLength),
            Source = Truncate(source, SourceMaxLength),
            UserId = userId,
            UserEmail = userEmail is null ? null : Truncate(userEmail, UserEmailMaxLength),
            EntityType = entityType is null ? null : Truncate(entityType, EntityTypeMaxLength),
            EntityId = entityId,
            Details = details,
            IpAddress = ipAddress is null ? null : Truncate(ipAddress, IpAddressMaxLength)
        };

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
