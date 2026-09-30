using Application.Common;

namespace Application.Customers;

public sealed record CustomerResponse
{
    public Guid Id { get; init; }

    public string Code { get; init; }

    public string Name { get; init; }

    public TaxIdentityResponse TaxIdentity { get; init; }

    public string? Address { get; init; }

    public string? Phone { get; init; }

    public string? Email { get; init; }

    public int PaymentTermDays { get; init; }

    public decimal CreditLimit { get; init; }

    public bool IsActive { get; init; }
}
