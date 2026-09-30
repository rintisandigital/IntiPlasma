using Application.Abstractions.Messaging;
using Domain.MasterData.TaxCodes;

namespace Application.TaxCodes.Create;

/// <param name="VatTreatment">Required when <paramref name="Type"/> is VAT (PPN).</param>
/// <param name="IncomeTaxArticle">Required when <paramref name="Type"/> is income tax (PPh).</param>
public sealed record CreateTaxCodeCommand(
    string Code,
    string Name,
    TaxType Type,
    VatTreatment? VatTreatment,
    IncomeTaxArticle? IncomeTaxArticle,
    IReadOnlyList<TaxRateRequest> Rates) : ICommand<Guid>;
