using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Access.Menus;
using Domain.Access;
using SharedKernel;
using Web.App.Infrastructure.Authorization;

namespace Web.App.Infrastructure.Navigation;

/// <summary>
/// A module in the mainboard sidebar: an icon tab in the left column and its menu in the right column.
/// </summary>
public sealed record MenuGroup(string Code, string Title, string Icon, IReadOnlyList<MenuSection> Sections);

public sealed record MenuSection(string Title, IReadOnlyList<MenuItem> Items);

/// <param name="Url">Page opened in the content iframe; null for a submenu that only holds children.</param>
public sealed record MenuItem(string Code, string Title, string? Url, IReadOnlyList<MenuItem> Children)
{
    public static MenuItem Link(string code, string title, string url) => new(code, title, url, []);
}

/// <summary>
/// The menu shown to the current user: the built-in Dashboard plus every usable catalog page (released,
/// active, in the catalog) the user may view, grouped by module. Groups without such a page are hidden.
/// </summary>
public interface IMainboardMenuProvider
{
    Task<IReadOnlyList<MenuGroup>> GetMenuAsync(CancellationToken cancellationToken = default);
}

internal sealed class DatabaseMainboardMenuProvider(
    IQueryHandler<GetMenusQuery, IReadOnlyList<MenuResponse>> menusQuery,
    IMenuRights menuRights,
    LinkGenerator links,
    IHttpContextAccessor httpContextAccessor) : IMainboardMenuProvider
{
    public async Task<IReadOnlyList<MenuGroup>> GetMenuAsync(CancellationToken cancellationToken = default)
    {
        HttpContext httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("The menu requires an HTTP request.");

        string dashboardUrl = links.GetPathByAction(httpContext, "Dashboard", "Main") ?? "/";
        string pathBase = httpContext.Request.PathBase;

        var groups = new List<MenuGroup>
        {
            new("dashboard", "Dashboard", "ti ti-smart-home",
                [new MenuSection("Dashboard", [MenuItem.Link("dashboard", "Dashboard", dashboardUrl)])])
        };

        Result<IReadOnlyList<MenuResponse>> menus = await menusQuery.Handle(new GetMenusQuery(), cancellationToken);
        if (menus.IsFailure)
        {
            return groups;
        }

        MenuAccess access = await menuRights.GetAsync();

        foreach (MenuResponse group in menus.Value.Where(m => m.ParentCode is null && m.InCatalog && m.IsActive))
        {
            List<MenuItem> items = [.. menus.Value
                .Where(m => m.ParentCode == group.Code && m.Route is not null && access.Has(m.Code, MenuRights.View))
                .Select(m => MenuItem.Link(m.Code, m.Name, pathBase + m.Route))];

            if (items.Count > 0)
            {
                groups.Add(new MenuGroup(group.Code, group.Name, group.Icon ?? "ti ti-folder", [new MenuSection(group.Name, items)]));
            }
        }

        return groups;
    }
}
