using SharedKernel;

namespace Domain.Access;

/// <summary>
/// A Web.App menu: either a module group (sidebar icon tab, no route) or a page inside a group. The structure
/// comes from the code catalog (synchronized at startup); the administrator may customize the display name,
/// icon, order and active flag without a deployment.
/// </summary>
public sealed class Menu : AggregateRoot
{
    private Menu(Guid id, MenuDefinition definition)
        : base(id)
    {
        Code = definition.Code;
        Name = definition.Name;
        Icon = definition.Icon;
        SortOrder = definition.SortOrder;
        IsActive = true;
        ApplyStructure(definition);
    }

    private Menu()
    {
    }

    public string Code { get; private set; }

    /// <summary>
    /// Code of the module group; null for a group itself.
    /// </summary>
    public string? ParentCode { get; private set; }

    /// <summary>
    /// Name shown in the sidebar (may be customized).
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Name from the code catalog.
    /// </summary>
    public string DefaultName { get; private set; }

    public string? Icon { get; private set; }

    /// <summary>
    /// App-relative path of the page; null for a group.
    /// </summary>
    public string? Route { get; private set; }

    public int SortOrder { get; private set; }

    /// <summary>
    /// Set by the administrator: an inactive menu is hidden and blocked for everyone.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// The page has been released (W-25: unreleased menus are hidden from the sidebar but can already be granted).
    /// </summary>
    public bool IsAvailable { get; private set; }

    /// <summary>
    /// False once the menu disappears from the code catalog (kept so existing grants are not lost).
    /// </summary>
    public bool InCatalog { get; private set; }

    public bool SupportsCreate { get; private set; }
    public bool SupportsEdit { get; private set; }
    public bool SupportsDelete { get; private set; }
    public bool SupportsExport { get; private set; }

    /// <summary>
    /// Name, icon, order or active flag were changed by the administrator; the catalog no longer overwrites them.
    /// </summary>
    public bool IsCustomized { get; private set; }

    public bool IsGroup => ParentCode is null;

    /// <summary>
    /// A menu is usable (shown and accessible) only when it is in the catalog, released and active.
    /// </summary>
    public bool IsUsable => InCatalog && IsAvailable && IsActive;

    /// <summary>
    /// Every page supports View; groups support no right at all (they are derived from their pages).
    /// </summary>
    public MenuRights SupportedRights
    {
        get
        {
            if (IsGroup)
            {
                return MenuRights.None;
            }

            MenuRights rights = MenuRights.View;
            rights |= SupportsCreate ? MenuRights.Create : MenuRights.None;
            rights |= SupportsEdit ? MenuRights.Edit : MenuRights.None;
            rights |= SupportsDelete ? MenuRights.Delete : MenuRights.None;
            rights |= SupportsExport ? MenuRights.Export : MenuRights.None;
            return rights;
        }
    }

    public static Menu Create(MenuDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new Menu(Guid.CreateVersion7(), definition);
    }

    /// <summary>
    /// Applies the catalog entry. Display fields follow the catalog only while the menu is not customized.
    /// Returns true when anything changed.
    /// </summary>
    public bool Sync(MenuDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        string before = Snapshot();

        ApplyStructure(definition);

        if (!IsCustomized)
        {
            Name = definition.Name;
            Icon = definition.Icon;
            SortOrder = definition.SortOrder;
        }

        return before != Snapshot();
    }

    /// <summary>
    /// The menu is no longer part of the code catalog. Returns true when it was still in the catalog.
    /// </summary>
    public bool RemoveFromCatalog()
    {
        if (!InCatalog)
        {
            return false;
        }

        InCatalog = false;
        return true;
    }

    public Result Customize(string name, string? icon, int sortOrder, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(MenuErrors.NameRequired);
        }

        Name = name.Trim();
        Icon = string.IsNullOrWhiteSpace(icon) ? null : icon.Trim();
        SortOrder = sortOrder;
        IsActive = isActive;
        IsCustomized = true;

        return Result.Success();
    }

    private void ApplyStructure(MenuDefinition definition)
    {
        ParentCode = definition.ParentCode;
        DefaultName = definition.Name;
        Route = definition.Route;
        IsAvailable = definition.IsAvailable;
        InCatalog = true;
        SupportsCreate = definition.Supports.HasFlag(MenuRights.Create);
        SupportsEdit = definition.Supports.HasFlag(MenuRights.Edit);
        SupportsDelete = definition.Supports.HasFlag(MenuRights.Delete);
        SupportsExport = definition.Supports.HasFlag(MenuRights.Export);
    }

    private string Snapshot() =>
        string.Join('|', ParentCode, Name, DefaultName, Icon, Route, SortOrder, IsAvailable, InCatalog,
            SupportsCreate, SupportsEdit, SupportsDelete, SupportsExport);
}

/// <summary>
/// One entry of the Web.App code catalog.
/// </summary>
/// <param name="ParentCode">Group code; null for a group.</param>
/// <param name="Route">Page path; null for a group.</param>
/// <param name="Supports">Rights besides View that the page offers (Create/Edit/Delete/Export).</param>
/// <param name="IsAvailable">The page is released.</param>
public sealed record MenuDefinition(
    string Code,
    string? ParentCode,
    string Name,
    string? Icon,
    string? Route,
    int SortOrder,
    MenuRights Supports,
    bool IsAvailable);
