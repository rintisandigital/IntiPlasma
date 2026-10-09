namespace Application.Users.GetCurrent;

public sealed record CurrentUserResponse
{
    public Guid Id { get; init; }

    public string Email { get; init; }

    public string FirstName { get; init; }

    public string LastName { get; init; }

    /// <summary>
    /// Branch selected after sign-in (null: the first accessible branch).
    /// </summary>
    public Guid? DefaultBranchId { get; init; }

    /// <summary>
    /// True when the user may access every branch, including branches created later.
    /// </summary>
    public bool AllBranches { get; init; }

    /// <summary>
    /// Active branches the user may access, ordered by code.
    /// </summary>
    public IReadOnlyList<CurrentUserBranch> Branches { get; init; } = [];

    /// <summary>
    /// API roles of the user, ordered by name.
    /// </summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>
    /// API permissions granted through the roles ({module}:{action}), ordered. The mobile app shows its menus from
    /// this list because the access token carries no permission claims (PLAN-MOBILE M-10).
    /// </summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];
}

public sealed record CurrentUserBranch
{
    public Guid Id { get; init; }

    public string Code { get; init; }

    public string Name { get; init; }
}
