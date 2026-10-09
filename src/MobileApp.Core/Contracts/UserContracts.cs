namespace MobileApp.Core.Contracts;

/// <summary>
/// Response of <c>POST users/login</c> and <c>POST users/refresh-token</c>.
/// </summary>
public sealed record AccessTokens(string AccessToken, string RefreshToken);

/// <summary>
/// Response of <c>GET users/me</c>: profile, branches and API permissions of the signed-in user.
/// </summary>
public sealed record CurrentUser
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public Guid? DefaultBranchId { get; init; }

    public bool AllBranches { get; init; }

    public IReadOnlyList<CurrentUserBranch> Branches { get; init; } = [];

    public IReadOnlyList<string> Roles { get; init; } = [];

    public IReadOnlyList<string> Permissions { get; init; } = [];

    public string FullName => $"{FirstName} {LastName}".Trim();

    /// <summary>
    /// Branch shown in the header: the default branch, otherwise the first accessible one.
    /// </summary>
    public CurrentUserBranch? MainBranch =>
        Branches.FirstOrDefault(b => b.Id == DefaultBranchId) ?? (Branches.Count > 0 ? Branches[0] : null);

    public bool Has(string permission) => Permissions.Contains(permission, StringComparer.Ordinal);

    /// <summary>
    /// A PPL: the server limits the data to the farmers and coops assigned to the user (PLAN-MOBILE M-36). Users
    /// without this see the whole branch and get the PPL filter.
    /// </summary>
    public bool IsLimitedToAssignedData => Has(Session.AppPermissions.PartnershipAssignedOnly);
}

public sealed record CurrentUserBranch(Guid Id, string Code, string Name);
