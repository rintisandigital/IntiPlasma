using SharedKernel;

namespace Domain.Users;

public sealed class User : AggregateRoot
{
    private readonly List<UserRole> _roles = [];

    private User(Guid id, string email, string firstName, string lastName, string passwordHash)
        : base(id)
    {
        Email = email;
        FirstName = firstName;
        LastName = lastName;
        PasswordHash = passwordHash;
    }

    private User()
    {
    }

    public string Email { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string PasswordHash { get; private set; }
    public IReadOnlyCollection<UserRole> Roles => [.. _roles];

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
}
