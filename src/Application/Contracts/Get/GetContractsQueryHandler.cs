using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using SharedKernel;

namespace Application.Contracts.Get;

internal sealed class GetContractsQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetContractsQuery, PagedList<ContractResponse>>
{
    private const string Filter =
        """
        WHERE (@AllBranches OR c.branch_id = ANY(@BranchIds))
          AND (@BranchId::uuid IS NULL OR c.branch_id = @BranchId)
          AND (@Status::text IS NULL OR c.status = @Status)
          AND (@Search IS NULL OR c.code ILIKE @Search OR c.name ILIKE @Search)
        """;

    public async Task<Result<PagedList<ContractResponse>>> Handle(GetContractsQuery query, CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        return await connection.QueryPagedAsync<ContractResponse>(
            $"SELECT COUNT(*) FROM partnership.contracts c {Filter}",
            $"{ContractResponse.Select} {Filter} ORDER BY c.valid_from DESC, c.code LIMIT @PageSize OFFSET @Offset",
            query.Paging,
            new { scope.AllBranches, scope.BranchIds, query.BranchId, Status = query.Status?.ToString() },
            cancellationToken);
    }
}
