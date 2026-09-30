using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.MasterData.Uoms;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Uoms.Create;

internal sealed class CreateUomCommandHandler(IApplicationDbContext context)
    : ICommandHandler<CreateUomCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateUomCommand command, CancellationToken cancellationToken)
    {
        var uom = Uom.Create(command.Code, command.Name);

        if (await context.Uoms.AnyAsync(u => u.Code == uom.Code, cancellationToken))
        {
            return Result.Failure<Guid>(UomErrors.CodeNotUnique(uom.Code));
        }

        context.Uoms.Add(uom);

        await context.SaveChangesAsync(cancellationToken);

        return uom.Id;
    }
}
