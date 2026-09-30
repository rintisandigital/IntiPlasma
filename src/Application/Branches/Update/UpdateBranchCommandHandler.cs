using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.MasterData.Branches;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Branches.Update;

internal sealed class UpdateBranchCommandHandler(IApplicationDbContext context)
    : ICommandHandler<UpdateBranchCommand>
{
    public async Task<Result> Handle(UpdateBranchCommand command, CancellationToken cancellationToken)
    {
        Branch? branch = await context.Branches.SingleOrDefaultAsync(b => b.Id == command.BranchId, cancellationToken);

        if (branch is null)
        {
            return Result.Failure(BranchErrors.NotFound(command.BranchId));
        }

        branch.Update(command.Name, command.Address, command.Phone, command.IsActive);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
