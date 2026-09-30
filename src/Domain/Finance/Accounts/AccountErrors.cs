using Domain.Common;
using SharedKernel;

namespace Domain.Finance.Accounts;

public static class AccountErrors
{
    public static Error NotFound(Guid accountId) => Error.NotFound(
        "Accounts.NotFound",
        $"The account with the Id = '{accountId}' was not found");

    public static Error CodeNotUnique(string code) => CommonErrors.CodeNotUnique("Account", code);

    public static Error ParentMustBeHeader(Guid parentId) => Error.Problem(
        "Accounts.ParentMustBeHeader",
        $"The parent account '{parentId}' is a postable account; only header accounts can have children");

    public static Error NotPostable(Guid accountId) => Error.Problem(
        "Accounts.NotPostable",
        $"The account with the Id = '{accountId}' is a header account or inactive and cannot be posted to");

    public static readonly Error ParentTypeMismatch = Error.Problem(
        "Accounts.ParentTypeMismatch",
        "An account must have the same type as its parent");
}
