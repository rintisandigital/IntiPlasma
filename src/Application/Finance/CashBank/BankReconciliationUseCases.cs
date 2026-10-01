using System.Globalization;
using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Finance.CashBank;
using Domain.Finance.Journals;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.CashBank;

public sealed record StartBankReconciliationCommand(Guid CashBankAccountId, DateOnly StatementDate, decimal StatementBalance) : ICommand<Guid>;

public sealed record UpdateStatementBalanceCommand(Guid BankReconciliationId, decimal StatementBalance) : ICommand;

/// <param name="Amount">Positive for money in (kredit on the statement), negative for money out (debet).</param>
public sealed record StatementLineRequest(DateOnly Date, string Description, decimal Amount);

public sealed record AddStatementLinesCommand(Guid BankReconciliationId, IReadOnlyList<StatementLineRequest> Lines) : ICommand;

/// <summary>
/// Imports bank statement lines from CSV: <c>date,description,debit,credit</c> per line (bank's point of view:
/// debit = money out, credit = money in), date as yyyy-MM-dd or dd/MM/yyyy, ',' or ';' separated, header optional.
/// Returns the number of lines imported.
/// </summary>
public sealed record ImportStatementLinesCommand(Guid BankReconciliationId, string Csv) : ICommand<int>;

public sealed record RemoveStatementLineCommand(Guid BankReconciliationId, int LineNumber) : ICommand;

/// <summary>
/// Matches a statement line with a ledger line (journal entry + line number) of the bank's account.
/// </summary>
public sealed record MatchStatementLineCommand(Guid BankReconciliationId, int LineNumber, Guid JournalEntryId, int JournalLineNumber) : ICommand;

public sealed record UnmatchStatementLineCommand(Guid BankReconciliationId, int LineNumber) : ICommand;

/// <summary>
/// Matches every unmatched statement line with an uncleared ledger line of the same amount dated within
/// <see cref="BankReconciliationSupport.AutoMatchDays"/> days (closest date first). Returns the number of lines matched.
/// </summary>
public sealed record AutoMatchStatementLinesCommand(Guid BankReconciliationId) : ICommand<int>;

public sealed record CompleteBankReconciliationCommand(Guid BankReconciliationId) : ICommand;

internal sealed class AddStatementLinesCommandValidator : AbstractValidator<AddStatementLinesCommand>
{
    public AddStatementLinesCommandValidator()
    {
        RuleFor(c => c.BankReconciliationId).NotEmpty();
        RuleFor(c => c.Lines).NotEmpty();
        RuleForEach(c => c.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Description).NotEmpty().MaximumLength(250);
            line.RuleFor(l => l.Amount).NotEqual(0);
        });
    }
}

internal sealed class ImportStatementLinesCommandValidator : AbstractValidator<ImportStatementLinesCommand>
{
    public ImportStatementLinesCommandValidator()
    {
        RuleFor(c => c.BankReconciliationId).NotEmpty();
        RuleFor(c => c.Csv).NotEmpty().MaximumLength(2_000_000);
    }
}

internal static class BankReconciliationSupport
{
    public const int AutoMatchDays = 3;

    public static async Task<Result<(BankReconciliation Reconciliation, CashBankAccount Account)>> LoadAsync(
        IApplicationDbContext context,
        IBranchAccess branchAccess,
        Guid bankReconciliationId,
        CancellationToken cancellationToken)
    {
        BankReconciliation? reconciliation = await context.BankReconciliations
            .Include(r => r.Lines)
            .SingleOrDefaultAsync(r => r.Id == bankReconciliationId, cancellationToken);

        if (reconciliation is null)
        {
            return Result.Failure<(BankReconciliation, CashBankAccount)>(BankReconciliationErrors.NotFound(bankReconciliationId));
        }

        Result access = await branchAccess.EnsureAccessAsync(reconciliation.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<(BankReconciliation, CashBankAccount)>(access.Error);
        }

        CashBankAccount account = await context.CashBankAccounts.AsNoTracking()
            .SingleAsync(a => a.Id == reconciliation.CashBankAccountId, cancellationToken);

        return (reconciliation, account);
    }

    /// <summary>
    /// Posted ledger lines of a COA account up to a date (amount = debit − credit).
    /// </summary>
    public static async Task<List<BookEntry>> BookEntriesAsync(
        IApplicationDbContext context,
        Guid accountId,
        DateOnly upTo,
        CancellationToken cancellationToken)
    {
        var rows = await context.JournalEntries.AsNoTracking()
            .Where(j => (j.Status == JournalStatus.Posted || j.Status == JournalStatus.Reversed) && j.Date <= upTo)
            .SelectMany(j => j.Lines, (j, l) => new { JournalEntryId = j.Id, l.LineNumber, j.Date, l.AccountId, Debit = l.Debit.Amount, Credit = l.Credit.Amount })
            .Where(x => x.AccountId == accountId)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new BookEntry(r.JournalEntryId, r.LineNumber, r.Date, new Money(r.Debit - r.Credit)))];
    }

    /// <summary>
    /// Ledger lines already matched by any bank reconciliation, optionally except one.
    /// </summary>
    public static async Task<HashSet<(Guid, int)>> ClearedAsync(
        IApplicationDbContext context,
        Guid cashBankAccountId,
        Guid? exceptReconciliationId,
        CancellationToken cancellationToken)
    {
        var matched = await context.BankReconciliations.AsNoTracking()
            .Where(r => r.CashBankAccountId == cashBankAccountId && r.Id != exceptReconciliationId)
            .SelectMany(r => r.Lines)
            .Where(l => l.MatchedJournalEntryId != null)
            .Select(l => new { EntryId = l.MatchedJournalEntryId!.Value, LineNumber = l.MatchedJournalLineNumber!.Value })
            .ToListAsync(cancellationToken);

        return [.. matched.Select(m => (m.EntryId, m.LineNumber))];
    }

    /// <summary>
    /// Ledger lines up to the statement date that no reconciliation has matched (this one's matches count as cleared).
    /// </summary>
    public static async Task<List<BookEntry>> UnclearedAsync(
        IApplicationDbContext context,
        BankReconciliation reconciliation,
        CashBankAccount account,
        CancellationToken cancellationToken)
    {
        List<BookEntry> entries = await BookEntriesAsync(context, account.AccountId, reconciliation.StatementDate, cancellationToken);
        HashSet<(Guid, int)> cleared = await ClearedAsync(context, account.Id, reconciliation.Id, cancellationToken);

        foreach (BankStatementLine line in reconciliation.Lines.Where(l => l.IsMatched))
        {
            cleared.Add((line.MatchedJournalEntryId!.Value, line.MatchedJournalLineNumber!.Value));
        }

        return [.. entries.Where(e => !cleared.Contains((e.JournalEntryId, e.JournalLineNumber)))];
    }

    public static Result<List<(DateOnly Date, string Description, Money Amount)>> ParseCsv(string csv)
    {
        var lines = new List<(DateOnly, string, Money)>();
        string[] rows = csv.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (int i = 0; i < rows.Length; i++)
        {
            List<string> fields = SplitCsvRow(rows[i]);
            if (fields.Count < 4)
            {
                return Result.Failure<List<(DateOnly, string, Money)>>(ImportError(i + 1));
            }

            if (!TryParseDate(fields[0], out DateOnly date))
            {
                // A first row that is not a date is the header.
                if (i == 0)
                {
                    continue;
                }

                return Result.Failure<List<(DateOnly, string, Money)>>(ImportError(i + 1));
            }

            if (!TryParseAmount(fields[2], out decimal debit) || !TryParseAmount(fields[3], out decimal credit))
            {
                return Result.Failure<List<(DateOnly, string, Money)>>(ImportError(i + 1));
            }

            lines.Add((date, fields[1], new Money(credit - debit)));
        }

        return lines;
    }

    private static Error ImportError(int row) => Error.Problem(
        "BankReconciliations.InvalidCsv",
        $"Row {row} of the CSV is not 'date,description,debit,credit'");

    private static bool TryParseDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(value, ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static bool TryParseAmount(string value, out decimal amount)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            amount = 0m;
            return true;
        }

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }

    /// <summary>
    /// Splits a CSV row on ',' or ';' (whichever the row uses), honoring double-quoted fields.
    /// </summary>
    private static List<string> SplitCsvRow(string row)
    {
        char separator = row.Contains(';', StringComparison.Ordinal) ? ';' : ',';
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        bool quoted = false;

        foreach (char c in row)
        {
            if (c == '"')
            {
                quoted = !quoted;
            }
            else if (c == separator && !quoted)
            {
                fields.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString().Trim());

        return fields;
    }
}

internal sealed class StartBankReconciliationCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<StartBankReconciliationCommand, Guid>
{
    public async Task<Result<Guid>> Handle(StartBankReconciliationCommand command, CancellationToken cancellationToken)
    {
        CashBankAccount? account = await context.CashBankAccounts.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == command.CashBankAccountId, cancellationToken);

        if (account is null)
        {
            return Result.Failure<Guid>(CashBankErrors.NotFound(command.CashBankAccountId));
        }

        Result access = await branchAccess.EnsureAccessAsync(account.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<Guid>(access.Error);
        }

        if (await context.BankReconciliations.AnyAsync(
                r => r.CashBankAccountId == account.Id && r.Status == BankReconciliationStatus.InProgress, cancellationToken))
        {
            return Result.Failure<Guid>(BankReconciliationErrors.InProgressExists);
        }

        DateOnly? lastCompleted = await context.BankReconciliations
            .Where(r => r.CashBankAccountId == account.Id && r.Status == BankReconciliationStatus.Completed)
            .MaxAsync(r => (DateOnly?)r.StatementDate, cancellationToken);

        Result<BankReconciliation> reconciliation = BankReconciliation.Start(
            account, command.StatementDate, new Money(command.StatementBalance), lastCompleted);

        if (reconciliation.IsFailure)
        {
            return Result.Failure<Guid>(reconciliation.Error);
        }

        context.BankReconciliations.Add(reconciliation.Value);

        await context.SaveChangesAsync(cancellationToken);

        return reconciliation.Value.Id;
    }
}

internal sealed class UpdateStatementBalanceCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<UpdateStatementBalanceCommand>
{
    public async Task<Result> Handle(UpdateStatementBalanceCommand command, CancellationToken cancellationToken)
    {
        Result<(BankReconciliation Reconciliation, CashBankAccount Account)> loaded = await BankReconciliationSupport.LoadAsync(context, branchAccess, command.BankReconciliationId, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded;
        }

        Result result = loaded.Value.Reconciliation.UpdateStatementBalance(new Money(command.StatementBalance));
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class AddStatementLinesCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<AddStatementLinesCommand>
{
    public async Task<Result> Handle(AddStatementLinesCommand command, CancellationToken cancellationToken)
    {
        Result<(BankReconciliation Reconciliation, CashBankAccount Account)> loaded = await BankReconciliationSupport.LoadAsync(context, branchAccess, command.BankReconciliationId, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded;
        }

        Result result = loaded.Value.Reconciliation.AddLines(
            [.. command.Lines.Select(l => (l.Date, l.Description, new Money(l.Amount)))]);

        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class ImportStatementLinesCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<ImportStatementLinesCommand, int>
{
    public async Task<Result<int>> Handle(ImportStatementLinesCommand command, CancellationToken cancellationToken)
    {
        Result<(BankReconciliation Reconciliation, CashBankAccount Account)> loaded = await BankReconciliationSupport.LoadAsync(context, branchAccess, command.BankReconciliationId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<int>(loaded.Error);
        }

        Result<List<(DateOnly Date, string Description, Money Amount)>> lines = BankReconciliationSupport.ParseCsv(command.Csv);
        if (lines.IsFailure)
        {
            return Result.Failure<int>(lines.Error);
        }

        Result result = loaded.Value.Reconciliation.AddLines(lines.Value);
        if (result.IsFailure)
        {
            return Result.Failure<int>(result.Error);
        }

        await context.SaveChangesAsync(cancellationToken);

        return lines.Value.Count;
    }
}

internal sealed class RemoveStatementLineCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<RemoveStatementLineCommand>
{
    public async Task<Result> Handle(RemoveStatementLineCommand command, CancellationToken cancellationToken)
    {
        Result<(BankReconciliation Reconciliation, CashBankAccount Account)> loaded = await BankReconciliationSupport.LoadAsync(context, branchAccess, command.BankReconciliationId, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded;
        }

        Result result = loaded.Value.Reconciliation.RemoveLine(command.LineNumber);
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class MatchStatementLineCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<MatchStatementLineCommand>
{
    public async Task<Result> Handle(MatchStatementLineCommand command, CancellationToken cancellationToken)
    {
        Result<(BankReconciliation Reconciliation, CashBankAccount Account)> loaded = await BankReconciliationSupport.LoadAsync(context, branchAccess, command.BankReconciliationId, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded;
        }

        (BankReconciliation reconciliation, CashBankAccount account) = loaded.Value;

        List<BookEntry> entries = await BankReconciliationSupport.BookEntriesAsync(
            context, account.AccountId, DateOnly.MaxValue, cancellationToken);

        BookEntry? entry = entries.Find(e => e.JournalEntryId == command.JournalEntryId && e.JournalLineNumber == command.JournalLineNumber);
        if (entry is null)
        {
            return Result.Failure(BankReconciliationErrors.EntryNotFound);
        }

        HashSet<(Guid, int)> cleared = await BankReconciliationSupport.ClearedAsync(context, account.Id, reconciliation.Id, cancellationToken);
        if (cleared.Contains((entry.JournalEntryId, entry.JournalLineNumber)))
        {
            return Result.Failure(BankReconciliationErrors.EntryAlreadyCleared);
        }

        Result result = reconciliation.Match(command.LineNumber, entry);
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class UnmatchStatementLineCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<UnmatchStatementLineCommand>
{
    public async Task<Result> Handle(UnmatchStatementLineCommand command, CancellationToken cancellationToken)
    {
        Result<(BankReconciliation Reconciliation, CashBankAccount Account)> loaded = await BankReconciliationSupport.LoadAsync(context, branchAccess, command.BankReconciliationId, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded;
        }

        Result result = loaded.Value.Reconciliation.Unmatch(command.LineNumber);
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class AutoMatchStatementLinesCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<AutoMatchStatementLinesCommand, int>
{
    public async Task<Result<int>> Handle(AutoMatchStatementLinesCommand command, CancellationToken cancellationToken)
    {
        Result<(BankReconciliation Reconciliation, CashBankAccount Account)> loaded = await BankReconciliationSupport.LoadAsync(context, branchAccess, command.BankReconciliationId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<int>(loaded.Error);
        }

        (BankReconciliation reconciliation, CashBankAccount account) = loaded.Value;

        List<BookEntry> candidates = await BankReconciliationSupport.UnclearedAsync(context, reconciliation, account, cancellationToken);
        int matched = 0;

        foreach (BankStatementLine line in reconciliation.Lines.Where(l => !l.IsMatched).OrderBy(l => l.Date).ThenBy(l => l.LineNumber))
        {
            BookEntry? entry = candidates
                .Where(e => e.Amount == line.Amount && Math.Abs(e.Date.DayNumber - line.Date.DayNumber) <= BankReconciliationSupport.AutoMatchDays)
                .OrderBy(e => Math.Abs(e.Date.DayNumber - line.Date.DayNumber))
                .FirstOrDefault();

            if (entry is not null && reconciliation.Match(line.LineNumber, entry).IsSuccess)
            {
                candidates.Remove(entry);
                matched++;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        return matched;
    }
}

internal sealed class CompleteBankReconciliationCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<CompleteBankReconciliationCommand>
{
    public async Task<Result> Handle(CompleteBankReconciliationCommand command, CancellationToken cancellationToken)
    {
        Result<(BankReconciliation Reconciliation, CashBankAccount Account)> loaded = await BankReconciliationSupport.LoadAsync(context, branchAccess, command.BankReconciliationId, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded;
        }

        (BankReconciliation reconciliation, CashBankAccount account) = loaded.Value;

        List<BookEntry> book = await BankReconciliationSupport.BookEntriesAsync(context, account.AccountId, reconciliation.StatementDate, cancellationToken);
        List<BookEntry> uncleared = await BankReconciliationSupport.UnclearedAsync(context, reconciliation, account, cancellationToken);

        Money bookBalance = book.Aggregate(Money.Zero, (total, e) => total + e.Amount);
        Money unclearedNet = uncleared.Aggregate(Money.Zero, (total, e) => total + e.Amount);

        Result result = reconciliation.Complete(bookBalance, unclearedNet, userContext.UserId, dateTimeProvider.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
