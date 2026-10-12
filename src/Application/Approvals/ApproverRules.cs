using Application.Abstractions.Data;
using Domain.Approvals;
using Domain.Roles;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Approvals;

/// <summary>
/// Who may be an approver (PLAN-MOBILE M-86): an active user holding <c>approvals:decide</c> through a role (the
/// system Administrator role holds every permission). Checked when a flow is saved and again when deciding.
/// </summary>
internal static class ApproverRules
{
    public const string DecidePermission = Permissions.ApprovalsDecide;

    /// <summary>
    /// Asia/Jakarta has no daylight saving time; delegation periods are Jakarta dates.
    /// </summary>
    private static readonly TimeSpan JakartaOffset = TimeSpan.FromHours(7);

    public static DateOnly Today(DateTime utcNow) => DateOnly.FromDateTime(utcNow + JakartaOffset);

    public static Task<bool> IsEligibleAsync(IApplicationDbContext context, Guid userId, CancellationToken cancellationToken) =>
        Eligible(context).AnyAsync(u => u.Id == userId, cancellationToken);

    public static async Task<Result> EnsureEligibleAsync(
        IApplicationDbContext context,
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken)
    {
        Guid[] ids = [.. userIds.Distinct()];

        List<Guid> eligible = await Eligible(context)
            .Where(u => ids.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        Guid invalid = Array.Find(ids, id => !eligible.Contains(id));

        return invalid == Guid.Empty ? Result.Success() : Result.Failure(ApprovalErrors.InvalidApprover(invalid));
    }

    /// <summary>
    /// SQL condition for an eligible user aliased <paramref name="alias"/>; the query passes <c>@DecidePermission</c>.
    /// </summary>
    public static string EligibleSql(string alias) =>
        $"""
         {alias}.is_active
         AND EXISTS (SELECT 1 FROM identity.user_roles ap_ur
                     JOIN identity.role_permissions ap_rp ON ap_rp.role_id = ap_ur.role_id
                     WHERE ap_ur.user_id = {alias}.id AND ap_rp.permission = @DecidePermission)
         """;

    private static IQueryable<Domain.Users.User> Eligible(IApplicationDbContext context) =>
        context.Users
            .Where(u => u.IsActive)
            .Where(u => u.Roles.Any(ur => context.Roles.Any(r =>
                r.Id == ur.RoleId && r.Permissions.Any(p => p.Permission == DecidePermission))));
}
