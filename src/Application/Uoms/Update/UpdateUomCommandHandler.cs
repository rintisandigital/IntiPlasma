using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.MasterData.Uoms;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Uoms.Update;

internal sealed class UpdateUomCommandHandler(IApplicationDbContext context)
    : ICommandHandler<UpdateUomCommand>
{
    public async Task<Result> Handle(UpdateUomCommand command, CancellationToken cancellationToken)
    {
        Uom? uom = await context.Uoms.SingleOrDefaultAsync(u => u.Id == command.UomId, cancellationToken);

        if (uom is null)
        {
            return Result.Failure(UomErrors.NotFound(command.UomId));
        }

        uom.Update(command.Name);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
