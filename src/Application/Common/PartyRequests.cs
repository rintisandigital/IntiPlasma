using Domain.Common;
using FluentValidation;
using SharedKernel;

namespace Application.Common;

public sealed record TaxIdentityRequest(string? Npwp, string? Nitku, bool IsPkp)
{
    public Result<TaxIdentity> ToDomain() => TaxIdentity.Create(Npwp, Nitku, IsPkp);
}

public sealed record BankAccountRequest(string? BankName, string? AccountNumber, string? AccountHolderName)
{
    public Result<BankAccount> ToDomain() => BankAccount.Create(BankName, AccountNumber, AccountHolderName);
}

public sealed record TaxIdentityResponse(string? Npwp, string? Nitku, bool IsPkp);

public sealed record BankAccountResponse(string? BankName, string? AccountNumber, string? AccountHolderName);

internal sealed class TaxIdentityRequestValidator : AbstractValidator<TaxIdentityRequest>
{
    public TaxIdentityRequestValidator()
    {
        RuleFor(t => t.Npwp).MaximumLength(30);
        RuleFor(t => t.Nitku).MaximumLength(40);
    }
}

internal sealed class BankAccountRequestValidator : AbstractValidator<BankAccountRequest>
{
    public BankAccountRequestValidator()
    {
        RuleFor(b => b.BankName).MaximumLength(100);
        RuleFor(b => b.AccountNumber).MaximumLength(40);
        RuleFor(b => b.AccountHolderName).MaximumLength(150);
    }
}
