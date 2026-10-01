using System.Globalization;
using SharedKernel;

namespace Domain.Finance.CashBank;

/// <summary>
/// Rekonsiliasi bank of one bank account up to a statement date. Bank statement lines (entered or imported from CSV)
/// are matched one-to-one with ledger lines of the bank's account that were not cleared by an earlier reconciliation.
/// It can be completed when every statement line is matched and the statement balance equals the book balance minus
/// the book movements that have not appeared on the bank statement yet (outstanding deposits and payments).
/// </summary>
public sealed class BankReconciliation : AggregateRoot
{
    private readonly List<BankStatementLine> _lines = [];

    private BankReconciliation(Guid id)
        : base(id)
    {
    }

    private BankReconciliation()
    {
    }

    public Guid BranchId { get; private set; }
    public Guid CashBankAccountId { get; private set; }
    public DateOnly StatementDate { get; private set; }

    /// <summary>
    /// Closing balance on the bank statement at the statement date.
    /// </summary>
    public Money StatementBalance { get; private set; }

    public BankReconciliationStatus Status { get; private set; }
    public Guid? CompletedBy { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public IReadOnlyCollection<BankStatementLine> Lines => [.. _lines];

    /// <param name="lastCompletedStatementDate">Statement date of the account's last completed reconciliation.</param>
    public static Result<BankReconciliation> Start(
        CashBankAccount account,
        DateOnly statementDate,
        Money statementBalance,
        DateOnly? lastCompletedStatementDate)
    {
        if (account.Type != CashBankAccountType.Bank)
        {
            return Result.Failure<BankReconciliation>(BankReconciliationErrors.NotABankAccount);
        }

        if (lastCompletedStatementDate is not null && statementDate <= lastCompletedStatementDate)
        {
            return Result.Failure<BankReconciliation>(BankReconciliationErrors.NotAfterLastReconciliation(lastCompletedStatementDate.Value));
        }

        return new BankReconciliation(Guid.CreateVersion7())
        {
            BranchId = account.BranchId,
            CashBankAccountId = account.Id,
            StatementDate = statementDate,
            StatementBalance = statementBalance,
            Status = BankReconciliationStatus.InProgress
        };
    }

    public Result UpdateStatementBalance(Money statementBalance)
    {
        Result editable = EnsureInProgress();
        if (editable.IsFailure)
        {
            return editable;
        }

        StatementBalance = statementBalance;

        return Result.Success();
    }

    /// <summary>
    /// Adds bank statement lines; amount is positive for money in (kredit on the statement), negative for money out.
    /// </summary>
    public Result AddLines(IReadOnlyList<(DateOnly Date, string Description, Money Amount)> lines)
    {
        Result editable = EnsureInProgress();
        if (editable.IsFailure)
        {
            return editable;
        }

        if (lines.Count == 0 || lines.Any(l => l.Amount.IsZero || l.Date > StatementDate))
        {
            return Result.Failure(BankReconciliationErrors.InvalidStatementLines(StatementDate));
        }

        int next = _lines.Count == 0 ? 1 : _lines.Max(l => l.LineNumber) + 1;

        foreach ((DateOnly date, string description, Money amount) in lines)
        {
            _lines.Add(new BankStatementLine(Id, next++, date, description.Trim(), amount));
        }

        return Result.Success();
    }

    public Result RemoveLine(int lineNumber)
    {
        Result editable = EnsureInProgress();
        if (editable.IsFailure)
        {
            return editable;
        }

        BankStatementLine? line = _lines.Find(l => l.LineNumber == lineNumber);
        if (line is null)
        {
            return Result.Failure(BankReconciliationErrors.LineNotFound(lineNumber));
        }

        _lines.Remove(line);

        return Result.Success();
    }

    /// <summary>
    /// Matches a statement line with an uncleared ledger line of the bank account (same signed amount).
    /// </summary>
    public Result Match(int lineNumber, BookEntry entry)
    {
        Result editable = EnsureInProgress();
        if (editable.IsFailure)
        {
            return editable;
        }

        BankStatementLine? line = _lines.Find(l => l.LineNumber == lineNumber);
        if (line is null)
        {
            return Result.Failure(BankReconciliationErrors.LineNotFound(lineNumber));
        }

        if (line.IsMatched)
        {
            return Result.Failure(BankReconciliationErrors.AlreadyMatched(lineNumber));
        }

        if (entry.Amount != line.Amount)
        {
            return Result.Failure(BankReconciliationErrors.AmountMismatch(line.Amount, entry.Amount));
        }

        if (entry.Date > StatementDate)
        {
            return Result.Failure(BankReconciliationErrors.EntryAfterStatementDate);
        }

        if (_lines.Exists(l => l.MatchedJournalEntryId == entry.JournalEntryId && l.MatchedJournalLineNumber == entry.JournalLineNumber))
        {
            return Result.Failure(BankReconciliationErrors.EntryAlreadyCleared);
        }

        line.MatchWith(entry);

        return Result.Success();
    }

    public Result Unmatch(int lineNumber)
    {
        Result editable = EnsureInProgress();
        if (editable.IsFailure)
        {
            return editable;
        }

        BankStatementLine? line = _lines.Find(l => l.LineNumber == lineNumber);
        if (line is null)
        {
            return Result.Failure(BankReconciliationErrors.LineNotFound(lineNumber));
        }

        line.ClearMatch();

        return Result.Success();
    }

    /// <param name="bookBalance">Balance of the bank's ledger account at the statement date.</param>
    /// <param name="unclearedBookNet">Net of the ledger lines up to the statement date that no reconciliation matched.</param>
    public Result Complete(Money bookBalance, Money unclearedBookNet, Guid? userId, DateTime utcNow)
    {
        Result editable = EnsureInProgress();
        if (editable.IsFailure)
        {
            return editable;
        }

        int unmatched = _lines.Count(l => !l.IsMatched);
        if (unmatched > 0)
        {
            return Result.Failure(BankReconciliationErrors.UnmatchedLines(unmatched));
        }

        Money difference = StatementBalance - (bookBalance - unclearedBookNet);
        if (!difference.IsZero)
        {
            return Result.Failure(BankReconciliationErrors.NotBalanced(difference));
        }

        Status = BankReconciliationStatus.Completed;
        CompletedBy = userId;
        CompletedAtUtc = utcNow;

        return Result.Success();
    }

    private Result EnsureInProgress() =>
        Status == BankReconciliationStatus.InProgress
            ? Result.Success()
            : Result.Failure(BankReconciliationErrors.AlreadyCompleted);
}

public sealed class BankStatementLine
{
    internal BankStatementLine(Guid bankReconciliationId, int lineNumber, DateOnly date, string description, Money amount)
    {
        BankReconciliationId = bankReconciliationId;
        LineNumber = lineNumber;
        Date = date;
        Description = description;
        Amount = amount with { };
    }

    private BankStatementLine()
    {
    }

    public Guid BankReconciliationId { get; private set; }
    public int LineNumber { get; private set; }
    public DateOnly Date { get; private set; }
    public string Description { get; private set; }

    /// <summary>
    /// Positive: money in (kredit on the statement); negative: money out (debet).
    /// </summary>
    public Money Amount { get; private set; }

    /// <summary>
    /// The ledger line (journal entry + line number) this statement line was matched with.
    /// </summary>
    public Guid? MatchedJournalEntryId { get; private set; }

    public int? MatchedJournalLineNumber { get; private set; }

    public bool IsMatched => MatchedJournalEntryId is not null;

    internal void MatchWith(BookEntry entry)
    {
        MatchedJournalEntryId = entry.JournalEntryId;
        MatchedJournalLineNumber = entry.JournalLineNumber;
    }

    internal void ClearMatch()
    {
        MatchedJournalEntryId = null;
        MatchedJournalLineNumber = null;
    }
}

/// <summary>
/// A posted ledger line on the bank's account; amount = debit − credit (positive: money in).
/// </summary>
public sealed record BookEntry(Guid JournalEntryId, int JournalLineNumber, DateOnly Date, Money Amount);

public enum BankReconciliationStatus
{
    InProgress = 1,
    Completed = 2
}

public static class BankReconciliationErrors
{
    public static Error NotFound(Guid bankReconciliationId) => Error.NotFound(
        "BankReconciliations.NotFound",
        $"The bank reconciliation with the Id = '{bankReconciliationId}' was not found");

    public static Error NotAfterLastReconciliation(DateOnly lastDate) => Error.Problem(
        "BankReconciliations.NotAfterLastReconciliation",
        $"The statement date must be after the last completed reconciliation ({lastDate:yyyy-MM-dd})");

    public static Error InvalidStatementLines(DateOnly statementDate) => Error.Problem(
        "BankReconciliations.InvalidStatementLines",
        $"Statement lines need a non-zero amount and a date on or before the statement date {statementDate:yyyy-MM-dd}");

    public static Error LineNotFound(int lineNumber) => Error.Problem(
        "BankReconciliations.LineNotFound",
        $"The reconciliation has no statement line {lineNumber}");

    public static Error AlreadyMatched(int lineNumber) => Error.Problem(
        "BankReconciliations.AlreadyMatched",
        $"Statement line {lineNumber} is already matched");

    public static Error AmountMismatch(Money statementAmount, Money bookAmount) => Error.Problem(
        "BankReconciliations.AmountMismatch",
        string.Create(CultureInfo.InvariantCulture, $"The statement amount {statementAmount} does not equal the ledger amount {bookAmount}"));

    public static Error UnmatchedLines(int count) => Error.Problem(
        "BankReconciliations.UnmatchedLines",
        $"{count} statement line(s) are not matched; record the missing transactions (e.g. bank charges) and match them first");

    public static Error NotBalanced(Money difference) => Error.Problem(
        "BankReconciliations.NotBalanced",
        string.Create(CultureInfo.InvariantCulture, $"The statement balance differs {difference} from the reconciled book balance"));

    public static readonly Error NotABankAccount = Error.Problem(
        "BankReconciliations.NotABankAccount",
        "Only bank accounts can be reconciled");

    public static readonly Error InProgressExists = Error.Conflict(
        "BankReconciliations.InProgressExists",
        "The bank account already has a reconciliation in progress");

    public static readonly Error AlreadyCompleted = Error.Problem(
        "BankReconciliations.AlreadyCompleted",
        "The reconciliation is completed and can no longer be changed");

    public static readonly Error EntryNotFound = Error.Problem(
        "BankReconciliations.EntryNotFound",
        "The ledger line was not found on the bank's account");

    public static readonly Error EntryAfterStatementDate = Error.Problem(
        "BankReconciliations.EntryAfterStatementDate",
        "The ledger line is dated after the statement date");

    public static readonly Error EntryAlreadyCleared = Error.Conflict(
        "BankReconciliations.EntryAlreadyCleared",
        "The ledger line is already matched with a bank statement line");
}
