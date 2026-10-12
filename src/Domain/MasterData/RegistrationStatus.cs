using Domain.Common;
using SharedKernel;

namespace Domain.MasterData;

/// <summary>
/// Approval state of a new farmer or coop (PLAN-MOBILE M-81). Separate from <c>IsActive</c>, which can only be true
/// once the record is approved, so every existing "active only" filter also leaves out records awaiting approval.
/// </summary>
public enum RegistrationStatus
{
    /// <summary>
    /// Saved but not (or no longer) submitted: the requester cancelled the request.
    /// </summary>
    Draft = 1,

    PendingApproval = 2,
    Rejected = 3,
    Approved = 4
}

/// <summary>
/// The status transitions shared by <c>Farmer</c> and <c>Coop</c>.
/// </summary>
internal static class Registration
{
    public static Result Submit(RegistrationStatus status, string entity, Guid id) =>
        status is RegistrationStatus.Draft or RegistrationStatus.Rejected
            ? Result.Success()
            : Result.Failure(CommonErrors.NotSubmittable(entity, id));

    public static Result Decide(RegistrationStatus status, string entity, Guid id) =>
        status == RegistrationStatus.PendingApproval
            ? Result.Success()
            : Result.Failure(CommonErrors.NotPendingApproval(entity, id));

    public static Result Edit(RegistrationStatus status, string entity, Guid id) =>
        status == RegistrationStatus.PendingApproval
            ? Result.Failure(CommonErrors.PendingApproval(entity, id))
            : Result.Success();
}
