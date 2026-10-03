using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Caching;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using Domain.Access;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Access.Menus;

/// <summary>
/// Brings the menus table in line with the Web.App code catalog (run at Web.App startup): adds new menus,
/// updates structure, keeps administrator customizations, and takes menus missing from the catalog out of use.
/// </summary>
public sealed record SyncMenuCatalogCommand(IReadOnlyList<MenuDefinition> Definitions) : ICommand<int>;

public sealed record GetMenusQuery : IQuery<IReadOnlyList<MenuResponse>>;

public sealed record MenuResponse
{
    public Guid Id { get; init; }

    public string Code { get; init; }

    public string? ParentCode { get; init; }

    public string Name { get; init; }

    public string DefaultName { get; init; }

    public string? Icon { get; init; }

    public string? Route { get; init; }

    public int SortOrder { get; init; }

    public bool IsActive { get; init; }

    public bool IsAvailable { get; init; }

    public bool InCatalog { get; init; }

    public bool IsCustomized { get; init; }

    public bool SupportsCreate { get; init; }

    public bool SupportsEdit { get; init; }

    public bool SupportsDelete { get; init; }

    public bool SupportsExport { get; init; }
}

public sealed record UpdateMenuCommand(Guid MenuId, string Name, string? Icon, int SortOrder, bool IsActive) : ICommand;

internal sealed class SyncMenuCatalogCommandValidator : AbstractValidator<SyncMenuCatalogCommand>
{
    public SyncMenuCatalogCommandValidator()
    {
        RuleFor(c => c.Definitions).NotEmpty();
        RuleFor(c => c.Definitions)
            .Must(d => d.Select(x => x.Code).Distinct(StringComparer.Ordinal).Count() == d.Count)
            .WithMessage("Menu codes in the catalog must be unique.");
    }
}

internal sealed class UpdateMenuCommandValidator : AbstractValidator<UpdateMenuCommand>
{
    public UpdateMenuCommandValidator()
    {
        RuleFor(c => c.MenuId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Icon).MaximumLength(100);
        RuleFor(c => c.SortOrder).InclusiveBetween(0, 9999);
    }
}

internal sealed class SyncMenuCatalogCommandHandler(IApplicationDbContext context, ICacheInvalidator cache)
    : ICommandHandler<SyncMenuCatalogCommand, int>
{
    public async Task<Result<int>> Handle(SyncMenuCatalogCommand command, CancellationToken cancellationToken)
    {
        var existing = (await context.Menus.ToListAsync(cancellationToken))
            .ToDictionary(m => m.Code, StringComparer.Ordinal);

        int changes = 0;

        foreach (MenuDefinition definition in command.Definitions)
        {
            if (existing.Remove(definition.Code, out Menu? menu))
            {
                changes += menu.Sync(definition) ? 1 : 0;
            }
            else
            {
                context.Menus.Add(Menu.Create(definition));
                changes++;
            }
        }

        // Whatever is left is no longer in the code catalog.
        changes += existing.Values.Count(m => m.RemoveFromCatalog());

        if (changes > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            await cache.RemoveByTagAsync(PermissionCacheKeys.Tag, cancellationToken);
        }

        return changes;
    }
}

internal sealed class GetMenusQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetMenusQuery, IReadOnlyList<MenuResponse>>
{
    public async Task<Result<IReadOnlyList<MenuResponse>>> Handle(GetMenusQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        const string sql =
            """
            SELECT m.id AS Id, m.code AS Code, m.parent_code AS ParentCode, m.name AS Name, m.default_name AS DefaultName,
                   m.icon AS Icon, m.route AS Route, m.sort_order AS SortOrder, m.is_active AS IsActive,
                   m.is_available AS IsAvailable, m.in_catalog AS InCatalog, m.is_customized AS IsCustomized,
                   m.supports_create AS SupportsCreate, m.supports_edit AS SupportsEdit,
                   m.supports_delete AS SupportsDelete, m.supports_export AS SupportsExport
            FROM identity.menus m
            LEFT JOIN identity.menus g ON g.code = m.parent_code
            ORDER BY COALESCE(g.sort_order, m.sort_order), COALESCE(g.code, m.code), m.parent_code NULLS FIRST, m.sort_order, m.name
            """;

        IEnumerable<MenuResponse> menus = await connection.QueryAsync<MenuResponse>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return menus.ToList();
    }
}

internal sealed class UpdateMenuCommandHandler(IApplicationDbContext context, ICacheInvalidator cache)
    : ICommandHandler<UpdateMenuCommand>
{
    public async Task<Result> Handle(UpdateMenuCommand command, CancellationToken cancellationToken)
    {
        Menu? menu = await context.Menus.SingleOrDefaultAsync(m => m.Id == command.MenuId, cancellationToken);

        if (menu is null)
        {
            return Result.Failure(MenuErrors.NotFound(command.MenuId));
        }

        Result result = menu.Customize(command.Name, command.Icon, command.SortOrder, command.IsActive);

        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        await cache.RemoveByTagAsync(PermissionCacheKeys.Tag, cancellationToken);

        return Result.Success();
    }
}
