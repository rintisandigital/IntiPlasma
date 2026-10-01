using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Finance.Reports;
using Dapper;
using Domain.Finance.CashBank;
using SharedKernel;

namespace Application.Finance.CashBank;

public sealed record GetCashBankAccountsQuery(Guid? BranchId, CashBankAccountType? Type, bool IncludeInactive)
    : IQuery<IReadOnlyList<CashBankAccountResponse>>;

public sealed record GetCashTransactionsQuery(
    PageRequest Paging,
    Guid? BranchId,
    Guid? CashBankAccountId,
    CashDirection? Direction,
    CashTransactionStatus? Status,
    DateOnly? From,
    DateOnly? To) : IQuery<PagedList<CashTransactionResponse>>;

public sealed record GetCashTransactionByIdQuery(Guid CashTransactionId) : IQuery<CashTransactionResponse>;

public sealed record GetBankTransfersQuery(PageRequest Paging, Guid? BranchId, Guid? CashBankAccountId, DateOnly? From, DateOnly? To)
    : IQuery<PagedList<BankTransferResponse>>;

/// <summary>
/// Buku kas/bank: opening balance, every posted movement of the cash/bank account's ledger account with a running
/// balance, closing balance.
/// </summary>
public sealed record GetCashBankLedgerQuery(Guid CashBankAccountId, DateOnly From, DateOnly To) : IQuery<CashBankLedgerResponse>;

public sealed record GetBankReconciliationsQuery(Guid? CashBankAccountId, Guid? BranchId) : IQuery<IReadOnlyList<BankReconciliationSummary>>;

/// <summary>
/// A reconciliation with its statement lines, the uncleared ledger lines up to the statement date and the summary.
/// </summary>
public sealed record GetBankReconciliationByIdQuery(Guid BankReconciliationId) : IQuery<BankReconciliationResponse>;

/// <param name="Balance">Ledger balance of the account today (debit − credit).</param>
public sealed record CashBankAccountResponse(
    Guid Id,
    string Code,
    string Name,
    string Type,
    Guid BranchId,
    string BranchCode,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    string? BankName,
    string? AccountNumber,
    bool IsActive,
    decimal Balance);

public sealed record CashTransactionResponse
{
    public const string Select =
        """
        SELECT t.id AS Id, t.number AS Number, t.branch_id AS BranchId, b.code AS BranchCode,
               t.cash_bank_account_id AS CashBankAccountId, cb.code AS CashBankCode, cb.name AS CashBankName,
               t.direction AS Direction, t.date AS Date, t.description AS Description, t.reference AS Reference,
               t.status AS Status, t.amount AS Amount, t.approved_at_utc AS ApprovedAtUtc, t.posted_at_utc AS PostedAtUtc,
               t.cancellation_reason AS CancellationReason
        FROM finance.cash_transactions t
        JOIN master.branches b ON b.id = t.branch_id
        JOIN finance.cash_bank_accounts cb ON cb.id = t.cash_bank_account_id
        """;

    public Guid Id { get; init; }

    /// <summary>
    /// Null until posted.
    /// </summary>
    public string? Number { get; init; }

    public Guid BranchId { get; init; }

    public string BranchCode { get; init; }

    public Guid CashBankAccountId { get; init; }

    public string CashBankCode { get; init; }

    public string CashBankName { get; init; }

    public string Direction { get; init; }

    public DateOnly Date { get; init; }

    public string Description { get; init; }

    public string? Reference { get; init; }

    public string Status { get; init; }

    public decimal Amount { get; init; }

    public DateTime? ApprovedAtUtc { get; init; }

    public DateTime? PostedAtUtc { get; init; }

    public string? CancellationReason { get; init; }

    /// <summary>
    /// Only filled by the detail endpoint.
    /// </summary>
    public IReadOnlyList<CashTransactionLineResponse>? Lines { get; init; }
}

public sealed record CashTransactionLineResponse(
    int LineNumber,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    Guid? CostCenterId,
    string? CostCenterCode,
    string? Description,
    decimal Amount);

public sealed record BankTransferResponse(
    Guid Id,
    string Number,
    Guid BranchId,
    string BranchCode,
    DateOnly Date,
    Guid FromCashBankAccountId,
    string FromCode,
    string FromName,
    Guid ToCashBankAccountId,
    string ToCode,
    string ToName,
    decimal Amount,
    string? Reference,
    string? Notes);

public sealed record CashBankLedgerResponse(
    Guid CashBankAccountId,
    string Code,
    string Name,
    string AccountCode,
    DateOnly From,
    DateOnly To,
    decimal OpeningBalance,
    decimal TotalIn,
    decimal TotalOut,
    decimal ClosingBalance,
    IReadOnlyList<CashBankLedgerLine> Lines);

public sealed record CashBankLedgerLine(
    Guid JournalId,
    int JournalLineNumber,
    string Number,
    DateOnly Date,
    string Description,
    string? SourceType,
    decimal In,
    decimal Out,
    decimal Balance);

public sealed record BankReconciliationSummary(
    Guid Id,
    Guid CashBankAccountId,
    string CashBankCode,
    DateOnly StatementDate,
    decimal StatementBalance,
    string Status,
    DateTime? CompletedAtUtc);

/// <param name="BookBalance">Ledger balance of the bank's account at the statement date.</param>
/// <param name="UnclearedNet">Ledger movements up to the statement date not on a bank statement yet.</param>
/// <param name="Difference">Statement balance − (book balance − uncleared); zero when reconciled.</param>
public sealed record BankReconciliationResponse(
    Guid Id,
    Guid CashBankAccountId,
    string CashBankCode,
    string CashBankName,
    DateOnly StatementDate,
    decimal StatementBalance,
    string Status,
    decimal BookBalance,
    decimal UnclearedNet,
    decimal Difference,
    int UnmatchedStatementLines,
    IReadOnlyList<StatementLineResponse> StatementLines,
    IReadOnlyList<UnclearedEntryResponse> UnclearedEntries);

public sealed record StatementLineResponse(
    int LineNumber,
    DateOnly Date,
    string Description,
    decimal Amount,
    Guid? MatchedJournalEntryId,
    int? MatchedJournalLineNumber,
    string? MatchedJournalNumber);

public sealed record UnclearedEntryResponse(Guid JournalEntryId, int JournalLineNumber, string Number, DateOnly Date, string Description, decimal Amount);

internal sealed class GetCashBankAccountsQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetCashBankAccountsQuery, IReadOnlyList<CashBankAccountResponse>>
{
    public async Task<Result<IReadOnlyList<CashBankAccountResponse>>> Handle(GetCashBankAccountsQuery query, CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        const string sql =
            """
            SELECT cb.id AS Id, cb.code AS Code, cb.name AS Name, cb.type AS Type, cb.branch_id AS BranchId, b.code AS BranchCode,
                   cb.account_id AS AccountId, a.code AS AccountCode, a.name AS AccountName, cb.bank_name AS BankName,
                   cb.account_number AS AccountNumber, cb.is_active AS IsActive,
                   COALESCE((SELECT SUM(l.debit - l.credit) FROM finance.journal_lines l
                             JOIN finance.journal_entries j ON j.id = l.journal_entry_id
                             WHERE l.account_id = cb.account_id AND j.status IN ('Posted', 'Reversed')), 0) AS Balance
            FROM finance.cash_bank_accounts cb
            JOIN master.branches b ON b.id = cb.branch_id
            JOIN finance.accounts a ON a.id = cb.account_id
            WHERE (@AllBranches OR cb.branch_id = ANY(@BranchIds))
              AND (@BranchId::uuid IS NULL OR cb.branch_id = @BranchId)
              AND (@Type::text IS NULL OR cb.type = @Type)
              AND (@IncludeInactive OR cb.is_active)
            ORDER BY b.code, cb.code;
            """;

        IEnumerable<CashBankAccountResponse> rows = await connection.QueryAsync<CashBankAccountResponse>(new CommandDefinition(
            sql,
            new { scope.AllBranches, scope.BranchIds, query.BranchId, Type = query.Type?.ToString(), query.IncludeInactive },
            cancellationToken: cancellationToken));

        return rows.ToList();
    }
}

internal sealed class GetCashTransactionsQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetCashTransactionsQuery, PagedList<CashTransactionResponse>>
{
    public async Task<Result<PagedList<CashTransactionResponse>>> Handle(GetCashTransactionsQuery query, CancellationToken cancellationToken)
    {
        const string filter =
            """
            WHERE (@AllBranches OR t.branch_id = ANY(@BranchIds))
              AND (@BranchId::uuid IS NULL OR t.branch_id = @BranchId)
              AND (@CashBankAccountId::uuid IS NULL OR t.cash_bank_account_id = @CashBankAccountId)
              AND (@Direction::text IS NULL OR t.direction = @Direction)
              AND (@Status::text IS NULL OR t.status = @Status)
              AND (@From::date IS NULL OR t.date >= @From)
              AND (@To::date IS NULL OR t.date <= @To)
              AND (@Search IS NULL OR t.number ILIKE @Search OR t.description ILIKE @Search)
            """;

        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        return await connection.QueryPagedAsync<CashTransactionResponse>(
            $"SELECT COUNT(*) FROM finance.cash_transactions t {filter}",
            $"{CashTransactionResponse.Select} {filter} ORDER BY t.date DESC, t.number DESC NULLS FIRST LIMIT @PageSize OFFSET @Offset",
            query.Paging,
            new
            {
                scope.AllBranches,
                scope.BranchIds,
                query.BranchId,
                query.CashBankAccountId,
                Direction = query.Direction?.ToString(),
                Status = query.Status?.ToString(),
                query.From,
                query.To
            },
            cancellationToken);
    }
}

internal sealed class GetCashTransactionByIdQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetCashTransactionByIdQuery, CashTransactionResponse>
{
    public async Task<Result<CashTransactionResponse>> Handle(GetCashTransactionByIdQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql =
            $"""
            {CashTransactionResponse.Select}
            WHERE t.id = @CashTransactionId;

            SELECT l.line_number AS LineNumber, l.account_id AS AccountId, a.code AS AccountCode, a.name AS AccountName,
                   l.cost_center_id AS CostCenterId, c.code AS CostCenterCode, l.description AS Description, l.amount AS Amount
            FROM finance.cash_transaction_lines l
            JOIN finance.accounts a ON a.id = l.account_id
            LEFT JOIN finance.cost_centers c ON c.id = l.cost_center_id
            WHERE l.cash_transaction_id = @CashTransactionId
            ORDER BY l.line_number;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { query.CashTransactionId }, cancellationToken: cancellationToken));

        CashTransactionResponse? transaction = await multi.ReadSingleOrDefaultAsync<CashTransactionResponse>();
        if (transaction is null)
        {
            return Result.Failure<CashTransactionResponse>(CashTransactionErrors.NotFound(query.CashTransactionId));
        }

        Result access = await branchAccess.EnsureAccessAsync(transaction.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<CashTransactionResponse>(access.Error);
        }

        return transaction with { Lines = [.. await multi.ReadAsync<CashTransactionLineResponse>()] };
    }
}

internal sealed class GetBankTransfersQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetBankTransfersQuery, PagedList<BankTransferResponse>>
{
    public async Task<Result<PagedList<BankTransferResponse>>> Handle(GetBankTransfersQuery query, CancellationToken cancellationToken)
    {
        const string filter =
            """
            WHERE (@AllBranches OR t.branch_id = ANY(@BranchIds))
              AND (@BranchId::uuid IS NULL OR t.branch_id = @BranchId)
              AND (@CashBankAccountId::uuid IS NULL OR t.from_cash_bank_account_id = @CashBankAccountId OR t.to_cash_bank_account_id = @CashBankAccountId)
              AND (@From::date IS NULL OR t.date >= @From)
              AND (@To::date IS NULL OR t.date <= @To)
              AND (@Search IS NULL OR t.number ILIKE @Search)
            """;

        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        return await connection.QueryPagedAsync<BankTransferResponse>(
            $"SELECT COUNT(*) FROM finance.bank_transfers t {filter}",
            $"""
             SELECT t.id AS Id, t.number AS Number, t.branch_id AS BranchId, b.code AS BranchCode, t.date AS Date,
                    t.from_cash_bank_account_id AS FromCashBankAccountId, f.code AS FromCode, f.name AS FromName,
                    t.to_cash_bank_account_id AS ToCashBankAccountId, d.code AS ToCode, d.name AS ToName,
                    t.amount AS Amount, t.reference AS Reference, t.notes AS Notes
             FROM finance.bank_transfers t
             JOIN master.branches b ON b.id = t.branch_id
             JOIN finance.cash_bank_accounts f ON f.id = t.from_cash_bank_account_id
             JOIN finance.cash_bank_accounts d ON d.id = t.to_cash_bank_account_id
             {filter}
             ORDER BY t.date DESC, t.number DESC LIMIT @PageSize OFFSET @Offset
             """,
            query.Paging,
            new { scope.AllBranches, scope.BranchIds, query.BranchId, query.CashBankAccountId, query.From, query.To },
            cancellationToken);
    }
}

internal sealed class GetCashBankLedgerQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetCashBankLedgerQuery, CashBankLedgerResponse>
{
    public async Task<Result<CashBankLedgerResponse>> Handle(GetCashBankLedgerQuery query, CancellationToken cancellationToken)
    {
        if (query.To < query.From)
        {
            return Result.Failure<CashBankLedgerResponse>(ReportErrors.InvalidDateRange);
        }

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        const string sql =
            """
            SELECT cb.id AS Id, cb.code AS Code, cb.name AS Name, cb.branch_id AS BranchId, cb.account_id AS AccountId, a.code AS AccountCode
            FROM finance.cash_bank_accounts cb JOIN finance.accounts a ON a.id = cb.account_id
            WHERE cb.id = @CashBankAccountId;

            SELECT COALESCE(SUM(l.debit - l.credit), 0)
            FROM finance.journal_lines l
            JOIN finance.journal_entries j ON j.id = l.journal_entry_id
            JOIN finance.cash_bank_accounts cb ON cb.account_id = l.account_id
            WHERE cb.id = @CashBankAccountId AND j.status IN ('Posted', 'Reversed') AND j.date < @From;

            SELECT j.id AS JournalId, l.line_number AS JournalLineNumber, j.number AS Number, j.date AS Date,
                   COALESCE(l.description, j.description) AS Description, j.source_type AS SourceType,
                   l.debit AS In, l.credit AS Out
            FROM finance.journal_lines l
            JOIN finance.journal_entries j ON j.id = l.journal_entry_id
            JOIN finance.cash_bank_accounts cb ON cb.account_id = l.account_id
            WHERE cb.id = @CashBankAccountId AND j.status IN ('Posted', 'Reversed') AND j.date BETWEEN @From AND @To
            ORDER BY j.date, j.number, l.line_number;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(new CommandDefinition(
            sql, new { query.CashBankAccountId, query.From, query.To }, cancellationToken: cancellationToken));

        AccountRow? account = await multi.ReadSingleOrDefaultAsync<AccountRow>();
        if (account is null)
        {
            return Result.Failure<CashBankLedgerResponse>(CashBankErrors.NotFound(query.CashBankAccountId));
        }

        Result access = await branchAccess.EnsureAccessAsync(account.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<CashBankLedgerResponse>(access.Error);
        }

        decimal opening = await multi.ReadSingleAsync<decimal>();
        decimal balance = opening;

        var lines = new List<CashBankLedgerLine>();
        foreach (MovementRow row in await multi.ReadAsync<MovementRow>())
        {
            balance += row.In - row.Out;
            lines.Add(new CashBankLedgerLine(
                row.JournalId, row.JournalLineNumber, row.Number, row.Date, row.Description, row.SourceType, row.In, row.Out, balance));
        }

        return new CashBankLedgerResponse(
            account.Id, account.Code, account.Name, account.AccountCode, query.From, query.To,
            opening, lines.Sum(l => l.In), lines.Sum(l => l.Out), balance, lines);
    }

    private sealed record AccountRow(Guid Id, string Code, string Name, Guid BranchId, Guid AccountId, string AccountCode);

    private sealed record MovementRow(
        Guid JournalId,
        int JournalLineNumber,
        string Number,
        DateOnly Date,
        string Description,
        string? SourceType,
        decimal In,
        decimal Out);
}

internal sealed class GetBankReconciliationsQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetBankReconciliationsQuery, IReadOnlyList<BankReconciliationSummary>>
{
    public async Task<Result<IReadOnlyList<BankReconciliationSummary>>> Handle(GetBankReconciliationsQuery query, CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        const string sql =
            """
            SELECT r.id AS Id, r.cash_bank_account_id AS CashBankAccountId, cb.code AS CashBankCode, r.statement_date AS StatementDate,
                   r.statement_balance AS StatementBalance, r.status AS Status, r.completed_at_utc AS CompletedAtUtc
            FROM finance.bank_reconciliations r
            JOIN finance.cash_bank_accounts cb ON cb.id = r.cash_bank_account_id
            WHERE (@AllBranches OR r.branch_id = ANY(@BranchIds))
              AND (@BranchId::uuid IS NULL OR r.branch_id = @BranchId)
              AND (@CashBankAccountId::uuid IS NULL OR r.cash_bank_account_id = @CashBankAccountId)
            ORDER BY cb.code, r.statement_date DESC;
            """;

        IEnumerable<BankReconciliationSummary> rows = await connection.QueryAsync<BankReconciliationSummary>(new CommandDefinition(
            sql, new { scope.AllBranches, scope.BranchIds, query.BranchId, query.CashBankAccountId }, cancellationToken: cancellationToken));

        return rows.ToList();
    }
}

/// <summary>
/// Uses the write model's rules (uncleared = not matched by any reconciliation) so the summary shown is exactly what
/// completing the reconciliation checks.
/// </summary>
internal sealed class GetBankReconciliationByIdQueryHandler(IApplicationDbContext context, IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetBankReconciliationByIdQuery, BankReconciliationResponse>
{
    public async Task<Result<BankReconciliationResponse>> Handle(GetBankReconciliationByIdQuery query, CancellationToken cancellationToken)
    {
        Result<(BankReconciliation Reconciliation, CashBankAccount Account)> loaded =
            await BankReconciliationSupport.LoadAsync(context, branchAccess, query.BankReconciliationId, cancellationToken);

        if (loaded.IsFailure)
        {
            return Result.Failure<BankReconciliationResponse>(loaded.Error);
        }

        (BankReconciliation reconciliation, CashBankAccount account) = loaded.Value;

        List<BookEntry> book = await BankReconciliationSupport.BookEntriesAsync(context, account.AccountId, reconciliation.StatementDate, cancellationToken);
        List<BookEntry> uncleared = await BankReconciliationSupport.UnclearedAsync(context, reconciliation, account, cancellationToken);

        decimal bookBalance = book.Sum(e => e.Amount.Amount);
        decimal unclearedNet = uncleared.Sum(e => e.Amount.Amount);

        Guid[] journalIds = uncleared.Select(e => e.JournalEntryId)
            .Concat(reconciliation.Lines.Where(l => l.IsMatched).Select(l => l.MatchedJournalEntryId!.Value))
            .Distinct()
            .ToArray();

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        var journals = (await connection.QueryAsync<(Guid Id, string Number, string Description)>(
                new CommandDefinition(
                    "SELECT id, number, description FROM finance.journal_entries WHERE id = ANY(@Ids)",
                    new { Ids = journalIds },
                    cancellationToken: cancellationToken)))
            .ToDictionary(j => j.Id, j => (j.Number, j.Description));

        return new BankReconciliationResponse(
            reconciliation.Id,
            account.Id,
            account.Code,
            account.Name,
            reconciliation.StatementDate,
            reconciliation.StatementBalance.Amount,
            reconciliation.Status.ToString(),
            bookBalance,
            unclearedNet,
            reconciliation.StatementBalance.Amount - (bookBalance - unclearedNet),
            reconciliation.Lines.Count(l => !l.IsMatched),
            [
                .. reconciliation.Lines.OrderBy(l => l.LineNumber).Select(l => new StatementLineResponse(
                    l.LineNumber,
                    l.Date,
                    l.Description,
                    l.Amount.Amount,
                    l.MatchedJournalEntryId,
                    l.MatchedJournalLineNumber,
                    l.MatchedJournalEntryId is null ? null : journals.GetValueOrDefault(l.MatchedJournalEntryId.Value).Number))
            ],
            [
                .. uncleared.OrderBy(e => e.Date).Select(e => new UnclearedEntryResponse(
                    e.JournalEntryId,
                    e.JournalLineNumber,
                    journals.GetValueOrDefault(e.JournalEntryId).Number,
                    e.Date,
                    journals.GetValueOrDefault(e.JournalEntryId).Description,
                    e.Amount.Amount))
            ]);
    }
}
