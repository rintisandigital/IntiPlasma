using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.MasterData.TaxCodes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.TaxCodes.Create;

internal sealed class CreateTaxCodeCommandHandler(IApplicationDbContext context)
    : ICommandHandler<CreateTaxCodeCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateTaxCodeCommand command, CancellationToken cancellationToken)
    {
        TaxCode taxCode = command.Type == TaxType.Vat
            ? TaxCode.CreateVat(command.Code, command.Name, command.VatTreatment!.Value)
            : TaxCode.CreateIncomeTax(command.Code, command.Name, command.IncomeTaxArticle!.Value);

        if (await context.TaxCodes.AnyAsync(t => t.Code == taxCode.Code, cancellationToken))
        {
            return Result.Failure<Guid>(TaxCodeErrors.CodeNotUnique(taxCode.Code));
        }

        Result result = taxCode.SetRates(command.Rates.Select(r => (r.EffectiveFrom, r.RatePercent, r.TaxBaseRatio)));
        if (result.IsFailure)
        {
            return Result.Failure<Guid>(result.Error);
        }

        context.TaxCodes.Add(taxCode);

        await context.SaveChangesAsync(cancellationToken);

        return taxCode.Id;
    }
}
