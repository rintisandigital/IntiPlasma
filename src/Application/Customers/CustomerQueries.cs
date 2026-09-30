using Application.Common;

namespace Application.Customers;

/// <summary>
/// Shared SQL and row mapping for customer queries (tax identity and credit limit are flattened columns).
/// </summary>
internal static class CustomerQueries
{
    public const string Select =
        """
        SELECT c.id AS Id, c.code AS Code, c.name AS Name,
               c.tax_identity_npwp AS Npwp, c.tax_identity_nitku AS Nitku, c.tax_identity_is_pkp AS IsPkp,
               c.address AS Address, c.phone AS Phone, c.email AS Email, c.payment_term_days AS PaymentTermDays,
               c.credit_limit AS CreditLimit, c.is_active AS IsActive
        FROM master.customers c
        """;

    internal sealed class Row
    {
        public Guid Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string? Npwp { get; set; }
        public string? Nitku { get; set; }
        public bool IsPkp { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public int PaymentTermDays { get; set; }
        public decimal CreditLimit { get; set; }
        public bool IsActive { get; set; }

        public CustomerResponse ToResponse() => new()
        {
            Id = Id,
            Code = Code,
            Name = Name,
            TaxIdentity = new TaxIdentityResponse(Npwp, Nitku, IsPkp),
            Address = Address,
            Phone = Phone,
            Email = Email,
            PaymentTermDays = PaymentTermDays,
            CreditLimit = CreditLimit,
            IsActive = IsActive
        };
    }
}
