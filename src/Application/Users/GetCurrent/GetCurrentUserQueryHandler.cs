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
            SELECT u.id AS Id, u.email AS Email, u.first_name AS FirstName, u.last_name AS LastName
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

        return user with { AllBranches = scope.AllBranches, Branches = [.. branches] };
    }
}
