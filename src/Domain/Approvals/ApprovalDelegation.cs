using SharedKernel;

namespace Domain.Approvals;

/// <summary>
/// Delegasi approval (PLAN-MOBILE M-98): while an approver is away, requests waiting for them also reach the delegate,
/// who decides on their behalf. The period is a range of Asia/Jakarta dates, both inclusive; the delegation ends by
/// itself or can be revoked. Delegations are not chained: the delegate's own delegations do not pass on.
/// The caller keeps the periods of one approver from overlapping.
/// </summary>
public sealed class ApprovalDelegation : AggregateRoot
{
    public const int MaxDays = 90;

    private ApprovalDelegation(Guid id, Guid delegatorUserId, Guid delegateUserId, DateOnly fromDate, DateOnly toDate, string? reason)
        : base(id)
    {
        DelegatorUserId = delegatorUserId;
        DelegateUserId = delegateUserId;
        FromDate = fromDate;
        ToDate = toDate;
        Reason = reason;
    }

    private ApprovalDelegation()
    {
    }

    public Guid DelegatorUserId { get; private set; }
    public Guid DelegateUserId { get; private set; }
    public DateOnly FromDate { get; private set; }
    public DateOnly ToDate { get; private set; }
    public string? Reason { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public Guid? RevokedBy { get; private set; }

    public static Result<ApprovalDelegation> Create(
        Guid delegatorUserId,
        Guid delegateUserId,
        DateOnly fromDate,
        DateOnly toDate,
        string? reason,
        DateOnly today)
    {
        if (delegatorUserId == delegateUserId)
        {
            return Result.Failure<ApprovalDelegation>(ApprovalErrors.DelegateToSelf);
        }

        if (toDate < fromDate || toDate < today || toDate.DayNumber - fromDate.DayNumber + 1 > MaxDays)
        {
            return Result.Failure<ApprovalDelegation>(ApprovalErrors.InvalidDelegationPeriod);
        }

        return new ApprovalDelegation(
            Guid.CreateVersion7(),
            delegatorUserId,
            delegateUserId,
            fromDate,
            toDate,
            string.IsNullOrWhiteSpace(reason) ? null : reason.Trim());
    }

    public bool IsActiveOn(DateOnly date) => RevokedAtUtc is null && FromDate <= date && date <= ToDate;

    public bool Overlaps(DateOnly fromDate, DateOnly toDate) =>
        RevokedAtUtc is null && FromDate <= toDate && fromDate <= ToDate;

    public Result Revoke(Guid userId, DateTime utcNow, DateOnly today)
    {
        if (RevokedAtUtc is not null || ToDate < today)
        {
            return Result.Failure(ApprovalErrors.DelegationEnded);
        }

        RevokedAtUtc = utcNow;
        RevokedBy = userId;

        return Result.Success();
    }
}
