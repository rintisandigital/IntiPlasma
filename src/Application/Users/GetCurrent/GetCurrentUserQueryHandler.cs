using System.Data.Common;
using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using Domain.Users;
using SharedKernel;

namespace Application.Users.GetCurrent;

internal sealed class GetCurrentUserQueryHandler(
    IDbConnectionFactory dbConnectionFactory,
    IUserContext userContext,
    IBranchAccess branchAccess) : IQueryHandler<GetCurrentUserQuery, CurrentUserResponse>
{
    public async Task<Result<CurrentUserResponse>> Handle(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        Guid userId = userContext.UserId;
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        const string userSql =
            """
            SELECT u.id AS Id, u.email AS Email, u.first_name AS FirstName, u.last_name AS LastName,
                   u.default_branch_id AS DefaultBranchId
            FROM identity.users u
            WHERE u.id = @UserId;
            """;

        CurrentUserResponse? user = await connection.QuerySingleOrDefaultAsync<CurrentUserResponse>(
            new CommandDefinition(userSql, new { UserId = userId }, cancellationToken: cancellationToken));

        if (user is null)
        {
            return Result.Failure<CurrentUserResponse>(UserErrors.NotFound(userId));
        }

        const string branchSql =
            """
            SELECT b.id AS Id, b.code AS Code, b.name AS Name
            FROM master.branches b
            WHERE b.is_active AND (@AllBranches OR b.id = ANY(@BranchIds))
            ORDER BY b.code;
            """;

        IEnumerable<CurrentUserBranch> branches = await connection.QueryAsync<CurrentUserBranch>(
            new CommandDefinition(
                branchSql,
                new { scope.AllBranches, scope.BranchIds },
                cancellationToken: cancellationToken));

        const string roleSql =
            """
            SELECT r.name
            FROM identity.user_roles ur
            JOIN identity.roles r ON r.id = ur.role_id
            WHERE ur.user_id = @UserId
            ORDER BY r.name;

            SELECT DISTINCT rp.permission
            FROM identity.user_roles ur
            JOIN identity.role_permissions rp ON rp.role_id = ur.role_id
            WHERE ur.user_id = @UserId
            ORDER BY rp.permission;
            """;

        await using SqlMapper.GridReader grid = await connection.QueryMultipleAsync(
            new CommandDefinition(roleSql, new { UserId = userId }, cancellationToken: cancellationToken));

        IEnumerable<string> roles = await grid.ReadAsync<string>();
        IEnumerable<string> permissions = await grid.ReadAsync<string>();

        return user with
        {
            AllBranches = scope.AllBranches,
            Branches = [.. branches],
            Roles = [.. roles],
            Permissions = [.. permissions]
        };
    }
}
