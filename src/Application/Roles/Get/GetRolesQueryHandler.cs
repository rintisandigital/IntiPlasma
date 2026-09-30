using System.Data.Common;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using SharedKernel;

namespace Application.Roles.Get;

internal sealed class GetRolesQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetRolesQuery, IReadOnlyList<RoleResponse>>
{
    public async Task<Result<IReadOnlyList<RoleResponse>>> Handle(GetRolesQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        const string sql =
            """
            SELECT r.id AS Id, r.name AS Name, r.description AS Description, r.is_system AS IsSystem
            FROM identity.roles r
            ORDER BY r.name;

            SELECT rp.role_id AS RoleId, rp.permission AS Permission
            FROM identity.role_permissions rp
            ORDER BY rp.permission;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        List<RoleResponse> roles = [.. await multi.ReadAsync<RoleResponse>()];

        ILookup<Guid, string> permissionsByRole = (await multi.ReadAsync<RolePermissionRow>())
            .ToLookup(rp => rp.RoleId, rp => rp.Permission);

        return roles
            .Select(r => r with { Permissions = [.. permissionsByRole[r.Id]] })
            .ToList();
    }

    private sealed record RolePermissionRow(Guid RoleId, string Permission);
}
