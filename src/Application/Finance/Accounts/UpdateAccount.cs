using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Finance.Accounts;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.Accounts;

public sealed record UpdateAccountCommand(Guid AccountId, string Name, bool IsActive) : ICommand;

internal sealed class UpdateAccountCommandValidator : AbstractValidator<UpdateAccountCommand>
{
    public UpdateAccountCommandValidator()
    {
        RuleFor(c => c.AccountId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(150);
    }
}

internal sealed class UpdateAccountCommandHandler(IApplicationDbContext context)
    : ICommandHandler<UpdateAccountCommand>
{
    public async Task<Result> Handle(UpdateAccountCommand command, CancellationToken cancellationToken)
    {
        Account? account = await context.Accounts.SingleOrDefaultAsync(a => a.Id == command.AccountId, cancellationToken);

        if (account is null)
        {
            return Result.Failure(AccountErrors.NotFound(command.AccountId));
        }

        account.Update(command.Name, command.IsActive);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
