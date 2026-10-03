using System.Data.Common;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using Domain.Users;
using SharedKernel;

namespace Application.Users.GetById;

internal sealed class GetUserByIdQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetUserByIdQuery, UserResponse>
{
    public async Task<Result<UserResponse>> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        const string sql =
            """
            SELECT u.id AS Id, u.email AS Email, u.first_name AS FirstName, u.last_name AS LastName,
                   u.is_active AS IsActive, u.created_at_utc AS CreatedAtUtc,
                   u.access_failed_count AS AccessFailedCount, u.lockout_end_utc AS LockoutEndUtc,
                   u.menu_access_profile_id AS MenuAccessProfileId, mp.name AS MenuAccessProfileName,
                   u.branch_access_profile_id AS BranchAccessProfileId, bp.name AS BranchAccessProfileName,
                   COALESCE(bp.all_branches, false) AS AllBranches,
                   u.default_branch_id AS DefaultBranchId, db.name AS DefaultBranchName
            FROM identity.users u
            LEFT JOIN identity.menu_access_profiles mp ON mp.id = u.menu_access_profile_id
            LEFT JOIN identity.branch_access_profiles bp ON bp.id = u.branch_access_profile_id
            LEFT JOIN master.branches db ON db.id = u.default_branch_id
            WHERE u.id = @UserId;

            SELECT r.id AS Id, r.name AS Name
            FROM identity.user_roles ur
            JOIN identity.roles r ON r.id = ur.role_id
            WHERE ur.user_id = @UserId
            ORDER BY r.name;

            SELECT b.id AS Id, b.code AS Code, b.name AS Name
            FROM identity.users u
            JOIN identity.branch_access_profile_branches pb ON pb.profile_id = u.branch_access_profile_id
            JOIN master.branches b ON b.id = pb.branch_id
            WHERE u.id = @UserId AND b.is_active
            ORDER BY b.code;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { query.UserId }, cancellationToken: cancellationToken));

        UserResponse? user = await multi.ReadSingleOrDefaultAsync<UserResponse>();

        if (user is null)
        {
            return Result.Failure<UserResponse>(UserErrors.NotFound(query.UserId));
        }

        List<RoleRow> roles = [.. await multi.ReadAsync<RoleRow>()];
        List<UserBranchResponse> branches = [.. await multi.ReadAsync<UserBranchResponse>()];

        return user with
        {
            RoleIds = [.. roles.Select(r => r.Id)],
            Roles = [.. roles.Select(r => r.Name)],
            Branches = user.AllBranches ? [] : branches
        };
    }

    internal sealed class RoleRow
    {
        public Guid Id { get; set; }

        public string Name { get; set; }
    }
}
