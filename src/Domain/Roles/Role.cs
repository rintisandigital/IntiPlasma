using SharedKernel;

namespace Domain.Roles;

public sealed class Role : AggregateRoot
{
    public const string AdministratorName = "Administrator";

    private readonly List<RolePermission> _permissions = [];

    private Role(Guid id, string name, string? description, bool isSystem)
        : base(id)
    {
        Name = name;
        Description = description;
        IsSystem = isSystem;
    }

    private Role()
    {
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }

    /// <summary>
    /// System roles are managed by the application (seeded) and cannot be edited by users.
    /// </summary>
    public bool IsSystem { get; private set; }

    public IReadOnlyCollection<RolePermission> Permissions => [.. _permissions];

    public static Result<Role> Create(string name, string? description, IEnumerable<string> permissions)
    {
        var role = new Role(Guid.CreateVersion7(), name, description, isSystem: false);

        Result result = role.ApplyPermissions(permissions);

        return result.IsSuccess ? role : Result.Failure<Role>(result.Error);
    }

    public static Role CreateAdministrator()
    {
        var role = new Role(Guid.CreateVersion7(), AdministratorName, "Full access to every module.", isSystem: true);

        role.ApplyPermissions(Roles.Permissions.All);

        return role;
    }

    public Result Update(string name, string? description, IEnumerable<string> permissions)
    {
        if (IsSystem)
        {
            return Result.Failure(RoleErrors.SystemRoleIsReadOnly);
        }

        Name = name;
        Description = description;

        return ApplyPermissions(permissions);
    }

    /// <summary>
    /// Keeps a system role in sync with the permission catalog when new permissions are introduced.
    /// </summary>
    public void SyncSystemPermissions()
    {
        if (IsSystem)
        {
            ApplyPermissions(Roles.Permissions.All);
        }
    }

    private Result ApplyPermissions(IEnumerable<string> permissions)
    {
        var desired = permissions.ToHashSet(StringComparer.Ordinal);

        string? unknown = desired.FirstOrDefault(p => !Roles.Permissions.Exists(p));
        if (unknown is not null)
        {
            return Result.Failure(RoleErrors.UnknownPermission(unknown));
        }

        int removed = _permissions.RemoveAll(p => !desired.Contains(p.Permission));
        var added = desired.Where(p => _permissions.TrueForAll(existing => existing.Permission != p)).ToList();

        _permissions.AddRange(added.Select(p => new RolePermission(Id, p)));

        if (removed > 0 || added.Count > 0)
        {
            Raise(new RolePermissionsChangedDomainEvent(Id));
        }

        return Result.Success();
    }
}
