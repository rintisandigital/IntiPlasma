using System.Data.Common;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using SharedKernel;

namespace Application.Users.Manage;

/// <summary>
/// User list for administration: search (email, name), status and access profile filters.
/// </summary>
public sealed record GetUsersQuery(
    PageRequest Paging,
    bool? IsActive = null,
    Guid? MenuAccessProfileId = null,
    Guid? BranchAccessProfileId = null) : IQuery<PagedList<UserListItem>>;

public sealed record UserListItem
{
    public Guid Id { get; init; }

    public string Email { get; init; }

    public string FirstName { get; init; }

    public string LastName { get; init; }

    public bool IsActive { get; init; }

    /// <summary>
    /// Set while the account is locked out after too many wrong passwords (may lie in the past).
    /// </summary>
    public DateTime? LockoutEndUtc { get; init; }

    public string? MenuAccessProfileName { get; init; }

    public string? BranchAccessProfileName { get; init; }

    /// <summary>
    /// API roles, comma separated.
    /// </summary>
    public string? Roles { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}

internal sealed class GetUsersQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetUsersQuery, PagedList<UserListItem>>
{
    private const string Filter =
        """
        WHERE (@Search IS NULL OR u.email ILIKE @Search OR u.first_name ILIKE @Search OR u.last_name ILIKE @Search
               OR (u.first_name || ' ' || u.last_name) ILIKE @Search)
          AND (@IsActive IS NULL OR u.is_active = @IsActive)
          AND (@MenuAccessProfileId IS NULL OR u.menu_access_profile_id = @MenuAccessProfileId)
          AND (@BranchAccessProfileId IS NULL OR u.branch_access_profile_id = @BranchAccessProfileId)
        """;

    public async Task<Result<PagedList<UserListItem>>> Handle(GetUsersQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        return await connection.QueryPagedAsync<UserListItem>(
            $"SELECT COUNT(*) FROM identity.users u {Filter}",
            $"""
            SELECT u.id AS Id, u.email AS Email, u.first_name AS FirstName, u.last_name AS LastName,
                   u.is_active AS IsActive, u.lockout_end_utc AS LockoutEndUtc, u.created_at_utc AS CreatedAtUtc,
                   mp.name AS MenuAccessProfileName, bp.name AS BranchAccessProfileName,
                   (SELECT string_agg(r.name, ', ' ORDER BY r.name)
                    FROM identity.user_roles ur JOIN identity.roles r ON r.id = ur.role_id
                    WHERE ur.user_id = u.id) AS Roles
            FROM identity.users u
            LEFT JOIN identity.menu_access_profiles mp ON mp.id = u.menu_access_profile_id
            LEFT JOIN identity.branch_access_profiles bp ON bp.id = u.branch_access_profile_id
            {Filter}
            ORDER BY u.first_name, u.last_name, u.email
            LIMIT @PageSize OFFSET @Offset
            """,
            query.Paging,
            new { query.IsActive, query.MenuAccessProfileId, query.BranchAccessProfileId },
            cancellationToken);
    }
}
