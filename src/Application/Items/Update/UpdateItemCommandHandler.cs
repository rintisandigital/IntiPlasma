using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.MasterData.Items;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Items.Update;

internal sealed class UpdateItemCommandHandler(IApplicationDbContext context)
    : ICommandHandler<UpdateItemCommand>
{
    public async Task<Result> Handle(UpdateItemCommand command, CancellationToken cancellationToken)
    {
        Item? item = await context.Items
            .Include(i => i.Conversions)
            .SingleOrDefaultAsync(i => i.Id == command.ItemId, cancellationToken);

        if (item is null)
        {
            return Result.Failure(ItemErrors.NotFound(command.ItemId));
        }

        Result references = await ItemReferenceValidator.ValidateAsync(
            context,
            command.Conversions.Select(c => c.UomId),
            command.TaxCodeId,
            cancellationToken);

        if (references.IsFailure)
        {
            return references;
        }

        Result result = item.SetConversions(command.Conversions.Select(c => (c.UomId, c.Factor)));
        if (result.IsFailure)
        {
            return result;
        }

        item.Update(command.Name, command.TaxCodeId, command.IsActive);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
