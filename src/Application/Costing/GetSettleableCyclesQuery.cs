using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Dapper;
using SharedKernel;

namespace Application.Costing;

/// <summary>
/// Closed plasma cycles (with a partnership contract) that have no settlement yet, or only cancelled ones — the cycles a
/// settlement can be created for. Search matches the cycle number, coop and farmer.
/// </summary>
public sealed record GetSettleableCyclesQuery(string? Search, Guid? BranchId = null) : IQuery<IReadOnlyList<SettleableCycleResponse>>;

public sealed record SettleableCycleResponse
{
    public Guid CycleId { get; init; }

    public string CycleNumber { get; init; }

    public string BranchCode { get; init; }

    public Guid FarmerId { get; init; }

    public string FarmerCode { get; init; }

    public string FarmerName { get; init; }

    public string CoopCode { get; init; }

    public string ContractCode { get; init; }

    public string Scheme { get; init; }

    public DateOnly ClosedDate { get; init; }
}

internal sealed class GetSettleableCyclesQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetSettleableCyclesQuery, IReadOnlyList<SettleableCycleResponse>>
{
    private const int MaxResults = 30;

    public async Task<Result<IReadOnlyList<SettleableCycleResponse>>> Handle(GetSettleableCyclesQuery query, CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        const string sql =
            """
            SELECT pc.id AS CycleId, pc.number AS CycleNumber, b.code AS BranchCode, f.id AS FarmerId, f.code AS FarmerCode,
                   f.name AS FarmerName, co.code AS CoopCode, ct.code AS ContractCode, ct.scheme AS Scheme, pc.closed_date AS ClosedDate
            FROM partnership.production_cycles pc
            JOIN master.branches b ON b.id = pc.branch_id
            JOIN master.farmers f ON f.id = pc.farmer_id
            JOIN master.coops co ON co.id = pc.coop_id
            JOIN partnership.contracts ct ON ct.id = pc.contract_id
            WHERE pc.status = 'Closed'
              AND (@AllBranches OR pc.branch_id = ANY(@BranchIds))
              AND (@BranchId::uuid IS NULL OR pc.branch_id = @BranchId)
              AND NOT EXISTS (SELECT 1 FROM costing.plasma_settlements s WHERE s.cycle_id = pc.id AND s.status <> 'Cancelled')
              AND (@Search::text IS NULL OR pc.number ILIKE @Search OR co.code ILIKE @Search OR f.code ILIKE @Search OR f.name ILIKE @Search)
            ORDER BY pc.closed_date, pc.number
            LIMIT @MaxResults;
            """;

        IEnumerable<SettleableCycleResponse> rows = await connection.QueryAsync<SettleableCycleResponse>(new CommandDefinition(
            sql,
            new
            {
                scope.AllBranches,
                scope.BranchIds,
                query.BranchId,
                Search = new PageRequest(null, null, query.Search).SearchPattern,
                MaxResults
            },
            cancellationToken: cancellationToken));

        return rows.ToList();
    }
}
