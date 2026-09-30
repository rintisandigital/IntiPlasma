using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.MasterData.TaxCodes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.TaxCodes.Update;

internal sealed class UpdateTaxCodeCommandHandler(IApplicationDbContext context)
    : ICommandHandler<UpdateTaxCodeCommand>
{
    public async Task<Result> Handle(UpdateTaxCodeCommand command, CancellationToken cancellationToken)
    {
        TaxCode? taxCode = await context.TaxCodes
            .Include(t => t.Rates)
            .SingleOrDefaultAsync(t => t.Id == command.TaxCodeId, cancellationToken);

        if (taxCode is null)
        {
            return Result.Failure(TaxCodeErrors.NotFound(command.TaxCodeId));
        }

        Result result = taxCode.SetRates(command.Rates.Select(r => (r.EffectiveFrom, r.RatePercent, r.TaxBaseRatio)));
        if (result.IsFailure)
        {
            return result;
        }

        taxCode.Update(command.Name, command.IsActive);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
