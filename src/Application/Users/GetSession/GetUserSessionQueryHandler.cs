using System.Data.Common;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using Domain.Users;
using SharedKernel;

namespace Application.Users.GetSession;

internal sealed class GetUserSessionQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetUserSessionQuery, UserSessionResponse>
{
    public async Task<Result<UserSessionResponse>> Handle(GetUserSessionQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        const string sql =
            """
            SELECT u.is_active AS IsActive, u.security_stamp AS SecurityStamp
            FROM identity.users u
            WHERE u.id = @UserId;
            """;

        UserSessionResponse? session = await connection.QuerySingleOrDefaultAsync<UserSessionResponse>(
            new CommandDefinition(sql, new { query.UserId }, cancellationToken: cancellationToken));

        return session ?? Result.Failure<UserSessionResponse>(UserErrors.NotFound(query.UserId));
    }
}
