using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Finance.Accounts;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.Accounts;

/// <param name="NormalBalance">Only needed for contra accounts; defaults from the account type.</param>
public sealed record CreateAccountCommand(
    string Code,
    string Name,
    AccountType Type,
    Guid? ParentId,
    bool IsPostable,
    BalanceSide? NormalBalance) : ICommand<Guid>;

internal sealed class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountCommandValidator()
    {
        RuleFor(c => c.Code).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9.-]+$");
        RuleFor(c => c.Name).NotEmpty().MaximumLength(150);
        RuleFor(c => c.Type).IsInEnum();
        RuleFor(c => c.NormalBalance).IsInEnum();
    }
}

internal sealed class CreateAccountCommandHandler(IApplicationDbContext context)
    : ICommandHandler<CreateAccountCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateAccountCommand command, CancellationToken cancellationToken)
    {
        Account? parent = null;
        if (command.ParentId is Guid parentId)
        {
            parent = await context.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == parentId, cancellationToken);
            if (parent is null)
            {
                return Result.Failure<Guid>(AccountErrors.NotFound(parentId));
            }
        }

        Result<Account> account = Account.Create(
            command.Code, command.Name, command.Type, parent, command.IsPostable, command.NormalBalance);

        if (account.IsFailure)
        {
            return Result.Failure<Guid>(account.Error);
        }

        if (await context.Accounts.AnyAsync(a => a.Code == account.Value.Code, cancellationToken))
        {
            return Result.Failure<Guid>(AccountErrors.CodeNotUnique(account.Value.Code));
        }

        context.Accounts.Add(account.Value);

        await context.SaveChangesAsync(cancellationToken);

        return account.Value.Id;
    }
}
