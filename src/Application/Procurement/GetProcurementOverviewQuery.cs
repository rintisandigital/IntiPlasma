using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using SharedKernel;

namespace Application.Procurement;

/// <summary>
/// Key figures of the Procurement overview as of <see cref="AsOf"/>, in the accessible branches (or one branch):
/// purchase orders to approve, orders still awaiting goods, this month's orders, and the vendor invoices to post or pay.
/// </summary>
public sealed record GetProcurementOverviewQuery(DateOnly AsOf, Guid? BranchId) : IQuery<ProcurementOverviewResponse>;

/// <param name="DraftOrders">Purchase orders waiting for approval, and their value (subtotal).</param>
/// <param name="OpenOrders">Approved or partially received orders; <paramref name="OpenOrdersValue"/> is the value not received yet.</param>
/// <param name="LateOrders">Open orders whose expected delivery date has passed.</param>
/// <param name="OrdersThisMonth">Orders dated this month (not draft, not cancelled), and their value (subtotal).</param>
/// <param name="DraftInvoices">Vendor invoices not posted yet, and their value (total).</param>
/// <param name="Payables">Unpaid balance of the posted vendor invoices; <paramref name="PayablesOverdue"/> is the part past due.</param>
public sealed record ProcurementOverviewResponse(
    DateOnly AsOf,
    int DraftOrders,
    decimal DraftOrdersValue,
    int OpenOrders,
    decimal OpenOrdersValue,
    int LateOrders,
    int OrdersThisMonth,
    decimal OrdersThisMonthValue,
    int DraftInvoices,
    decimal DraftInvoicesValue,
    int UnpaidInvoices,
    decimal Payables,
    decimal PayablesOverdue);

internal sealed class GetProcurementOverviewQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetProcurementOverviewQuery, ProcurementOverviewResponse>
{
    private static string Branch(string alias) =>
        $"(@AllBranches OR {alias}.branch_id = ANY(@BranchIds)) AND (@BranchId::uuid IS NULL OR {alias}.branch_id = @BranchId)";

    public async Task<Result<ProcurementOverviewResponse>> Handle(GetProcurementOverviewQuery query, CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        var monthStart = new DateOnly(query.AsOf.Year, query.AsOf.Month, 1);

        string sql =
            $"""
            WITH orders AS (
                SELECT o.status, o.order_date, o.expected_date,
                       COALESCE(SUM(l.quantity * l.unit_price), 0) AS subtotal,
                       COALESCE(SUM((l.quantity - l.quantity_received) * l.unit_price), 0) AS outstanding
                FROM procurement.purchase_orders o
                LEFT JOIN procurement.purchase_order_lines l ON l.purchase_order_id = o.id
                WHERE {Branch("o")}
                GROUP BY o.id
            )
            SELECT COUNT(*) FILTER (WHERE status = 'Draft')::int AS DraftOrders,
                   COALESCE(SUM(subtotal) FILTER (WHERE status = 'Draft'), 0) AS DraftOrdersValue,
                   COUNT(*) FILTER (WHERE status IN ('Approved', 'PartiallyReceived'))::int AS OpenOrders,
                   COALESCE(SUM(outstanding) FILTER (WHERE status IN ('Approved', 'PartiallyReceived')), 0) AS OpenOrdersValue,
                   COUNT(*) FILTER (WHERE status IN ('Approved', 'PartiallyReceived') AND expected_date < @AsOf)::int AS LateOrders,
                   COUNT(*) FILTER (WHERE status NOT IN ('Draft', 'Cancelled') AND order_date BETWEEN @MonthStart AND @AsOf)::int AS OrdersThisMonth,
                   COALESCE(SUM(subtotal) FILTER (WHERE status NOT IN ('Draft', 'Cancelled') AND order_date BETWEEN @MonthStart AND @AsOf), 0) AS OrdersThisMonthValue
            FROM orders;

            SELECT COUNT(*) FILTER (WHERE i.status = 'Draft')::int AS DraftInvoices,
                   COALESCE(SUM(i.total) FILTER (WHERE i.status = 'Draft'), 0) AS DraftInvoicesValue,
                   COUNT(*) FILTER (WHERE i.status IN ('Posted', 'PartiallyPaid'))::int AS UnpaidInvoices,
                   COALESCE(SUM(i.total - i.paid_amount) FILTER (WHERE i.status IN ('Posted', 'PartiallyPaid')), 0) AS Payables,
                   COALESCE(SUM(i.total - i.paid_amount) FILTER (WHERE i.status IN ('Posted', 'PartiallyPaid') AND i.due_date < @AsOf), 0) AS PayablesOverdue
            FROM finance.vendor_invoices i
            WHERE {Branch("i")};
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new { query.AsOf, query.BranchId, scope.AllBranches, scope.BranchIds, MonthStart = monthStart },
            cancellationToken: cancellationToken));

        (int draftOrders, decimal draftOrdersValue, int openOrders, decimal openOrdersValue, int lateOrders, int ordersThisMonth,
            decimal ordersThisMonthValue) = await multi.ReadSingleAsync<(int, decimal, int, decimal, int, int, decimal)>();
        (int draftInvoices, decimal draftInvoicesValue, int unpaidInvoices, decimal payables, decimal payablesOverdue) =
            await multi.ReadSingleAsync<(int, decimal, int, decimal, decimal)>();

        return new ProcurementOverviewResponse(
            query.AsOf,
            draftOrders,
            draftOrdersValue,
            openOrders,
            openOrdersValue,
            lateOrders,
            ordersThisMonth,
            ordersThisMonthValue,
            draftInvoices,
            draftInvoicesValue,
            unpaidInvoices,
            payables,
            payablesOverdue);
    }
}
