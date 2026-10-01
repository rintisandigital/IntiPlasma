using Domain.Common;
using SharedKernel;

namespace Domain.Finance.CashBank;

public static class CashBankErrors
{
    public static Error NotFound(Guid cashBankAccountId) => Error.NotFound(
        "CashBankAccounts.NotFound",
        $"The cash/bank account with the Id = '{cashBankAccountId}' was not found");

    public static Error CodeNotUnique(string code) => CommonErrors.CodeNotUnique("Cash/bank account", code);

    public static Error AccountAlreadyUsed(Guid accountId) => Error.Conflict(
        "CashBankAccounts.AccountAlreadyUsed",
        $"The chart of accounts account '{accountId}' is already used by another cash/bank account");

    public static Error Unusable(Guid cashBankAccountId) => Error.Problem(
        "CashBankAccounts.Unusable",
        $"The cash/bank account with the Id = '{cashBankAccountId}' is inactive or belongs to another branch");

    public static readonly Error InvalidAccount = Error.Problem(
        "CashBankAccounts.InvalidAccount",
        "A cash/bank account must be booked on an active, postable asset account");

    public static readonly Error BankDetailsRequired = Error.Problem(
        "CashBankAccounts.BankDetailsRequired",
        "A bank account needs the bank name and account number");
}
