namespace Domain.Approvals;

/// <summary>
/// Documents that go through the multi-level approval module (PLAN-MOBILE M-5, §5). Finance documents keep their
/// own maker-checker (M-34).
/// </summary>
public enum ApprovalDocumentType
{
    /// <summary>
    /// A new farmer (peternak).
    /// </summary>
    Farmer = 1,

    /// <summary>
    /// A change of an approved farmer's NIK or bank account (M-14, M-84).
    /// </summary>
    FarmerChange = 2,

    /// <summary>
    /// A new coop (kandang).
    /// </summary>
    Coop = 3,

    /// <summary>
    /// Activation of a partnership contract.
    /// </summary>
    Contract = 4
}

public enum ApprovalStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4
}

public enum ApprovalOutcome
{
    Approved = 1,
    Rejected = 2
}
