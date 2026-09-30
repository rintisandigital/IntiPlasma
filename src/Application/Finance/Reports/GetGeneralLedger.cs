using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using Domain.Finance.Accounts;
using SharedKernel;

namespace Application.Finance.Reports;

/// <summary>
/// Buku besar: opening balance, every posted mutation with a running balance, and closing balance of one account.
/// Balances are expressed in the account's normal balance direction (positive = normal).
/// </summary>
public sealed record GetGeneralLedgerQuery(
    Guid AccountId,
    DateOnly From,
    DateOnly To,
    Guid? BranchId,
    Guid? CostCenterId) : IQuery<GeneralLedgerResponse>;

public sealed record GeneralLedgerResponse(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    string NormalBalance,
    DateOnly From,
    DateOnly To,
    decimal OpeningBalance,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal ClosingBalance,
    IReadOnlyList<GeneralLedgerLine> Lines);

public sealed record GeneralLedgerLine(
    Guid JournalId,
    string Number,
    DateOnly Date,
    string BranchCode,
    string Description,
    string? LineDescription,
    string? CostCenterCode,
    decimal Debit,
    decimal Credit,
    decimal Balance);

internal static class LedgerSql
{
    /// <summary>
    /// Posted and reversed journals are both in the ledger (a reversal journal offsets the original).
    /// </summary>
    public const string PostedStatuses = "('Posted', 'Reversed')";

    public const string BranchFilter =
        """
        (@AllBranches OR j.branch_id = ANY(@BranchIds))
        AND (@BranchId::uuid IS NULL OR j.branch_id = @BranchId)
        """;
}

internal sealed class GetGeneralLedgerQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetGeneralLedgerQuery, GeneralLedgerResponse>
{
    public async Task<Result<GeneralLedgerResponse>> Handle(GetGeneralLedgerQuery query, CancellationToken cancellationToken)
    {
        if (query.To < query.From)
        {
            return Result.Failure<GeneralLedgerResponse>(ReportErrors.InvalidDateRange);
        }

        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql =
            $"""
            SELECT a.id AS Id, a.code AS Code, a.name AS Name, a.normal_balance AS NormalBalance
            FROM finance.accounts a WHERE a.id = @AccountId;

            SELECT COALESCE(SUM(l.debit - l.credit), 0)
            FROM finance.journal_lines l
            JOIN finance.journal_entries j ON j.id = l.journal_entry_id
            WHERE l.account_id = @AccountId AND j.status IN {LedgerSql.PostedStatuses} AND j.date < @From
              AND (@CostCenterId::uuid IS NULL OR l.cost_center_id = @CostCenterId)
              AND {LedgerSql.BranchFilter};

            SELECT j.id AS JournalId, j.number AS Number, j.date AS Date, b.code AS BranchCode, j.description AS Description,
                   l.description AS LineDescription, c.code AS CostCenterCode, l.debit AS Debit, l.credit AS Credit
            FROM finance.journal_lines l
            JOIN finance.journal_entries j ON j.id = l.journal_entry_id
            JOIN master.branches b ON b.id = j.branch_id
            LEFT JOIN finance.cost_centers c ON c.id = l.cost_center_id
            WHERE l.account_id = @AccountId AND j.status IN {LedgerSql.PostedStatuses}
              AND j.date BETWEEN @From AND @To
              AND (@CostCenterId::uuid IS NULL OR l.cost_center_id = @CostCenterId)
              AND {LedgerSql.BranchFilter}
            ORDER BY j.date, j.number, l.line_number;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new
            {
                query.AccountId,
                query.From,
                query.To,
                query.BranchId,
                query.CostCenterId,
                scope.AllBranches,
                scope.BranchIds
            },
            cancellationToken: cancellationToken));

        AccountRow? account = await multi.ReadSingleOrDefaultAsync<AccountRow>();
        if (account is null)
        {
            return Result.Failure<GeneralLedgerResponse>(AccountErrors.NotFound(query.AccountId));
        }

        // Debit-normal accounts grow with debits; credit-normal accounts grow with credits.
        decimal sign = account.NormalBalance == nameof(BalanceSide.Debit) ? 1m : -1m;

        decimal opening = sign * await multi.ReadSingleAsync<decimal>();
        decimal balance = opening;

        var lines = new List<GeneralLedgerLine>();
        foreach (MutationRow row in await multi.ReadAsync<MutationRow>())
        {
            balance += sign * (row.Debit - row.Credit);
            lines.Add(new GeneralLedgerLine(
                row.JournalId, row.Number, row.Date, row.BranchCode, row.Description, row.LineDescription,
                row.CostCenterCode, row.Debit, row.Credit, balance));
        }

        return new GeneralLedgerResponse(
            account.Id,
            account.Code,
            account.Name,
            account.NormalBalance,
            query.From,
            query.To,
            opening,
            lines.Sum(l => l.Debit),
            lines.Sum(l => l.Credit),
            balance,
            lines);
    }

    private sealed record AccountRow(Guid Id, string Code, string Name, string NormalBalance);

    private sealed record MutationRow(
        Guid JournalId,
        string Number,
        DateOnly Date,
        string BranchCode,
        string Description,
        string? LineDescription,
        string? CostCenterCode,
        decimal Debit,
        decimal Credit);
}
