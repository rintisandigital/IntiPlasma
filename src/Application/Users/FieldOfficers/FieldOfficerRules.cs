using Application.Abstractions.Data;
using Domain.Roles;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.FieldOfficers;

/// <summary>
/// The PPL a farmer or coop is assigned to must be an active field officer covering the branch
/// (same rule as <see cref="GetFieldOfficersQuery"/>).
/// </summary>
internal static class FieldOfficerRules
{
    public static async Task<Result> EnsureValidAsync(
        IApplicationDbContext context,
        Guid? userId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        if (userId is null)
        {
            return Result.Success();
        }

        bool valid = await context.Users
            .Where(u => u.Id == userId && u.IsActive)
            .Where(u => u.Roles.Any(ur => context.Roles.Any(r =>
                r.Id == ur.RoleId && r.Permissions.Any(p => p.Permission == Permissions.PartnershipAssignedOnly))))
            .Where(u => !u.Roles.Any(ur => context.Roles.Any(r =>
                r.Id == ur.RoleId && r.IsSystem && r.Name == Role.AdministratorName)))
            .AnyAsync(u => context.BranchAccessProfiles.Any(p =>
                    p.Id == u.BranchAccessProfileId && (p.AllBranches || p.Branches.Any(b => b.BranchId == branchId))),
                cancellationToken);

        return valid ? Result.Success() : Result.Failure(UserErrors.NotFieldOfficer(userId.Value));
    }
}
