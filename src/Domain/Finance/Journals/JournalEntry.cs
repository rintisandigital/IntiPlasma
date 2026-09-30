using Domain.Finance.FiscalPeriods;
using SharedKernel;

namespace Domain.Finance.Journals;

/// <summary>
/// Double-entry journal. Manual journals follow Draft → Approved → Posted (maker-checker: the approver cannot be
/// the creator). Automatic journals, produced by the auto journal engine from operational transactions, are posted
/// immediately. A posted journal is immutable; corrections are made by reversing it.
/// The document number is assigned when the journal is posted, so posted numbers have no gaps.
/// </summary>
public sealed class JournalEntry : AggregateRoot
{
    public const int MinimumLines = 2;

    private readonly List<JournalLine> _lines = [];

    private JournalEntry(
        Guid id,
        Guid branchId,
        JournalSource source,
        string? sourceType,
        Guid? sourceId)
        : base(id)
    {
        BranchId = branchId;
        Source = source;
        SourceType = sourceType;
        SourceId = sourceId;
        Status = JournalStatus.Draft;
    }

    private JournalEntry()
    {
    }

    public string? Number { get; private set; }
    public Guid BranchId { get; private set; }
    public DateOnly Date { get; private set; }
    public string Description { get; private set; }
    public JournalSource Source { get; private set; }

    /// <summary>
    /// For automatic journals: the accounting event that produced it (e.g. "PurchaseReceipt").
    /// </summary>
    public string? SourceType { get; private set; }

    /// <summary>
    /// For automatic journals: the id of the source document. Unique together with <see cref="SourceType"/>,
    /// which makes the auto journal engine idempotent.
    /// </summary>
    public Guid? SourceId { get; private set; }

    public JournalStatus Status { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public Guid? PostedBy { get; private set; }
    public DateTime? PostedAtUtc { get; private set; }

    /// <summary>
    /// Set on a reversal journal: the journal it reverses.
    /// </summary>
    public Guid? ReversalOfId { get; private set; }

    /// <summary>
    /// Set on a reversed journal: the journal that reverses it.
    /// </summary>
    public Guid? ReversedById { get; private set; }

    public IReadOnlyCollection<JournalLine> Lines => [.. _lines];

    public Money TotalDebit => _lines.Aggregate(Money.Zero, (total, line) => total + line.Debit);

    public Money TotalCredit => _lines.Aggregate(Money.Zero, (total, line) => total + line.Credit);

    public static Result<JournalEntry> CreateManual(
        Guid branchId,
        DateOnly date,
        string description,
        IReadOnlyList<JournalLineInput> lines)
    {
        var journal = new JournalEntry(Guid.CreateVersion7(), branchId, JournalSource.Manual, null, null);

        Result result = journal.Apply(date, description, lines);

        return result.IsSuccess ? journal : Result.Failure<JournalEntry>(result.Error);
    }

    public static Result<JournalEntry> CreateAutomatic(
        Guid branchId,
        DateOnly date,
        string description,
        string sourceType,
        Guid sourceId,
        IReadOnlyList<JournalLineInput> lines)
    {
        var journal = new JournalEntry(Guid.CreateVersion7(), branchId, JournalSource.Automatic, sourceType, sourceId);

        Result result = journal.Apply(date, description, lines);

        return result.IsSuccess ? journal : Result.Failure<JournalEntry>(result.Error);
    }

    public Result Update(DateOnly date, string description, IReadOnlyList<JournalLineInput> lines)
    {
        if (Status != JournalStatus.Draft || Source != JournalSource.Manual)
        {
            return Result.Failure(JournalErrors.NotEditable(Id));
        }

        return Apply(date, description, lines);
    }

    public Result Approve(Guid approverId, DateTime utcNow)
    {
        if (Status != JournalStatus.Draft)
        {
            return Result.Failure(JournalErrors.InvalidTransition(Status, JournalStatus.Approved));
        }

        if (CreatedBy == approverId)
        {
            return Result.Failure(JournalErrors.SelfApprovalNotAllowed);
        }

        Status = JournalStatus.Approved;
        ApprovedBy = approverId;
        ApprovedAtUtc = utcNow;

        return Result.Success();
    }

    /// <summary>
    /// Posts the journal to the general ledger. Manual journals must be approved first.
    /// </summary>
    public Result Post(string number, FiscalPeriod period, Guid? userId, DateTime utcNow)
    {
        bool canPost = Source == JournalSource.Automatic
            ? Status == JournalStatus.Draft
            : Status == JournalStatus.Approved;

        if (!canPost)
        {
            return Result.Failure(JournalErrors.InvalidTransition(Status, JournalStatus.Posted));
        }

        Result periodResult = EnsureOpenPeriod(period, Date);
        if (periodResult.IsFailure)
        {
            return periodResult;
        }

        Number = number;
        Status = JournalStatus.Posted;
        PostedBy = userId;
        PostedAtUtc = utcNow;

        Raise(new JournalPostedDomainEvent(Id));

        return Result.Success();
    }

    /// <summary>
    /// Creates and posts a journal with debit and credit swapped, and marks this journal as reversed.
    /// </summary>
    public Result<JournalEntry> Reverse(
        DateOnly date,
        string reason,
        string number,
        FiscalPeriod period,
        Guid? userId,
        DateTime utcNow)
    {
        if (Status != JournalStatus.Posted)
        {
            return Result.Failure<JournalEntry>(JournalErrors.InvalidTransition(Status, JournalStatus.Reversed));
        }

        if (date < Date)
        {
            return Result.Failure<JournalEntry>(JournalErrors.ReversalBeforeOriginal);
        }

        Result periodResult = EnsureOpenPeriod(period, date);
        if (periodResult.IsFailure)
        {
            return Result.Failure<JournalEntry>(periodResult.Error);
        }

        var reversal = new JournalEntry(Guid.CreateVersion7(), BranchId, Source, SourceType is null ? null : $"{SourceType}.Reversal", SourceId)
        {
            Date = date,
            Description = $"Pembalik {Number}: {reason}",
            ReversalOfId = Id,
            Number = number,
            Status = JournalStatus.Posted,
            PostedBy = userId,
            PostedAtUtc = utcNow
        };

        reversal._lines.AddRange(_lines.Select(line => new JournalLine(
            reversal.Id, line.LineNumber, line.AccountId, line.CostCenterId, line.Description, line.Credit, line.Debit)));

        Status = JournalStatus.Reversed;
        ReversedById = reversal.Id;

        Raise(new JournalReversedDomainEvent(Id, reversal.Id));
        reversal.Raise(new JournalPostedDomainEvent(reversal.Id));

        return reversal;
    }

    public bool CanBeDeleted => Status == JournalStatus.Draft && Source == JournalSource.Manual;

    private static Result EnsureOpenPeriod(FiscalPeriod period, DateOnly date)
    {
        if (!period.Contains(date))
        {
            return Result.Failure(FiscalPeriodErrors.NotFoundForDate(date));
        }

        return period.IsOpen ? Result.Success() : Result.Failure(FiscalPeriodErrors.Closed(date));
    }

    private Result Apply(DateOnly date, string description, IReadOnlyList<JournalLineInput> lines)
    {
        Result validation = Validate(lines);
        if (validation.IsFailure)
        {
            return validation;
        }

        Date = date;
        Description = description.Trim();

        _lines.Clear();
        _lines.AddRange(lines.Select((line, index) => new JournalLine(
            Id, index + 1, line.AccountId, line.CostCenterId, line.Description, line.Debit, line.Credit)));

        return Result.Success();
    }

    private static Result Validate(IReadOnlyList<JournalLineInput> lines)
    {
        if (lines.Count < MinimumLines)
        {
            return Result.Failure(JournalErrors.TooFewLines);
        }

        foreach (JournalLineInput line in lines)
        {
            bool debitOnly = line.Debit > Money.Zero && line.Credit.IsZero;
            bool creditOnly = line.Credit > Money.Zero && line.Debit.IsZero;

            if (!debitOnly && !creditOnly)
            {
                return Result.Failure(JournalErrors.InvalidLineAmount);
            }
        }

        Money debit = lines.Aggregate(Money.Zero, (total, line) => total + line.Debit);
        Money credit = lines.Aggregate(Money.Zero, (total, line) => total + line.Credit);

        return debit == credit ? Result.Success() : Result.Failure(JournalErrors.NotBalanced(debit, credit));
    }
}
