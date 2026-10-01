using SharedKernel;

namespace Domain.Finance.CashBank;

/// <summary>
/// Pemindahbukuan between two cash/bank accounts of the same branch, e.g. a cash deposit to the bank or a petty cash
/// replenishment. Posted on creation; journals Dr destination / Cr source.
/// </summary>
public sealed class BankTransfer : AggregateRoot
{
    private BankTransfer(Guid id)
        : base(id)
    {
    }

    private BankTransfer()
    {
    }

    public string Number { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid FromCashBankAccountId { get; private set; }
    public Guid ToCashBankAccountId { get; private set; }
    public DateOnly Date { get; private set; }
    public Money Amount { get; private set; }
    public string? Reference { get; private set; }
    public string? Notes { get; private set; }

    public static Result<BankTransfer> Create(
        string number,
        CashBankAccount from,
        CashBankAccount to,
        DateOnly date,
        Money amount,
        string? reference,
        string? notes)
    {
        if (from.Id == to.Id)
        {
            return Result.Failure<BankTransfer>(BankTransferErrors.SameAccount);
        }

        if (from.BranchId != to.BranchId)
        {
            return Result.Failure<BankTransfer>(BankTransferErrors.DifferentBranches);
        }

        if (!from.IsActive || !to.IsActive)
        {
            return Result.Failure<BankTransfer>(CashBankErrors.Unusable(from.IsActive ? to.Id : from.Id));
        }

        if (amount.IsNegative || amount.IsZero)
        {
            return Result.Failure<BankTransfer>(BankTransferErrors.InvalidAmount);
        }

        var transfer = new BankTransfer(Guid.CreateVersion7())
        {
            Number = number,
            BranchId = from.BranchId,
            FromCashBankAccountId = from.Id,
            ToCashBankAccountId = to.Id,
            Date = date,
            Amount = amount,
            Reference = reference,
            Notes = notes
        };

        transfer.Raise(new BankTransferPostedDomainEvent(transfer.Id));

        return transfer;
    }
}

public sealed record BankTransferPostedDomainEvent(Guid BankTransferId) : DomainEvent;

public static class BankTransferErrors
{
    public static Error NotFound(Guid bankTransferId) => Error.NotFound(
        "BankTransfers.NotFound",
        $"The bank transfer with the Id = '{bankTransferId}' was not found");

    public static readonly Error SameAccount = Error.Problem(
        "BankTransfers.SameAccount",
        "The source and destination cash/bank accounts must differ");

    public static readonly Error DifferentBranches = Error.Problem(
        "BankTransfers.DifferentBranches",
        "Transfers are only allowed between cash/bank accounts of the same branch");

    public static readonly Error InvalidAmount = Error.Problem(
        "BankTransfers.InvalidAmount",
        "The transfer amount must be positive");
}
