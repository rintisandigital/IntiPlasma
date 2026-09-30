using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Dapper;
using Domain.Inventory.GoodsReceipts;
using Domain.Inventory.StockTransfers;
using SharedKernel;

namespace Application.Inventory;

public sealed record GetGoodsReceiptsQuery(
    PageRequest Paging,
    Guid? BranchId,
    Guid? PurchaseOrderId,
    Guid? WarehouseId,
    DateOnly? From,
    DateOnly? To) : IQuery<PagedList<InventoryDocumentResponse>>;

public sealed record GetGoodsReceiptByIdQuery(Guid GoodsReceiptId) : IQuery<InventoryDocumentResponse>;

public sealed record GetStockTransfersQuery(
    PageRequest Paging,
    Guid? BranchId,
    Guid? WarehouseId,
    Guid? CycleId,
    DateOnly? From,
    DateOnly? To) : IQuery<PagedList<InventoryDocumentResponse>>;

public sealed record GetStockTransferByIdQuery(Guid StockTransferId) : IQuery<InventoryDocumentResponse>;

/// <summary>
/// Header and lines of a goods receipt or a stock transfer.
/// </summary>
public sealed record InventoryDocumentResponse
{
    public Guid Id { get; init; }

    public string Number { get; init; }

    public Guid BranchId { get; init; }

    public string BranchCode { get; init; }

    public DateOnly Date { get; init; }

    /// <summary>
    /// Goods receipts: the purchase order number. Transfers: the source warehouse code.
    /// </summary>
    public string Reference { get; init; }

    /// <summary>
    /// Goods receipts: the vendor name. Transfers: null.
    /// </summary>
    public string? Party { get; init; }

    /// <summary>
    /// Destination warehouse (receiving warehouse for goods receipts).
    /// </summary>
    public string WarehouseCode { get; init; }

    public Guid? CycleId { get; init; }

    public string? CycleNumber { get; init; }

    public string? Notes { get; init; }

    public decimal TotalValue { get; init; }

    /// <summary>
    /// Only filled by the detail endpoints.
    /// </summary>
    public IReadOnlyList<InventoryDocumentLineResponse>? Lines { get; init; }
}

public sealed record InventoryDocumentLineResponse(
    int LineNumber,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string UomCode,
    decimal Quantity,
    decimal BaseQuantity,
    string BaseUomCode,
    decimal UnitCost,
    decimal Value);

internal static class InventoryDocumentSql
{
    public const string GoodsReceiptSelect =
        """
        SELECT r.id AS Id, r.number AS Number, r.branch_id AS BranchId, b.code AS BranchCode, r.receipt_date AS Date,
               o.number AS Reference, v.name AS Party, w.code AS WarehouseCode, r.cycle_id AS CycleId, c.number AS CycleNumber,
               r.notes AS Notes,
               (SELECT COALESCE(SUM(l.value), 0) FROM inventory.goods_receipt_lines l WHERE l.goods_receipt_id = r.id) AS TotalValue
        FROM inventory.goods_receipts r
        JOIN master.branches b ON b.id = r.branch_id
        JOIN procurement.purchase_orders o ON o.id = r.purchase_order_id
        JOIN master.vendors v ON v.id = r.vendor_id
        JOIN master.warehouses w ON w.id = r.warehouse_id
        LEFT JOIN partnership.production_cycles c ON c.id = r.cycle_id
        """;

    public const string StockTransferSelect =
        """
        SELECT t.id AS Id, t.number AS Number, t.branch_id AS BranchId, b.code AS BranchCode, t.transfer_date AS Date,
               wf.code AS Reference, NULL AS Party, wt.code AS WarehouseCode, t.cycle_id AS CycleId, c.number AS CycleNumber,
               t.notes AS Notes,
               (SELECT COALESCE(SUM(l.value), 0) FROM inventory.stock_transfer_lines l WHERE l.stock_transfer_id = t.id) AS TotalValue
        FROM inventory.stock_transfers t
        JOIN master.branches b ON b.id = t.branch_id
        JOIN master.warehouses wf ON wf.id = t.from_warehouse_id
        JOIN master.warehouses wt ON wt.id = t.to_warehouse_id
        LEFT JOIN partnership.production_cycles c ON c.id = t.cycle_id
        """;

    public const string LineColumns =
        """
        l.line_number AS LineNumber, l.item_id AS ItemId, i.code AS ItemCode, i.name AS ItemName, u.code AS UomCode,
        l.quantity AS Quantity, l.base_quantity AS BaseQuantity, bu.code AS BaseUomCode, l.unit_cost AS UnitCost, l.value AS Value
        """;

    public const string LineJoins =
        """
        JOIN master.items i ON i.id = l.item_id
        JOIN master.uoms u ON u.id = l.uom_id
        JOIN master.uoms bu ON bu.id = i.base_uom_id
        """;
}

internal sealed class GetGoodsReceiptsQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetGoodsReceiptsQuery, PagedList<InventoryDocumentResponse>>
{
    private const string Filter =
        """
        WHERE (@AllBranches OR r.branch_id = ANY(@BranchIds))
          AND (@BranchId::uuid IS NULL OR r.branch_id = @BranchId)
          AND (@PurchaseOrderId::uuid IS NULL OR r.purchase_order_id = @PurchaseOrderId)
          AND (@WarehouseId::uuid IS NULL OR r.warehouse_id = @WarehouseId)
          AND (@From::date IS NULL OR r.receipt_date >= @From)
          AND (@To::date IS NULL OR r.receipt_date <= @To)
          AND (@Search IS NULL OR r.number ILIKE @Search OR r.delivery_note_number ILIKE @Search)
        """;

    public async Task<Result<PagedList<InventoryDocumentResponse>>> Handle(GetGoodsReceiptsQuery query, CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        return await connection.QueryPagedAsync<InventoryDocumentResponse>(
            $"SELECT COUNT(*) FROM inventory.goods_receipts r {Filter}",
            $"{InventoryDocumentSql.GoodsReceiptSelect} {Filter} ORDER BY r.receipt_date DESC, r.number DESC LIMIT @PageSize OFFSET @Offset",
            query.Paging,
            new
            {
                scope.AllBranches,
                scope.BranchIds,
                query.BranchId,
                query.PurchaseOrderId,
                query.WarehouseId,
                query.From,
                query.To
            },
            cancellationToken);
    }
}

internal sealed class GetGoodsReceiptByIdQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetGoodsReceiptByIdQuery, InventoryDocumentResponse>
{
    public async Task<Result<InventoryDocumentResponse>> Handle(GetGoodsReceiptByIdQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql =
            $"""
            {InventoryDocumentSql.GoodsReceiptSelect}
            WHERE r.id = @Id;

            SELECT {InventoryDocumentSql.LineColumns}
            FROM inventory.goods_receipt_lines l
            {InventoryDocumentSql.LineJoins}
            WHERE l.goods_receipt_id = @Id
            ORDER BY l.line_number;
            """;

        return await InventoryDocumentReader.ReadAsync(
            connection, branchAccess, sql, query.GoodsReceiptId, GoodsReceiptErrors.NotFound(query.GoodsReceiptId), cancellationToken);
    }
}

internal sealed class GetStockTransfersQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetStockTransfersQuery, PagedList<InventoryDocumentResponse>>
{
    private const string Filter =
        """
        WHERE (@AllBranches OR t.branch_id = ANY(@BranchIds))
          AND (@BranchId::uuid IS NULL OR t.branch_id = @BranchId)
          AND (@WarehouseId::uuid IS NULL OR t.from_warehouse_id = @WarehouseId OR t.to_warehouse_id = @WarehouseId)
          AND (@CycleId::uuid IS NULL OR t.cycle_id = @CycleId)
          AND (@From::date IS NULL OR t.transfer_date >= @From)
          AND (@To::date IS NULL OR t.transfer_date <= @To)
          AND (@Search IS NULL OR t.number ILIKE @Search)
        """;

    public async Task<Result<PagedList<InventoryDocumentResponse>>> Handle(GetStockTransfersQuery query, CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        return await connection.QueryPagedAsync<InventoryDocumentResponse>(
            $"SELECT COUNT(*) FROM inventory.stock_transfers t {Filter}",
            $"{InventoryDocumentSql.StockTransferSelect} {Filter} ORDER BY t.transfer_date DESC, t.number DESC LIMIT @PageSize OFFSET @Offset",
            query.Paging,
            new
            {
                scope.AllBranches,
                scope.BranchIds,
                query.BranchId,
                query.WarehouseId,
                query.CycleId,
                query.From,
                query.To
            },
            cancellationToken);
    }
}

internal sealed class GetStockTransferByIdQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetStockTransferByIdQuery, InventoryDocumentResponse>
{
    public async Task<Result<InventoryDocumentResponse>> Handle(GetStockTransferByIdQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql =
            $"""
            {InventoryDocumentSql.StockTransferSelect}
            WHERE t.id = @Id;

            SELECT {InventoryDocumentSql.LineColumns}
            FROM inventory.stock_transfer_lines l
            {InventoryDocumentSql.LineJoins}
            WHERE l.stock_transfer_id = @Id
            ORDER BY l.line_number;
            """;

        return await InventoryDocumentReader.ReadAsync(
            connection, branchAccess, sql, query.StockTransferId, StockTransferErrors.NotFound(query.StockTransferId), cancellationToken);
    }
}

internal static class InventoryDocumentReader
{
    public static async Task<Result<InventoryDocumentResponse>> ReadAsync(
        DbConnection connection,
        IBranchAccess branchAccess,
        string sql,
        Guid id,
        Error notFound,
        CancellationToken cancellationToken)
    {
        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));

        InventoryDocumentResponse? document = await multi.ReadSingleOrDefaultAsync<InventoryDocumentResponse>();
        if (document is null)
        {
            return Result.Failure<InventoryDocumentResponse>(notFound);
        }

        Result access = await branchAccess.EnsureAccessAsync(document.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<InventoryDocumentResponse>(access.Error);
        }

        return document with { Lines = [.. await multi.ReadAsync<InventoryDocumentLineResponse>()] };
    }
}
