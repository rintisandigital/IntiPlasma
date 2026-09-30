using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Partnership.Cycles;
using SharedKernel;

namespace Application.Cycles.Cancel;

internal sealed class CancelCycleCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<CancelCycleCommand>
{
    public async Task<Result> Handle(CancelCycleCommand command, CancellationToken cancellationToken)
    {
        Result<ProductionCycle> cycle = await CycleLoader.LoadAsync(context, branchAccess, command.CycleId, cancellationToken);
        if (cycle.IsFailure)
        {
            return cycle;
        }

        Result result = cycle.Value.Cancel(command.Reason);
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
