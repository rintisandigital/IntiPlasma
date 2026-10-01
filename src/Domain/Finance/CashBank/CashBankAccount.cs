using Domain.Common;
using SharedKernel;

namespace Domain.Finance.CashBank;

/// <summary>
/// A cash box, petty cash fund or bank account of a branch, booked on its own postable asset account in the chart of
/// accounts. Every cash and bank transaction (receipts, payments, transfers, reconciliation) goes through one of these.
/// </summary>
public sealed class CashBankAccount : AggregateRoot
{
    private CashBankAccount(Guid id, string code, CashBankAccountType type, Guid branchId, Guid accountId)
        : base(id)
    {
        Code = code;
        Type = type;
        BranchId = branchId;
        AccountId = accountId;
        IsActive = true;
    }

    private CashBankAccount()
    {
    }

    public string Code { get; private set; }
    public string Name { get; private set; }
    public CashBankAccountType Type { get; private set; }
    public Guid BranchId { get; private set; }

    /// <summary>
    /// The chart of accounts account this cash/bank account is booked on (one cash/bank account per COA account).
    /// </summary>
    public Guid AccountId { get; private set; }

    public string? BankName { get; private set; }
    public string? AccountNumber { get; private set; }
    public bool IsActive { get; private set; }

    /// <param name="accountIsPostableAsset">Whether the COA account is an active, postable asset account.</param>
    public static Result<CashBankAccount> Create(
        string code,
        string name,
        CashBankAccountType type,
        Guid branchId,
        Guid accountId,
        bool accountIsPostableAsset,
        string? bankName,
        string? accountNumber)
    {
        if (!accountIsPostableAsset)
        {
            return Result.Failure<CashBankAccount>(CashBankErrors.InvalidAccount);
        }

        var cashBank = new CashBankAccount(Guid.CreateVersion7(), Codes.Normalize(code), type, branchId, accountId);

        Result result = cashBank.Update(name, bankName, accountNumber, isActive: true);

        return result.IsSuccess ? cashBank : Result.Failure<CashBankAccount>(result.Error);
    }

    public Result Update(string name, string? bankName, string? accountNumber, bool isActive)
    {
        if (Type == CashBankAccountType.Bank && (string.IsNullOrWhiteSpace(bankName) || string.IsNullOrWhiteSpace(accountNumber)))
        {
            return Result.Failure(CashBankErrors.BankDetailsRequired);
        }

        Name = name.Trim();
        BankName = string.IsNullOrWhiteSpace(bankName) ? null : bankName.Trim();
        AccountNumber = string.IsNullOrWhiteSpace(accountNumber) ? null : accountNumber.Trim();
        IsActive = isActive;

        return Result.Success();
    }
}

public enum CashBankAccountType
{
    /// <summary>
    /// Kas (cash box).
    /// </summary>
    Cash = 1,

    /// <summary>
    /// Rekening bank; can be reconciled against bank statements.
    /// </summary>
    Bank = 2,

    /// <summary>
    /// Kas kecil (imprest fund), replenished by a transfer from a bank or cash account.
    /// </summary>
    PettyCash = 3
}
