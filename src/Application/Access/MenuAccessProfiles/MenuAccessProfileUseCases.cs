using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Caching;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Dapper;
using Domain.Access;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Access.MenuAccessProfiles;

// ---- Queries ---------------------------------------------------------------------------------------------

public sealed record GetMenuAccessProfilesQuery(PageRequest Paging) : IQuery<PagedList<MenuAccessProfileListItem>>;

public sealed record MenuAccessProfileListItem
{
    public Guid Id { get; init; }

    public string Name { get; init; }

    public string? Description { get; init; }

    public bool IsSystem { get; init; }

    public long UserCount { get; init; }

    /// <summary>
    /// Menus with the View right (null for Full Access: every menu).
    /// </summary>
    public long? MenuCount { get; init; }
}

public sealed record GetMenuAccessProfileByIdQuery(Guid ProfileId) : IQuery<MenuAccessProfileResponse>;

public sealed record MenuAccessProfileResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; }

    public string? Description { get; init; }

    public bool IsSystem { get; init; }

    public long UserCount { get; init; }

    /// <summary>
    /// Every page of the catalog (grouped and ordered) with the profile's rights on it.
    /// </summary>
    public IReadOnlyList<MenuAccessProfileItem> Items { get; init; } = [];
}

public sealed record MenuAccessProfileItem
{
    public Guid MenuId { get; init; }

    public string Code { get; init; }

    public string Name { get; init; }

    public string GroupCode { get; init; }

    public string GroupName { get; init; }

    public bool IsAvailable { get; init; }

    public bool IsActive { get; init; }

    public MenuRights SupportedRights { get; init; }

    public MenuRights Rights { get; init; }
}

internal sealed class GetMenuAccessProfilesQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetMenuAccessProfilesQuery, PagedList<MenuAccessProfileListItem>>
{
    private const string Filter = "WHERE (@Search IS NULL OR p.name ILIKE @Search OR p.description ILIKE @Search)";

    public async Task<Result<PagedList<MenuAccessProfileListItem>>> Handle(
        GetMenuAccessProfilesQuery query,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        return await connection.QueryPagedAsync<MenuAccessProfileListItem>(
            $"SELECT COUNT(*) FROM identity.menu_access_profiles p {Filter}",
            $"""
            SELECT p.id AS Id, p.name AS Name, p.description AS Description, p.is_system AS IsSystem,
                   (SELECT COUNT(*) FROM identity.users u WHERE u.menu_access_profile_id = p.id) AS UserCount,
                   CASE WHEN p.is_system THEN NULL
                        ELSE (SELECT COUNT(*) FROM identity.menu_access_profile_items i WHERE i.profile_id = p.id AND i.can_view)
                   END AS MenuCount
            FROM identity.menu_access_profiles p
            {Filter}
            ORDER BY p.is_system DESC, p.name
            LIMIT @PageSize OFFSET @Offset
            """,
            query.Paging,
            null,
            cancellationToken);
    }
}

internal sealed class GetMenuAccessProfileByIdQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetMenuAccessProfileByIdQuery, MenuAccessProfileResponse>
{
    public async Task<Result<MenuAccessProfileResponse>> Handle(
        GetMenuAccessProfileByIdQuery query,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        const string sql =
            """
            SELECT p.id AS Id, p.name AS Name, p.description AS Description, p.is_system AS IsSystem,
                   (SELECT COUNT(*) FROM identity.users u WHERE u.menu_access_profile_id = p.id) AS UserCount
            FROM identity.menu_access_profiles p
            WHERE p.id = @ProfileId;

            SELECT m.id AS MenuId, m.code AS Code, m.name AS Name, m.parent_code AS GroupCode, g.name AS GroupName,
                   m.is_available AS IsAvailable, m.is_active AS IsActive,
                   m.supports_create AS SupportsCreate, m.supports_edit AS SupportsEdit,
                   m.supports_delete AS SupportsDelete, m.supports_export AS SupportsExport,
                   COALESCE(i.can_view, false) AS CanView, COALESCE(i.can_create, false) AS CanCreate,
                   COALESCE(i.can_edit, false) AS CanEdit, COALESCE(i.can_delete, false) AS CanDelete,
                   COALESCE(i.can_export, false) AS CanExport
            FROM identity.menus m
            JOIN identity.menus g ON g.code = m.parent_code
            LEFT JOIN identity.menu_access_profile_items i ON i.menu_id = m.id AND i.profile_id = @ProfileId
            WHERE m.in_catalog
            ORDER BY g.sort_order, g.name, m.sort_order, m.name;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { query.ProfileId }, cancellationToken: cancellationToken));

        MenuAccessProfileResponse? profile = await multi.ReadSingleOrDefaultAsync<MenuAccessProfileResponse>();

        if (profile is null)
        {
            return Result.Failure<MenuAccessProfileResponse>(MenuAccessProfileErrors.NotFound(query.ProfileId));
        }

        List<MenuRow> rows = [.. await multi.ReadAsync<MenuRow>()];

        return profile with
        {
            Items = [.. rows.Select(r => r.ToItem(profile.IsSystem))]
        };
    }

    internal sealed class MenuRow
    {
        public Guid MenuId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string GroupCode { get; set; }
        public string GroupName { get; set; }
        public bool IsAvailable { get; set; }
        public bool IsActive { get; set; }
        public bool SupportsCreate { get; set; }
        public bool SupportsEdit { get; set; }
        public bool SupportsDelete { get; set; }
        public bool SupportsExport { get; set; }
        public bool CanView { get; set; }
        public bool CanCreate { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public bool CanExport { get; set; }

        public MenuAccessProfileItem ToItem(bool fullAccess)
        {
            MenuRights supported = Combine(true, SupportsCreate, SupportsEdit, SupportsDelete, SupportsExport);

            return new MenuAccessProfileItem
            {
                MenuId = MenuId,
                Code = Code,
                Name = Name,
                GroupCode = GroupCode,
                GroupName = GroupName,
                IsAvailable = IsAvailable,
                IsActive = IsActive,
                SupportedRights = supported,
                Rights = fullAccess ? supported : Combine(CanView, CanCreate, CanEdit, CanDelete, CanExport)
            };
        }

        private static MenuRights Combine(bool view, bool create, bool edit, bool delete, bool export)
        {
            MenuRights rights = view ? MenuRights.View : MenuRights.None;
            rights |= create ? MenuRights.Create : MenuRights.None;
            rights |= edit ? MenuRights.Edit : MenuRights.None;
            rights |= delete ? MenuRights.Delete : MenuRights.None;
            rights |= export ? MenuRights.Export : MenuRights.None;
            return rights;
        }
    }
}

// ---- Commands --------------------------------------------------------------------------------------------

public sealed record CreateMenuAccessProfileCommand(string Name, string? Description, IReadOnlyList<MenuAccessGrant> Grants)
    : ICommand<Guid>;

public sealed record UpdateMenuAccessProfileCommand(
    Guid ProfileId,
    string Name,
    string? Description,
    IReadOnlyList<MenuAccessGrant> Grants) : ICommand;

public sealed record DuplicateMenuAccessProfileCommand(Guid ProfileId, string Name) : ICommand<Guid>;

public sealed record DeleteMenuAccessProfileCommand(Guid ProfileId) : ICommand;

internal sealed class CreateMenuAccessProfileCommandValidator : AbstractValidator<CreateMenuAccessProfileCommand>
{
    public CreateMenuAccessProfileCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Description).MaximumLength(500);
        RuleFor(c => c.Grants).NotNull();
    }
}

internal sealed class UpdateMenuAccessProfileCommandValidator : AbstractValidator<UpdateMenuAccessProfileCommand>
{
    public UpdateMenuAccessProfileCommandValidator()
    {
        RuleFor(c => c.ProfileId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Description).MaximumLength(500);
        RuleFor(c => c.Grants).NotNull();
    }
}

internal sealed class DuplicateMenuAccessProfileCommandValidator : AbstractValidator<DuplicateMenuAccessProfileCommand>
{
    public DuplicateMenuAccessProfileCommandValidator()
    {
        RuleFor(c => c.ProfileId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
    }
}

internal sealed class CreateMenuAccessProfileCommandHandler(IApplicationDbContext context)
    : ICommandHandler<CreateMenuAccessProfileCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateMenuAccessProfileCommand command, CancellationToken cancellationToken)
    {
        string name = command.Name.Trim();

        if (await context.MenuAccessProfiles.AnyAsync(p => p.Name == name, cancellationToken))
        {
            return Result.Failure<Guid>(MenuAccessProfileErrors.NameNotUnique);
        }

        List<Menu> menus = await context.Menus.ToListAsync(cancellationToken);

        Result<MenuAccessProfile> result = MenuAccessProfile.Create(name, command.Description, command.Grants, menus);

        if (result.IsFailure)
        {
            return Result.Failure<Guid>(result.Error);
        }

        context.MenuAccessProfiles.Add(result.Value);

        await context.SaveChangesAsync(cancellationToken);

        return result.Value.Id;
    }
}

internal sealed class UpdateMenuAccessProfileCommandHandler(IApplicationDbContext context, ICacheInvalidator cache)
    : ICommandHandler<UpdateMenuAccessProfileCommand>
{
    public async Task<Result> Handle(UpdateMenuAccessProfileCommand command, CancellationToken cancellationToken)
    {
        MenuAccessProfile? profile = await context.MenuAccessProfiles
            .Include(p => p.Items)
            .SingleOrDefaultAsync(p => p.Id == command.ProfileId, cancellationToken);

        if (profile is null)
        {
            return Result.Failure(MenuAccessProfileErrors.NotFound(command.ProfileId));
        }

        string name = command.Name.Trim();

        if (await context.MenuAccessProfiles.AnyAsync(p => p.Id != command.ProfileId && p.Name == name, cancellationToken))
        {
            return Result.Failure(MenuAccessProfileErrors.NameNotUnique);
        }

        List<Menu> menus = await context.Menus.ToListAsync(cancellationToken);

        Result result = profile.Update(name, command.Description, command.Grants, menus);

        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        // Every user of the profile is affected: drop all cached access (cross-process).
        await cache.RemoveByTagAsync(PermissionCacheKeys.Tag, cancellationToken);

        return Result.Success();
    }
}

internal sealed class DuplicateMenuAccessProfileCommandHandler(IApplicationDbContext context)
    : ICommandHandler<DuplicateMenuAccessProfileCommand, Guid>
{
    public async Task<Result<Guid>> Handle(DuplicateMenuAccessProfileCommand command, CancellationToken cancellationToken)
    {
        MenuAccessProfile? source = await context.MenuAccessProfiles
            .AsNoTracking()
            .Include(p => p.Items)
            .SingleOrDefaultAsync(p => p.Id == command.ProfileId, cancellationToken);

        if (source is null)
        {
            return Result.Failure<Guid>(MenuAccessProfileErrors.NotFound(command.ProfileId));
        }

        string name = command.Name.Trim();

        if (await context.MenuAccessProfiles.AnyAsync(p => p.Name == name, cancellationToken))
        {
            return Result.Failure<Guid>(MenuAccessProfileErrors.NameNotUnique);
        }

        List<Menu> menus = await context.Menus.ToListAsync(cancellationToken);

        Result<MenuAccessProfile> copy = source.Duplicate(name, menus);

        if (copy.IsFailure)
        {
            return Result.Failure<Guid>(copy.Error);
        }

        context.MenuAccessProfiles.Add(copy.Value);

        await context.SaveChangesAsync(cancellationToken);

        return copy.Value.Id;
    }
}

internal sealed class DeleteMenuAccessProfileCommandHandler(IApplicationDbContext context)
    : ICommandHandler<DeleteMenuAccessProfileCommand>
{
    public async Task<Result> Handle(DeleteMenuAccessProfileCommand command, CancellationToken cancellationToken)
    {
        MenuAccessProfile? profile = await context.MenuAccessProfiles
            .SingleOrDefaultAsync(p => p.Id == command.ProfileId, cancellationToken);

        if (profile is null)
        {
            return Result.Failure(MenuAccessProfileErrors.NotFound(command.ProfileId));
        }

        Result deletable = profile.EnsureDeletable();
        if (deletable.IsFailure)
        {
            return deletable;
        }

        int userCount = await context.Users.CountAsync(u => u.MenuAccessProfileId == profile.Id, cancellationToken);
        if (userCount > 0)
        {
            return Result.Failure(MenuAccessProfileErrors.InUse(userCount));
        }

        context.MenuAccessProfiles.Remove(profile);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
