using Application.Abstractions.Messaging;
using Application.Common;

namespace Application.Vendors.Update;

public sealed record UpdateVendorCommand(
    Guid VendorId,
    string Name,
    TaxIdentityRequest TaxIdentity,
    string? Address,
    string? Phone,
    string? Email,
    int PaymentTermDays,
    BankAccountRequest BankAccount,
    bool IsActive,
    decimal? PriceTolerancePercent = null,
    IReadOnlyList<Guid>? Documents = null) : ICommand;
