using Application.Abstractions.Messaging;
using Application.Common;

namespace Application.Customers.Update;

public sealed record UpdateCustomerCommand(
    Guid CustomerId,
    string Name,
    TaxIdentityRequest TaxIdentity,
    string? Address,
    string? Phone,
    string? Email,
    int PaymentTermDays,
    decimal CreditLimit,
    bool IsActive,
    IReadOnlyList<Guid>? Documents = null) : ICommand;
