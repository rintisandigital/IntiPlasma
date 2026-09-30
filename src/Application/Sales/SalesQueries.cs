using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Dapper;
using Domain.Sales.DeliveryOrders;
using Domain.Sales.SalesInvoices;
using Domain.Sales.SalesOrders;
using SharedKernel;

namespace Application.Sales;

public sealed record GetSalesOrdersQuery(
    PageRequest Paging,
    Guid? BranchId,
    Guid? CustomerId,
    SalesOrderStatus? Status,
    DateOnly? From,
    DateOnly? To) : IQuery<PagedList<SalesOrderResponse>>;

public sealed record GetSalesOrderByIdQuery(Guid SalesOrderId) : IQuery<SalesOrderResponse>;

public sealed record GetDeliveryOrdersQuery(
    PageRequest Paging,
    Guid? BranchId,
    Guid? CustomerId,
    Guid? SalesOrderId,
    DeliveryOrderStatus? Status,
    DateOnly? From,
    DateOnly? To) : IQuery<PagedList<DeliveryOrderResponse>>;

public sealed record GetDeliveryOrderByIdQuery(Guid DeliveryOrderId) : IQuery<DeliveryOrderResponse>;

public sealed record GetSalesInvoicesQuery(
    PageRequest Paging,
    Guid? BranchId,
    Guid? CustomerId,
    SalesInvoiceStatus? Status,
    DateOnly? From,
    DateOnly? To) : IQuery<PagedList<SalesInvoiceResponse>>;

public sealed record GetSalesInvoiceByIdQuery(Guid SalesInvoiceId) : IQuery<SalesInvoiceResponse>;

/// <summary>
/// Harvests not on any active delivery order yet: what can still be delivered.
/// </summary>
public sealed record GetUndeliveredHarvestsQuery(Guid? BranchId, Guid? CycleId) : IQuery<IReadOnlyList<UndeliveredHarvestResponse>>;

public sealed record SalesOrderResponse
{
    public const string Select =
        """
        SELECT o.id AS Id, o.number AS Number, o.branch_id AS BranchId, b.code AS BranchCode,
               o.customer_id AS CustomerId, c.code AS CustomerCode, c.name AS CustomerName,
               o.order_date AS OrderDate, o.delivery_date AS DeliveryDate, o.status AS Status, o.notes AS Notes,
               s.birds AS Birds, s.delivered_birds AS DeliveredBirds, s.delivered_weight_kg AS DeliveredWeightKg,
               s.estimated_amount AS EstimatedAmount,
               o.approved_at_utc AS ApprovedAtUtc, o.credit_override_reason AS CreditOverrideReason,
               o.cancellation_reason AS CancellationReason
        FROM sales.sales_orders o
        JOIN master.branches b ON b.id = o.branch_id
        JOIN master.customers c ON c.id = o.customer_id
        CROSS JOIN LATERAL (
            SELECT COALESCE(SUM(l.birds), 0)::int AS birds, COALESCE(SUM(l.delivered_birds), 0)::int AS delivered_birds,
                   COALESCE(SUM(l.delivered_weight_kg), 0) AS delivered_weight_kg,
                   COALESCE(SUM(ROUND(l.estimated_weight_kg * l.price_per_kg, 2)), 0) AS estimated_amount
            FROM sales.sales_order_lines l WHERE l.sales_order_id = o.id) s
        """;

    public Guid Id { get; init; }

    public string Number { get; init; }

    public Guid BranchId { get; init; }

    public string BranchCode { get; init; }

    public Guid CustomerId { get; init; }

    public string CustomerCode { get; init; }

    public string CustomerName { get; init; }

    public DateOnly OrderDate { get; init; }

    public DateOnly? DeliveryDate { get; init; }

    public string Status { get; init; }

    public string? Notes { get; init; }

    public int Birds { get; init; }

    public int DeliveredBirds { get; init; }

    public decimal DeliveredWeightKg { get; init; }

    /// <summary>
    /// Estimated weight × price per kg, excluding VAT.
    /// </summary>
    public decimal EstimatedAmount { get; init; }

    public DateTime? ApprovedAtUtc { get; init; }

    /// <summary>
    /// Filled when the order was approved above the customer's credit limit.
    /// </summary>
    public string? CreditOverrideReason { get; init; }

    public string? CancellationReason { get; init; }

    /// <summary>
    /// Only filled by the detail endpoint.
    /// </summary>
    public IReadOnlyList<SalesOrderLineResponse>? Lines { get; init; }
}

public sealed record SalesOrderLineResponse(
    int LineNumber,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    int Birds,
    decimal EstimatedWeightKg,
    decimal PricePerKg,
    decimal EstimatedAmount,
    Guid? TaxCodeId,
    string? TaxCode,
    int DeliveredBirds,
    decimal DeliveredWeightKg,
    int OutstandingBirds);

public sealed record DeliveryOrderResponse
{
    public const string Select =
        """
        SELECT d.id AS Id, d.number AS Number, d.branch_id AS BranchId, b.code AS BranchCode,
               d.sales_order_id AS SalesOrderId, o.number AS SalesOrderNumber,
               d.customer_id AS CustomerId, c.code AS CustomerCode, c.name AS CustomerName,
               d.delivery_date AS DeliveryDate, d.vehicle_number AS VehicleNumber, d.driver_name AS DriverName,
               d.status AS Status, d.sales_invoice_id AS SalesInvoiceId, i.number AS SalesInvoiceNumber, d.notes AS Notes,
               s.birds AS Birds, s.weight_kg AS WeightKg, s.amount AS Amount, d.cancellation_reason AS CancellationReason
        FROM sales.delivery_orders d
        JOIN master.branches b ON b.id = d.branch_id
        JOIN sales.sales_orders o ON o.id = d.sales_order_id
        JOIN master.customers c ON c.id = d.customer_id
        LEFT JOIN sales.sales_invoices i ON i.id = d.sales_invoice_id
        CROSS JOIN LATERAL (
            SELECT COALESCE(SUM(l.birds), 0)::int AS birds, COALESCE(SUM(l.weight_kg), 0) AS weight_kg,
                   COALESCE(SUM(l.amount), 0) AS amount
            FROM sales.delivery_order_lines l WHERE l.delivery_order_id = d.id) s
        """;

    public Guid Id { get; init; }

    public string Number { get; init; }

    public Guid BranchId { get; init; }

    public string BranchCode { get; init; }

    public Guid SalesOrderId { get; init; }

    public string SalesOrderNumber { get; init; }

    public Guid CustomerId { get; init; }

    public string CustomerCode { get; init; }

    public string CustomerName { get; init; }

    public DateOnly DeliveryDate { get; init; }

    public string? VehicleNumber { get; init; }

    public string? DriverName { get; init; }

    public string Status { get; init; }

    public Guid? SalesInvoiceId { get; init; }

    /// <summary>
    /// Null while the invoice is a draft.
    /// </summary>
    public string? SalesInvoiceNumber { get; init; }

    public string? Notes { get; init; }

    public int Birds { get; init; }

    public decimal WeightKg { get; init; }

    /// <summary>
    /// Weighed kg × price per kg, excluding VAT.
    /// </summary>
    public decimal Amount { get; init; }

    public string? CancellationReason { get; init; }

    /// <summary>
    /// Only filled by the detail endpoint.
    /// </summary>
    public IReadOnlyList<DeliveryOrderLineResponse>? Lines { get; init; }
}

public sealed record DeliveryOrderLineResponse(
    int LineNumber,
    int SalesOrderLineNumber,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    Guid HarvestId,
    DateOnly HarvestDate,
    Guid CycleId,
    string CycleNumber,
    string CoopName,
    string FarmerName,
    int Birds,
    decimal WeightKg,
    decimal AverageWeightKg,
    decimal PricePerKg,
    decimal Amount,
    string? TaxCode);

public sealed record SalesInvoiceResponse
{
    public const string Select =
        """
        SELECT i.id AS Id, i.number AS Number, i.branch_id AS BranchId, b.code AS BranchCode,
               i.customer_id AS CustomerId, c.code AS CustomerCode, c.name AS CustomerName,
               i.invoice_date AS InvoiceDate, i.due_date AS DueDate, i.status AS Status, i.notes AS Notes,
               i.subtotal AS Subtotal, i.vat_amount AS VatAmount, i.total AS Total, i.paid_amount AS PaidAmount,
               (i.total - i.paid_amount) AS Outstanding, i.posted_at_utc AS PostedAtUtc,
               i.cancellation_reason AS CancellationReason
        FROM sales.sales_invoices i
        JOIN master.branches b ON b.id = i.branch_id
        JOIN master.customers c ON c.id = i.customer_id
        """;

    public Guid Id { get; init; }

    /// <summary>
    /// Null while the invoice is a draft.
    /// </summary>
    public string? Number { get; init; }

    public Guid BranchId { get; init; }

    public string BranchCode { get; init; }

    public Guid CustomerId { get; init; }

    public string CustomerCode { get; init; }

    public string CustomerName { get; init; }

    public DateOnly InvoiceDate { get; init; }

    public DateOnly DueDate { get; init; }

    public string Status { get; init; }

    public string? Notes { get; init; }

    /// <summary>
    /// Total excluding VAT.
    /// </summary>
    public decimal Subtotal { get; init; }

    public decimal VatAmount { get; init; }

    public decimal Total { get; init; }

    public decimal PaidAmount { get; init; }

    public decimal Outstanding { get; init; }

    public DateTime? PostedAtUtc { get; init; }

    public string? CancellationReason { get; init; }

    /// <summary>
    /// Only filled by the detail endpoint.
    /// </summary>
    public IReadOnlyList<SalesInvoiceLineResponse>? Lines { get; init; }
}

public sealed record SalesInvoiceLineResponse(
    int LineNumber,
    Guid DeliveryOrderId,
    string DeliveryOrderNumber,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    Guid CycleId,
    string CycleNumber,
    int Birds,
    decimal WeightKg,
    decimal PricePerKg,
    decimal Amount,
    string? TaxCode,
    decimal VatRatePercent,
    decimal VatTaxBase,
    decimal VatAmount);

public sealed record UndeliveredHarvestResponse(
    Guid HarvestId,
    DateOnly Date,
    int AgeDays,
    int Birds,
    decimal WeightKg,
    decimal AverageWeightKg,
    string? Notes,
    Guid CycleId,
    string CycleNumber,
    Guid BranchId,
    string BranchCode,
    string CoopName,
    string FarmerName);

internal static class SalesSql
{
    public static string Branch(string alias) =>
        $"""
        (@AllBranches OR {alias}.branch_id = ANY(@BranchIds))
        AND (@BranchId::uuid IS NULL OR {alias}.branch_id = @BranchId)
        """;
}

internal sealed class GetSalesOrdersQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetSalesOrdersQuery, PagedList<SalesOrderResponse>>
{
    public async Task<Result<PagedList<SalesOrderResponse>>> Handle(GetSalesOrdersQuery query, CancellationToken cancellationToken)
    {
        string filter =
            $"""
            WHERE {SalesSql.Branch("o")}
              AND (@CustomerId::uuid IS NULL OR o.customer_id = @CustomerId)
              AND (@Status::text IS NULL OR o.status = @Status)
              AND (@From::date IS NULL OR o.order_date >= @From)
              AND (@To::date IS NULL OR o.order_date <= @To)
              AND (@Search IS NULL OR o.number ILIKE @Search)
            """;

        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        return await connection.QueryPagedAsync<SalesOrderResponse>(
            $"SELECT COUNT(*) FROM sales.sales_orders o {filter}",
            $"{SalesOrderResponse.Select} {filter} ORDER BY o.order_date DESC, o.number DESC LIMIT @PageSize OFFSET @Offset",
            query.Paging,
            new
            {
                scope.AllBranches,
                scope.BranchIds,
                query.BranchId,
                query.CustomerId,
                Status = query.Status?.ToString(),
                query.From,
                query.To
            },
            cancellationToken);
    }
}

internal sealed class GetSalesOrderByIdQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetSalesOrderByIdQuery, SalesOrderResponse>
{
    public async Task<Result<SalesOrderResponse>> Handle(GetSalesOrderByIdQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql =
            $"""
            {SalesOrderResponse.Select}
            WHERE o.id = @SalesOrderId;

            SELECT l.line_number AS LineNumber, l.item_id AS ItemId, i.code AS ItemCode, i.name AS ItemName, l.birds AS Birds,
                   l.estimated_weight_kg AS EstimatedWeightKg, l.price_per_kg AS PricePerKg,
                   ROUND(l.estimated_weight_kg * l.price_per_kg, 2) AS EstimatedAmount, l.tax_code_id AS TaxCodeId, t.code AS TaxCode,
                   l.delivered_birds AS DeliveredBirds, l.delivered_weight_kg AS DeliveredWeightKg,
                   (l.birds - l.delivered_birds) AS OutstandingBirds
            FROM sales.sales_order_lines l
            JOIN master.items i ON i.id = l.item_id
            LEFT JOIN master.tax_codes t ON t.id = l.tax_code_id
            WHERE l.sales_order_id = @SalesOrderId
            ORDER BY l.line_number;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { query.SalesOrderId }, cancellationToken: cancellationToken));

        SalesOrderResponse? order = await multi.ReadSingleOrDefaultAsync<SalesOrderResponse>();
        if (order is null)
        {
            return Result.Failure<SalesOrderResponse>(SalesOrderErrors.NotFound(query.SalesOrderId));
        }

        Result access = await branchAccess.EnsureAccessAsync(order.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<SalesOrderResponse>(access.Error);
        }

        return order with { Lines = [.. await multi.ReadAsync<SalesOrderLineResponse>()] };
    }
}

internal sealed class GetDeliveryOrdersQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetDeliveryOrdersQuery, PagedList<DeliveryOrderResponse>>
{
    public async Task<Result<PagedList<DeliveryOrderResponse>>> Handle(GetDeliveryOrdersQuery query, CancellationToken cancellationToken)
    {
        string filter =
            $"""
            WHERE {SalesSql.Branch("d")}
              AND (@CustomerId::uuid IS NULL OR d.customer_id = @CustomerId)
              AND (@SalesOrderId::uuid IS NULL OR d.sales_order_id = @SalesOrderId)
              AND (@Status::text IS NULL OR d.status = @Status)
              AND (@From::date IS NULL OR d.delivery_date >= @From)
              AND (@To::date IS NULL OR d.delivery_date <= @To)
              AND (@Search IS NULL OR d.number ILIKE @Search)
            """;

        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        return await connection.QueryPagedAsync<DeliveryOrderResponse>(
            $"SELECT COUNT(*) FROM sales.delivery_orders d {filter}",
            $"{DeliveryOrderResponse.Select} {filter} ORDER BY d.delivery_date DESC, d.number DESC LIMIT @PageSize OFFSET @Offset",
            query.Paging,
            new
            {
                scope.AllBranches,
                scope.BranchIds,
                query.BranchId,
                query.CustomerId,
                query.SalesOrderId,
                Status = query.Status?.ToString(),
                query.From,
                query.To
            },
            cancellationToken);
    }
}

internal sealed class GetDeliveryOrderByIdQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetDeliveryOrderByIdQuery, DeliveryOrderResponse>
{
    public async Task<Result<DeliveryOrderResponse>> Handle(GetDeliveryOrderByIdQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql =
            $"""
            {DeliveryOrderResponse.Select}
            WHERE d.id = @DeliveryOrderId;

            SELECT l.line_number AS LineNumber, l.sales_order_line_number AS SalesOrderLineNumber, l.item_id AS ItemId,
                   i.code AS ItemCode, i.name AS ItemName, l.harvest_id AS HarvestId, h.date AS HarvestDate,
                   l.cycle_id AS CycleId, pc.number AS CycleNumber, co.name AS CoopName, f.name AS FarmerName,
                   l.birds AS Birds, l.weight_kg AS WeightKg, ROUND(l.weight_kg / l.birds, 3) AS AverageWeightKg,
                   l.price_per_kg AS PricePerKg, l.amount AS Amount, t.code AS TaxCode
            FROM sales.delivery_order_lines l
            JOIN master.items i ON i.id = l.item_id
            JOIN partnership.cycle_harvests h ON h.id = l.harvest_id
            JOIN partnership.production_cycles pc ON pc.id = l.cycle_id
            JOIN master.coops co ON co.id = pc.coop_id
            JOIN master.farmers f ON f.id = pc.farmer_id
            LEFT JOIN master.tax_codes t ON t.id = l.tax_code_id
            WHERE l.delivery_order_id = @DeliveryOrderId
            ORDER BY l.line_number;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { query.DeliveryOrderId }, cancellationToken: cancellationToken));

        DeliveryOrderResponse? delivery = await multi.ReadSingleOrDefaultAsync<DeliveryOrderResponse>();
        if (delivery is null)
        {
            return Result.Failure<DeliveryOrderResponse>(DeliveryOrderErrors.NotFound(query.DeliveryOrderId));
        }

        Result access = await branchAccess.EnsureAccessAsync(delivery.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<DeliveryOrderResponse>(access.Error);
        }

        return delivery with { Lines = [.. await multi.ReadAsync<DeliveryOrderLineResponse>()] };
    }
}

internal sealed class GetSalesInvoicesQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetSalesInvoicesQuery, PagedList<SalesInvoiceResponse>>
{
    public async Task<Result<PagedList<SalesInvoiceResponse>>> Handle(GetSalesInvoicesQuery query, CancellationToken cancellationToken)
    {
        string filter =
            $"""
            WHERE {SalesSql.Branch("i")}
              AND (@CustomerId::uuid IS NULL OR i.customer_id = @CustomerId)
              AND (@Status::text IS NULL OR i.status = @Status)
              AND (@From::date IS NULL OR i.invoice_date >= @From)
              AND (@To::date IS NULL OR i.invoice_date <= @To)
              AND (@Search IS NULL OR i.number ILIKE @Search)
            """;

        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        return await connection.QueryPagedAsync<SalesInvoiceResponse>(
            $"SELECT COUNT(*) FROM sales.sales_invoices i {filter}",
            $"{SalesInvoiceResponse.Select} {filter} ORDER BY i.invoice_date DESC, i.number DESC NULLS FIRST LIMIT @PageSize OFFSET @Offset",
            query.Paging,
            new
            {
                scope.AllBranches,
                scope.BranchIds,
                query.BranchId,
                query.CustomerId,
                Status = query.Status?.ToString(),
                query.From,
                query.To
            },
            cancellationToken);
    }
}

internal sealed class GetSalesInvoiceByIdQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetSalesInvoiceByIdQuery, SalesInvoiceResponse>
{
    public async Task<Result<SalesInvoiceResponse>> Handle(GetSalesInvoiceByIdQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql =
            $"""
            {SalesInvoiceResponse.Select}
            WHERE i.id = @SalesInvoiceId;

            SELECT l.line_number AS LineNumber, l.delivery_order_id AS DeliveryOrderId, d.number AS DeliveryOrderNumber,
                   l.item_id AS ItemId, it.code AS ItemCode, it.name AS ItemName, l.cycle_id AS CycleId, pc.number AS CycleNumber,
                   l.birds AS Birds, l.weight_kg AS WeightKg, l.price_per_kg AS PricePerKg, l.amount AS Amount, t.code AS TaxCode,
                   l.vat_rate_percent AS VatRatePercent, l.vat_tax_base AS VatTaxBase, l.vat_amount AS VatAmount
            FROM sales.sales_invoice_lines l
            JOIN sales.delivery_orders d ON d.id = l.delivery_order_id
            JOIN master.items it ON it.id = l.item_id
            JOIN partnership.production_cycles pc ON pc.id = l.cycle_id
            LEFT JOIN master.tax_codes t ON t.id = l.tax_code_id
            WHERE l.sales_invoice_id = @SalesInvoiceId
            ORDER BY l.line_number;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { query.SalesInvoiceId }, cancellationToken: cancellationToken));

        SalesInvoiceResponse? invoice = await multi.ReadSingleOrDefaultAsync<SalesInvoiceResponse>();
        if (invoice is null)
        {
            return Result.Failure<SalesInvoiceResponse>(SalesInvoiceErrors.NotFound(query.SalesInvoiceId));
        }

        Result access = await branchAccess.EnsureAccessAsync(invoice.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<SalesInvoiceResponse>(access.Error);
        }

        return invoice with { Lines = [.. await multi.ReadAsync<SalesInvoiceLineResponse>()] };
    }
}

internal sealed class GetUndeliveredHarvestsQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetUndeliveredHarvestsQuery, IReadOnlyList<UndeliveredHarvestResponse>>
{
    public async Task<Result<IReadOnlyList<UndeliveredHarvestResponse>>> Handle(
        GetUndeliveredHarvestsQuery query,
        CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql =
            $"""
            SELECT h.id AS HarvestId, h.date AS Date, h.age_days AS AgeDays, h.birds AS Birds, h.weight_kg AS WeightKg,
                   ROUND(h.weight_kg / h.birds, 3) AS AverageWeightKg, h.notes AS Notes,
                   pc.id AS CycleId, pc.number AS CycleNumber, pc.branch_id AS BranchId, b.code AS BranchCode,
                   co.name AS CoopName, f.name AS FarmerName
            FROM partnership.cycle_harvests h
            JOIN partnership.production_cycles pc ON pc.id = h.cycle_id
            JOIN master.branches b ON b.id = pc.branch_id
            JOIN master.coops co ON co.id = pc.coop_id
            JOIN master.farmers f ON f.id = pc.farmer_id
            WHERE {SalesSql.Branch("pc")}
              AND (@CycleId::uuid IS NULL OR pc.id = @CycleId)
              AND NOT EXISTS (SELECT 1 FROM sales.delivery_order_lines l WHERE l.harvest_id = h.id AND NOT l.is_cancelled)
            ORDER BY h.date, pc.number;
            """;

        IEnumerable<UndeliveredHarvestResponse> rows = await connection.QueryAsync<UndeliveredHarvestResponse>(
            new CommandDefinition(
                sql,
                new { scope.AllBranches, scope.BranchIds, query.BranchId, query.CycleId },
                cancellationToken: cancellationToken));

        return rows.ToList();
    }
}
