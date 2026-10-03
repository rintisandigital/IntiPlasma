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

    /// <summary>
    /// "Akses Menu": the Web.App menus and rights of the user. Without it the user cannot sign in to Web.App.
    /// </summary>
    public Guid? MenuAccessProfileId { get; private set; }

    /// <summary>
    /// "Akses Cabang": the branches the user may work in (Web.Api and Web.App). Without it no branch is visible.
    /// </summary>
    public Guid? BranchAccessProfileId { get; private set; }

    /// <summary>
    /// Branch selected in Web.App after sign-in; must be covered by the branch access profile.
    /// </summary>
    public Guid? DefaultBranchId { get; private set; }

    /// <summary>
    /// Consecutive wrong passwords since the last successful sign-in or lockout (W10, Web.App and Web.Api).
    /// </summary>
    public int AccessFailedCount { get; private set; }

    /// <summary>
    /// Sign-in is refused until this moment after too many wrong passwords.
    /// </summary>
    public DateTime? LockoutEndUtc { get; private set; }

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

    public void UpdateProfile(string firstName, string lastName)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
    }

    /// <summary>
    /// Assigns the menu and branch access profiles. The caller checks that the profiles exist and that the
    /// default branch is covered by the branch profile (it needs the profile's data).
    /// </summary>
    public void SetAccess(Guid? menuAccessProfileId, Guid? branchAccessProfileId, Guid? defaultBranchId)
    {
        MenuAccessProfileId = menuAccessProfileId;
        BranchAccessProfileId = branchAccessProfileId;
        DefaultBranchId = branchAccessProfileId is null ? null : defaultBranchId;
    }

    public void ChangePassword(string passwordHash)
    {
        PasswordHash = passwordHash;
        SecurityStamp = NewSecurityStamp();
        Unlock();
    }

    public bool IsLockedOut(DateTime utcNow) => LockoutEndUtc > utcNow;

    /// <summary>
    /// Counts a wrong password. Reaching <paramref name="maxFailedAttempts"/> locks the account for
    /// <paramref name="lockoutDuration"/> and starts a new count; returns whether this attempt locked it.
    /// </summary>
    public bool RegisterFailedSignIn(DateTime utcNow, int maxFailedAttempts, TimeSpan lockoutDuration)
    {
        AccessFailedCount++;

        if (AccessFailedCount < maxFailedAttempts)
        {
            return false;
        }

        AccessFailedCount = 0;
        LockoutEndUtc = utcNow.Add(lockoutDuration);

        return true;
    }

    public void RegisterSuccessfulSignIn() => Unlock();

    /// <summary>
    /// Lifts a lockout (administrator action, password reset, activation) and forgets earlier wrong passwords.
    /// </summary>
    public void Unlock()
    {
        AccessFailedCount = 0;
        LockoutEndUtc = null;
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

    public void Activate()
    {
        IsActive = true;
        Unlock();
    }

    private static string NewSecurityStamp() => Guid.NewGuid().ToString("N");
}
