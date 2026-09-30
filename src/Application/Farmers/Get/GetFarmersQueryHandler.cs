using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using SharedKernel;

namespace Application.Farmers.Get;

internal sealed class GetFarmersQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetFarmersQuery, PagedList<FarmerResponse>>
{
    private const string Filter =
        """
        WHERE (@AllBranches OR f.branch_id = ANY(@BranchIds))
          AND (@BranchId::uuid IS NULL OR f.branch_id = @BranchId)
          AND (@Type::text IS NULL OR f.type = @Type)
          AND (@Search IS NULL OR f.code ILIKE @Search OR f.name ILIKE @Search OR f.nik ILIKE @Search)
        """;

    public async Task<Result<PagedList<FarmerResponse>>> Handle(GetFarmersQuery query, CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        PagedList<FarmerQueries.Row> rows = await connection.QueryPagedAsync<FarmerQueries.Row>(
            $"SELECT COUNT(*) FROM master.farmers f {Filter}",
            $"{FarmerQueries.Select} {Filter} ORDER BY f.code LIMIT @PageSize OFFSET @Offset",
            query.Paging,
            new
            {
                scope.AllBranches,
                scope.BranchIds,
                query.BranchId,
                Type = query.Type?.ToString()
            },
            cancellationToken);

        return new PagedList<FarmerResponse>(
            [.. rows.Items.Select(r => r.ToResponse())], rows.Page, rows.PageSize, rows.TotalCount);
    }
}
