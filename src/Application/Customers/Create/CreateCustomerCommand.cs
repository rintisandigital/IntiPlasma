using Application.Abstractions.Messaging;
using Application.Common;

namespace Application.Customers.Create;

public sealed record CreateCustomerCommand(
    string Code,
    string Name,
    TaxIdentityRequest TaxIdentity,
    string? Address,
    string? Phone,
    string? Email,
    int PaymentTermDays,
    decimal CreditLimit,
    IReadOnlyList<Guid>? Documents = null) : ICommand<Guid>;
