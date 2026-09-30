using Application.Abstractions.Messaging;

namespace Application.TaxCodes.Update;

public sealed record UpdateTaxCodeCommand(
    Guid TaxCodeId,
    string Name,
    bool IsActive,
    IReadOnlyList<TaxRateRequest> Rates) : ICommand;
