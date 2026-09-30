using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Dapper;
using Domain.MasterData.Items;
using Domain.MasterData.Warehouses;
using SharedKernel;

namespace Application.Inventory;

/// <summary>
/// Current stock per (warehouse, item) with moving average cost.
/// </summary>
public sealed record GetStockBalancesQuery(
    PageRequest Paging,
    Guid? BranchId,
    Guid? WarehouseId,
    Guid? ItemId,
    ItemCategory? Category,
    bool IncludeEmpty) : IQuery<PagedList<StockBalanceResponse>>;

public sealed record StockBalanceResponse(
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseType,
    Guid BranchId,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string ItemCategory,
    string BaseUomCode,
    decimal Quantity,
    decimal AverageCost,
    decimal Value);

/// <summary>
/// Kartu stok of one item in one warehouse: opening balance, movements with running balance, closing balance.
/// </summary>
public sealed record GetStockCardQuery(Guid WarehouseId, Guid ItemId, DateOnly From, DateOnly To) : IQuery<StockCardResponse>;

public sealed record StockCardResponse(
    Guid WarehouseId,
    Guid ItemId,
    DateOnly From,
    DateOnly To,
    decimal OpeningQuantity,
    decimal OpeningValue,
    decimal ClosingQuantity,
    decimal ClosingValue,
    IReadOnlyList<StockCardLine> Lines);

public sealed record StockCardLine
{
    public DateOnly Date { get; init; }

    public string Type { get; init; }

    public string SourceType { get; init; }

    public Guid SourceId { get; init; }

    public string SourceNumber { get; init; }

    public string? CycleNumber { get; init; }

    public decimal Quantity { get; init; }

    public decimal UnitCost { get; init; }

    public decimal Value { get; init; }

    public decimal BalanceQuantity { get; init; }

    public decimal BalanceValue { get; init; }
}

internal sealed class GetStockBalancesQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetStockBalancesQuery, PagedList<StockBalanceResponse>>
{
    private const string Filter =
        """
        WHERE (@AllBranches OR w.branch_id = ANY(@BranchIds))
          AND (@BranchId::uuid IS NULL OR w.branch_id = @BranchId)
          AND (@WarehouseId::uuid IS NULL OR s.warehouse_id = @WarehouseId)
          AND (@ItemId::uuid IS NULL OR s.item_id = @ItemId)
          AND (@Category::text IS NULL OR i.category = @Category)
          AND (@IncludeEmpty OR s.quantity <> 0)
          AND (@Search IS NULL OR i.code ILIKE @Search OR i.name ILIKE @Search)
        """;

    private const string From =
        """
        FROM inventory.stock_balances s
        JOIN master.warehouses w ON w.id = s.warehouse_id
        JOIN master.items i ON i.id = s.item_id
        JOIN master.uoms u ON u.id = i.base_uom_id
        """;

    public async Task<Result<PagedList<StockBalanceResponse>>> Handle(GetStockBalancesQuery query, CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        return await connection.QueryPagedAsync<StockBalanceResponse>(
            $"SELECT COUNT(*) {From} {Filter}",
            $"""
            SELECT s.warehouse_id AS WarehouseId, w.code AS WarehouseCode, w.type AS WarehouseType, w.branch_id AS BranchId,
                   s.item_id AS ItemId, i.code AS ItemCode, i.name AS ItemName, i.category AS ItemCategory,
                   u.code AS BaseUomCode, s.quantity AS Quantity,
                   CASE WHEN s.quantity = 0 THEN 0 ELSE round(s.value / s.quantity, 6) END AS AverageCost,
                   s.value AS Value
            {From}
            {Filter}
            ORDER BY w.code, i.code
            LIMIT @PageSize OFFSET @Offset
            """,
            query.Paging,
            new
            {
                scope.AllBranches,
                scope.BranchIds,
                query.BranchId,
                query.WarehouseId,
                query.ItemId,
                Category = query.Category?.ToString(),
                query.IncludeEmpty
            },
            cancellationToken);
    }
}

internal sealed class GetStockCardQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetStockCardQuery, StockCardResponse>
{
    public async Task<Result<StockCardResponse>> Handle(GetStockCardQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        const string sql =
            """
            SELECT w.branch_id FROM master.warehouses w WHERE w.id = @WarehouseId;

            SELECT COALESCE(SUM(e.quantity), 0) AS Quantity, COALESCE(SUM(e.value), 0) AS Value
            FROM inventory.stock_ledger_entries e
            WHERE e.warehouse_id = @WarehouseId AND e.item_id = @ItemId AND e.date < @From;

            SELECT e.date AS Date, e.type AS Type, e.source_type AS SourceType, e.source_id AS SourceId,
                   e.source_number AS SourceNumber, c.number AS CycleNumber, e.quantity AS Quantity, e.unit_cost AS UnitCost,
                   e.value AS Value, e.balance_quantity AS BalanceQuantity, e.balance_value AS BalanceValue
            FROM inventory.stock_ledger_entries e
            LEFT JOIN partnership.production_cycles c ON c.id = e.cycle_id
            WHERE e.warehouse_id = @WarehouseId AND e.item_id = @ItemId AND e.date BETWEEN @From AND @To
            ORDER BY e.date, e.id;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { query.WarehouseId, query.ItemId, query.From, query.To }, cancellationToken: cancellationToken));

        Guid? branchId = await multi.ReadSingleOrDefaultAsync<Guid?>();
        if (branchId is null)
        {
            return Result.Failure<StockCardResponse>(WarehouseErrors.NotFound(query.WarehouseId));
        }

        Result access = await branchAccess.EnsureAccessAsync(branchId.Value, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<StockCardResponse>(access.Error);
        }

        Opening opening = await multi.ReadSingleAsync<Opening>();

        // The running balance is recomputed from the opening so the card reads correctly even when movements were
        // posted with back dates (the stored balance columns reflect posting order).
        decimal quantity = opening.Quantity;
        decimal value = opening.Value;
        var lines = new List<StockCardLine>();

        foreach (StockCardLine line in await multi.ReadAsync<StockCardLine>())
        {
            quantity += line.Quantity;
            value += line.Value;
            lines.Add(line with { BalanceQuantity = quantity, BalanceValue = value });
        }

        return new StockCardResponse(
            query.WarehouseId, query.ItemId, query.From, query.To, opening.Quantity, opening.Value, quantity, value, lines);
    }

    private sealed record Opening(decimal Quantity, decimal Value);
}
