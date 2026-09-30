using Application.Common;

namespace Application.Farmers;

internal static class FarmerQueries
{
    public const string Select =
        """
        SELECT f.id AS Id, f.code AS Code, f.name AS Name, f.type AS Type, f.branch_id AS BranchId, b.code AS BranchCode,
               f.nik AS Nik, f.tax_identity_npwp AS Npwp, f.tax_identity_nitku AS Nitku, f.tax_identity_is_pkp AS IsPkp,
               f.address AS Address, f.phone AS Phone,
               f.bank_account_bank_name AS BankName, f.bank_account_account_number AS AccountNumber,
               f.bank_account_account_holder_name AS AccountHolderName,
               (SELECT COUNT(*) FROM master.coops c WHERE c.farmer_id = f.id)::int AS CoopCount,
               f.is_active AS IsActive
        FROM master.farmers f
        JOIN master.branches b ON b.id = f.branch_id
        """;

    internal sealed class Row
    {
        public Guid Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public Guid BranchId { get; set; }
        public string BranchCode { get; set; }
        public string? Nik { get; set; }
        public string? Npwp { get; set; }
        public string? Nitku { get; set; }
        public bool IsPkp { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? BankName { get; set; }
        public string? AccountNumber { get; set; }
        public string? AccountHolderName { get; set; }
        public int CoopCount { get; set; }
        public bool IsActive { get; set; }

        public FarmerResponse ToResponse() => new()
        {
            Id = Id,
            Code = Code,
            Name = Name,
            Type = Type,
            BranchId = BranchId,
            BranchCode = BranchCode,
            Nik = Nik,
            TaxIdentity = new TaxIdentityResponse(Npwp, Nitku, IsPkp),
            Address = Address,
            Phone = Phone,
            BankAccount = new BankAccountResponse(BankName, AccountNumber, AccountHolderName),
            CoopCount = CoopCount,
            IsActive = IsActive
        };
    }
}
