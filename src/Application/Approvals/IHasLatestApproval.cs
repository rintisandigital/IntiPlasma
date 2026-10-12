namespace Application.Approvals;

/// <summary>
/// The latest approval request of a farmer, coop or contract (PLAN-MOBILE M-89); all null when it never had one.
/// The full timeline is served by the document's <c>…/approvals</c> endpoint.
/// </summary>
public interface IHasLatestApproval
{
    Guid? ApprovalRequestId { get; }

    /// <summary>
    /// Farmer, FarmerChange, Coop or Contract.
    /// </summary>
    string? ApprovalDocumentType { get; }

    /// <summary>
    /// Pending, Approved, Rejected or Cancelled.
    /// </summary>
    string? ApprovalStatus { get; }

    /// <summary>
    /// The level waiting for a decision (or that completed the request).
    /// </summary>
    int? ApprovalLevel { get; }

    int? ApprovalLevelCount { get; }

    string? ApprovalLevelName { get; }

    string? ApprovalRejectReason { get; }
}
