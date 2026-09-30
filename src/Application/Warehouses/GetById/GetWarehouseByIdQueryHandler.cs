using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using Domain.MasterData.Warehouses;
using SharedKernel;

namespace Application.Warehouses.GetById;

internal sealed class GetWarehouseByIdQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetWarehouseByIdQuery, WarehouseResponse>
{
    public async Task<Result<WarehouseResponse>> Handle(GetWarehouseByIdQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        WarehouseResponse? warehouse = await connection.QuerySingleOrDefaultAsync<WarehouseResponse>(new CommandDefinition(
            """
            SELECT w.id AS Id, w.code AS Code, w.name AS Name, w.branch_id AS BranchId, b.code AS BranchCode,
                   w.type AS Type, w.coop_id AS CoopId, c.code AS CoopCode, w.address AS Address, w.is_active AS IsActive
            FROM master.warehouses w
            JOIN master.branches b ON b.id = w.branch_id
            LEFT JOIN master.coops c ON c.id = w.coop_id
            WHERE w.id = @WarehouseId
            """,
            new { query.WarehouseId },
            cancellationToken: cancellationToken));

        if (warehouse is null)
        {
            return Result.Failure<WarehouseResponse>(WarehouseErrors.NotFound(query.WarehouseId));
        }

        Result access = await branchAccess.EnsureAccessAsync(warehouse.BranchId, cancellationToken);

        return access.IsSuccess ? warehouse : Result.Failure<WarehouseResponse>(access.Error);
    }
}
