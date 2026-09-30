using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using SharedKernel;

namespace Application.Cycles.Get;

internal sealed class GetCyclesQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetCyclesQuery, PagedList<CycleResponse>>
{
    private const string Filter =
        """
        WHERE (@AllBranches OR pc.branch_id = ANY(@BranchIds))
          AND (@BranchId::uuid IS NULL OR pc.branch_id = @BranchId)
          AND (@FarmerId::uuid IS NULL OR pc.farmer_id = @FarmerId)
          AND (@CoopId::uuid IS NULL OR pc.coop_id = @CoopId)
          AND (@Status::text IS NULL OR pc.status = @Status)
          AND (@Search IS NULL OR pc.number ILIKE @Search)
        """;

    public async Task<Result<PagedList<CycleResponse>>> Handle(GetCyclesQuery query, CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        return await connection.QueryPagedAsync<CycleResponse>(
            $"SELECT COUNT(*) FROM partnership.production_cycles pc {Filter}",
            $"{CycleResponse.Select} {Filter} ORDER BY pc.planned_chick_in_date DESC, pc.number LIMIT @PageSize OFFSET @Offset",
            query.Paging,
            new
            {
                scope.AllBranches,
                scope.BranchIds,
                query.BranchId,
                query.FarmerId,
                query.CoopId,
                Status = query.Status?.ToString()
            },
            cancellationToken);
    }
}
