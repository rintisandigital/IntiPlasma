using Application.Common;

namespace Application.Vendors;

public sealed record VendorResponse
{
    public Guid Id { get; init; }

    public string Code { get; init; }

    public string Name { get; init; }

    public TaxIdentityResponse TaxIdentity { get; init; }

    public string? Address { get; init; }

    public string? Phone { get; init; }

    public string? Email { get; init; }

    public int PaymentTermDays { get; init; }

    public BankAccountResponse BankAccount { get; init; }

    public bool IsActive { get; init; }
}
