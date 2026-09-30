using System.Data.Common;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using SharedKernel;

namespace Application.Branches.Get;

internal sealed class GetBranchesQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetBranchesQuery, PagedList<BranchResponse>>
{
    private const string Filter = "WHERE (@Search IS NULL OR b.code ILIKE @Search OR b.name ILIKE @Search)";

    public async Task<Result<PagedList<BranchResponse>>> Handle(GetBranchesQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        return await connection.QueryPagedAsync<BranchResponse>(
            $"SELECT COUNT(*) FROM master.branches b {Filter}",
            $"""
            SELECT b.id AS Id, b.code AS Code, b.name AS Name, b.address AS Address, b.phone AS Phone, b.is_active AS IsActive
            FROM master.branches b
            {Filter}
            ORDER BY b.code
            LIMIT @PageSize OFFSET @Offset
            """,
            query.Paging,
            null,
            cancellationToken);
    }
}
