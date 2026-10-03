using Application.Abstractions.Auditing;
using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Caching;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Dapper;
using Domain.Access;
using Domain.MasterData.Branches;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Access.BranchAccessProfiles;

// ---- Queries ---------------------------------------------------------------------------------------------

public sealed record GetBranchAccessProfilesQuery(PageRequest Paging) : IQuery<PagedList<BranchAccessProfileListItem>>;

public sealed record BranchAccessProfileListItem
{
    public Guid Id { get; init; }

    public string Name { get; init; }

    public string? Description { get; init; }

    public bool AllBranches { get; init; }

    public bool IsSystem { get; init; }

    public long BranchCount { get; init; }

    public long UserCount { get; init; }

    /// <summary>
    /// Branch codes, comma separated (empty for all branches).
    /// </summary>
    public string? BranchCodes { get; init; }
}

public sealed record GetBranchAccessProfileByIdQuery(Guid ProfileId) : IQuery<BranchAccessProfileResponse>;

public sealed record BranchAccessProfileResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; }

    public string? Description { get; init; }

    public bool AllBranches { get; init; }

    public bool IsSystem { get; init; }

    public long UserCount { get; init; }

    /// <summary>
    /// Branches of the profile (inactive ones included, flagged).
    /// </summary>
    public IReadOnlyList<BranchAccessProfileBranchItem> Branches { get; init; } = [];
}

public sealed record BranchAccessProfileBranchItem
{
    public Guid Id { get; init; }

    public string Code { get; init; }

    public string Name { get; init; }

    public bool IsActive { get; init; }
}

internal sealed class GetBranchAccessProfilesQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetBranchAccessProfilesQuery, PagedList<BranchAccessProfileListItem>>
{
    private const string Filter = "WHERE (@Search IS NULL OR p.name ILIKE @Search OR p.description ILIKE @Search)";

    public async Task<Result<PagedList<BranchAccessProfileListItem>>> Handle(
        GetBranchAccessProfilesQuery query,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        return await connection.QueryPagedAsync<BranchAccessProfileListItem>(
            $"SELECT COUNT(*) FROM identity.branch_access_profiles p {Filter}",
            $"""
            SELECT p.id AS Id, p.name AS Name, p.description AS Description, p.all_branches AS AllBranches,
                   p.is_system AS IsSystem,
                   (SELECT COUNT(*) FROM identity.branch_access_profile_branches pb WHERE pb.profile_id = p.id) AS BranchCount,
                   (SELECT COUNT(*) FROM identity.users u WHERE u.branch_access_profile_id = p.id) AS UserCount,
                   (SELECT string_agg(b.code, ', ' ORDER BY b.code)
                    FROM identity.branch_access_profile_branches pb JOIN master.branches b ON b.id = pb.branch_id
                    WHERE pb.profile_id = p.id) AS BranchCodes
            FROM identity.branch_access_profiles p
            {Filter}
            ORDER BY p.is_system DESC, p.name
            LIMIT @PageSize OFFSET @Offset
            """,
            query.Paging,
            null,
            cancellationToken);
    }
}

internal sealed class GetBranchAccessProfileByIdQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetBranchAccessProfileByIdQuery, BranchAccessProfileResponse>
{
    public async Task<Result<BranchAccessProfileResponse>> Handle(
        GetBranchAccessProfileByIdQuery query,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        const string sql =
            """
            SELECT p.id AS Id, p.name AS Name, p.description AS Description, p.all_branches AS AllBranches,
                   p.is_system AS IsSystem,
                   (SELECT COUNT(*) FROM identity.users u WHERE u.branch_access_profile_id = p.id) AS UserCount
            FROM identity.branch_access_profiles p
            WHERE p.id = @ProfileId;

            SELECT b.id AS Id, b.code AS Code, b.name AS Name, b.is_active AS IsActive
            FROM identity.branch_access_profile_branches pb
            JOIN master.branches b ON b.id = pb.branch_id
            WHERE pb.profile_id = @ProfileId
            ORDER BY b.code;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { query.ProfileId }, cancellationToken: cancellationToken));

        BranchAccessProfileResponse? profile = await multi.ReadSingleOrDefaultAsync<BranchAccessProfileResponse>();

        if (profile is null)
        {
            return Result.Failure<BranchAccessProfileResponse>(BranchAccessProfileErrors.NotFound(query.ProfileId));
        }

        return profile with { Branches = [.. await multi.ReadAsync<BranchAccessProfileBranchItem>()] };
    }
}

// ---- Commands --------------------------------------------------------------------------------------------

public sealed record CreateBranchAccessProfileCommand(
    string Name,
    string? Description,
    bool AllBranches,
    IReadOnlyList<Guid> BranchIds) : ICommand<Guid>, IAuditedCommand
{
    string IAuditedCommand.AuditEntityType => "BranchAccessProfile";
}

public sealed record UpdateBranchAccessProfileCommand(
    Guid ProfileId,
    string Name,
    string? Description,
    bool AllBranches,
    IReadOnlyList<Guid> BranchIds) : ICommand, IAuditedCommand
{
    string IAuditedCommand.AuditEntityType => "BranchAccessProfile";
}

public sealed record DeleteBranchAccessProfileCommand(Guid ProfileId) : ICommand, IAuditedCommand
{
    string IAuditedCommand.AuditEntityType => "BranchAccessProfile";
}

internal sealed class CreateBranchAccessProfileCommandValidator : AbstractValidator<CreateBranchAccessProfileCommand>
{
    public CreateBranchAccessProfileCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Description).MaximumLength(500);
        RuleFor(c => c.BranchIds).NotNull();
    }
}

internal sealed class UpdateBranchAccessProfileCommandValidator : AbstractValidator<UpdateBranchAccessProfileCommand>
{
    public UpdateBranchAccessProfileCommandValidator()
    {
        RuleFor(c => c.ProfileId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Description).MaximumLength(500);
        RuleFor(c => c.BranchIds).NotNull();
    }
}

internal static class BranchAccessProfileRules
{
    public static async Task<Result> EnsureBranchesExistAsync(
        IApplicationDbContext context,
        bool allBranches,
        IReadOnlyList<Guid> branchIds,
        CancellationToken cancellationToken)
    {
        if (allBranches)
        {
            return Result.Success();
        }

        List<Guid> existing = await context.Branches
            .Where(b => branchIds.Contains(b.Id))
            .Select(b => b.Id)
            .ToListAsync(cancellationToken);

        Guid missing = branchIds.FirstOrDefault(id => !existing.Contains(id));

        return missing == Guid.Empty ? Result.Success() : Result.Failure(BranchErrors.NotFound(missing));
    }
}

internal sealed class CreateBranchAccessProfileCommandHandler(IApplicationDbContext context)
    : ICommandHandler<CreateBranchAccessProfileCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateBranchAccessProfileCommand command, CancellationToken cancellationToken)
    {
        string name = command.Name.Trim();

        if (await context.BranchAccessProfiles.AnyAsync(p => p.Name == name, cancellationToken))
        {
            return Result.Failure<Guid>(BranchAccessProfileErrors.NameNotUnique);
        }

        Result branches = await BranchAccessProfileRules.EnsureBranchesExistAsync(
            context, command.AllBranches, command.BranchIds, cancellationToken);

        if (branches.IsFailure)
        {
            return Result.Failure<Guid>(branches.Error);
        }

        Result<BranchAccessProfile> result = BranchAccessProfile.Create(
            name, command.Description, command.AllBranches, command.BranchIds);

        if (result.IsFailure)
        {
            return Result.Failure<Guid>(result.Error);
        }

        context.BranchAccessProfiles.Add(result.Value);

        await context.SaveChangesAsync(cancellationToken);

        return result.Value.Id;
    }
}

internal sealed class UpdateBranchAccessProfileCommandHandler(IApplicationDbContext context, ICacheInvalidator cache)
    : ICommandHandler<UpdateBranchAccessProfileCommand>
{
    public async Task<Result> Handle(UpdateBranchAccessProfileCommand command, CancellationToken cancellationToken)
    {
        BranchAccessProfile? profile = await context.BranchAccessProfiles
            .Include(p => p.Branches)
            .SingleOrDefaultAsync(p => p.Id == command.ProfileId, cancellationToken);

        if (profile is null)
        {
            return Result.Failure(BranchAccessProfileErrors.NotFound(command.ProfileId));
        }

        string name = command.Name.Trim();

        if (await context.BranchAccessProfiles.AnyAsync(p => p.Id != command.ProfileId && p.Name == name, cancellationToken))
        {
            return Result.Failure(BranchAccessProfileErrors.NameNotUnique);
        }

        Result branches = await BranchAccessProfileRules.EnsureBranchesExistAsync(
            context, command.AllBranches, command.BranchIds, cancellationToken);

        if (branches.IsFailure)
        {
            return branches;
        }

        Result result = profile.Update(name, command.Description, command.AllBranches, command.BranchIds);

        if (result.IsFailure)
        {
            return result;
        }

        // A default branch the profile no longer covers is cleared (Web.App then falls back to the first branch).
        if (!profile.AllBranches)
        {
            var covered = profile.Branches.Select(b => b.BranchId).ToList();

            List<Domain.Users.User> users = await context.Users
                .Where(u => u.BranchAccessProfileId == profile.Id &&
                            u.DefaultBranchId != null &&
                            !covered.Contains(u.DefaultBranchId.Value))
                .ToListAsync(cancellationToken);

            foreach (Domain.Users.User user in users)
            {
                user.SetAccess(user.MenuAccessProfileId, user.BranchAccessProfileId, defaultBranchId: null);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        await cache.RemoveByTagAsync(PermissionCacheKeys.Tag, cancellationToken);

        return Result.Success();
    }
}

internal sealed class DeleteBranchAccessProfileCommandHandler(IApplicationDbContext context)
    : ICommandHandler<DeleteBranchAccessProfileCommand>
{
    public async Task<Result> Handle(DeleteBranchAccessProfileCommand command, CancellationToken cancellationToken)
    {
        BranchAccessProfile? profile = await context.BranchAccessProfiles
            .SingleOrDefaultAsync(p => p.Id == command.ProfileId, cancellationToken);

        if (profile is null)
        {
            return Result.Failure(BranchAccessProfileErrors.NotFound(command.ProfileId));
        }

        Result deletable = profile.EnsureDeletable();
        if (deletable.IsFailure)
        {
            return deletable;
        }

        int userCount = await context.Users.CountAsync(u => u.BranchAccessProfileId == profile.Id, cancellationToken);
        if (userCount > 0)
        {
            return Result.Failure(BranchAccessProfileErrors.InUse(userCount));
        }

        context.BranchAccessProfiles.Remove(profile);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
