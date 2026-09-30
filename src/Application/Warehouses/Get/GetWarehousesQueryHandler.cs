using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using SharedKernel;

namespace Application.Warehouses.Get;

internal sealed class GetWarehousesQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetWarehousesQuery, PagedList<WarehouseResponse>>
{
    private const string Filter =
        """
        WHERE (@AllBranches OR w.branch_id = ANY(@BranchIds))
          AND (@BranchId::uuid IS NULL OR w.branch_id = @BranchId)
          AND (@Type::text IS NULL OR w.type = @Type)
          AND (@Search IS NULL OR w.code ILIKE @Search OR w.name ILIKE @Search)
        """;

    public async Task<Result<PagedList<WarehouseResponse>>> Handle(GetWarehousesQuery query, CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        return await connection.QueryPagedAsync<WarehouseResponse>(
            $"SELECT COUNT(*) FROM master.warehouses w {Filter}",
            $"""
            SELECT w.id AS Id, w.code AS Code, w.name AS Name, w.branch_id AS BranchId, b.code AS BranchCode,
                   w.type AS Type, w.coop_id AS CoopId, c.code AS CoopCode, w.address AS Address, w.is_active AS IsActive
            FROM master.warehouses w
            JOIN master.branches b ON b.id = w.branch_id
            LEFT JOIN master.coops c ON c.id = w.coop_id
            {Filter}
            ORDER BY w.code
            LIMIT @PageSize OFFSET @Offset
            """,
            query.Paging,
            new
            {
                scope.AllBranches,
                scope.BranchIds,
                query.BranchId,
                Type = query.Type?.ToString()
            },
            cancellationToken);
    }
}
