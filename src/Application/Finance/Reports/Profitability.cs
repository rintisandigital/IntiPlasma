using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using SharedKernel;

namespace Application.Finance.Reports;

public enum ProfitabilityGrouping
{
    Cycle = 1,
    Coop = 2,
    Farmer = 3,
    Branch = 4
}

/// <summary>
/// Analisa profitabilitas (contribution margin, no overhead allocation): net sales (DPP − credit notes) − HPP
/// (estimate on the invoices + the closing adjustment) − plasma fee (settlement income; a loss is negative), from the
/// documents dated in the range. Per branch the ledger's net income is shown too, so the difference is the branch's
/// overhead and other income/expenses.
/// </summary>
public sealed record GetProfitabilityQuery(
    DateOnly From,
    DateOnly To,
    ProfitabilityGrouping GroupBy,
    Guid? BranchId,
    Guid? FarmerId) : IQuery<ProfitabilityResponse>;

/// <param name="LedgerNetIncome">Branch grouping only: net income of the branch per the general ledger.</param>
/// <param name="OverheadAndOther">Branch grouping only: ledger net income − contribution margin.</param>
public sealed record ProfitabilityRow(
    Guid Id,
    string Code,
    string Name,
    int Cycles,
    int Birds,
    decimal WeightKg,
    decimal Sales,
    decimal CreditNotes,
    decimal NetSales,
    decimal CostOfGoodsSold,
    decimal PlasmaFee,
    decimal Margin,
    decimal? MarginPerKg,
    decimal? LedgerNetIncome,
    decimal? OverheadAndOther);

public sealed record ProfitabilityResponse(
    DateOnly From,
    DateOnly To,
    string GroupBy,
    IReadOnlyList<ProfitabilityRow> Rows,
    ProfitabilityRow Total);

internal sealed class GetProfitabilityQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetProfitabilityQuery, ProfitabilityResponse>
{
    private const string CycleFigures =
        """
        WITH sales AS (
            SELECT l.cycle_id, SUM(l.amount) AS sales, SUM(l.birds) AS birds, SUM(l.weight_kg) AS kg, SUM(l.cost_amount) AS cost
            FROM sales.sales_invoice_lines l JOIN sales.sales_invoices i ON i.id = l.sales_invoice_id
            WHERE i.status IN ('Posted', 'PartiallyPaid', 'Paid') AND i.invoice_date BETWEEN @From AND @To
            GROUP BY l.cycle_id),
        credits AS (
            SELECT cl.cycle_id, SUM(cl.amount) AS credits
            FROM sales.sales_credit_note_lines cl JOIN sales.sales_credit_notes n ON n.id = cl.sales_credit_note_id
            WHERE n.date BETWEEN @From AND @To
            GROUP BY cl.cycle_id),
        adjustments AS (
            SELECT c.id AS cycle_id, (c.closing_cost ->> 'adjustment')::numeric AS adjustment
            FROM partnership.production_cycles c
            WHERE c.closing_cost IS NOT NULL AND c.closed_date BETWEEN @From AND @To),
        fees AS (
            SELECT s.cycle_id, SUM(s.gross_income) AS fee
            FROM costing.plasma_settlements s
            WHERE s.status IN ('Approved', 'PartiallyPaid', 'Paid') AND s.settlement_date BETWEEN @From AND @To
            GROUP BY s.cycle_id),
        ids AS (
            SELECT cycle_id FROM sales UNION SELECT cycle_id FROM credits
            UNION SELECT cycle_id FROM adjustments UNION SELECT cycle_id FROM fees)
        SELECT c.id AS CycleId, c.number AS CycleNumber, c.branch_id AS BranchId, b.code AS BranchCode, b.name AS BranchName,
               c.coop_id AS CoopId, co.code AS CoopCode, co.name AS CoopName, c.farmer_id AS FarmerId, f.code AS FarmerCode,
               f.name AS FarmerName, COALESCE(s.birds, 0)::int AS Birds, COALESCE(s.kg, 0) AS WeightKg,
               COALESCE(s.sales, 0) AS Sales, COALESCE(cr.credits, 0) AS CreditNotes,
               COALESCE(s.cost, 0) + COALESCE(a.adjustment, 0) AS CostOfGoodsSold, COALESCE(fe.fee, 0) AS PlasmaFee
        FROM ids
        JOIN partnership.production_cycles c ON c.id = ids.cycle_id
        JOIN master.branches b ON b.id = c.branch_id
        JOIN master.coops co ON co.id = c.coop_id
        JOIN master.farmers f ON f.id = c.farmer_id
        LEFT JOIN sales s ON s.cycle_id = c.id
        LEFT JOIN credits cr ON cr.cycle_id = c.id
        LEFT JOIN adjustments a ON a.cycle_id = c.id
        LEFT JOIN fees fe ON fe.cycle_id = c.id
        WHERE (@AllBranches OR c.branch_id = ANY(@BranchIds))
          AND (@BranchId::uuid IS NULL OR c.branch_id = @BranchId)
          AND (@FarmerId::uuid IS NULL OR c.farmer_id = @FarmerId)
        ORDER BY c.number;
        """;

    public async Task<Result<ProfitabilityResponse>> Handle(GetProfitabilityQuery query, CancellationToken cancellationToken)
    {
        if (query.To < query.From)
        {
            return Result.Failure<ProfitabilityResponse>(ReportErrors.InvalidDateRange);
        }

        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        var parameters = new { query.From, query.To, query.BranchId, query.FarmerId, scope.AllBranches, scope.BranchIds };

        List<CycleRow> cycles =
        [
            .. await connection.QueryAsync<CycleRow>(new CommandDefinition(CycleFigures, parameters, cancellationToken: cancellationToken))
        ];

        Dictionary<Guid, decimal> ledgerNetIncome = query.GroupBy == ProfitabilityGrouping.Branch
            ? await LedgerNetIncomeAsync(connection, parameters, cancellationToken)
            : [];

        IReadOnlyList<ProfitabilityRow> rows = query.GroupBy switch
        {
            ProfitabilityGrouping.Coop => Group(cycles, c => (c.CoopId, c.CoopCode, c.CoopName), null),
            ProfitabilityGrouping.Farmer => Group(cycles, c => (c.FarmerId, c.FarmerCode, c.FarmerName), null),
            ProfitabilityGrouping.Branch => Group(cycles, c => (c.BranchId, c.BranchCode, c.BranchName), ledgerNetIncome),
            _ => Group(cycles, c => (c.CycleId, c.CycleNumber, $"{c.CoopName} — {c.FarmerName}"), null)
        };

        ProfitabilityRow total = Row(
            Guid.Empty, "TOTAL", "Total", cycles,
            ledgerNetIncome.Count > 0 ? ledgerNetIncome.Values.Sum() : null);

        return new ProfitabilityResponse(query.From, query.To, query.GroupBy.ToString(), rows, total);
    }

    private static IReadOnlyList<ProfitabilityRow> Group(
        List<CycleRow> cycles,
        Func<CycleRow, (Guid Id, string Code, string Name)> key,
        Dictionary<Guid, decimal>? ledgerNetIncome) =>
    [
        .. cycles
            .GroupBy(key)
            .OrderBy(g => g.Key.Code, StringComparer.Ordinal)
            .Select(g => Row(
                g.Key.Id, g.Key.Code, g.Key.Name, [.. g],
                ledgerNetIncome?.GetValueOrDefault(g.Key.Id)))
    ];

    private static ProfitabilityRow Row(Guid id, string code, string name, List<CycleRow> cycles, decimal? ledgerNetIncome)
    {
        decimal sales = cycles.Sum(c => c.Sales);
        decimal credits = cycles.Sum(c => c.CreditNotes);
        decimal cost = cycles.Sum(c => c.CostOfGoodsSold);
        decimal fee = cycles.Sum(c => c.PlasmaFee);
        decimal kg = cycles.Sum(c => c.WeightKg);
        decimal margin = sales - credits - cost - fee;

        return new ProfitabilityRow(
            id,
            code,
            name,
            cycles.Count,
            cycles.Sum(c => c.Birds),
            kg,
            sales,
            credits,
            sales - credits,
            cost,
            fee,
            margin,
            kg > 0 ? decimal.Round(margin / kg, 2, MidpointRounding.AwayFromZero) : null,
            ledgerNetIncome,
            ledgerNetIncome is null ? null : ledgerNetIncome - margin);
    }

    /// <summary>
    /// Net income per branch from the general ledger (closing journals excluded).
    /// </summary>
    private static async Task<Dictionary<Guid, decimal>> LedgerNetIncomeAsync(DbConnection connection, object parameters, CancellationToken cancellationToken)
    {
        string sql =
            $"""
            SELECT j.branch_id AS BranchId, -SUM(l.debit - l.credit) AS NetIncome
            FROM finance.journal_lines l
            JOIN finance.journal_entries j ON j.id = l.journal_entry_id
            JOIN finance.accounts a ON a.id = l.account_id
            WHERE j.status IN {LedgerSql.PostedStatuses} AND j.date BETWEEN @From AND @To
              AND a.type IN ('Revenue', 'Expense')
              AND (j.source_type IS NULL OR j.source_type NOT IN {StatementSql.ClosingSourceTypes})
              AND {LedgerSql.BranchFilter}
            GROUP BY j.branch_id;
            """;

        IEnumerable<(Guid BranchId, decimal NetIncome)> rows = await connection.QueryAsync<(Guid BranchId, decimal NetIncome)>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        return rows.ToDictionary(r => r.BranchId, r => r.NetIncome);
    }

    internal sealed class CycleRow
    {
        public Guid CycleId { get; set; }
        public string CycleNumber { get; set; }
        public Guid BranchId { get; set; }
        public string BranchCode { get; set; }
        public string BranchName { get; set; }
        public Guid CoopId { get; set; }
        public string CoopCode { get; set; }
        public string CoopName { get; set; }
        public Guid FarmerId { get; set; }
        public string FarmerCode { get; set; }
        public string FarmerName { get; set; }
        public int Birds { get; set; }
        public decimal WeightKg { get; set; }
        public decimal Sales { get; set; }
        public decimal CreditNotes { get; set; }
        public decimal CostOfGoodsSold { get; set; }
        public decimal PlasmaFee { get; set; }
    }
}
