using Application.Approvals;
using Application.Common;

namespace Application.Farmers;

public sealed record FarmerResponse : IHasLatestApproval
{
    /// <summary>
    /// Lampiran: attachment ids; metadata via <c>GET /attachments?ids=</c>.
    /// </summary>
    public Guid[] Documents { get; init; } = [];

    public Guid Id { get; init; }

    public string Code { get; init; }

    public string Name { get; init; }

    public string Type { get; init; }

    public Guid BranchId { get; init; }

    public string BranchCode { get; init; }

    public string? Nik { get; init; }

    public TaxIdentityResponse TaxIdentity { get; init; }

    public string? Address { get; init; }

    public string? Phone { get; init; }

    public BankAccountResponse BankAccount { get; init; }

    public int CoopCount { get; init; }

    public bool IsActive { get; init; }

    /// <summary>
    /// Draft, PendingApproval, Rejected or Approved (PLAN-MOBILE M-81).
    /// </summary>
    public string RegistrationStatus { get; init; }

    /// <summary>
    /// New NIK and bank account waiting for approval (M-84); the values above stay in use until approved.
    /// </summary>
    public FarmerPendingChangeResponse? PendingChange { get; init; }

    public Guid? ApprovalRequestId { get; init; }

    public string? ApprovalDocumentType { get; init; }

    public string? ApprovalStatus { get; init; }

    public int? ApprovalLevel { get; init; }

    public int? ApprovalLevelCount { get; init; }

    public string? ApprovalLevelName { get; init; }

    public string? ApprovalRejectReason { get; init; }

    /// <summary>
    /// PPL responsible for the farmer (PLAN-MOBILE M-11).
    /// </summary>
    public Guid? FieldOfficerUserId { get; init; }

    public string? FieldOfficerName { get; init; }
}

public sealed record FarmerPendingChangeResponse(string? Nik, BankAccountResponse BankAccount);
