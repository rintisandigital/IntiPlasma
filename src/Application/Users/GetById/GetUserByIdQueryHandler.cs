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
            SELECT u.id AS Id, u.email AS Email, u.first_name AS FirstName, u.last_name AS LastName
            FROM identity.users u
            WHERE u.id = @UserId;

            SELECT r.name
            FROM identity.user_roles ur
            JOIN identity.roles r ON r.id = ur.role_id
            WHERE ur.user_id = @UserId
            ORDER BY r.name;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { query.UserId }, cancellationToken: cancellationToken));

        UserResponse? user = await multi.ReadSingleOrDefaultAsync<UserResponse>();

        if (user is null)
        {
            return Result.Failure<UserResponse>(UserErrors.NotFound(query.UserId));
        }

        IEnumerable<string> roles = await multi.ReadAsync<string>();

        return user with { Roles = [.. roles] };
    }
}
