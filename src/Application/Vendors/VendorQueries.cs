using Application.Common;

namespace Application.Vendors;

/// <summary>
/// Shared SQL and row mapping for vendor queries (tax identity and bank account are flattened columns).
/// </summary>
internal static class VendorQueries
{
    public const string Select =
        """
        SELECT v.id AS Id, v.code AS Code, v.name AS Name,
               v.tax_identity_npwp AS Npwp, v.tax_identity_nitku AS Nitku, v.tax_identity_is_pkp AS IsPkp,
               v.address AS Address, v.phone AS Phone, v.email AS Email, v.payment_term_days AS PaymentTermDays,
               v.bank_account_bank_name AS BankName, v.bank_account_account_number AS AccountNumber,
               v.bank_account_account_holder_name AS AccountHolderName, v.price_tolerance_percent AS PriceTolerancePercent,
               v.is_active AS IsActive
        FROM master.vendors v
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
        public string? BankName { get; set; }
        public string? AccountNumber { get; set; }
        public string? AccountHolderName { get; set; }
        public decimal PriceTolerancePercent { get; set; }
        public bool IsActive { get; set; }

        public VendorResponse ToResponse() => new()
        {
            Id = Id,
            Code = Code,
            Name = Name,
            TaxIdentity = new TaxIdentityResponse(Npwp, Nitku, IsPkp),
            Address = Address,
            Phone = Phone,
            Email = Email,
            PaymentTermDays = PaymentTermDays,
            BankAccount = new BankAccountResponse(BankName, AccountNumber, AccountHolderName),
            PriceTolerancePercent = PriceTolerancePercent,
            IsActive = IsActive
        };
    }
}
