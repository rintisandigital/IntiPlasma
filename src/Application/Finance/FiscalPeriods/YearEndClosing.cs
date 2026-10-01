using Application.Abstractions.Data;
using Application.Abstractions.Numbering;
using Application.Finance.Journals;
using Domain.Finance.Accounts;
using Domain.Finance.FiscalPeriods;
using Domain.Finance.JournalMappings;
using Domain.Finance.Journals;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.FiscalPeriods;

/// <summary>
/// Penutupan tahun buku. When December is closed, one closing journal per branch brings every revenue and expense
/// account of the year to zero against retained earnings (the credit account of the <c>YearEndClosing</c> mapping).
/// Reopening December reverses those journals.
/// </summary>
internal static class YearEndClosing
{
    public const string ReversalSourceType = AccountingEvents.YearEndClosing + ".Reversal";

    private static readonly JournalStatus[] LedgerStatuses = [JournalStatus.Posted, JournalStatus.Reversed];

    public static async Task<Result<IReadOnlyList<JournalEntry>>> CreateClosingJournalsAsync(
        IApplicationDbContext context,
        IDocumentNumberGenerator numberGenerator,
        FiscalPeriod december,
        Guid? userId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        Guid? retainedEarnings = await context.JournalMappings.AsNoTracking()
            .Where(m => m.EventType == AccountingEvents.YearEndClosing && m.BranchId == null && m.IsActive)
            .SelectMany(m => m.Lines)
            .Where(l => l.Component == "NetIncome")
            .Select(l => (Guid?)l.CreditAccountId)
            .FirstOrDefaultAsync(cancellationToken);

        if (retainedEarnings is null)
        {
            return Result.Failure<IReadOnlyList<JournalEntry>>(
                JournalMappingErrors.NotConfigured(AccountingEvents.YearEndClosing, Guid.Empty));
        }

        var yearStart = new DateOnly(december.Year, 1, 1);
        DateOnly yearEnd = december.EndDate;

        var balances = await context.JournalEntries.AsNoTracking()
            .Where(j => LedgerStatuses.Contains(j.Status) && j.Date >= yearStart && j.Date <= yearEnd &&
                        j.SourceType != AccountingEvents.YearEndClosing && j.SourceType != ReversalSourceType)
            .SelectMany(j => j.Lines, (j, l) => new { j.BranchId, l.AccountId, Debit = l.Debit.Amount, Credit = l.Credit.Amount })
            .Join(
                context.Accounts.Where(a => a.Type == AccountType.Revenue || a.Type == AccountType.Expense),
                l => l.AccountId,
                a => a.Id,
                (l, _) => l)
            .ToListAsync(cancellationToken);

        var journals = new List<JournalEntry>();

        foreach (IGrouping<Guid, (Guid BranchId, Guid AccountId, decimal Balance)> branch in balances
                     .GroupBy(b => (b.BranchId, b.AccountId))
                     .Select(g => (g.Key.BranchId, g.Key.AccountId, Balance: g.Sum(b => b.Debit - b.Credit)))
                     .Where(b => b.Balance != 0m)
                     .GroupBy(b => b.BranchId))
        {
            // Close each account by posting its balance on the opposite side.
            List<JournalLineInput> lines =
            [
                .. branch.Select(b => b.Balance > 0
                    ? JournalLineInput.CreditLine(b.AccountId, new Money(b.Balance))
                    : JournalLineInput.DebitLine(b.AccountId, new Money(-b.Balance)))
            ];

            // Revenue (credit balances) − expenses (debit balances) = net income, credited to retained earnings.
            decimal netIncome = -branch.Sum(b => b.Balance);
            if (netIncome != 0m)
            {
                lines.Add(netIncome > 0
                    ? JournalLineInput.CreditLine(retainedEarnings.Value, new Money(netIncome))
                    : JournalLineInput.DebitLine(retainedEarnings.Value, new Money(-netIncome)));
            }

            Result<JournalEntry> journal = JournalEntry.CreateAutomatic(
                branch.Key, yearEnd, $"Jurnal penutup tahun buku {december.Year}", AccountingEvents.YearEndClosing,
                Guid.CreateVersion7(), lines);

            if (journal.IsFailure)
            {
                return Result.Failure<IReadOnlyList<JournalEntry>>(journal.Error);
            }

            string number = await JournalSupport.NextNumberAsync(
                context, numberGenerator, JournalSource.Automatic, branch.Key, yearEnd, cancellationToken);

            Result posted = journal.Value.Post(number, december, userId, utcNow);
            if (posted.IsFailure)
            {
                return Result.Failure<IReadOnlyList<JournalEntry>>(posted.Error);
            }

            journals.Add(journal.Value);
        }

        return journals;
    }

    /// <summary>
    /// Reverses the posted closing journals of the year (December must already be reopened).
    /// </summary>
    public static async Task<Result<IReadOnlyList<JournalEntry>>> ReverseClosingJournalsAsync(
        IApplicationDbContext context,
        IDocumentNumberGenerator numberGenerator,
        FiscalPeriod december,
        Guid? userId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        List<JournalEntry> closing = await context.JournalEntries
            .Include(j => j.Lines)
            .Where(j => j.SourceType == AccountingEvents.YearEndClosing && j.Status == JournalStatus.Posted && j.Date == december.EndDate)
            .ToListAsync(cancellationToken);

        var reversals = new List<JournalEntry>();

        foreach (JournalEntry journal in closing)
        {
            string number = await JournalSupport.NextNumberAsync(
                context, numberGenerator, JournalSource.Automatic, journal.BranchId, december.EndDate, cancellationToken);

            Result<JournalEntry> reversal = journal.Reverse(
                december.EndDate, "Periode Desember dibuka kembali", number, december, userId, utcNow);

            if (reversal.IsFailure)
            {
                return Result.Failure<IReadOnlyList<JournalEntry>>(reversal.Error);
            }

            reversals.Add(reversal.Value);
        }

        return reversals;
    }
}
