using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.MasterData.Branches;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.Users.AssignBranches;

internal sealed class AssignUserBranchesCommandHandler(IApplicationDbContext context, HybridCache cache)
    : ICommandHandler<AssignUserBranchesCommand>
{
    public async Task<Result> Handle(AssignUserBranchesCommand command, CancellationToken cancellationToken)
    {
        User? user = await context.Users
            .Include(u => u.Branches)
            .SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(command.UserId));
        }

        List<Guid> existingBranchIds = await context.Branches
            .Where(b => command.BranchIds.Contains(b.Id))
            .Select(b => b.Id)
            .ToListAsync(cancellationToken);

        Guid missingBranchId = command.BranchIds.FirstOrDefault(id => !existingBranchIds.Contains(id));
        if (missingBranchId != Guid.Empty)
        {
            return Result.Failure(BranchErrors.NotFound(missingBranchId));
        }

        user.SetBranches(command.BranchIds);

        await context.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(PermissionCacheKeys.BranchesForUser(user.Id), cancellationToken);

        return Result.Success();
    }
}
