using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using SharedKernel;

namespace Application.Inventory;

/// <summary>
/// Key figures of the Inventory overview as of <see cref="AsOf"/>, in the accessible branches (or one branch): stock
/// value in central and coop warehouses, purchase orders still to be received, and this month's stock documents.
/// </summary>
public sealed record GetInventoryOverviewQuery(DateOnly AsOf, Guid? BranchId) : IQuery<InventoryOverviewResponse>;

/// <param name="CentralStockValue">Stock value in the central warehouses (moving average).</param>
/// <param name="CoopStockValue">Stock value in the coop warehouses (moving average).</param>
/// <param name="ItemsInStock">Item/warehouse balances with a positive quantity.</param>
/// <param name="OrdersToReceive">Approved or partially received purchase orders; <paramref name="OrdersToReceiveValue"/> is the value not received yet.</param>
public sealed record InventoryOverviewResponse(
    DateOnly AsOf,
    decimal CentralStockValue,
    decimal CoopStockValue,
    int ItemsInStock,
    int OrdersToReceive,
    decimal OrdersToReceiveValue,
    int GoodsReceiptsThisMonth,
    decimal GoodsReceiptsThisMonthValue,
    int StockTransfersThisMonth,
    decimal StockTransfersThisMonthValue,
    int StockReturnsThisMonth,
    decimal StockReturnsThisMonthValue);

internal sealed class GetInventoryOverviewQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetInventoryOverviewQuery, InventoryOverviewResponse>
{
    private static string Branch(string alias) =>
        $"(@AllBranches OR {alias}.branch_id = ANY(@BranchIds)) AND (@BranchId::uuid IS NULL OR {alias}.branch_id = @BranchId)";

    public async Task<Result<InventoryOverviewResponse>> Handle(GetInventoryOverviewQuery query, CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        var monthStart = new DateOnly(query.AsOf.Year, query.AsOf.Month, 1);

        string sql =
            $"""
            SELECT COALESCE(SUM(s.value) FILTER (WHERE w.type = 'Central'), 0) AS CentralStockValue,
                   COALESCE(SUM(s.value) FILTER (WHERE w.type = 'Coop'), 0) AS CoopStockValue,
                   COUNT(*) FILTER (WHERE s.quantity > 0)::int AS ItemsInStock
            FROM inventory.stock_balances s JOIN master.warehouses w ON w.id = s.warehouse_id
            WHERE {Branch("w")};

            SELECT COUNT(DISTINCT o.id)::int AS OrdersToReceive,
                   COALESCE(SUM((l.quantity - l.quantity_received) * l.unit_price), 0) AS OrdersToReceiveValue
            FROM procurement.purchase_orders o
            LEFT JOIN procurement.purchase_order_lines l ON l.purchase_order_id = o.id
            WHERE o.status IN ('Approved', 'PartiallyReceived') AND {Branch("o")};

            SELECT COUNT(*)::int, COALESCE(SUM((SELECT COALESCE(SUM(l.value), 0) FROM inventory.goods_receipt_lines l WHERE l.goods_receipt_id = r.id)), 0)
            FROM inventory.goods_receipts r
            WHERE r.receipt_date BETWEEN @MonthStart AND @AsOf AND {Branch("r")};

            SELECT COUNT(*)::int, COALESCE(SUM((SELECT COALESCE(SUM(l.value), 0) FROM inventory.stock_transfer_lines l WHERE l.stock_transfer_id = t.id)), 0)
            FROM inventory.stock_transfers t
            WHERE t.transfer_date BETWEEN @MonthStart AND @AsOf AND {Branch("t")};

            SELECT COUNT(*)::int, COALESCE(SUM((SELECT COALESCE(SUM(l.value), 0) FROM inventory.stock_return_lines l WHERE l.stock_return_id = t.id)), 0)
            FROM inventory.stock_returns t
            WHERE t.return_date BETWEEN @MonthStart AND @AsOf AND {Branch("t")};
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new { query.AsOf, query.BranchId, scope.AllBranches, scope.BranchIds, MonthStart = monthStart },
            cancellationToken: cancellationToken));

        (decimal centralValue, decimal coopValue, int itemsInStock) = await multi.ReadSingleAsync<(decimal, decimal, int)>();
        (int ordersToReceive, decimal ordersToReceiveValue) = await multi.ReadSingleAsync<(int, decimal)>();
        (int receipts, decimal receiptsValue) = await multi.ReadSingleAsync<(int, decimal)>();
        (int transfers, decimal transfersValue) = await multi.ReadSingleAsync<(int, decimal)>();
        (int returns, decimal returnsValue) = await multi.ReadSingleAsync<(int, decimal)>();

        return new InventoryOverviewResponse(
            query.AsOf,
            centralValue,
            coopValue,
            itemsInStock,
            ordersToReceive,
            ordersToReceiveValue,
            receipts,
            receiptsValue,
            transfers,
            transfersValue,
            returns,
            returnsValue);
    }
}
