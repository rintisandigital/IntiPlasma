namespace Application.Users.GetById;

public sealed record UserResponse
{
    public Guid Id { get; init; }

    public string Email { get; init; }

    public string FirstName { get; init; }

    public string LastName { get; init; }

    public bool IsActive { get; init; }

    /// <summary>
    /// Wrong passwords since the last successful sign-in or lockout.
    /// </summary>
    public int AccessFailedCount { get; init; }

    /// <summary>
    /// Set while the account is locked out after too many wrong passwords (may lie in the past).
    /// </summary>
    public DateTime? LockoutEndUtc { get; init; }

    public Guid? MenuAccessProfileId { get; init; }

    public string? MenuAccessProfileName { get; init; }

    public Guid? BranchAccessProfileId { get; init; }

    public string? BranchAccessProfileName { get; init; }

    public Guid? DefaultBranchId { get; init; }

    public string? DefaultBranchName { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public IReadOnlyList<Guid> RoleIds { get; init; } = [];

    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>
    /// The branch access profile covers every branch.
    /// </summary>
    public bool AllBranches { get; init; }

    /// <summary>
    /// Active branches the user may work in (empty when <see cref="AllBranches"/>).
    /// </summary>
    public IReadOnlyList<UserBranchResponse> Branches { get; init; } = [];
}

public sealed record UserBranchResponse
{
    public Guid Id { get; init; }

    public string Code { get; init; }

    public string Name { get; init; }
}
