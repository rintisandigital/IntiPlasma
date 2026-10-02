using SharedKernel;

namespace Domain.Users;

public sealed class User : AggregateRoot
{
    private readonly List<UserRole> _roles = [];
    private readonly List<UserBranch> _branches = [];

    private User(Guid id, string email, string firstName, string lastName, string passwordHash)
        : base(id)
    {
        Email = email;
        FirstName = firstName;
        LastName = lastName;
        PasswordHash = passwordHash;
        IsActive = true;
        SecurityStamp = NewSecurityStamp();
    }

    private User()
    {
    }

    public string Email { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string PasswordHash { get; private set; }

    /// <summary>
    /// Inactive users can neither sign in to Web.App nor obtain tokens from Web.Api.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Changes whenever existing sessions must stop working (password change, deactivation). A cookie session
    /// carries the stamp it was issued with and is rejected once the stamp no longer matches.
    /// </summary>
    public string SecurityStamp { get; private set; }

    public IReadOnlyCollection<UserRole> Roles => [.. _roles];
    public IReadOnlyCollection<UserBranch> Branches => [.. _branches];

    public static User Create(string email, string firstName, string lastName, string passwordHash)
    {
        var user = new User(Guid.CreateVersion7(), email, firstName, lastName, passwordHash);

        user.Raise(new UserRegisteredDomainEvent(user.Id));

        return user;
    }

    /// <summary>
    /// Replaces the user's roles. The caller is responsible for rejecting unknown role ids.
    /// </summary>
    public void SetRoles(IEnumerable<Guid> roleIds)
    {
        var desired = roleIds.ToHashSet();

        _roles.RemoveAll(r => !desired.Contains(r.RoleId));

        foreach (Guid roleId in desired.Where(id => _roles.TrueForAll(r => r.RoleId != id)))
        {
            _roles.Add(new UserRole(Id, roleId));
        }

        Raise(new UserRolesChangedDomainEvent(Id));
    }

    /// <summary>
    /// Replaces the branches the user may access. The caller is responsible for rejecting unknown branch ids.
    /// </summary>
    public void SetBranches(IEnumerable<Guid> branchIds)
    {
        var desired = branchIds.ToHashSet();

        _branches.RemoveAll(b => !desired.Contains(b.BranchId));

        foreach (Guid branchId in desired.Where(id => _branches.TrueForAll(b => b.BranchId != id)))
        {
            _branches.Add(new UserBranch(Id, branchId));
        }
    }

    public void ChangePassword(string passwordHash)
    {
        PasswordHash = passwordHash;
        SecurityStamp = NewSecurityStamp();
    }

    public void Deactivate()
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        SecurityStamp = NewSecurityStamp();
    }

    public void Activate() => IsActive = true;

    private static string NewSecurityStamp() => Guid.NewGuid().ToString("N");
}
