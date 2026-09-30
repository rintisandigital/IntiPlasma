using System.Data.Common;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using Domain.MasterData.Branches;
using SharedKernel;

namespace Application.Branches.GetById;

internal sealed class GetBranchByIdQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetBranchByIdQuery, BranchResponse>
{
    public async Task<Result<BranchResponse>> Handle(GetBranchByIdQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        BranchResponse? branch = await connection.QuerySingleOrDefaultAsync<BranchResponse>(new CommandDefinition(
            """
            SELECT b.id AS Id, b.code AS Code, b.name AS Name, b.address AS Address, b.phone AS Phone, b.is_active AS IsActive
            FROM master.branches b
            WHERE b.id = @BranchId
            """,
            new { query.BranchId },
            cancellationToken: cancellationToken));

        return branch ?? Result.Failure<BranchResponse>(BranchErrors.NotFound(query.BranchId));
    }
}
