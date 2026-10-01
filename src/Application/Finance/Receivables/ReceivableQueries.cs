using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Finance.Reports;
using Dapper;
using Domain.Finance.Receivables;
using Domain.MasterData.Customers;
using SharedKernel;

namespace Application.Finance.Receivables;

public sealed record GetCustomerReceiptsQuery(
    PageRequest Paging,
    Guid? BranchId,
    Guid? CustomerId,
    DateOnly? From,
    DateOnly? To) : IQuery<PagedList<CustomerReceiptResponse>>;

public sealed record GetCustomerReceiptByIdQuery(Guid CustomerReceiptId) : IQuery<CustomerReceiptResponse>;

/// <summary>
/// Kartu piutang: opening balance, posted invoices (debit) and receipts (credit) with a running balance, closing balance.
/// </summary>
public sealed record GetReceivableLedgerQuery(Guid CustomerId, DateOnly From, DateOnly To, Guid? BranchId)
    : IQuery<ReceivableLedgerResponse>;

/// <summary>
/// Umur piutang per customer as of a date: the outstanding of every posted invoice (receipts up to that date),
/// bucketed by days past the due date.
/// </summary>
public sealed record GetReceivableAgingQuery(DateOnly AsOf, Guid? BranchId, Guid? CustomerId) : IQuery<ReceivableAgingResponse>;

public sealed record CustomerReceiptResponse
{
    /// <summary>
    /// Lampiran: attachment ids; metadata via <c>GET /attachments?ids=</c>.
    /// </summary>
    public Guid[] Documents { get; init; } = [];

    public const string Select =
        """
        SELECT r.id AS Id, r.number AS Number, r.branch_id AS BranchId, b.code AS BranchCode,
               r.customer_id AS CustomerId, c.code AS CustomerCode, c.name AS CustomerName, r.receipt_date AS ReceiptDate,
               r.cash_bank_account_id AS CashBankAccountId, cb.code AS CashBankCode,
               r.cash_account_id AS CashAccountId, a.code AS CashAccountCode, a.name AS CashAccountName,
               r.amount AS Amount, r.advance_amount AS AdvanceAmount, r.applied_advance_amount AS AppliedAdvanceAmount,
               CASE WHEN r.status = 'Voided' THEN 0 ELSE r.advance_amount - r.applied_advance_amount END AS UnappliedAdvance,
               r.status AS Status, r.reference AS Reference, r.notes AS Notes, r.void_date AS VoidDate, r.void_reason AS VoidReason,
               r.documents AS Documents
        FROM finance.customer_receipts r
        JOIN master.branches b ON b.id = r.branch_id
        JOIN master.customers c ON c.id = r.customer_id
        JOIN finance.accounts a ON a.id = r.cash_account_id
        LEFT JOIN finance.cash_bank_accounts cb ON cb.id = r.cash_bank_account_id
        """;

    public Guid Id { get; init; }

    public string Number { get; init; }

    public Guid BranchId { get; init; }

    public string BranchCode { get; init; }

    public Guid CustomerId { get; init; }

    public string CustomerCode { get; init; }

    public string CustomerName { get; init; }

    public DateOnly ReceiptDate { get; init; }

    /// <summary>
    /// Null for receipts recorded before cash/bank accounts existed.
    /// </summary>
    public Guid? CashBankAccountId { get; init; }

    public string? CashBankCode { get; init; }

    public Guid CashAccountId { get; init; }

    public string CashAccountCode { get; init; }

    public string CashAccountName { get; init; }

    /// <summary>
    /// Total received: allocations + advance.
    /// </summary>
    public decimal Amount { get; init; }

    /// <summary>
    /// Uang muka penjualan received with this receipt.
    /// </summary>
    public decimal AdvanceAmount { get; init; }

    public decimal AppliedAdvanceAmount { get; init; }

    public decimal UnappliedAdvance { get; init; }

    public string Status { get; init; }

    public string? Reference { get; init; }

    public string? Notes { get; init; }

    public DateOnly? VoidDate { get; init; }

    public string? VoidReason { get; init; }

    /// <summary>
    /// Only filled by the detail endpoint.
    /// </summary>
    public IReadOnlyList<CustomerReceiptAllocationResponse>? Allocations { get; init; }

    /// <summary>
    /// Advance applied to invoices later; only filled by the detail endpoint.
    /// </summary>
    public IReadOnlyList<AdvanceApplicationResponse>? Applications { get; init; }
}

public sealed record CustomerReceiptAllocationResponse(Guid SalesInvoiceId, string InvoiceNumber, DateOnly InvoiceDate, decimal Amount);

public sealed record AdvanceApplicationResponse(Guid Id, Guid SalesInvoiceId, string InvoiceNumber, DateOnly Date, decimal Amount);

public sealed record ReceivableLedgerResponse(
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    DateOnly From,
    DateOnly To,
    decimal OpeningBalance,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal ClosingBalance,
    IReadOnlyList<ReceivableLedgerLine> Lines);

/// <param name="DocumentType">"Invoice", "Receipt", "ReceiptVoid", "AdvanceApplied" or "CreditNote".</param>
public sealed record ReceivableLedgerLine(
    string DocumentType,
    Guid DocumentId,
    string Number,
    DateOnly Date,
    string BranchCode,
    string? Description,
    decimal Debit,
    decimal Credit,
    decimal Balance);

public sealed record ReceivableAgingResponse(
    DateOnly AsOf,
    AgingBuckets Totals,
    IReadOnlyList<CustomerAging> Customers);

public sealed record CustomerAging(
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    AgingBuckets Buckets,
    IReadOnlyList<InvoiceAging> Invoices);

/// <param name="DaysOverdue">Days past the due date on the as-of date; zero or negative means not due yet.</param>
public sealed record InvoiceAging(
    Guid SalesInvoiceId,
    string Number,
    string BranchCode,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    int DaysOverdue,
    decimal Total,
    decimal Outstanding);

/// <param name="Current">Not due yet.</param>
public sealed record AgingBuckets(
    decimal Current,
    decimal Days1To30,
    decimal Days31To60,
    decimal Days61To90,
    decimal Over90Days,
    decimal Total)
{
    public static readonly AgingBuckets Zero = new(0, 0, 0, 0, 0, 0);

    public static AgingBuckets For(int daysOverdue, decimal amount) => daysOverdue switch
    {
        <= 0 => Zero with { Current = amount, Total = amount },
        <= 30 => Zero with { Days1To30 = amount, Total = amount },
        <= 60 => Zero with { Days31To60 = amount, Total = amount },
        <= 90 => Zero with { Days61To90 = amount, Total = amount },
        _ => Zero with { Over90Days = amount, Total = amount }
    };

    public AgingBuckets Add(AgingBuckets other) => new(
        Current + other.Current,
        Days1To30 + other.Days1To30,
        Days31To60 + other.Days31To60,
        Days61To90 + other.Days61To90,
        Over90Days + other.Over90Days,
        Total + other.Total);
}

internal static class ReceivableSql
{
    /// <summary>
    /// Invoices that are part of the receivable ledger.
    /// </summary>
    public const string PostedInvoice = "i.status IN ('Posted', 'PartiallyPaid', 'Paid')";

    public static string Branch(string alias) =>
        $"""
        (@AllBranches OR {alias}.branch_id = ANY(@BranchIds))
        AND (@BranchId::uuid IS NULL OR {alias}.branch_id = @BranchId)
        """;

    /// <summary>
    /// Every movement of a customer's receivable (@CustomerId): posted invoices (debit), the invoice part of receipts
    /// (credit; an advance is not a receivable movement), voided receipts (debit on the void date), advances applied
    /// to invoices (credit) and credit notes (credit).
    /// </summary>
    public static readonly string Movements =
        $"""
        SELECT 'Invoice' AS DocumentType, i.id AS DocumentId, i.number AS Number, i.invoice_date AS Date,
               b.code AS BranchCode, i.notes AS Description, i.total AS Debit, 0::numeric AS Credit
        FROM sales.sales_invoices i
        JOIN master.branches b ON b.id = i.branch_id
        WHERE i.customer_id = @CustomerId AND {PostedInvoice} AND {Branch("i")}
        UNION ALL
        SELECT 'Receipt', r.id, r.number, r.receipt_date, b.code, r.reference, 0::numeric, r.amount - r.advance_amount
        FROM finance.customer_receipts r
        JOIN master.branches b ON b.id = r.branch_id
        WHERE r.customer_id = @CustomerId AND r.amount > r.advance_amount AND {Branch("r")}
        UNION ALL
        SELECT 'ReceiptVoid', r.id, r.number, r.void_date, b.code, r.void_reason, r.amount - r.advance_amount, 0::numeric
        FROM finance.customer_receipts r
        JOIN master.branches b ON b.id = r.branch_id
        WHERE r.customer_id = @CustomerId AND r.status = 'Voided' AND r.amount > r.advance_amount AND {Branch("r")}
        UNION ALL
        SELECT 'AdvanceApplied', p.id, r.number, p.date, b.code, i.number, 0::numeric, p.amount
        FROM finance.customer_advance_applications p
        JOIN finance.customer_receipts r ON r.id = p.customer_receipt_id
        JOIN sales.sales_invoices i ON i.id = p.sales_invoice_id
        JOIN master.branches b ON b.id = r.branch_id
        WHERE r.customer_id = @CustomerId AND {Branch("r")}
        UNION ALL
        SELECT 'CreditNote', n.id, n.number, n.date, b.code, n.reason, 0::numeric, n.total
        FROM sales.sales_credit_notes n
        JOIN master.branches b ON b.id = n.branch_id
        WHERE n.customer_id = @CustomerId AND {Branch("n")}
        """;
}

internal sealed class GetCustomerReceiptsQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetCustomerReceiptsQuery, PagedList<CustomerReceiptResponse>>
{
    public async Task<Result<PagedList<CustomerReceiptResponse>>> Handle(GetCustomerReceiptsQuery query, CancellationToken cancellationToken)
    {
        string filter =
            $"""
            WHERE {ReceivableSql.Branch("r")}
              AND (@CustomerId::uuid IS NULL OR r.customer_id = @CustomerId)
              AND (@From::date IS NULL OR r.receipt_date >= @From)
              AND (@To::date IS NULL OR r.receipt_date <= @To)
              AND (@Search IS NULL OR r.number ILIKE @Search OR r.reference ILIKE @Search)
            """;

        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        return await connection.QueryPagedAsync<CustomerReceiptResponse>(
            $"SELECT COUNT(*) FROM finance.customer_receipts r {filter}",
            $"{CustomerReceiptResponse.Select} {filter} ORDER BY r.receipt_date DESC, r.number DESC LIMIT @PageSize OFFSET @Offset",
            query.Paging,
            new { scope.AllBranches, scope.BranchIds, query.BranchId, query.CustomerId, query.From, query.To },
            cancellationToken);
    }
}

internal sealed class GetCustomerReceiptByIdQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetCustomerReceiptByIdQuery, CustomerReceiptResponse>
{
    public async Task<Result<CustomerReceiptResponse>> Handle(GetCustomerReceiptByIdQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql =
            $"""
            {CustomerReceiptResponse.Select}
            WHERE r.id = @CustomerReceiptId;

            SELECT a.sales_invoice_id AS SalesInvoiceId, i.number AS InvoiceNumber, i.invoice_date AS InvoiceDate, a.amount AS Amount
            FROM finance.customer_receipt_allocations a
            JOIN sales.sales_invoices i ON i.id = a.sales_invoice_id
            WHERE a.customer_receipt_id = @CustomerReceiptId
            ORDER BY i.invoice_date, i.number;

            SELECT p.id AS Id, p.sales_invoice_id AS SalesInvoiceId, i.number AS InvoiceNumber, p.date AS Date, p.amount AS Amount
            FROM finance.customer_advance_applications p
            JOIN sales.sales_invoices i ON i.id = p.sales_invoice_id
            WHERE p.customer_receipt_id = @CustomerReceiptId
            ORDER BY p.date, i.number;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { query.CustomerReceiptId }, cancellationToken: cancellationToken));

        CustomerReceiptResponse? receipt = await multi.ReadSingleOrDefaultAsync<CustomerReceiptResponse>();
        if (receipt is null)
        {
            return Result.Failure<CustomerReceiptResponse>(CustomerReceiptErrors.NotFound(query.CustomerReceiptId));
        }

        Result access = await branchAccess.EnsureAccessAsync(receipt.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<CustomerReceiptResponse>(access.Error);
        }

        return receipt with
        {
            Allocations = [.. await multi.ReadAsync<CustomerReceiptAllocationResponse>()],
            Applications = [.. await multi.ReadAsync<AdvanceApplicationResponse>()]
        };
    }
}

internal sealed class GetReceivableLedgerQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetReceivableLedgerQuery, ReceivableLedgerResponse>
{
    public async Task<Result<ReceivableLedgerResponse>> Handle(GetReceivableLedgerQuery query, CancellationToken cancellationToken)
    {
        if (query.To < query.From)
        {
            return Result.Failure<ReceivableLedgerResponse>(ReportErrors.InvalidDateRange);
        }

        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql =
            $"""
            SELECT c.id AS Id, c.code AS Code, c.name AS Name FROM master.customers c WHERE c.id = @CustomerId;

            WITH m AS ({ReceivableSql.Movements})
            SELECT COALESCE(SUM(m.Debit - m.Credit), 0) FROM m WHERE m.Date < @From;

            WITH m AS ({ReceivableSql.Movements})
            SELECT m.DocumentType, m.DocumentId, m.Number, m.Date, m.BranchCode, m.Description, m.Debit, m.Credit
            FROM m WHERE m.Date BETWEEN @From AND @To
            ORDER BY m.Date, m.DocumentType, m.Number;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new { query.CustomerId, query.From, query.To, query.BranchId, scope.AllBranches, scope.BranchIds },
            cancellationToken: cancellationToken));

        CustomerRow? customer = await multi.ReadSingleOrDefaultAsync<CustomerRow>();
        if (customer is null)
        {
            return Result.Failure<ReceivableLedgerResponse>(CustomerErrors.NotFound(query.CustomerId));
        }

        decimal opening = await multi.ReadSingleAsync<decimal>();
        decimal balance = opening;

        var lines = new List<ReceivableLedgerLine>();
        foreach (MutationRow row in await multi.ReadAsync<MutationRow>())
        {
            balance += row.Debit - row.Credit;
            lines.Add(new ReceivableLedgerLine(
                row.DocumentType, row.DocumentId, row.Number, row.Date, row.BranchCode, row.Description, row.Debit, row.Credit, balance));
        }

        return new ReceivableLedgerResponse(
            customer.Id,
            customer.Code,
            customer.Name,
            query.From,
            query.To,
            opening,
            lines.Sum(l => l.Debit),
            lines.Sum(l => l.Credit),
            balance,
            lines);
    }

    private sealed record CustomerRow(Guid Id, string Code, string Name);

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

internal sealed class GetReceivableAgingQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetReceivableAgingQuery, ReceivableAgingResponse>
{
    public async Task<Result<ReceivableAgingResponse>> Handle(GetReceivableAgingQuery query, CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql =
            $"""
            SELECT * FROM (
                SELECT i.id AS SalesInvoiceId, i.number AS Number, b.code AS BranchCode,
                       i.customer_id AS CustomerId, c.code AS CustomerCode, c.name AS CustomerName,
                       i.invoice_date AS InvoiceDate, i.due_date AS DueDate, i.total AS Total,
                       i.total
                       - COALESCE((SELECT SUM(a.amount)
                                   FROM finance.customer_receipt_allocations a
                                   JOIN finance.customer_receipts r ON r.id = a.customer_receipt_id
                                   WHERE a.sales_invoice_id = i.id AND r.receipt_date <= @AsOf
                                     AND (r.status <> 'Voided' OR r.void_date > @AsOf)), 0)
                       - COALESCE((SELECT SUM(p.amount) FROM finance.customer_advance_applications p
                                   WHERE p.sales_invoice_id = i.id AND p.date <= @AsOf), 0)
                       - COALESCE((SELECT SUM(n.total) FROM sales.sales_credit_notes n
                                   WHERE n.sales_invoice_id = i.id AND n.date <= @AsOf), 0) AS Outstanding
                FROM sales.sales_invoices i
                JOIN master.branches b ON b.id = i.branch_id
                JOIN master.customers c ON c.id = i.customer_id
                WHERE {ReceivableSql.PostedInvoice} AND i.invoice_date <= @AsOf
                  AND (@CustomerId::uuid IS NULL OR i.customer_id = @CustomerId)
                  AND {ReceivableSql.Branch("i")}) x
            WHERE x.Outstanding <> 0
            ORDER BY x.CustomerCode, x.DueDate, x.Number;
            """;

        IEnumerable<InvoiceRow> rows = await connection.QueryAsync<InvoiceRow>(new CommandDefinition(
            sql,
            new { query.AsOf, query.CustomerId, query.BranchId, scope.AllBranches, scope.BranchIds },
            cancellationToken: cancellationToken));

        var customers = rows
            .GroupBy(r => (r.CustomerId, r.CustomerCode, r.CustomerName))
            .Select(g =>
            {
                var invoices = g
                    .Select(r => new InvoiceAging(
                        r.SalesInvoiceId,
                        r.Number,
                        r.BranchCode,
                        r.InvoiceDate,
                        r.DueDate,
                        query.AsOf.DayNumber - r.DueDate.DayNumber,
                        r.Total,
                        r.Outstanding))
                    .ToList();

                AgingBuckets buckets = invoices.Aggregate(
                    AgingBuckets.Zero, (total, i) => total.Add(AgingBuckets.For(i.DaysOverdue, i.Outstanding)));

                return new CustomerAging(g.Key.CustomerId, g.Key.CustomerCode, g.Key.CustomerName, buckets, invoices);
            })
            .ToList();

        AgingBuckets totals = customers.Aggregate(AgingBuckets.Zero, (total, c) => total.Add(c.Buckets));

        return new ReceivableAgingResponse(query.AsOf, totals, customers);
    }

    private sealed record InvoiceRow(
        Guid SalesInvoiceId,
        string Number,
        string BranchCode,
        Guid CustomerId,
        string CustomerCode,
        string CustomerName,
        DateOnly InvoiceDate,
        DateOnly DueDate,
        decimal Total,
        decimal Outstanding);
}
