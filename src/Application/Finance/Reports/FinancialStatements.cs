using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using Domain.Finance.Accounts;
using SharedKernel;

namespace Application.Finance.Reports;

/// <summary>
/// Laporan Laba Rugi for a date range: revenue and expense accounts grouped by their top-level account in the chart of
/// accounts. Year-end closing journals are left out, so a closed year still shows its result.
/// </summary>
public sealed record GetIncomeStatementQuery(DateOnly From, DateOnly To, Guid? BranchId) : IQuery<IncomeStatementResponse>;

/// <summary>
/// Neraca as of a date: asset, liability and equity accounts by top-level account, plus the profit of the current
/// year and of earlier years not closed yet (both zero after the year-end closing).
/// </summary>
public sealed record GetBalanceSheetQuery(DateOnly AsOf, Guid? BranchId) : IQuery<BalanceSheetResponse>;

/// <summary>
/// Laporan Arus Kas, direct method: every posted movement of a cash/bank account classified by the cash flow category
/// of its counter accounts (operating, investing, financing). Transfers between cash/bank accounts net to zero.
/// </summary>
public sealed record GetCashFlowStatementQuery(DateOnly From, DateOnly To, Guid? BranchId) : IQuery<CashFlowStatementResponse>;

/// <param name="Amount">In the statement's direction: revenue positive as credit, expense positive as debit.</param>
public sealed record StatementAccount(Guid AccountId, string Code, string Name, decimal Amount);

public sealed record StatementSection(string Code, string Name, string Type, decimal Total, IReadOnlyList<StatementAccount> Accounts);

public sealed record IncomeStatementResponse(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<StatementSection> Sections,
    decimal TotalRevenue,
    decimal TotalExpense,
    decimal NetIncome);

public sealed record BalanceSheetResponse(
    DateOnly AsOf,
    IReadOnlyList<StatementSection> Assets,
    IReadOnlyList<StatementSection> Liabilities,
    IReadOnlyList<StatementSection> Equity,
    decimal CurrentYearEarnings,
    decimal UnclosedPriorYearsEarnings,
    decimal TotalAssets,
    decimal TotalLiabilities,
    decimal TotalEquity,
    bool IsBalanced);

public sealed record CashFlowLine(string AccountCode, string AccountName, decimal Inflow, decimal Outflow, decimal Net);

public sealed record CashFlowSection(string Category, decimal Inflow, decimal Outflow, decimal Net, IReadOnlyList<CashFlowLine> Lines);

/// <param name="ClosingCashPerLedger">Cash/bank account balances at the end date; equals <paramref name="ClosingCash"/>.</param>
public sealed record CashFlowStatementResponse(
    DateOnly From,
    DateOnly To,
    decimal OpeningCash,
    IReadOnlyList<CashFlowSection> Sections,
    decimal NetChange,
    decimal ClosingCash,
    decimal ClosingCashPerLedger,
    bool IsConsistent);

internal static class StatementSql
{
    public const string ClosingSourceTypes = "('YearEndClosing', 'YearEndClosing.Reversal')";

    /// <summary>
    /// Balance (debit − credit) per postable account with its top-level account. Parameters: @From (nullable), @To,
    /// @AllBranches, @BranchIds, @BranchId, @Types (account types), @ExcludeClosing.
    /// </summary>
    public const string AccountBalances =
        $"""
        WITH RECURSIVE tree AS (
            SELECT a.id, a.id AS root_id FROM finance.accounts a WHERE a.parent_id IS NULL
            UNION ALL
            SELECT c.id, t.root_id FROM finance.accounts c JOIN tree t ON c.parent_id = t.id
        )
        SELECT r.code AS SectionCode, r.name AS SectionName, a.type AS Type, a.id AS AccountId, a.code AS Code, a.name AS Name,
               SUM(l.debit - l.credit) AS Balance
        FROM finance.journal_lines l
        JOIN finance.journal_entries j ON j.id = l.journal_entry_id
        JOIN finance.accounts a ON a.id = l.account_id
        JOIN tree t ON t.id = a.id
        JOIN finance.accounts r ON r.id = t.root_id
        WHERE j.status IN {LedgerSql.PostedStatuses}
          AND (@From::date IS NULL OR j.date >= @From) AND j.date <= @To
          AND a.type = ANY(@Types)
          AND (NOT @ExcludeClosing OR j.source_type IS NULL OR j.source_type NOT IN {ClosingSourceTypes})
          AND {LedgerSql.BranchFilter}
        GROUP BY r.code, r.name, a.type, a.id, a.code, a.name
        HAVING SUM(l.debit - l.credit) <> 0
        ORDER BY a.code
        """;

    /// <summary>
    /// Debit-normal types show debit − credit, credit-normal types credit − debit.
    /// </summary>
    public static decimal Signed(string type, decimal balance) =>
        type is nameof(AccountType.Asset) or nameof(AccountType.Expense) ? balance : -balance;

    public static IReadOnlyList<StatementSection> Sections(IEnumerable<BalanceRow> rows) =>
    [
        .. rows
            .GroupBy(r => (r.SectionCode, r.SectionName, r.Type))
            .OrderBy(g => g.Key.SectionCode, StringComparer.Ordinal)
            .Select(g =>
            {
                var accounts = g.Select(r => new StatementAccount(r.AccountId, r.Code, r.Name, Signed(r.Type, r.Balance))).ToList();
                return new StatementSection(g.Key.SectionCode, g.Key.SectionName, g.Key.Type, accounts.Sum(a => a.Amount), accounts);
            })
    ];

    public static async Task<List<BalanceRow>> BalancesAsync(
        DbConnection connection,
        BranchScope scope,
        Guid? branchId,
        DateOnly? from,
        DateOnly to,
        string[] types,
        bool excludeClosing,
        CancellationToken cancellationToken) =>
        [
            .. await connection.QueryAsync<BalanceRow>(new CommandDefinition(
                AccountBalances,
                new { From = from, To = to, Types = types, ExcludeClosing = excludeClosing, scope.AllBranches, scope.BranchIds, BranchId = branchId },
                cancellationToken: cancellationToken))
        ];

    internal sealed class BalanceRow
    {
        public string SectionCode { get; set; }
        public string SectionName { get; set; }
        public string Type { get; set; }
        public Guid AccountId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public decimal Balance { get; set; }
    }
}

internal sealed class GetIncomeStatementQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetIncomeStatementQuery, IncomeStatementResponse>
{
    private static readonly string[] Types = [nameof(AccountType.Revenue), nameof(AccountType.Expense)];

    public async Task<Result<IncomeStatementResponse>> Handle(GetIncomeStatementQuery query, CancellationToken cancellationToken)
    {
        if (query.To < query.From)
        {
            return Result.Failure<IncomeStatementResponse>(ReportErrors.InvalidDateRange);
        }

        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        List<StatementSql.BalanceRow> rows = await StatementSql.BalancesAsync(
            connection, scope, query.BranchId, query.From, query.To, Types, excludeClosing: true, cancellationToken);

        IReadOnlyList<StatementSection> sections = StatementSql.Sections(rows);
        decimal revenue = sections.Where(s => s.Type == nameof(AccountType.Revenue)).Sum(s => s.Total);
        decimal expense = sections.Where(s => s.Type == nameof(AccountType.Expense)).Sum(s => s.Total);

        return new IncomeStatementResponse(query.From, query.To, sections, revenue, expense, revenue - expense);
    }
}

internal sealed class GetBalanceSheetQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetBalanceSheetQuery, BalanceSheetResponse>
{
    private static readonly string[] PositionTypes = [nameof(AccountType.Asset), nameof(AccountType.Liability), nameof(AccountType.Equity)];
    private static readonly string[] ProfitTypes = [nameof(AccountType.Revenue), nameof(AccountType.Expense)];

    public async Task<Result<BalanceSheetResponse>> Handle(GetBalanceSheetQuery query, CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        List<StatementSql.BalanceRow> positions = await StatementSql.BalancesAsync(
            connection, scope, query.BranchId, null, query.AsOf, PositionTypes, excludeClosing: false, cancellationToken);

        // Profit not yet closed to retained earnings: the closing journals are included, so a closed year nets to zero.
        var yearStart = new DateOnly(query.AsOf.Year, 1, 1);
        List<StatementSql.BalanceRow> profit = await StatementSql.BalancesAsync(
            connection, scope, query.BranchId, null, query.AsOf, ProfitTypes, excludeClosing: false, cancellationToken);
        List<StatementSql.BalanceRow> priorProfit = await StatementSql.BalancesAsync(
            connection, scope, query.BranchId, null, yearStart.AddDays(-1), ProfitTypes, excludeClosing: false, cancellationToken);

        decimal prior = -priorProfit.Sum(r => r.Balance);
        decimal current = -profit.Sum(r => r.Balance) - prior;

        IReadOnlyList<StatementSection> sections = StatementSql.Sections(positions);
        IReadOnlyList<StatementSection> assets = [.. sections.Where(s => s.Type == nameof(AccountType.Asset))];
        IReadOnlyList<StatementSection> liabilities = [.. sections.Where(s => s.Type == nameof(AccountType.Liability))];
        IReadOnlyList<StatementSection> equity = [.. sections.Where(s => s.Type == nameof(AccountType.Equity))];

        decimal totalAssets = assets.Sum(s => s.Total);
        decimal totalLiabilities = liabilities.Sum(s => s.Total);
        decimal totalEquity = equity.Sum(s => s.Total) + current + prior;

        return new BalanceSheetResponse(
            query.AsOf, assets, liabilities, equity, current, prior, totalAssets, totalLiabilities, totalEquity,
            totalAssets == totalLiabilities + totalEquity);
    }
}

internal sealed class GetCashFlowStatementQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetCashFlowStatementQuery, CashFlowStatementResponse>
{
    private static readonly string[] Categories =
        [nameof(CashFlowCategory.Operating), nameof(CashFlowCategory.Investing), nameof(CashFlowCategory.Financing)];

    public async Task<Result<CashFlowStatementResponse>> Handle(GetCashFlowStatementQuery query, CancellationToken cancellationToken)
    {
        if (query.To < query.From)
        {
            return Result.Failure<CashFlowStatementResponse>(ReportErrors.InvalidDateRange);
        }

        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql =
            $"""
            WITH cash AS (SELECT account_id FROM finance.cash_bank_accounts)
            SELECT COALESCE(SUM(l.debit - l.credit), 0)
            FROM finance.journal_lines l JOIN finance.journal_entries j ON j.id = l.journal_entry_id
            WHERE l.account_id IN (SELECT account_id FROM cash) AND j.status IN {LedgerSql.PostedStatuses}
              AND j.date < @From AND {LedgerSql.BranchFilter};

            WITH cash AS (SELECT account_id FROM finance.cash_bank_accounts)
            SELECT COALESCE(SUM(l.debit - l.credit), 0)
            FROM finance.journal_lines l JOIN finance.journal_entries j ON j.id = l.journal_entry_id
            WHERE l.account_id IN (SELECT account_id FROM cash) AND j.status IN {LedgerSql.PostedStatuses}
              AND j.date <= @To AND {LedgerSql.BranchFilter};

            WITH cash AS (SELECT account_id FROM finance.cash_bank_accounts),
            cash_journals AS (
                SELECT DISTINCT j.id FROM finance.journal_entries j JOIN finance.journal_lines l ON l.journal_entry_id = j.id
                WHERE l.account_id IN (SELECT account_id FROM cash) AND j.status IN {LedgerSql.PostedStatuses}
                  AND j.date BETWEEN @From AND @To AND {LedgerSql.BranchFilter})
            SELECT a.cash_flow_category AS Category, a.code AS AccountCode, a.name AS AccountName,
                   SUM(l.credit) AS Inflow, SUM(l.debit) AS Outflow
            FROM finance.journal_lines l
            JOIN cash_journals cj ON cj.id = l.journal_entry_id
            JOIN finance.accounts a ON a.id = l.account_id
            WHERE l.account_id NOT IN (SELECT account_id FROM cash)
            GROUP BY a.cash_flow_category, a.code, a.name
            ORDER BY a.code;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new { query.From, query.To, query.BranchId, scope.AllBranches, scope.BranchIds },
            cancellationToken: cancellationToken));

        decimal opening = await multi.ReadSingleAsync<decimal>();
        decimal closingPerLedger = await multi.ReadSingleAsync<decimal>();
        List<FlowRow> rows = [.. await multi.ReadAsync<FlowRow>()];

        IReadOnlyList<CashFlowSection> sections =
        [
            .. Categories.Select(category =>
            {
                var lines = rows
                    .Where(r => r.Category == category)
                    .Select(r => new CashFlowLine(r.AccountCode, r.AccountName, r.Inflow, r.Outflow, r.Inflow - r.Outflow))
                    .ToList();

                decimal inflow = lines.Sum(l => l.Inflow);
                decimal outflow = lines.Sum(l => l.Outflow);

                return new CashFlowSection(category, inflow, outflow, inflow - outflow, lines);
            })
        ];

        decimal netChange = sections.Sum(s => s.Net);
        decimal closing = opening + netChange;

        return new CashFlowStatementResponse(
            query.From, query.To, opening, sections, netChange, closing, closingPerLedger, closing == closingPerLedger);
    }

    internal sealed class FlowRow
    {
        public string Category { get; set; }
        public string AccountCode { get; set; }
        public string AccountName { get; set; }
        public decimal Inflow { get; set; }
        public decimal Outflow { get; set; }
    }
}
