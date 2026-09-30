using SharedKernel;

namespace Domain.Common;

/// <summary>
/// Bank account used to pay vendors and plasma farmers.
/// </summary>
public sealed record BankAccount
{
    public static readonly BankAccount None = new(null, null, null);

    private BankAccount(string? bankName, string? accountNumber, string? accountHolderName)
    {
        BankName = bankName;
        AccountNumber = accountNumber;
        AccountHolderName = accountHolderName;
    }

    public string? BankName { get; private init; }

    public string? AccountNumber { get; private init; }

    public string? AccountHolderName { get; private init; }

    public static Result<BankAccount> Create(string? bankName, string? accountNumber, string? accountHolderName)
    {
        bankName = string.IsNullOrWhiteSpace(bankName) ? null : bankName.Trim();
        accountNumber = Digits.Normalize(accountNumber);
        accountHolderName = string.IsNullOrWhiteSpace(accountHolderName) ? null : accountHolderName.Trim();

        if (bankName is null && accountNumber is null && accountHolderName is null)
        {
            return None;
        }

        if (bankName is null || accountNumber is null || accountHolderName is null)
        {
            return Result.Failure<BankAccount>(CommonErrors.IncompleteBankAccount);
        }

        return new BankAccount(bankName, accountNumber, accountHolderName);
    }
}
