using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using SharedKernel;

namespace Application.Costing;

/// <summary>
/// The plasma farmer's debt (piutang plasma) as far as settlements know it: the losses of approved settlements minus
/// the debt deducted from approved settlements, in the accessible branches. Informational, to suggest the debt
/// deduction of the next settlement; it is not a full sub-ledger (debts or repayments from elsewhere are not included).
/// </summary>
public sealed record GetFarmerPlasmaDebtQuery(Guid FarmerId) : IQuery<FarmerPlasmaDebtResponse>;

/// <param name="Balance">Deficits − deductions, never below zero.</param>
public sealed record FarmerPlasmaDebtResponse(Guid FarmerId, decimal Deficits, decimal Deducted, decimal Balance);

internal sealed class GetFarmerPlasmaDebtQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetFarmerPlasmaDebtQuery, FarmerPlasmaDebtResponse>
{
    public async Task<Result<FarmerPlasmaDebtResponse>> Handle(GetFarmerPlasmaDebtQuery query, CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        const string sql =
            """
            SELECT COALESCE(SUM(s.deficit), 0) AS Deficits, COALESCE(SUM(s.debt_deduction), 0) AS Deducted
            FROM costing.plasma_settlements s
            WHERE s.farmer_id = @FarmerId
              AND s.status IN ('Approved', 'PartiallyPaid', 'Paid')
              AND (@AllBranches OR s.branch_id = ANY(@BranchIds));
            """;

        (decimal deficits, decimal deducted) = await connection.QuerySingleAsync<(decimal, decimal)>(new CommandDefinition(
            sql,
            new { query.FarmerId, scope.AllBranches, scope.BranchIds },
            cancellationToken: cancellationToken));

        return new FarmerPlasmaDebtResponse(query.FarmerId, deficits, deducted, Math.Max(deficits - deducted, 0m));
    }
}
