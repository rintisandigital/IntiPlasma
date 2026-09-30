using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using SharedKernel;

namespace Application.Finance.Reports;

/// <summary>
/// Neraca saldo per postable account: opening balance before <see cref="From"/>, mutations in the range,
/// and closing balance, each split into debit and credit columns.
/// </summary>
public sealed record GetTrialBalanceQuery(DateOnly From, DateOnly To, Guid? BranchId, bool IncludeZeroBalances)
    : IQuery<TrialBalanceResponse>;

public sealed record TrialBalanceResponse(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<TrialBalanceLine> Lines,
    TrialBalanceTotals Totals);

public sealed record TrialBalanceLine(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    string AccountType,
    decimal OpeningDebit,
    decimal OpeningCredit,
    decimal MutationDebit,
    decimal MutationCredit,
    decimal ClosingDebit,
    decimal ClosingCredit);

public sealed record TrialBalanceTotals(
    decimal OpeningDebit,
    decimal OpeningCredit,
    decimal MutationDebit,
    decimal MutationCredit,
    decimal ClosingDebit,
    decimal ClosingCredit)
{
    /// <summary>
    /// Always true for a consistent ledger; exposed so clients can flag a problem immediately.
    /// </summary>
    public bool IsBalanced => OpeningDebit == OpeningCredit && MutationDebit == MutationCredit && ClosingDebit == ClosingCredit;
}

internal sealed class GetTrialBalanceQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetTrialBalanceQuery, TrialBalanceResponse>
{
    public async Task<Result<TrialBalanceResponse>> Handle(GetTrialBalanceQuery query, CancellationToken cancellationToken)
    {
        if (query.To < query.From)
        {
            return Result.Failure<TrialBalanceResponse>(ReportErrors.InvalidDateRange);
        }

        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql =
            $"""
            WITH movements AS (
                SELECT l.account_id,
                       SUM(CASE WHEN j.date < @From THEN l.debit - l.credit ELSE 0 END) AS opening,
                       SUM(CASE WHEN j.date >= @From THEN l.debit ELSE 0 END) AS debit,
                       SUM(CASE WHEN j.date >= @From THEN l.credit ELSE 0 END) AS credit
                FROM finance.journal_lines l
                JOIN finance.journal_entries j ON j.id = l.journal_entry_id
                WHERE j.status IN {LedgerSql.PostedStatuses} AND j.date <= @To AND {LedgerSql.BranchFilter}
                GROUP BY l.account_id
            )
            SELECT a.id AS AccountId, a.code AS AccountCode, a.name AS AccountName, a.type AS AccountType,
                   COALESCE(m.opening, 0) AS Opening, COALESCE(m.debit, 0) AS Debit, COALESCE(m.credit, 0) AS Credit
            FROM finance.accounts a
            LEFT JOIN movements m ON m.account_id = a.id
            WHERE a.is_postable
            ORDER BY a.code
            """;

        IEnumerable<Row> rows = await connection.QueryAsync<Row>(new CommandDefinition(
            sql,
            new { query.From, query.To, query.BranchId, scope.AllBranches, scope.BranchIds },
            cancellationToken: cancellationToken));

        var lines = rows
            .Select(r =>
            {
                decimal closing = r.Opening + r.Debit - r.Credit;

                return new TrialBalanceLine(
                    r.AccountId,
                    r.AccountCode,
                    r.AccountName,
                    r.AccountType,
                    Math.Max(r.Opening, 0),
                    Math.Max(-r.Opening, 0),
                    r.Debit,
                    r.Credit,
                    Math.Max(closing, 0),
                    Math.Max(-closing, 0));
            })
            .Where(l => query.IncludeZeroBalances ||
                        l.OpeningDebit != 0 || l.OpeningCredit != 0 || l.MutationDebit != 0 || l.MutationCredit != 0)
            .ToList();

        var totals = new TrialBalanceTotals(
            lines.Sum(l => l.OpeningDebit),
            lines.Sum(l => l.OpeningCredit),
            lines.Sum(l => l.MutationDebit),
            lines.Sum(l => l.MutationCredit),
            lines.Sum(l => l.ClosingDebit),
            lines.Sum(l => l.ClosingCredit));

        return new TrialBalanceResponse(query.From, query.To, lines, totals);
    }

    private sealed record Row(Guid AccountId, string AccountCode, string AccountName, string AccountType, decimal Opening, decimal Debit, decimal Credit);
}
