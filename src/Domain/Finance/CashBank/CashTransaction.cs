using Domain.Common;
using SharedKernel;

namespace Domain.Finance.CashBank;

/// <summary>
/// Kas/bank masuk atau keluar lain-lain (not a customer receipt or vendor payment): money into or out of a cash/bank
/// account against one or more accounts, e.g. bank interest, electricity, petty cash expenses.
/// Cash-out follows maker-checker (Draft → Approved by someone else → Posted); cash-in can be posted from Draft.
/// The number is given when posting.
/// </summary>
public sealed class CashTransaction : AggregateRoot, IHasDocuments
{
    private readonly List<CashTransactionLine> _lines = [];

    private CashTransaction(Guid id)
        : base(id)
    {
    }

    private CashTransaction()
    {
    }

    public string? Number { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid CashBankAccountId { get; private set; }
    public CashDirection Direction { get; private set; }
    public DateOnly Date { get; private set; }
    public string Description { get; private set; }
    public string? Reference { get; private set; }
    public CashTransactionStatus Status { get; private set; }
    public Money Amount { get; private set; } = new(0m);
    public Guid? ApprovedBy { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public Guid? PostedBy { get; private set; }
    public DateTime? PostedAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }
    public IReadOnlyCollection<CashTransactionLine> Lines => [.. _lines];

    /// <summary>
    /// Lampiran: ids of the attached photos and documents.
    /// </summary>
    public Guid[] Documents { get; private set; } = [];

    public Result SetDocuments(IEnumerable<Guid>? documents) =>
        Status == CashTransactionStatus.Cancelled
            ? Result.Failure(DocumentErrors.OwnerCancelled)
            : DocumentList.Apply(documents, value => Documents = value);

    /// <param name="cashAccountId">The chart of accounts account of the cash/bank account; lines cannot use it.</param>
    public static Result<CashTransaction> Create(
        Guid branchId,
        Guid cashBankAccountId,
        Guid cashAccountId,
        CashDirection direction,
        DateOnly date,
        string description,
        string? reference,
        IReadOnlyList<CashTransactionLineInput> lines)
    {
        if (lines.Count == 0 || lines.Any(l => l.Amount.IsNegative || l.Amount.IsZero))
        {
            return Result.Failure<CashTransaction>(CashTransactionErrors.InvalidLines);
        }

        if (lines.Any(l => l.AccountId == cashAccountId))
        {
            return Result.Failure<CashTransaction>(CashTransactionErrors.LineOnCashAccount);
        }

        var transaction = new CashTransaction(Guid.CreateVersion7())
        {
            BranchId = branchId,
            CashBankAccountId = cashBankAccountId,
            Direction = direction,
            Date = date,
            Description = description.Trim(),
            Reference = reference,
            Status = CashTransactionStatus.Draft,
            Amount = lines.Aggregate(new Money(0m), (total, l) => total + l.Amount)
        };

        transaction._lines.AddRange(lines.Select((l, index) => new CashTransactionLine(
            transaction.Id, index + 1, l.AccountId, l.CostCenterId, l.Description, l.Amount)));

        return transaction;
    }

    public Result Approve(Guid approverId, DateTime utcNow)
    {
        if (Status != CashTransactionStatus.Draft)
        {
            return Result.Failure(CashTransactionErrors.InvalidTransition(Status, CashTransactionStatus.Approved));
        }

        if (CreatedBy == approverId)
        {
            return Result.Failure(CashTransactionErrors.SelfApprovalNotAllowed);
        }

        Status = CashTransactionStatus.Approved;
        ApprovedBy = approverId;
        ApprovedAtUtc = utcNow;

        return Result.Success();
    }

    /// <summary>
    /// Cash-out must be approved first; cash-in can be posted from Draft.
    /// </summary>
    public Result EnsurePostable()
    {
        bool postable = Status == CashTransactionStatus.Approved ||
                        Status == CashTransactionStatus.Draft && Direction == CashDirection.In;

        return postable
            ? Result.Success()
            : Result.Failure(CashTransactionErrors.InvalidTransition(Status, CashTransactionStatus.Posted));
    }

    public Result Post(string number, Guid? userId, DateTime utcNow)
    {
        Result postable = EnsurePostable();
        if (postable.IsFailure)
        {
            return postable;
        }

        Number = number;
        Status = CashTransactionStatus.Posted;
        PostedBy = userId;
        PostedAtUtc = utcNow;

        Raise(new CashTransactionPostedDomainEvent(Id));

        return Result.Success();
    }

    public Result Cancel(string reason)
    {
        if (Status is not (CashTransactionStatus.Draft or CashTransactionStatus.Approved))
        {
            return Result.Failure(CashTransactionErrors.InvalidTransition(Status, CashTransactionStatus.Cancelled));
        }

        Status = CashTransactionStatus.Cancelled;
        CancellationReason = reason;

        return Result.Success();
    }
}

public sealed class CashTransactionLine
{
    internal CashTransactionLine(
        Guid cashTransactionId,
        int lineNumber,
        Guid accountId,
        Guid? costCenterId,
        string? description,
        Money amount)
    {
        CashTransactionId = cashTransactionId;
        LineNumber = lineNumber;
        AccountId = accountId;
        CostCenterId = costCenterId;
        Description = description;
        Amount = amount with { };
    }

    private CashTransactionLine()
    {
    }

    public Guid CashTransactionId { get; private set; }
    public int LineNumber { get; private set; }

    /// <summary>
    /// Counter account: credited for cash-in, debited for cash-out.
    /// </summary>
    public Guid AccountId { get; private set; }

    public Guid? CostCenterId { get; private set; }
    public string? Description { get; private set; }
    public Money Amount { get; private set; }
}

public sealed record CashTransactionLineInput(Guid AccountId, Guid? CostCenterId, string? Description, Money Amount);

public enum CashDirection
{
    In = 1,
    Out = 2
}

public enum CashTransactionStatus
{
    Draft = 1,
    Approved = 2,
    Posted = 3,
    Cancelled = 9
}

public sealed record CashTransactionPostedDomainEvent(Guid CashTransactionId) : DomainEvent;

public static class CashTransactionErrors
{
    public static Error NotFound(Guid cashTransactionId) => Error.NotFound(
        "CashTransactions.NotFound",
        $"The cash transaction with the Id = '{cashTransactionId}' was not found");

    public static Error InvalidTransition(CashTransactionStatus from, CashTransactionStatus to) => Error.Problem(
        "CashTransactions.InvalidTransition",
        $"A cash transaction cannot move from {from} to {to}");

    public static readonly Error InvalidLines = Error.Problem(
        "CashTransactions.InvalidLines",
        "A cash transaction needs at least one line, each with a positive amount");

    public static readonly Error LineOnCashAccount = Error.Problem(
        "CashTransactions.LineOnCashAccount",
        "A line cannot use the cash/bank account's own ledger account; use a bank transfer between cash/bank accounts");

    public static readonly Error SelfApprovalNotAllowed = Error.Problem(
        "CashTransactions.SelfApprovalNotAllowed",
        "A cash transaction must be approved by someone other than its creator");
}
