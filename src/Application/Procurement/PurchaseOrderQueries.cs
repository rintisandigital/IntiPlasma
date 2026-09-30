using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Dapper;
using Domain.Procurement.PurchaseOrders;
using SharedKernel;

namespace Application.Procurement;

public sealed record GetPurchaseOrdersQuery(
    PageRequest Paging,
    Guid? BranchId,
    Guid? VendorId,
    PurchaseOrderStatus? Status) : IQuery<PagedList<PurchaseOrderResponse>>;

public sealed record GetPurchaseOrderByIdQuery(Guid PurchaseOrderId) : IQuery<PurchaseOrderResponse>;

public sealed record PurchaseOrderResponse
{
    public const string Select =
        """
        SELECT o.id AS Id, o.number AS Number, o.branch_id AS BranchId, b.code AS BranchCode,
               o.vendor_id AS VendorId, v.code AS VendorCode, v.name AS VendorName,
               o.order_date AS OrderDate, o.expected_date AS ExpectedDate, o.status AS Status, o.notes AS Notes,
               (SELECT COALESCE(SUM(l.quantity * l.unit_price), 0) FROM procurement.purchase_order_lines l
                WHERE l.purchase_order_id = o.id)::numeric(18,2) AS Subtotal,
               o.approved_at_utc AS ApprovedAtUtc, o.cancellation_reason AS CancellationReason
        FROM procurement.purchase_orders o
        JOIN master.branches b ON b.id = o.branch_id
        JOIN master.vendors v ON v.id = o.vendor_id
        """;

    public Guid Id { get; init; }

    public string Number { get; init; }

    public Guid BranchId { get; init; }

    public string BranchCode { get; init; }

    public Guid VendorId { get; init; }

    public string VendorCode { get; init; }

    public string VendorName { get; init; }

    public DateOnly OrderDate { get; init; }

    public DateOnly? ExpectedDate { get; init; }

    public string Status { get; init; }

    public string? Notes { get; init; }

    /// <summary>
    /// Total excluding VAT.
    /// </summary>
    public decimal Subtotal { get; init; }

    public DateTime? ApprovedAtUtc { get; init; }

    public string? CancellationReason { get; init; }

    /// <summary>
    /// Only filled by the detail endpoint.
    /// </summary>
    public IReadOnlyList<PurchaseOrderLineResponse>? Lines { get; init; }
}

public sealed record PurchaseOrderLineResponse(
    int LineNumber,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string ItemCategory,
    Guid UomId,
    string UomCode,
    decimal Quantity,
    decimal QuantityReceived,
    decimal OutstandingQuantity,
    decimal UnitPrice,
    decimal Amount,
    Guid? TaxCodeId);

internal sealed class GetPurchaseOrdersQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetPurchaseOrdersQuery, PagedList<PurchaseOrderResponse>>
{
    private const string Filter =
        """
        WHERE (@AllBranches OR o.branch_id = ANY(@BranchIds))
          AND (@BranchId::uuid IS NULL OR o.branch_id = @BranchId)
          AND (@VendorId::uuid IS NULL OR o.vendor_id = @VendorId)
          AND (@Status::text IS NULL OR o.status = @Status)
          AND (@Search IS NULL OR o.number ILIKE @Search)
        """;

    public async Task<Result<PagedList<PurchaseOrderResponse>>> Handle(GetPurchaseOrdersQuery query, CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        return await connection.QueryPagedAsync<PurchaseOrderResponse>(
            $"SELECT COUNT(*) FROM procurement.purchase_orders o {Filter}",
            $"{PurchaseOrderResponse.Select} {Filter} ORDER BY o.order_date DESC, o.number DESC LIMIT @PageSize OFFSET @Offset",
            query.Paging,
            new
            {
                scope.AllBranches,
                scope.BranchIds,
                query.BranchId,
                query.VendorId,
                Status = query.Status?.ToString()
            },
            cancellationToken);
    }
}

internal sealed class GetPurchaseOrderByIdQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetPurchaseOrderByIdQuery, PurchaseOrderResponse>
{
    public async Task<Result<PurchaseOrderResponse>> Handle(GetPurchaseOrderByIdQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql =
            $"""
            {PurchaseOrderResponse.Select}
            WHERE o.id = @PurchaseOrderId;

            SELECT l.line_number AS LineNumber, l.item_id AS ItemId, i.code AS ItemCode, i.name AS ItemName,
                   i.category AS ItemCategory, l.uom_id AS UomId, u.code AS UomCode, l.quantity AS Quantity,
                   l.quantity_received AS QuantityReceived, (l.quantity - l.quantity_received) AS OutstandingQuantity,
                   l.unit_price AS UnitPrice, (l.quantity * l.unit_price)::numeric(18,2) AS Amount, l.tax_code_id AS TaxCodeId
            FROM procurement.purchase_order_lines l
            JOIN master.items i ON i.id = l.item_id
            JOIN master.uoms u ON u.id = l.uom_id
            WHERE l.purchase_order_id = @PurchaseOrderId
            ORDER BY l.line_number;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { query.PurchaseOrderId }, cancellationToken: cancellationToken));

        PurchaseOrderResponse? order = await multi.ReadSingleOrDefaultAsync<PurchaseOrderResponse>();
        if (order is null)
        {
            return Result.Failure<PurchaseOrderResponse>(PurchaseOrderErrors.NotFound(query.PurchaseOrderId));
        }

        Result access = await branchAccess.EnsureAccessAsync(order.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<PurchaseOrderResponse>(access.Error);
        }

        return order with { Lines = [.. await multi.ReadAsync<PurchaseOrderLineResponse>()] };
    }
}
