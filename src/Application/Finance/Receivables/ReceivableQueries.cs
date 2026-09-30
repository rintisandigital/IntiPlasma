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
    public const string Select =
        """
        SELECT r.id AS Id, r.number AS Number, r.branch_id AS BranchId, b.code AS BranchCode,
               r.customer_id AS CustomerId, c.code AS CustomerCode, c.name AS CustomerName, r.receipt_date AS ReceiptDate,
               r.cash_account_id AS CashAccountId, a.code AS CashAccountCode, a.name AS CashAccountName,
               r.amount AS Amount, r.reference AS Reference, r.notes AS Notes
        FROM finance.customer_receipts r
        JOIN master.branches b ON b.id = r.branch_id
        JOIN master.customers c ON c.id = r.customer_id
        JOIN finance.accounts a ON a.id = r.cash_account_id
        """;

    public Guid Id { get; init; }

    public string Number { get; init; }

    public Guid BranchId { get; init; }

    public string BranchCode { get; init; }

    public Guid CustomerId { get; init; }

    public string CustomerCode { get; init; }

    public string CustomerName { get; init; }

    public DateOnly ReceiptDate { get; init; }

    public Guid CashAccountId { get; init; }

    public string CashAccountCode { get; init; }

    public string CashAccountName { get; init; }

    public decimal Amount { get; init; }

    public string? Reference { get; init; }

    public string? Notes { get; init; }

    /// <summary>
    /// Only filled by the detail endpoint.
    /// </summary>
    public IReadOnlyList<CustomerReceiptAllocationResponse>? Allocations { get; init; }
}

public sealed record CustomerReceiptAllocationResponse(Guid SalesInvoiceId, string InvoiceNumber, DateOnly InvoiceDate, decimal Amount);

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

/// <param name="DocumentType">"Invoice" or "Receipt".</param>
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

        return receipt with { Allocations = [.. await multi.ReadAsync<CustomerReceiptAllocationResponse>()] };
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

            SELECT COALESCE((SELECT SUM(i.total) FROM sales.sales_invoices i
                             WHERE i.customer_id = @CustomerId AND {ReceivableSql.PostedInvoice}
                               AND i.invoice_date < @From AND {ReceivableSql.Branch("i")}), 0)
                 - COALESCE((SELECT SUM(r.amount) FROM finance.customer_receipts r
                             WHERE r.customer_id = @CustomerId AND r.receipt_date < @From AND {ReceivableSql.Branch("r")}), 0);

            SELECT * FROM (
                SELECT 'Invoice' AS DocumentType, i.id AS DocumentId, i.number AS Number, i.invoice_date AS Date,
                       b.code AS BranchCode, i.notes AS Description, i.total AS Debit, 0::numeric AS Credit
                FROM sales.sales_invoices i
                JOIN master.branches b ON b.id = i.branch_id
                WHERE i.customer_id = @CustomerId AND {ReceivableSql.PostedInvoice}
                  AND i.invoice_date BETWEEN @From AND @To AND {ReceivableSql.Branch("i")}
                UNION ALL
                SELECT 'Receipt', r.id, r.number, r.receipt_date, b.code, r.reference, 0::numeric, r.amount
                FROM finance.customer_receipts r
                JOIN master.branches b ON b.id = r.branch_id
                WHERE r.customer_id = @CustomerId
                  AND r.receipt_date BETWEEN @From AND @To AND {ReceivableSql.Branch("r")}) m
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
                       i.total - COALESCE((SELECT SUM(a.amount)
                                           FROM finance.customer_receipt_allocations a
                                           JOIN finance.customer_receipts r ON r.id = a.customer_receipt_id
                                           WHERE a.sales_invoice_id = i.id AND r.receipt_date <= @AsOf), 0) AS Outstanding
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
