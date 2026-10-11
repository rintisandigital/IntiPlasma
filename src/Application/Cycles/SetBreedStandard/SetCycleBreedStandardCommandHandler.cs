using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.BreedStandards;
using Domain.MasterData.BreedStandards;
using Domain.Partnership.Cycles;
using SharedKernel;

namespace Application.Cycles.SetBreedStandard;

internal sealed class SetCycleBreedStandardCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IFieldScope fieldScope)
    : ICommandHandler<SetCycleBreedStandardCommand>
{
    public async Task<Result> Handle(SetCycleBreedStandardCommand command, CancellationToken cancellationToken)
    {
        Result<ProductionCycle> cycle = await CycleLoader.LoadAsync(context, branchAccess, fieldScope, command.CycleId, cancellationToken);
        if (cycle.IsFailure)
        {
            return cycle;
        }

        Result usable = await CycleStandardRules.EnsureUsableAsync(context, command.BreedStandardId, cancellationToken);
        if (usable.IsFailure)
        {
            return usable;
        }

        BreedStandardSnapshot? effective = cycle.Value.Status == CycleStatus.Closed
            ? await BreedStandardReader.FindEffectiveAsync(context, command.BreedStandardId, cancellationToken)
            : null;

        Result result = cycle.Value.SetBreedStandard(command.BreedStandardId, effective);
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
