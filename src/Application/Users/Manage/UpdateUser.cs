using Application.Abstractions.Auditing;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Manage;

public sealed record UpdateUserCommand(Guid UserId, string FirstName, string LastName) : ICommand, IAuditedCommand
{
    string IAuditedCommand.AuditEntityType => "User";
}

internal sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(c => c.LastName).NotEmpty().MaximumLength(100);
    }
}

internal sealed class UpdateUserCommandHandler(IApplicationDbContext context) : ICommandHandler<UpdateUserCommand>
{
    public async Task<Result> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        User? user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(command.UserId));
        }

        user.UpdateProfile(command.FirstName, command.LastName);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
