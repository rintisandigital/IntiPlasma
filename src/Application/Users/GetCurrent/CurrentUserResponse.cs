namespace Application.Users.GetCurrent;

public sealed record CurrentUserResponse
{
    public Guid Id { get; init; }

    public string Email { get; init; }

    public string FirstName { get; init; }

    public string LastName { get; init; }

    /// <summary>
    /// True when the user may access every branch, including branches created later.
    /// </summary>
    public bool AllBranches { get; init; }

    /// <summary>
    /// Active branches the user may access, ordered by code.
    /// </summary>
    public IReadOnlyList<CurrentUserBranch> Branches { get; init; } = [];
}

public sealed record CurrentUserBranch
{
    public Guid Id { get; init; }

    public string Code { get; init; }

    public string Name { get; init; }
}
