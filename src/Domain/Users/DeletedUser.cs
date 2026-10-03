using SharedKernel;

namespace Domain.Users;

/// <summary>
/// Backup of a deleted user (W-22, table <c>identity.user_old</c>), written in the same transaction right
/// before the user is deleted. There is no restore screen; a user can be restored manually from this copy.
/// </summary>
public sealed class DeletedUser : Entity
{
    private DeletedUser(Guid id)
        : base(id)
    {
    }

    private DeletedUser()
    {
    }

    public Guid UserId { get; private set; }
    public string Email { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string PasswordHash { get; private set; }
    public bool IsActive { get; private set; }
    public Guid? MenuAccessProfileId { get; private set; }
    public Guid? BranchAccessProfileId { get; private set; }
    public Guid? DefaultBranchId { get; private set; }

    public Guid[] RoleIds { get; private set; } = [];

    /// <summary>
    /// Role names at deletion time (roles may be renamed or removed later).
    /// </summary>
    public string[] RoleNames { get; private set; } = [];

    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }

    public DateTime DeletedAtUtc { get; private set; }
    public Guid? DeletedBy { get; private set; }
    public string? Reason { get; private set; }

    /// <param name="roles">The user's roles (id and name).</param>
    public static DeletedUser From(
        User user,
        IReadOnlyCollection<(Guid Id, string Name)> roles,
        DateTime deletedAtUtc,
        Guid? deletedBy,
        string? reason)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(roles);

        return new DeletedUser(Guid.CreateVersion7())
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PasswordHash = user.PasswordHash,
            IsActive = user.IsActive,
            MenuAccessProfileId = user.MenuAccessProfileId,
            BranchAccessProfileId = user.BranchAccessProfileId,
            DefaultBranchId = user.DefaultBranchId,
            RoleIds = [.. roles.Select(r => r.Id)],
            RoleNames = [.. roles.Select(r => r.Name)],
            CreatedAtUtc = user.CreatedAtUtc,
            CreatedBy = user.CreatedBy,
            ModifiedAtUtc = user.ModifiedAtUtc,
            ModifiedBy = user.ModifiedBy,
            DeletedAtUtc = deletedAtUtc,
            DeletedBy = deletedBy,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()
        };
    }
}
