using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.MasterData.Branches;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Branches.Create;

internal sealed class CreateBranchCommandHandler(IApplicationDbContext context)
    : ICommandHandler<CreateBranchCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateBranchCommand command, CancellationToken cancellationToken)
    {
        var branch = Branch.Create(command.Code, command.Name, command.Address, command.Phone);

        if (await context.Branches.AnyAsync(b => b.Code == branch.Code, cancellationToken))
        {
            return Result.Failure<Guid>(BranchErrors.CodeNotUnique(branch.Code));
        }

        context.Branches.Add(branch);

        await context.SaveChangesAsync(cancellationToken);

        return branch.Id;
    }
}
