using SharedKernel;

namespace Domain.Access;

/// <summary>
/// "Akses Menu" (W-15): a named set of rights per Web.App menu, chosen per user. One profile can be used by
/// many users (W-18). The system profile Full Access grants every right on every menu and is read-only.
/// </summary>
public sealed class MenuAccessProfile : AggregateRoot
{
    /// <summary>
    /// Fixed id of the system profile, referenced by the PhaseW1 migration and the seeder.
    /// </summary>
    public static readonly Guid FullAccessId = Guid.Parse("0199a3c0-0000-7000-8000-000000000001");

    public const string FullAccessName = "Full Access";

    private readonly List<MenuAccessItem> _items = [];

    private MenuAccessProfile(Guid id, string name, string? description, bool isSystem)
        : base(id)
    {
        Name = name;
        Description = description;
        IsSystem = isSystem;
    }

    private MenuAccessProfile()
    {
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }

    /// <summary>
    /// Full Access: rights are computed (every right on every usable menu), no item rows are stored.
    /// </summary>
    public bool IsSystem { get; private set; }

    public bool IsFullAccess => IsSystem;

    public IReadOnlyCollection<MenuAccessItem> Items => [.. _items];

    public static Result<MenuAccessProfile> Create(
        string name,
        string? description,
        IEnumerable<MenuAccessGrant> grants,
        IReadOnlyCollection<Menu> menus)
    {
        var profile = new MenuAccessProfile(Guid.CreateVersion7(), name.Trim(), Normalize(description), isSystem: false);

        Result result = profile.ApplyGrants(grants, menus);

        return result.IsSuccess ? profile : Result.Failure<MenuAccessProfile>(result.Error);
    }

    public static MenuAccessProfile CreateFullAccess() =>
        new(FullAccessId, FullAccessName, "Every right on every menu (system profile).", isSystem: true);

    public Result Update(string name, string? description, IEnumerable<MenuAccessGrant> grants, IReadOnlyCollection<Menu> menus)
    {
        if (IsSystem)
        {
            return Result.Failure(MenuAccessProfileErrors.SystemReadOnly);
        }

        Name = name.Trim();
        Description = Normalize(description);

        return ApplyGrants(grants, menus);
    }

    /// <summary>
    /// A new profile with the same rights (Full Access duplicates into an explicit copy of every right).
    /// </summary>
    public Result<MenuAccessProfile> Duplicate(string name, IReadOnlyCollection<Menu> menus)
    {
        ArgumentNullException.ThrowIfNull(menus);

        IEnumerable<MenuAccessGrant> grants = IsSystem
            ? menus.Where(m => !m.IsGroup).Select(m => new MenuAccessGrant(m.Id, m.SupportedRights))
            : _items.Select(i => new MenuAccessGrant(i.MenuId, i.Rights));

        return Create(name, Description, grants, menus);
    }

    public Result EnsureDeletable() =>
        IsSystem ? Result.Failure(MenuAccessProfileErrors.SystemReadOnly) : Result.Success();

    /// <summary>
    /// Replaces the rights. A right other than View requires View; rights the menu does not offer are rejected;
    /// menus without any right are dropped.
    /// </summary>
    private Result ApplyGrants(IEnumerable<MenuAccessGrant> grants, IReadOnlyCollection<Menu> menus)
    {
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(menus);

        var menusById = menus.ToDictionary(m => m.Id);
        var desired = new Dictionary<Guid, MenuRights>();

        foreach (MenuAccessGrant grant in grants.Where(g => g.Rights != MenuRights.None))
        {
            if (!menusById.TryGetValue(grant.MenuId, out Menu? menu))
            {
                return Result.Failure(MenuErrors.NotFound(grant.MenuId));
            }

            if (!grant.Rights.HasFlag(MenuRights.View))
            {
                return Result.Failure(MenuAccessProfileErrors.ViewRequired(menu.Code));
            }

            if ((grant.Rights & ~menu.SupportedRights) != MenuRights.None)
            {
                return Result.Failure(MenuAccessProfileErrors.RightNotSupported(menu.Code));
            }

            desired[grant.MenuId] = grant.Rights;
        }

        _items.RemoveAll(i => !desired.ContainsKey(i.MenuId));

        foreach ((Guid menuId, MenuRights rights) in desired)
        {
            MenuAccessItem? existing = _items.Find(i => i.MenuId == menuId);
            if (existing is null)
            {
                _items.Add(new MenuAccessItem(Id, menuId, rights));
            }
            else
            {
                existing.SetRights(rights);
            }
        }

        return Result.Success();
    }

    private static string? Normalize(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}

/// <summary>
/// Rights of one profile on one menu.
/// </summary>
public sealed class MenuAccessItem
{
    internal MenuAccessItem(Guid profileId, Guid menuId, MenuRights rights)
    {
        ProfileId = profileId;
        MenuId = menuId;
        SetRights(rights);
    }

    private MenuAccessItem()
    {
    }

    public Guid ProfileId { get; private set; }
    public Guid MenuId { get; private set; }
    public bool CanView { get; private set; }
    public bool CanCreate { get; private set; }
    public bool CanEdit { get; private set; }
    public bool CanDelete { get; private set; }
    public bool CanExport { get; private set; }

    public MenuRights Rights =>
        (CanView ? MenuRights.View : MenuRights.None) |
        (CanCreate ? MenuRights.Create : MenuRights.None) |
        (CanEdit ? MenuRights.Edit : MenuRights.None) |
        (CanDelete ? MenuRights.Delete : MenuRights.None) |
        (CanExport ? MenuRights.Export : MenuRights.None);

    internal void SetRights(MenuRights rights)
    {
        CanView = rights.HasFlag(MenuRights.View);
        CanCreate = rights.HasFlag(MenuRights.Create);
        CanEdit = rights.HasFlag(MenuRights.Edit);
        CanDelete = rights.HasFlag(MenuRights.Delete);
        CanExport = rights.HasFlag(MenuRights.Export);
    }
}

public sealed record MenuAccessGrant(Guid MenuId, MenuRights Rights);
