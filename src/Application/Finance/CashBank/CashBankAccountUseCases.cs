using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Finance.Accounts;
using Domain.Finance.CashBank;
using Domain.MasterData.Branches;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.CashBank;

/// <param name="AccountId">A postable asset account of the chart of accounts not used by another cash/bank account.</param>
public sealed record CreateCashBankAccountCommand(
    string Code,
    string Name,
    CashBankAccountType Type,
    Guid BranchId,
    Guid AccountId,
    string? BankName,
    string? AccountNumber) : ICommand<Guid>;

public sealed record UpdateCashBankAccountCommand(
    Guid CashBankAccountId,
    string Name,
    string? BankName,
    string? AccountNumber,
    bool IsActive) : ICommand;

internal sealed class CreateCashBankAccountCommandValidator : AbstractValidator<CreateCashBankAccountCommand>
{
    public CreateCashBankAccountCommandValidator()
    {
        RuleFor(c => c.Code).NotEmpty().MaximumLength(20);
        RuleFor(c => c.Name).NotEmpty().MaximumLength(150);
        RuleFor(c => c.Type).IsInEnum();
        RuleFor(c => c.BranchId).NotEmpty();
        RuleFor(c => c.AccountId).NotEmpty();
        RuleFor(c => c.BankName).MaximumLength(100);
        RuleFor(c => c.AccountNumber).MaximumLength(40);
    }
}

internal sealed class UpdateCashBankAccountCommandValidator : AbstractValidator<UpdateCashBankAccountCommand>
{
    public UpdateCashBankAccountCommandValidator()
    {
        RuleFor(c => c.CashBankAccountId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(150);
        RuleFor(c => c.BankName).MaximumLength(100);
        RuleFor(c => c.AccountNumber).MaximumLength(40);
    }
}

internal static class CashBankSupport
{
    /// <summary>
    /// Loads a cash/bank account the user may use for a transaction of the branch: active, same branch.
    /// </summary>
    public static async Task<Result<CashBankAccount>> LoadUsableAsync(
        IApplicationDbContext context,
        Guid cashBankAccountId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        CashBankAccount? account = await context.CashBankAccounts.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == cashBankAccountId, cancellationToken);

        if (account is null)
        {
            return Result.Failure<CashBankAccount>(CashBankErrors.NotFound(cashBankAccountId));
        }

        return account.IsActive && account.BranchId == branchId
            ? account
            : Result.Failure<CashBankAccount>(CashBankErrors.Unusable(cashBankAccountId));
    }
}

internal sealed class CreateCashBankAccountCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<CreateCashBankAccountCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateCashBankAccountCommand command, CancellationToken cancellationToken)
    {
        Result access = await branchAccess.EnsureAccessAsync(command.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<Guid>(access.Error);
        }

        if (!await context.Branches.AnyAsync(b => b.Id == command.BranchId && b.IsActive, cancellationToken))
        {
            return Result.Failure<Guid>(BranchErrors.NotFound(command.BranchId));
        }

        bool postableAsset = await context.Accounts.AnyAsync(
            a => a.Id == command.AccountId && a.IsActive && a.IsPostable && a.Type == AccountType.Asset,
            cancellationToken);

        Result<CashBankAccount> account = CashBankAccount.Create(
            command.Code, command.Name, command.Type, command.BranchId, command.AccountId, postableAsset,
            command.BankName, command.AccountNumber);

        if (account.IsFailure)
        {
            return Result.Failure<Guid>(account.Error);
        }

        if (await context.CashBankAccounts.AnyAsync(a => a.Code == account.Value.Code, cancellationToken))
        {
            return Result.Failure<Guid>(CashBankErrors.CodeNotUnique(account.Value.Code));
        }

        if (await context.CashBankAccounts.AnyAsync(a => a.AccountId == command.AccountId, cancellationToken))
        {
            return Result.Failure<Guid>(CashBankErrors.AccountAlreadyUsed(command.AccountId));
        }

        context.CashBankAccounts.Add(account.Value);

        await context.SaveChangesAsync(cancellationToken);

        return account.Value.Id;
    }
}

internal sealed class UpdateCashBankAccountCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<UpdateCashBankAccountCommand>
{
    public async Task<Result> Handle(UpdateCashBankAccountCommand command, CancellationToken cancellationToken)
    {
        CashBankAccount? account = await context.CashBankAccounts
            .SingleOrDefaultAsync(a => a.Id == command.CashBankAccountId, cancellationToken);

        if (account is null)
        {
            return Result.Failure(CashBankErrors.NotFound(command.CashBankAccountId));
        }

        Result access = await branchAccess.EnsureAccessAsync(account.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return access;
        }

        Result result = account.Update(command.Name, command.BankName, command.AccountNumber, command.IsActive);
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
