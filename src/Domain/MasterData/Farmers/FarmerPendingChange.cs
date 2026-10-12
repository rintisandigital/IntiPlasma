using Domain.Common;
using SharedKernel;

namespace Domain.MasterData.Farmers;

/// <summary>
/// Proposed NIK and bank account of an approved farmer, applied once the FarmerChange request is approved (M-84).
/// </summary>
public sealed record FarmerPendingChange(string? Nik, string? BankName, string? AccountNumber, string? AccountHolderName)
{
    public static FarmerPendingChange Create(string? nik, BankAccount bankAccount) =>
        new(nik, bankAccount.BankName, bankAccount.AccountNumber, bankAccount.AccountHolderName);

    public BankAccount ToBankAccount()
    {
        Result<BankAccount> account = BankAccount.Create(BankName, AccountNumber, AccountHolderName);

        return account.IsSuccess ? account.Value : BankAccount.None;
    }
}
