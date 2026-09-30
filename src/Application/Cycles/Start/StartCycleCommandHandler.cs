using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Partnership.Cycles;
using SharedKernel;

namespace Application.Cycles.Start;

internal sealed class StartCycleCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<StartCycleCommand>
{
    public async Task<Result> Handle(StartCycleCommand command, CancellationToken cancellationToken)
    {
        Result<ProductionCycle> cycle = await CycleLoader.LoadAsync(context, branchAccess, command.CycleId, cancellationToken);
        if (cycle.IsFailure)
        {
            return cycle;
        }

        Result result = cycle.Value.Start(command.ChickInDate, command.InitialPopulation);
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
