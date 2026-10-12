using SharedKernel;

namespace Domain.Approvals;

public static class ApprovalErrors
{
    public static Error FlowNotFound(Guid flowId) => Error.NotFound(
        "ApprovalFlows.NotFound",
        $"The approval flow with the Id = '{flowId}' was not found");

    public static Error RequestNotFound(Guid requestId) => Error.NotFound(
        "Approvals.NotFound",
        $"The approval request with the Id = '{requestId}' was not found");

    public static Error DelegationNotFound(Guid delegationId) => Error.NotFound(
        "ApprovalDelegations.NotFound",
        $"The approval delegation with the Id = '{delegationId}' was not found");

    /// <summary>
    /// Shown as-is to WebApp and mobile users, hence in Indonesian (M-82).
    /// </summary>
    public static Error FlowNotConfigured(ApprovalDocumentType documentType, string branch) => Error.Problem(
        "Approvals.FlowNotConfigured",
        $"Alur approval belum dikonfigurasi untuk {DocumentLabel(documentType)} di cabang {branch}. Hubungi administrator.");

    public static readonly Error FlowAlreadyExists = Error.Conflict(
        "ApprovalFlows.AlreadyExists",
        "An active approval flow for this document type and branch already exists");

    public static readonly Error InvalidLevelCount = Error.Problem(
        "ApprovalFlows.InvalidLevelCount",
        $"An approval flow needs 1 to {ApprovalFlow.MaxLevels} levels");

    public static Error LevelNameRequired(int levelNo) => Error.Problem(
        "ApprovalFlows.LevelNameRequired",
        $"Level {levelNo} needs a name");

    public static Error LevelWithoutApprover(int levelNo) => Error.Problem(
        "ApprovalFlows.LevelWithoutApprover",
        $"Level {levelNo} needs at least one approver");

    public static Error InvalidApprover(Guid userId) => Error.Problem(
        "ApprovalFlows.InvalidApprover",
        $"The user with the Id = '{userId}' is not an active user allowed to decide approvals");

    public static readonly Error NotPending = Error.Problem(
        "Approvals.NotPending",
        "The approval request is no longer pending");

    public static readonly Error NotApprover = Error.Forbidden(
        "Approvals.NotApprover",
        "You are not an approver of the current level of this request");

    public static readonly Error RequesterCannotDecide = Error.Problem(
        "Approvals.RequesterCannotDecide",
        "The requester cannot decide their own request");

    public static readonly Error AlreadyDecidedOtherLevel = Error.Problem(
        "Approvals.AlreadyDecidedOtherLevel",
        "You already decided another level of this request; one user decides at most one level");

    public static readonly Error ReasonRequired = Error.Problem(
        "Approvals.ReasonRequired",
        "A reason is required to reject a request");

    public static readonly Error OnlyRequesterCanCancel = Error.Forbidden(
        "Approvals.OnlyRequesterCanCancel",
        "Only the requester can cancel the request");

    public static readonly Error LevelAlreadyDecided = Error.Problem(
        "Approvals.LevelAlreadyDecided",
        "This level has already been decided; its approvers can no longer be changed");

    public static readonly Error ApproverNotInLevel = Error.Problem(
        "Approvals.ApproverNotInLevel",
        "The user to replace is not an approver of this level");

    public static readonly Error DocumentPending = Error.Conflict(
        "Approvals.DocumentPending",
        "The document already has a pending approval request");

    public static readonly Error DelegateToSelf = Error.Problem(
        "ApprovalDelegations.DelegateToSelf",
        "You cannot delegate approvals to yourself");

    public static readonly Error InvalidDelegationPeriod = Error.Problem(
        "ApprovalDelegations.InvalidPeriod",
        $"The delegation period must not end in the past, must not end before it starts and lasts at most {ApprovalDelegation.MaxDays} days");

    public static readonly Error DelegationOverlaps = Error.Conflict(
        "ApprovalDelegations.Overlaps",
        "The delegation overlaps another delegation of the same approver");

    public static readonly Error DelegationEnded = Error.Problem(
        "ApprovalDelegations.Ended",
        "The delegation has already ended or been revoked");

    public static readonly Error InvalidDelegate = Error.Problem(
        "ApprovalDelegations.InvalidDelegate",
        "The delegate must be an active user allowed to decide approvals");

    private static string DocumentLabel(ApprovalDocumentType documentType) => documentType switch
    {
        ApprovalDocumentType.Farmer => "peternak baru",
        ApprovalDocumentType.FarmerChange => "perubahan NIK/rekening peternak",
        ApprovalDocumentType.Coop => "kandang baru",
        ApprovalDocumentType.Contract => "aktivasi kontrak",
        _ => documentType.ToString()
    };
}
