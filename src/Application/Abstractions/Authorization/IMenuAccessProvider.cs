using Domain.Access;

namespace Application.Abstractions.Authorization;

/// <summary>
/// Web.App menu rights of a user, from their menu access profile ("Akses Menu", W-2/W-15).
/// </summary>
public interface IMenuAccessProvider
{
    Task<MenuAccess> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Effective rights per menu code. Only usable menus (in the catalog, released and active) are present, and
/// every right is limited to what the menu offers.
/// </summary>
public sealed class MenuAccess(IReadOnlyDictionary<string, MenuRights> rights)
{
    public static readonly MenuAccess None = new(new Dictionary<string, MenuRights>());

    public IReadOnlyDictionary<string, MenuRights> Rights { get; } = rights;

    public MenuRights RightsFor(string menuCode) =>
        Rights.TryGetValue(menuCode, out MenuRights granted) ? granted : MenuRights.None;

    public bool Has(string menuCode, MenuRights right) =>
        right != MenuRights.None && (RightsFor(menuCode) & right) == right;
}
