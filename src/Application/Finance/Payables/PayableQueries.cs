using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Finance.Receivables;
using Application.Finance.Reports;
using Dapper;
using Domain.Finance.Payables;
using Domain.MasterData.Vendors;
using SharedKernel;

namespace Application.Finance.Payables;

public sealed record GetVendorInvoicesQuery(
    PageRequest Paging,
    Guid? BranchId,
    Guid? VendorId,
    VendorInvoiceStatus? Status,
    DateOnly? From,
    DateOnly? To) : IQuery<PagedList<VendorInvoiceResponse>>;

public sealed record GetVendorInvoiceByIdQuery(Guid VendorInvoiceId) : IQuery<VendorInvoiceResponse>;

/// <summary>
/// Received goods of a vendor not billed yet (what a vendor invoice can still bill).
/// </summary>
public sealed record GetUninvoicedReceiptsQuery(Guid VendorId, Guid? BranchId) : IQuery<IReadOnlyList<UninvoicedReceiptLineResponse>>;

public sealed record GetPaymentVouchersQuery(
    PageRequest Paging,
    Guid? BranchId,
    Guid? VendorId,
    PaymentVoucherStatus? Status,
    DateOnly? From,
    DateOnly? To) : IQuery<PagedList<PaymentVoucherResponse>>;

public sealed record GetPaymentVoucherByIdQuery(Guid PaymentVoucherId) : IQuery<PaymentVoucherResponse>;

/// <summary>
/// Kartu hutang: opening balance, posted invoices (credit) and payments (debit) with a running balance.
/// </summary>
public sealed record GetPayableLedgerQuery(Guid VendorId, DateOnly From, DateOnly To, Guid? BranchId) : IQuery<PayableLedgerResponse>;

/// <summary>
/// Umur hutang per vendor as of a date, bucketed by days past the due date.
/// </summary>
public sealed record GetPayableAgingQuery(DateOnly AsOf, Guid? BranchId, Guid? VendorId) : IQuery<PayableAgingResponse>;

public sealed record VendorInvoiceResponse
{
    public const string Select =
        """
        SELECT i.id AS Id, i.number AS Number, i.branch_id AS BranchId, b.code AS BranchCode,
               i.vendor_id AS VendorId, v.code AS VendorCode, v.name AS VendorName,
               i.vendor_invoice_number AS VendorInvoiceNumber, i.tax_invoice_number AS TaxInvoiceNumber,
               i.invoice_date AS InvoiceDate, i.due_date AS DueDate, i.status AS Status, i.notes AS Notes,
               i.subtotal AS Subtotal, i.goods_value AS GoodsValue, (i.subtotal - i.goods_value) AS PriceVariance,
               i.vat_amount AS VatAmount, t.code AS IncomeTaxCode, i.income_tax_rate_percent AS IncomeTaxRatePercent,
               i.income_tax_amount AS IncomeTaxAmount, i.total AS Total, i.paid_amount AS PaidAmount,
               (i.total - i.paid_amount) AS Outstanding, i.max_price_deviation_percent AS MaxPriceDeviationPercent,
               i.price_variance_approval_reason AS PriceVarianceApprovalReason, i.posted_at_utc AS PostedAtUtc,
               i.cancellation_reason AS CancellationReason
        FROM finance.vendor_invoices i
        JOIN master.branches b ON b.id = i.branch_id
        JOIN master.vendors v ON v.id = i.vendor_id
        LEFT JOIN master.tax_codes t ON t.id = i.income_tax_code_id
        """;

    public Guid Id { get; init; }

    /// <summary>
    /// Internal number; null while the invoice is a draft.
    /// </summary>
    public string? Number { get; init; }

    public Guid BranchId { get; init; }

    public string BranchCode { get; init; }

    public Guid VendorId { get; init; }

    public string VendorCode { get; init; }

    public string VendorName { get; init; }

    public string VendorInvoiceNumber { get; init; }

    public string? TaxInvoiceNumber { get; init; }

    public DateOnly InvoiceDate { get; init; }

    public DateOnly DueDate { get; init; }

    public string Status { get; init; }

    public string? Notes { get; init; }

    /// <summary>
    /// Invoiced amount excluding VAT.
    /// </summary>
    public decimal Subtotal { get; init; }

    /// <summary>
    /// Receipt value of the billed goods (cleared from hutang belum ditagih).
    /// </summary>
    public decimal GoodsValue { get; init; }

    public decimal PriceVariance { get; init; }

    public decimal VatAmount { get; init; }

    public string? IncomeTaxCode { get; init; }

    public decimal IncomeTaxRatePercent { get; init; }

    public decimal IncomeTaxAmount { get; init; }

    /// <summary>
    /// Payable: subtotal + VAT − income tax withheld.
    /// </summary>
    public decimal Total { get; init; }

    public decimal PaidAmount { get; init; }

    public decimal Outstanding { get; init; }

    public decimal MaxPriceDeviationPercent { get; init; }

    public string? PriceVarianceApprovalReason { get; init; }

    public DateTime? PostedAtUtc { get; init; }

    public string? CancellationReason { get; init; }

    /// <summary>
    /// Only filled by the detail endpoint.
    /// </summary>
    public IReadOnlyList<VendorInvoiceLineResponse>? Lines { get; init; }
}

public sealed record VendorInvoiceLineResponse(
    int LineNumber,
    Guid GoodsReceiptId,
    string GoodsReceiptNumber,
    int GoodsReceiptLineNumber,
    string PurchaseOrderNumber,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string UomCode,
    decimal Quantity,
    decimal OrderUnitPrice,
    decimal UnitPrice,
    decimal PriceDeviationPercent,
    decimal Amount,
    decimal GoodsValue,
    string? TaxCode,
    decimal VatRatePercent,
    decimal VatAmount);

public sealed record UninvoicedReceiptLineResponse(
    Guid GoodsReceiptId,
    string GoodsReceiptNumber,
    DateOnly ReceiptDate,
    int GoodsReceiptLineNumber,
    Guid BranchId,
    string PurchaseOrderNumber,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string UomCode,
    decimal Quantity,
    decimal QuantityInvoiced,
    decimal UninvoicedQuantity,
    decimal OrderUnitPrice,
    decimal UninvoicedValue);

public sealed record PaymentVoucherResponse
{
    public const string Select =
        """
        SELECT p.id AS Id, p.number AS Number, p.branch_id AS BranchId, b.code AS BranchCode,
               p.vendor_id AS VendorId, v.code AS VendorCode, v.name AS VendorName,
               p.cash_bank_account_id AS CashBankAccountId, cb.code AS CashBankCode, cb.name AS CashBankName,
               p.payment_date AS PaymentDate, p.reference AS Reference, p.notes AS Notes, p.amount AS Amount, p.status AS Status,
               p.approved_at_utc AS ApprovedAtUtc, p.paid_at_utc AS PaidAtUtc, p.cancellation_reason AS CancellationReason
        FROM finance.payment_vouchers p
        JOIN master.branches b ON b.id = p.branch_id
        JOIN master.vendors v ON v.id = p.vendor_id
        JOIN finance.cash_bank_accounts cb ON cb.id = p.cash_bank_account_id
        """;

    public Guid Id { get; init; }

    public string Number { get; init; }

    public Guid BranchId { get; init; }

    public string BranchCode { get; init; }

    public Guid VendorId { get; init; }

    public string VendorCode { get; init; }

    public string VendorName { get; init; }

    public Guid CashBankAccountId { get; init; }

    public string CashBankCode { get; init; }

    public string CashBankName { get; init; }

    public DateOnly PaymentDate { get; init; }

    public string? Reference { get; init; }

    public string? Notes { get; init; }

    public decimal Amount { get; init; }

    public string Status { get; init; }

    public DateTime? ApprovedAtUtc { get; init; }

    public DateTime? PaidAtUtc { get; init; }

    public string? CancellationReason { get; init; }

    /// <summary>
    /// Only filled by the detail endpoint.
    /// </summary>
    public IReadOnlyList<PaymentAllocationResponse>? Allocations { get; init; }
}

public sealed record PaymentAllocationResponse(Guid VendorInvoiceId, string? InvoiceNumber, string VendorInvoiceNumber, DateOnly DueDate, decimal Amount);

public sealed record PayableLedgerResponse(
    Guid VendorId,
    string VendorCode,
    string VendorName,
    DateOnly From,
    DateOnly To,
    decimal OpeningBalance,
    decimal TotalCredit,
    decimal TotalDebit,
    decimal ClosingBalance,
    IReadOnlyList<PayableLedgerLine> Lines);

/// <param name="DocumentType">"Invoice" (credit, increases the payable) or "Payment" (debit).</param>
public sealed record PayableLedgerLine(
    string DocumentType,
    Guid DocumentId,
    string Number,
    DateOnly Date,
    string BranchCode,
    string? Description,
    decimal Debit,
    decimal Credit,
    decimal Balance);

public sealed record PayableAgingResponse(DateOnly AsOf, AgingBuckets Totals, IReadOnlyList<VendorAging> Vendors);

public sealed record VendorAging(Guid VendorId, string VendorCode, string VendorName, AgingBuckets Buckets, IReadOnlyList<VendorInvoiceAging> Invoices);

public sealed record VendorInvoiceAging(
    Guid VendorInvoiceId,
    string Number,
    string VendorInvoiceNumber,
    string BranchCode,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    int DaysOverdue,
    decimal Total,
    decimal Outstanding);

internal static class PayableSql
{
    public const string PostedInvoice = "i.status IN ('Posted', 'PartiallyPaid', 'Paid')";

    public static string Branch(string alias) =>
        $"""
        (@AllBranches OR {alias}.branch_id = ANY(@BranchIds))
        AND (@BranchId::uuid IS NULL OR {alias}.branch_id = @BranchId)
        """;
}

internal sealed class GetVendorInvoicesQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetVendorInvoicesQuery, PagedList<VendorInvoiceResponse>>
{
    public async Task<Result<PagedList<VendorInvoiceResponse>>> Handle(GetVendorInvoicesQuery query, CancellationToken cancellationToken)
    {
        string filter =
            $"""
            WHERE {PayableSql.Branch("i")}
              AND (@VendorId::uuid IS NULL OR i.vendor_id = @VendorId)
              AND (@Status::text IS NULL OR i.status = @Status)
              AND (@From::date IS NULL OR i.invoice_date >= @From)
              AND (@To::date IS NULL OR i.invoice_date <= @To)
              AND (@Search IS NULL OR i.number ILIKE @Search OR i.vendor_invoice_number ILIKE @Search)
            """;

        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        return await connection.QueryPagedAsync<VendorInvoiceResponse>(
            $"SELECT COUNT(*) FROM finance.vendor_invoices i {filter}",
            $"{VendorInvoiceResponse.Select} {filter} ORDER BY i.invoice_date DESC, i.number DESC NULLS FIRST LIMIT @PageSize OFFSET @Offset",
            query.Paging,
            new
            {
                scope.AllBranches,
                scope.BranchIds,
                query.BranchId,
                query.VendorId,
                Status = query.Status?.ToString(),
                query.From,
                query.To
            },
            cancellationToken);
    }
}

internal sealed class GetVendorInvoiceByIdQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetVendorInvoiceByIdQuery, VendorInvoiceResponse>
{
    public async Task<Result<VendorInvoiceResponse>> Handle(GetVendorInvoiceByIdQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql =
            $"""
            {VendorInvoiceResponse.Select}
            WHERE i.id = @VendorInvoiceId;

            SELECT l.line_number AS LineNumber, l.goods_receipt_id AS GoodsReceiptId, r.number AS GoodsReceiptNumber,
                   l.goods_receipt_line_number AS GoodsReceiptLineNumber, o.number AS PurchaseOrderNumber,
                   l.item_id AS ItemId, it.code AS ItemCode, it.name AS ItemName, u.code AS UomCode, l.quantity AS Quantity,
                   l.order_unit_price AS OrderUnitPrice, l.unit_price AS UnitPrice, l.price_deviation_percent AS PriceDeviationPercent,
                   l.amount AS Amount, l.goods_value AS GoodsValue, t.code AS TaxCode, l.vat_rate_percent AS VatRatePercent,
                   l.vat_amount AS VatAmount
            FROM finance.vendor_invoice_lines l
            JOIN inventory.goods_receipts r ON r.id = l.goods_receipt_id
            JOIN procurement.purchase_orders o ON o.id = l.purchase_order_id
            JOIN master.items it ON it.id = l.item_id
            JOIN master.uoms u ON u.id = l.uom_id
            LEFT JOIN master.tax_codes t ON t.id = l.tax_code_id
            WHERE l.vendor_invoice_id = @VendorInvoiceId
            ORDER BY l.line_number;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { query.VendorInvoiceId }, cancellationToken: cancellationToken));

        VendorInvoiceResponse? invoice = await multi.ReadSingleOrDefaultAsync<VendorInvoiceResponse>();
        if (invoice is null)
        {
            return Result.Failure<VendorInvoiceResponse>(VendorInvoiceErrors.NotFound(query.VendorInvoiceId));
        }

        Result access = await branchAccess.EnsureAccessAsync(invoice.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<VendorInvoiceResponse>(access.Error);
        }

        return invoice with { Lines = [.. await multi.ReadAsync<VendorInvoiceLineResponse>()] };
    }
}

internal sealed class GetUninvoicedReceiptsQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetUninvoicedReceiptsQuery, IReadOnlyList<UninvoicedReceiptLineResponse>>
{
    public async Task<Result<IReadOnlyList<UninvoicedReceiptLineResponse>>> Handle(GetUninvoicedReceiptsQuery query, CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql =
            $"""
            SELECT r.id AS GoodsReceiptId, r.number AS GoodsReceiptNumber, r.receipt_date AS ReceiptDate,
                   l.line_number AS GoodsReceiptLineNumber, r.branch_id AS BranchId, o.number AS PurchaseOrderNumber,
                   l.item_id AS ItemId, it.code AS ItemCode, it.name AS ItemName, u.code AS UomCode, l.quantity AS Quantity,
                   l.quantity_invoiced AS QuantityInvoiced, (l.quantity - l.quantity_invoiced) AS UninvoicedQuantity,
                   ol.unit_price AS OrderUnitPrice, (l.value - l.value_invoiced) AS UninvoicedValue
            FROM inventory.goods_receipt_lines l
            JOIN inventory.goods_receipts r ON r.id = l.goods_receipt_id
            JOIN procurement.purchase_orders o ON o.id = r.purchase_order_id
            JOIN procurement.purchase_order_lines ol ON ol.purchase_order_id = o.id AND ol.line_number = l.purchase_order_line_number
            JOIN master.items it ON it.id = l.item_id
            JOIN master.uoms u ON u.id = l.uom_id
            WHERE r.vendor_id = @VendorId AND l.quantity > l.quantity_invoiced AND {PayableSql.Branch("r")}
            ORDER BY r.receipt_date, r.number, l.line_number;
            """;

        IEnumerable<UninvoicedReceiptLineResponse> rows = await connection.QueryAsync<UninvoicedReceiptLineResponse>(new CommandDefinition(
            sql, new { query.VendorId, query.BranchId, scope.AllBranches, scope.BranchIds }, cancellationToken: cancellationToken));

        return rows.ToList();
    }
}

internal sealed class GetPaymentVouchersQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetPaymentVouchersQuery, PagedList<PaymentVoucherResponse>>
{
    public async Task<Result<PagedList<PaymentVoucherResponse>>> Handle(GetPaymentVouchersQuery query, CancellationToken cancellationToken)
    {
        string filter =
            $"""
            WHERE {PayableSql.Branch("p")}
              AND (@VendorId::uuid IS NULL OR p.vendor_id = @VendorId)
              AND (@Status::text IS NULL OR p.status = @Status)
              AND (@From::date IS NULL OR p.payment_date >= @From)
              AND (@To::date IS NULL OR p.payment_date <= @To)
              AND (@Search IS NULL OR p.number ILIKE @Search OR p.reference ILIKE @Search)
            """;

        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        return await connection.QueryPagedAsync<PaymentVoucherResponse>(
            $"SELECT COUNT(*) FROM finance.payment_vouchers p {filter}",
            $"{PaymentVoucherResponse.Select} {filter} ORDER BY p.payment_date DESC, p.number DESC LIMIT @PageSize OFFSET @Offset",
            query.Paging,
            new
            {
                scope.AllBranches,
                scope.BranchIds,
                query.BranchId,
                query.VendorId,
                Status = query.Status?.ToString(),
                query.From,
                query.To
            },
            cancellationToken);
    }
}

internal sealed class GetPaymentVoucherByIdQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetPaymentVoucherByIdQuery, PaymentVoucherResponse>
{
    public async Task<Result<PaymentVoucherResponse>> Handle(GetPaymentVoucherByIdQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql =
            $"""
            {PaymentVoucherResponse.Select}
            WHERE p.id = @PaymentVoucherId;

            SELECT a.vendor_invoice_id AS VendorInvoiceId, i.number AS InvoiceNumber, i.vendor_invoice_number AS VendorInvoiceNumber,
                   i.due_date AS DueDate, a.amount AS Amount
            FROM finance.payment_voucher_allocations a
            JOIN finance.vendor_invoices i ON i.id = a.vendor_invoice_id
            WHERE a.payment_voucher_id = @PaymentVoucherId
            ORDER BY i.due_date, i.number;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { query.PaymentVoucherId }, cancellationToken: cancellationToken));

        PaymentVoucherResponse? voucher = await multi.ReadSingleOrDefaultAsync<PaymentVoucherResponse>();
        if (voucher is null)
        {
            return Result.Failure<PaymentVoucherResponse>(PaymentVoucherErrors.NotFound(query.PaymentVoucherId));
        }

        Result access = await branchAccess.EnsureAccessAsync(voucher.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<PaymentVoucherResponse>(access.Error);
        }

        return voucher with { Allocations = [.. await multi.ReadAsync<PaymentAllocationResponse>()] };
    }
}

internal sealed class GetPayableLedgerQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetPayableLedgerQuery, PayableLedgerResponse>
{
    public async Task<Result<PayableLedgerResponse>> Handle(GetPayableLedgerQuery query, CancellationToken cancellationToken)
    {
        if (query.To < query.From)
        {
            return Result.Failure<PayableLedgerResponse>(ReportErrors.InvalidDateRange);
        }

        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql =
            $"""
            SELECT v.id AS Id, v.code AS Code, v.name AS Name FROM master.vendors v WHERE v.id = @VendorId;

            SELECT COALESCE((SELECT SUM(i.total) FROM finance.vendor_invoices i
                             WHERE i.vendor_id = @VendorId AND {PayableSql.PostedInvoice}
                               AND i.invoice_date < @From AND {PayableSql.Branch("i")}), 0)
                 - COALESCE((SELECT SUM(p.amount) FROM finance.payment_vouchers p
                             WHERE p.vendor_id = @VendorId AND p.status = 'Paid'
                               AND p.payment_date < @From AND {PayableSql.Branch("p")}), 0);

            SELECT * FROM (
                SELECT 'Invoice' AS DocumentType, i.id AS DocumentId, i.number AS Number, i.invoice_date AS Date,
                       b.code AS BranchCode, i.vendor_invoice_number AS Description, 0::numeric AS Debit, i.total AS Credit
                FROM finance.vendor_invoices i
                JOIN master.branches b ON b.id = i.branch_id
                WHERE i.vendor_id = @VendorId AND {PayableSql.PostedInvoice}
                  AND i.invoice_date BETWEEN @From AND @To AND {PayableSql.Branch("i")}
                UNION ALL
                SELECT 'Payment', p.id, p.number, p.payment_date, b.code, p.reference, p.amount, 0::numeric
                FROM finance.payment_vouchers p
                JOIN master.branches b ON b.id = p.branch_id
                WHERE p.vendor_id = @VendorId AND p.status = 'Paid'
                  AND p.payment_date BETWEEN @From AND @To AND {PayableSql.Branch("p")}) m
            ORDER BY m.Date, m.DocumentType, m.Number;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new { query.VendorId, query.From, query.To, query.BranchId, scope.AllBranches, scope.BranchIds },
            cancellationToken: cancellationToken));

        VendorRow? vendor = await multi.ReadSingleOrDefaultAsync<VendorRow>();
        if (vendor is null)
        {
            return Result.Failure<PayableLedgerResponse>(VendorErrors.NotFound(query.VendorId));
        }

        decimal opening = await multi.ReadSingleAsync<decimal>();
        decimal balance = opening;

        var lines = new List<PayableLedgerLine>();
        foreach (MutationRow row in await multi.ReadAsync<MutationRow>())
        {
            balance += row.Credit - row.Debit;
            lines.Add(new PayableLedgerLine(
                row.DocumentType, row.DocumentId, row.Number, row.Date, row.BranchCode, row.Description, row.Debit, row.Credit, balance));
        }

        return new PayableLedgerResponse(
            vendor.Id, vendor.Code, vendor.Name, query.From, query.To, opening,
            lines.Sum(l => l.Credit), lines.Sum(l => l.Debit), balance, lines);
    }

    private sealed record VendorRow(Guid Id, string Code, string Name);

    private sealed record MutationRow(
        string DocumentType,
        Guid DocumentId,
        string Number,
        DateOnly Date,
        string BranchCode,
        string? Description,
        decimal Debit,
        decimal Credit);
}

internal sealed class GetPayableAgingQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetPayableAgingQuery, PayableAgingResponse>
{
    public async Task<Result<PayableAgingResponse>> Handle(GetPayableAgingQuery query, CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql =
            $"""
            SELECT * FROM (
                SELECT i.id AS VendorInvoiceId, i.number AS Number, i.vendor_invoice_number AS VendorInvoiceNumber, b.code AS BranchCode,
                       i.vendor_id AS VendorId, v.code AS VendorCode, v.name AS VendorName,
                       i.invoice_date AS InvoiceDate, i.due_date AS DueDate, i.total AS Total,
                       i.total - COALESCE((SELECT SUM(a.amount)
                                           FROM finance.payment_voucher_allocations a
                                           JOIN finance.payment_vouchers p ON p.id = a.payment_voucher_id
                                           WHERE a.vendor_invoice_id = i.id AND p.status = 'Paid' AND p.payment_date <= @AsOf), 0) AS Outstanding
                FROM finance.vendor_invoices i
                JOIN master.branches b ON b.id = i.branch_id
                JOIN master.vendors v ON v.id = i.vendor_id
                WHERE {PayableSql.PostedInvoice} AND i.invoice_date <= @AsOf
                  AND (@VendorId::uuid IS NULL OR i.vendor_id = @VendorId)
                  AND {PayableSql.Branch("i")}) x
            WHERE x.Outstanding <> 0
            ORDER BY x.VendorCode, x.DueDate, x.Number;
            """;

        IEnumerable<InvoiceRow> rows = await connection.QueryAsync<InvoiceRow>(new CommandDefinition(
            sql,
            new { query.AsOf, query.VendorId, query.BranchId, scope.AllBranches, scope.BranchIds },
            cancellationToken: cancellationToken));

        var vendors = rows
            .GroupBy(r => (r.VendorId, r.VendorCode, r.VendorName))
            .Select(g =>
            {
                var invoices = g
                    .Select(r => new VendorInvoiceAging(
                        r.VendorInvoiceId,
                        r.Number,
                        r.VendorInvoiceNumber,
                        r.BranchCode,
                        r.InvoiceDate,
                        r.DueDate,
                        query.AsOf.DayNumber - r.DueDate.DayNumber,
                        r.Total,
                        r.Outstanding))
                    .ToList();

                AgingBuckets buckets = invoices.Aggregate(
                    AgingBuckets.Zero, (total, i) => total.Add(AgingBuckets.For(i.DaysOverdue, i.Outstanding)));

                return new VendorAging(g.Key.VendorId, g.Key.VendorCode, g.Key.VendorName, buckets, invoices);
            })
            .ToList();

        AgingBuckets totals = vendors.Aggregate(AgingBuckets.Zero, (total, v) => total.Add(v.Buckets));

        return new PayableAgingResponse(query.AsOf, totals, vendors);
    }

    private sealed record InvoiceRow(
        Guid VendorInvoiceId,
        string Number,
        string VendorInvoiceNumber,
        string BranchCode,
        Guid VendorId,
        string VendorCode,
        string VendorName,
        DateOnly InvoiceDate,
        DateOnly DueDate,
        decimal Total,
        decimal Outstanding);
}
