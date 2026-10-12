using System.Text.Json;
using Application.Approvals;
using Application.Common;
using Domain.MasterData.Farmers;

namespace Application.Farmers;

internal static class FarmerQueries
{
    public static readonly string Select =
        $"""
        SELECT f.id AS Id, f.code AS Code, f.name AS Name, f.type AS Type, f.branch_id AS BranchId, b.code AS BranchCode,
               f.nik AS Nik, f.tax_identity_npwp AS Npwp, f.tax_identity_nitku AS Nitku, f.tax_identity_is_pkp AS IsPkp,
               f.address AS Address, f.phone AS Phone,
               f.bank_account_bank_name AS BankName, f.bank_account_account_number AS AccountNumber,
               f.bank_account_account_holder_name AS AccountHolderName,
               (SELECT COUNT(*) FROM master.coops c WHERE c.farmer_id = f.id)::int AS CoopCount,
               f.is_active AS IsActive,
               f.documents AS Documents,
               f.field_officer_user_id AS FieldOfficerUserId,
               NULLIF(TRIM(CONCAT(fo.first_name, ' ', fo.last_name)), '') AS FieldOfficerName,
               f.registration_status AS RegistrationStatus, f.pending_change::text AS PendingChange,
               {ApprovalSql.LatestColumns}
        FROM master.farmers f
        JOIN master.branches b ON b.id = f.branch_id
        LEFT JOIN identity.users fo ON fo.id = f.field_officer_user_id
        {ApprovalSql.LatestJoin("f", "Farmer", "FarmerChange")}
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
        public Guid[] Documents { get; set; } = [];
        public Guid? FieldOfficerUserId { get; set; }
        public string? FieldOfficerName { get; set; }
        public string RegistrationStatus { get; set; }
        public string? PendingChange { get; set; }
        public Guid? ApprovalRequestId { get; set; }
        public string? ApprovalDocumentType { get; set; }
        public string? ApprovalStatus { get; set; }
        public int? ApprovalLevel { get; set; }
        public int? ApprovalLevelCount { get; set; }
        public string? ApprovalLevelName { get; set; }
        public string? ApprovalRejectReason { get; set; }

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
            IsActive = IsActive,
            Documents = Documents,
            FieldOfficerUserId = FieldOfficerUserId,
            FieldOfficerName = FieldOfficerName,
            RegistrationStatus = RegistrationStatus,
            PendingChange = PendingChange is null ? null : ToPendingChange(PendingChange),
            ApprovalRequestId = ApprovalRequestId,
            ApprovalDocumentType = ApprovalDocumentType,
            ApprovalStatus = ApprovalStatus,
            ApprovalLevel = ApprovalLevel,
            ApprovalLevelCount = ApprovalLevelCount,
            ApprovalLevelName = ApprovalLevelName,
            ApprovalRejectReason = ApprovalRejectReason
        };

        private static FarmerPendingChangeResponse? ToPendingChange(string json)
        {
            FarmerPendingChange? change = JsonSerializer.Deserialize<FarmerPendingChange>(json, ApprovalService.PayloadOptions);

            return change is null
                ? null
                : new FarmerPendingChangeResponse(
                    change.Nik,
                    new BankAccountResponse(change.BankName, change.AccountNumber, change.AccountHolderName));
        }
    }
}
