using System.ComponentModel.DataAnnotations;
using Application.Access.BranchAccessProfiles;
using Application.Access.MenuAccessProfiles;
using Application.Access.Menus;
using Application.Branches;
using Application.Roles.Get;
using Application.Users.GetById;
using Application.Users.Manage;
using Domain.Access;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;

namespace Web.App.Areas.Admin.Models;

// ---- Users -----------------------------------------------------------------------------------------------

public sealed class UserIndexViewModel
{
    public required PagedList<UserListItem> Users { get; init; }

    public string? Search { get; init; }

    public string? Status { get; init; }

    public Guid? MenuAccessProfileId { get; init; }

    public Guid? BranchAccessProfileId { get; init; }

    public IReadOnlyList<SelectListItem> MenuProfiles { get; init; } = [];

    public IReadOnlyList<SelectListItem> BranchProfiles { get; init; } = [];
}

public sealed record UserDetailsViewModel(UserResponse User, bool IsCurrentUser);

public sealed class UserCreateViewModel : IUserAccessForm
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [DataType(DataType.Password)]
    [Display(Name = "Initial password")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Display(Name = "Menu access")]
    public Guid? MenuAccessProfileId { get; set; }

    [Display(Name = "Branch access")]
    public Guid? BranchAccessProfileId { get; set; }

    [Display(Name = "Default branch")]
    public Guid? DefaultBranchId { get; set; }

    public List<Guid> RoleIds { get; set; } = [];

    public UserAccessOptions Options { get; set; } = new();
}

public sealed class UserEditViewModel
{
    [Required]
    public Guid? Id { get; set; }

    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;
}

public sealed class UserAccessViewModel : IUserAccessForm
{
    [Required]
    public Guid? Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    [Display(Name = "Menu access")]
    public Guid? MenuAccessProfileId { get; set; }

    [Display(Name = "Branch access")]
    public Guid? BranchAccessProfileId { get; set; }

    [Display(Name = "Default branch")]
    public Guid? DefaultBranchId { get; set; }

    public List<Guid> RoleIds { get; set; } = [];

    public UserAccessOptions Options { get; set; } = new();
}

/// <summary>
/// Access fields shared by the create and change-access forms (partial _AccessFields).
/// </summary>
public interface IUserAccessForm
{
    Guid? MenuAccessProfileId { get; }

    Guid? BranchAccessProfileId { get; }

    Guid? DefaultBranchId { get; }

    List<Guid> RoleIds { get; }

    UserAccessOptions Options { get; }
}

public sealed class UserAccessOptions
{
    public IReadOnlyList<SelectListItem> MenuProfiles { get; init; } = [];

    public IReadOnlyList<SelectListItem> BranchProfiles { get; init; } = [];

    public IReadOnlyList<SelectListItem> DefaultBranches { get; init; } = [];

    public IReadOnlyList<RoleResponse> Roles { get; init; } = [];
}

public sealed class ResetPasswordInput
{
    [Required]
    [MinLength(8)]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class DeleteUserInput
{
    [StringLength(500)]
    public string? Reason { get; set; }
}

// ---- Menu access -----------------------------------------------------------------------------------------

public sealed class MenuAccessFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [BindNever]
    public bool IsSystem { get; set; }

    [BindNever]
    public long UserCount { get; set; }

    public List<MenuAccessRowInput> Rows { get; set; } = [];

    /// <summary>
    /// Fills the display fields (code, names, supported rights) from the catalog, keeping the posted rights.
    /// </summary>
    public void ApplyCatalog(IReadOnlyList<MenuResponse> menus, IReadOnlyDictionary<Guid, MenuRights>? rights = null)
    {
        ArgumentNullException.ThrowIfNull(menus);

        var posted = Rows.ToDictionary(r => r.MenuId);
        var groups = menus.Where(m => m.ParentCode is null).ToDictionary(m => m.Code, StringComparer.Ordinal);

        Rows = [.. menus
            .Where(m => m.ParentCode is not null && m.InCatalog && groups.ContainsKey(m.ParentCode))
            .Select(m =>
            {
                MenuRights current = MenuRights.None;
                if (rights is not null && rights.TryGetValue(m.Id, out MenuRights stored))
                {
                    current = stored;
                }

                MenuAccessRowInput row = posted.TryGetValue(m.Id, out MenuAccessRowInput? input)
                    ? input
                    : MenuAccessRowInput.From(m.Id, current);

                row.Code = m.Code;
                row.Name = m.Name;
                row.GroupCode = m.ParentCode!;
                row.GroupName = groups[m.ParentCode!].Name;
                row.IsAvailable = m.IsAvailable;
                row.IsActive = m.IsActive;
                row.Supported = MenuAccessRowInput.Supports(m);
                return row;
            })];
    }

    public IReadOnlyList<MenuAccessGrant> ToGrants() =>
        [.. Rows.Select(r => new MenuAccessGrant(r.MenuId, r.ToRights())).Where(g => g.Rights != MenuRights.None)];
}

public sealed class MenuAccessRowInput
{
    public Guid MenuId { get; set; }

    public bool? View { get; set; }

    public bool? Create { get; set; }

    public bool? Edit { get; set; }

    public bool? Delete { get; set; }

    public bool? Export { get; set; }

    // Display only (filled from the catalog, not posted).
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string GroupCode { get; set; } = string.Empty;

    public string GroupName { get; set; } = string.Empty;

    public bool IsAvailable { get; set; }

    public bool IsActive { get; set; }

    public MenuRights Supported { get; set; }

    public static MenuAccessRowInput From(Guid menuId, MenuRights rights) => new()
    {
        MenuId = menuId,
        View = rights.HasFlag(MenuRights.View),
        Create = rights.HasFlag(MenuRights.Create),
        Edit = rights.HasFlag(MenuRights.Edit),
        Delete = rights.HasFlag(MenuRights.Delete),
        Export = rights.HasFlag(MenuRights.Export)
    };

    public static MenuRights Supports(MenuResponse menu)
    {
        ArgumentNullException.ThrowIfNull(menu);

        MenuRights rights = MenuRights.View;
        rights |= menu.SupportsCreate ? MenuRights.Create : MenuRights.None;
        rights |= menu.SupportsEdit ? MenuRights.Edit : MenuRights.None;
        rights |= menu.SupportsDelete ? MenuRights.Delete : MenuRights.None;
        rights |= menu.SupportsExport ? MenuRights.Export : MenuRights.None;
        return rights;
    }

    public bool Has(MenuRights right) => (ToRights() & right) == right;

    public MenuRights ToRights()
    {
        MenuRights rights = View == true ? MenuRights.View : MenuRights.None;
        rights |= Create == true ? MenuRights.Create : MenuRights.None;
        rights |= Edit == true ? MenuRights.Edit : MenuRights.None;
        rights |= Delete == true ? MenuRights.Delete : MenuRights.None;
        rights |= Export == true ? MenuRights.Export : MenuRights.None;
        return rights;
    }
}

public sealed class DuplicateInput
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
}

// ---- Branch access ---------------------------------------------------------------------------------------

public sealed class BranchAccessFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Display(Name = "All branches")]
    public bool? AllBranches { get; set; }

    public List<Guid> BranchIds { get; set; } = [];

    [BindNever]
    public bool IsSystem { get; set; }

    [BindNever]
    public long UserCount { get; set; }

    public IReadOnlyList<BranchResponse> Branches { get; set; } = [];
}

public sealed record BranchAccessDetailsViewModel(BranchAccessProfileResponse Profile);

// ---- Menus -----------------------------------------------------------------------------------------------

public sealed class MenuEditViewModel
{
    [Required]
    public Guid? Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string DefaultName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "Icon (Tabler class)")]
    public string? Icon { get; set; }

    [Range(0, 9999)]
    [Display(Name = "Sort order")]
    public int? SortOrder { get; set; }

    [Display(Name = "Active")]
    public bool? IsActive { get; set; }

    [BindNever]
    public bool IsGroup { get; set; }
}

// ---- API roles -------------------------------------------------------------------------------------------

public sealed class ApiRoleFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [BindNever]
    public bool IsSystem { get; set; }

    public List<string> Permissions { get; set; } = [];

    /// <summary>
    /// The permission catalog grouped by module ("sales:approve" → "sales").
    /// </summary>
    public static IReadOnlyList<IGrouping<string, string>> Catalog =>
        [.. Domain.Roles.Permissions.All.GroupBy(p => p.Split(':')[0])];
}

// ---- Branches --------------------------------------------------------------------------------------------

public sealed class BranchFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(10)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Address { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    [Display(Name = "Active")]
    public bool? IsActive { get; set; }
}
