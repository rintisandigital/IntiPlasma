using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.MasterData.Items;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Items.Create;

internal sealed class CreateItemCommandHandler(IApplicationDbContext context)
    : ICommandHandler<CreateItemCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateItemCommand command, CancellationToken cancellationToken)
    {
        var item = Item.Create(command.Code, command.Name, command.Category, command.BaseUomId, command.TaxCodeId);

        if (await context.Items.AnyAsync(i => i.Code == item.Code, cancellationToken))
        {
            return Result.Failure<Guid>(ItemErrors.CodeNotUnique(item.Code));
        }

        Result references = await ItemReferenceValidator.ValidateAsync(
            context,
            command.Conversions.Select(c => c.UomId).Append(command.BaseUomId),
            command.TaxCodeId,
            cancellationToken);

        if (references.IsFailure)
        {
            return Result.Failure<Guid>(references.Error);
        }

        Result result = item.SetConversions(command.Conversions.Select(c => (c.UomId, c.Factor)));
        if (result.IsFailure)
        {
            return Result.Failure<Guid>(result.Error);
        }

        context.Items.Add(item);

        await context.SaveChangesAsync(cancellationToken);

        return item.Id;
    }
}
