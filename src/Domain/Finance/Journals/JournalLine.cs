using SharedKernel;

namespace Domain.Finance.Journals;

public sealed class JournalLine
{
    internal JournalLine(
        Guid journalEntryId,
        int lineNumber,
        Guid accountId,
        Guid? costCenterId,
        string? description,
        Money debit,
        Money credit)
    {
        JournalEntryId = journalEntryId;
        LineNumber = lineNumber;
        AccountId = accountId;
        CostCenterId = costCenterId;
        Description = description;
        Debit = debit;
        Credit = credit;
    }

    private JournalLine()
    {
    }

    public Guid JournalEntryId { get; private set; }
    public int LineNumber { get; private set; }
    public Guid AccountId { get; private set; }
    public Guid? CostCenterId { get; private set; }
    public string? Description { get; private set; }
    public Money Debit { get; private set; }
    public Money Credit { get; private set; }
}

/// <summary>
/// A journal line as requested by the caller. Exactly one of <see cref="Debit"/> and <see cref="Credit"/> is positive.
/// </summary>
public sealed record JournalLineInput(Guid AccountId, Guid? CostCenterId, string? Description, Money Debit, Money Credit)
{
    public static JournalLineInput DebitLine(Guid accountId, Money amount, Guid? costCenterId = null, string? description = null) =>
        new(accountId, costCenterId, description, amount, Money.Zero);

    public static JournalLineInput CreditLine(Guid accountId, Money amount, Guid? costCenterId = null, string? description = null) =>
        new(accountId, costCenterId, description, Money.Zero, amount);
}
