using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using SharedKernel;

namespace Application.Monitoring;

/// <summary>
/// Key figures of the Admin Office dashboard as of <see cref="AsOf"/>, in the accessible branches (or one branch):
/// cycles in production, this month's harvest, receivables and payables (overdue part), stock value, cash/bank,
/// documents waiting for someone, failed events, and the monthly sales and harvest of the last six months.
/// </summary>
public sealed record GetDashboardSummaryQuery(DateOnly AsOf, Guid? BranchId) : IQuery<DashboardSummaryResponse>;

/// <param name="Population">Birds alive in the cycles in production.</param>
/// <param name="Pending">Documents waiting for approval, posting or payment.</param>
/// <param name="FailedEvents">Dead-letter events (system-wide).</param>
public sealed record DashboardSummaryResponse(
    DateOnly AsOf,
    int ActiveCycles,
    int Population,
    int HarvestedBirdsThisMonth,
    decimal HarvestedKgThisMonth,
    decimal SalesThisMonth,
    decimal Receivables,
    decimal ReceivablesOverdue,
    decimal Payables,
    decimal PayablesOverdue,
    decimal StockValue,
    decimal CashAndBank,
    DashboardPending Pending,
    int FailedEvents,
    IReadOnlyList<DashboardMonth> Months);

public sealed record DashboardPending(
    int JournalsToApprove,
    int JournalsToPost,
    int PaymentVouchersToApprove,
    int PaymentVouchersToPay,
    int CashOutToApprove,
    int SettlementsToApprove,
    int ClosedCyclesToSettle);

/// <param name="Sales">Net sales (DPP) of the invoices posted in the month.</param>
public sealed record DashboardMonth(int Year, int Month, decimal Sales, decimal HarvestedKg);

internal sealed class GetDashboardSummaryQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetDashboardSummaryQuery, DashboardSummaryResponse>
{
    private const int TrendMonths = 6;

    private static string Branch(string alias) =>
        $"(@AllBranches OR {alias}.branch_id = ANY(@BranchIds)) AND (@BranchId::uuid IS NULL OR {alias}.branch_id = @BranchId)";

    public async Task<Result<DashboardSummaryResponse>> Handle(GetDashboardSummaryQuery query, CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        var monthStart = new DateOnly(query.AsOf.Year, query.AsOf.Month, 1);
        DateOnly trendStart = monthStart.AddMonths(1 - TrendMonths);

        string sql =
            $"""
            SELECT COUNT(*)::int AS ActiveCycles,
                   COALESCE(SUM(COALESCE(c.initial_population, 0) - c.total_mortality - c.total_culling - c.harvested_birds), 0)::int AS Population
            FROM partnership.production_cycles c
            WHERE c.status IN ('Active', 'Harvesting') AND {Branch("c")};

            SELECT COALESCE(SUM(h.birds), 0)::int AS Birds, COALESCE(SUM(h.weight_kg), 0) AS Kg
            FROM partnership.cycle_harvests h JOIN partnership.production_cycles c ON c.id = h.cycle_id
            WHERE h.date BETWEEN @MonthStart AND @AsOf AND {Branch("c")};

            SELECT COALESCE(SUM(i.total - i.paid_amount - i.credited_amount), 0) AS Total,
                   COALESCE(SUM(i.total - i.paid_amount - i.credited_amount) FILTER (WHERE i.due_date < @AsOf), 0) AS Overdue
            FROM sales.sales_invoices i
            WHERE i.status IN ('Posted', 'PartiallyPaid') AND {Branch("i")};

            SELECT COALESCE(SUM(i.total - i.paid_amount), 0) AS Total,
                   COALESCE(SUM(i.total - i.paid_amount) FILTER (WHERE i.due_date < @AsOf), 0) AS Overdue
            FROM finance.vendor_invoices i
            WHERE i.status IN ('Posted', 'PartiallyPaid') AND {Branch("i")};

            SELECT COALESCE(SUM(s.value), 0)
            FROM inventory.stock_balances s JOIN master.warehouses w ON w.id = s.warehouse_id
            WHERE {Branch("w")};

            SELECT COALESCE(SUM(l.debit - l.credit), 0)
            FROM finance.cash_bank_accounts cb
            JOIN finance.journal_lines l ON l.account_id = cb.account_id
            JOIN finance.journal_entries j ON j.id = l.journal_entry_id AND j.status IN ('Posted', 'Reversed')
            WHERE {Branch("cb")};

            SELECT
                (SELECT COUNT(*) FROM finance.journal_entries j WHERE j.status = 'Draft' AND j.source = 'Manual' AND {Branch("j")})::int AS JournalsToApprove,
                (SELECT COUNT(*) FROM finance.journal_entries j WHERE j.status = 'Approved' AND {Branch("j")})::int AS JournalsToPost,
                (SELECT COUNT(*) FROM finance.payment_vouchers v WHERE v.status = 'Draft' AND {Branch("v")})::int AS PaymentVouchersToApprove,
                (SELECT COUNT(*) FROM finance.payment_vouchers v WHERE v.status = 'Approved' AND {Branch("v")})::int AS PaymentVouchersToPay,
                (SELECT COUNT(*) FROM finance.cash_transactions t WHERE t.status = 'Draft' AND t.direction = 'Out' AND {Branch("t")})::int AS CashOutToApprove,
                (SELECT COUNT(*) FROM costing.plasma_settlements s WHERE s.status = 'Draft' AND {Branch("s")})::int AS SettlementsToApprove,
                (SELECT COUNT(*) FROM partnership.production_cycles c
                 WHERE c.status = 'Closed' AND c.contract_id IS NOT NULL AND {Branch("c")}
                   AND NOT EXISTS (SELECT 1 FROM costing.plasma_settlements s WHERE s.cycle_id = c.id AND s.status <> 'Cancelled'))::int AS ClosedCyclesToSettle;

            SELECT COUNT(*)::int FROM infrastructure.outbox_messages WHERE processed_on_utc IS NOT NULL AND error IS NOT NULL;

            SELECT EXTRACT(YEAR FROM m)::int AS Year, EXTRACT(MONTH FROM m)::int AS Month,
                   COALESCE((SELECT SUM(i.subtotal) FROM sales.sales_invoices i
                             WHERE i.status IN ('Posted', 'PartiallyPaid', 'Paid') AND {Branch("i")}
                               AND i.invoice_date >= m::date AND i.invoice_date < (m + interval '1 month')::date), 0) AS Sales,
                   COALESCE((SELECT SUM(h.weight_kg) FROM partnership.cycle_harvests h JOIN partnership.production_cycles c ON c.id = h.cycle_id
                             WHERE {Branch("c")} AND h.date >= m::date AND h.date < (m + interval '1 month')::date), 0) AS HarvestedKg
            FROM generate_series(@TrendStart::date, @MonthStart::date, interval '1 month') AS m
            ORDER BY m;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new { query.AsOf, query.BranchId, scope.AllBranches, scope.BranchIds, MonthStart = monthStart, TrendStart = trendStart },
            cancellationToken: cancellationToken));

        (int activeCycles, int population) = await multi.ReadSingleAsync<(int, int)>();
        (int harvestedBirds, decimal harvestedKg) = await multi.ReadSingleAsync<(int, decimal)>();
        (decimal receivables, decimal receivablesOverdue) = await multi.ReadSingleAsync<(decimal, decimal)>();
        (decimal payables, decimal payablesOverdue) = await multi.ReadSingleAsync<(decimal, decimal)>();
        decimal stockValue = await multi.ReadSingleAsync<decimal>();
        decimal cashAndBank = await multi.ReadSingleAsync<decimal>();
        DashboardPending pending = await multi.ReadSingleAsync<DashboardPending>();
        int failedEvents = await multi.ReadSingleAsync<int>();
        List<DashboardMonth> months = [.. await multi.ReadAsync<DashboardMonth>()];

        return new DashboardSummaryResponse(
            query.AsOf,
            activeCycles,
            population,
            harvestedBirds,
            harvestedKg,
            months.Count > 0 ? months[^1].Sales : 0m,
            receivables,
            receivablesOverdue,
            payables,
            payablesOverdue,
            stockValue,
            cashAndBank,
            pending,
            failedEvents,
            months);
    }
}
