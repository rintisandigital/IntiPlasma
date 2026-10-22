using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using SharedKernel;

namespace Application.Sales;

/// <summary>
/// Key figures of the Sales overview as of <see cref="AsOf"/>, in the accessible branches (or one branch): sales orders
/// to approve or deliver, deliveries not invoiced yet, invoices to post, this month's sales and the receivables.
/// </summary>
public sealed record GetSalesOverviewQuery(DateOnly AsOf, Guid? BranchId) : IQuery<SalesOverviewResponse>;

/// <param name="OrdersToDeliver">Approved or partially delivered orders; <paramref name="BirdsToDeliver"/> are the birds not delivered yet.</param>
/// <param name="DeliveriesToInvoice">Delivery orders not invoiced yet, and their amount.</param>
/// <param name="SalesThisMonth">Net sales (DPP) of the invoices posted this month.</param>
/// <param name="Receivables">Open balance of the posted invoices; <paramref name="ReceivablesOverdue"/> is the part past due.</param>
public sealed record SalesOverviewResponse(
    DateOnly AsOf,
    int DraftOrders,
    int OrdersToDeliver,
    int BirdsToDeliver,
    int DeliveriesToInvoice,
    decimal DeliveriesToInvoiceAmount,
    int DraftInvoices,
    decimal DraftInvoicesValue,
    int InvoicesThisMonth,
    decimal SalesThisMonth,
    int OpenInvoices,
    decimal Receivables,
    decimal ReceivablesOverdue);

internal sealed class GetSalesOverviewQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetSalesOverviewQuery, SalesOverviewResponse>
{
    private static string Branch(string alias) =>
        $"(@AllBranches OR {alias}.branch_id = ANY(@BranchIds)) AND (@BranchId::uuid IS NULL OR {alias}.branch_id = @BranchId)";

    public async Task<Result<SalesOverviewResponse>> Handle(GetSalesOverviewQuery query, CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        var monthStart = new DateOnly(query.AsOf.Year, query.AsOf.Month, 1);

        string sql =
            $"""
            SELECT COUNT(*) FILTER (WHERE o.status = 'Draft')::int AS DraftOrders,
                   COUNT(*) FILTER (WHERE o.status IN ('Approved', 'PartiallyDelivered'))::int AS OrdersToDeliver,
                   COALESCE(SUM((SELECT COALESCE(SUM(GREATEST(l.birds - l.delivered_birds, 0)), 0) FROM sales.sales_order_lines l WHERE l.sales_order_id = o.id))
                       FILTER (WHERE o.status IN ('Approved', 'PartiallyDelivered')), 0)::int AS BirdsToDeliver
            FROM sales.sales_orders o
            WHERE {Branch("o")};

            SELECT COUNT(*)::int, COALESCE(SUM((SELECT COALESCE(SUM(l.amount), 0) FROM sales.delivery_order_lines l WHERE l.delivery_order_id = d.id)), 0)
            FROM sales.delivery_orders d
            WHERE d.status = 'Delivered' AND {Branch("d")};

            SELECT COUNT(*) FILTER (WHERE i.status = 'Draft')::int AS DraftInvoices,
                   COALESCE(SUM(i.total) FILTER (WHERE i.status = 'Draft'), 0) AS DraftInvoicesValue,
                   COUNT(*) FILTER (WHERE i.status IN ('Posted', 'PartiallyPaid', 'Paid') AND i.invoice_date BETWEEN @MonthStart AND @AsOf)::int AS InvoicesThisMonth,
                   COALESCE(SUM(i.subtotal) FILTER (WHERE i.status IN ('Posted', 'PartiallyPaid', 'Paid') AND i.invoice_date BETWEEN @MonthStart AND @AsOf), 0) AS SalesThisMonth,
                   COUNT(*) FILTER (WHERE i.status IN ('Posted', 'PartiallyPaid'))::int AS OpenInvoices,
                   COALESCE(SUM(i.total - i.paid_amount - i.credited_amount) FILTER (WHERE i.status IN ('Posted', 'PartiallyPaid')), 0) AS Receivables,
                   COALESCE(SUM(i.total - i.paid_amount - i.credited_amount) FILTER (WHERE i.status IN ('Posted', 'PartiallyPaid') AND i.due_date < @AsOf), 0) AS ReceivablesOverdue
            FROM sales.sales_invoices i
            WHERE {Branch("i")};
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new { query.AsOf, query.BranchId, scope.AllBranches, scope.BranchIds, MonthStart = monthStart },
            cancellationToken: cancellationToken));

        (int draftOrders, int ordersToDeliver, int birdsToDeliver) = await multi.ReadSingleAsync<(int, int, int)>();
        (int deliveriesToInvoice, decimal deliveriesToInvoiceAmount) = await multi.ReadSingleAsync<(int, decimal)>();
        (int draftInvoices, decimal draftInvoicesValue, int invoicesThisMonth, decimal salesThisMonth, int openInvoices, decimal receivables,
            decimal receivablesOverdue) = await multi.ReadSingleAsync<(int, decimal, int, decimal, int, decimal, decimal)>();

        return new SalesOverviewResponse(
            query.AsOf,
            draftOrders,
            ordersToDeliver,
            birdsToDeliver,
            deliveriesToInvoice,
            deliveriesToInvoiceAmount,
            draftInvoices,
            draftInvoicesValue,
            invoicesThisMonth,
            salesThisMonth,
            openInvoices,
            receivables,
            receivablesOverdue);
    }
}
