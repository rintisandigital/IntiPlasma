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
/// The menu shown to the current user. W0 serves a fixed menu; W1 replaces it with the database catalog
/// filtered by the user's Menu Access (only <c>CanView</c> items).
/// </summary>
public interface IMainboardMenuProvider
{
    Task<IReadOnlyList<MenuGroup>> GetMenuAsync(CancellationToken cancellationToken = default);
}

internal sealed class StaticMainboardMenuProvider(LinkGenerator links, IHttpContextAccessor httpContextAccessor)
    : IMainboardMenuProvider
{
    public Task<IReadOnlyList<MenuGroup>> GetMenuAsync(CancellationToken cancellationToken = default)
    {
        HttpContext httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("The menu requires an HTTP request.");

        string Url(string action, string controller) =>
            links.GetPathByAction(httpContext, action, controller) ?? "/";

        IReadOnlyList<MenuGroup> menu =
        [
            new MenuGroup("dashboard", "Dashboard", "ti ti-smart-home",
            [
                new MenuSection("Dashboard", [MenuItem.Link("dashboard", "Dashboard", Url("Dashboard", "Main"))])
            ]),
            new MenuGroup("account", "My Account", "ti ti-user-circle",
            [
                new MenuSection("My Account", [MenuItem.Link("account.password", "Change Password", Url("ChangePassword", "Account"))])
            ])
        ];

        return Task.FromResult(menu);
    }
}
