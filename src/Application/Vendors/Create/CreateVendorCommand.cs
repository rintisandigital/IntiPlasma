using Application.Abstractions.Messaging;
using Application.Common;

namespace Application.Vendors.Create;

public sealed record CreateVendorCommand(
    string Code,
    string Name,
    TaxIdentityRequest TaxIdentity,
    string? Address,
    string? Phone,
    string? Email,
    int PaymentTermDays,
    BankAccountRequest BankAccount,
    decimal? PriceTolerancePercent = null) : ICommand<Guid>;
