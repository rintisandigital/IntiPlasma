using System.ComponentModel.DataAnnotations;
using Domain.MasterData.Items;
using Domain.MasterData.TaxCodes;
using Domain.MasterData.Warehouses;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;
using Web.App.Models.Shared;

namespace Web.App.Areas.MasterData.Models;

/// <summary>
/// A list page: rows plus the filters as entered (kept in the filter bar and the export links).
/// </summary>
public sealed class ListViewModel<T>
{
    public required PagedList<T> Rows { get; init; }

    public string? Search { get; init; }

    public string? Branch { get; init; }

    public IReadOnlyList<SelectListItem> BranchOptions { get; init; } = [];

    public IReadOnlyDictionary<string, string?> Filters { get; init; } = new Dictionary<string, string?>();

    public IReadOnlyDictionary<string, IReadOnlyList<SelectListItem>> FilterOptions { get; init; } =
        new Dictionary<string, IReadOnlyList<SelectListItem>>();
}

// ---- Units of measure --------------------------------------------------------------------------------------

public sealed class UomFormViewModel : MasterFormViewModel
{
    [Required]
    [StringLength(10)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;
}

// ---- Tax codes ---------------------------------------------------------------------------------------------

public sealed class TaxCodeFormViewModel : MasterFormViewModel
{
    [Required]
    [StringLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public TaxType? Type { get; set; }

    [Display(Name = "VAT treatment")]
    public VatTreatment? VatTreatment { get; set; }

    [Display(Name = "Income tax article")]
    public IncomeTaxArticle? IncomeTaxArticle { get; set; }

    public List<TaxRateInput> Rates { get; set; } = [];
}

public sealed class TaxRateInput
{
    [Required]
    [Display(Name = "Effective from")]
    public DateOnly? EffectiveFrom { get; set; }

    [Required]
    [Range(0, 100)]
    [Display(Name = "Rate %")]
    public decimal? RatePercent { get; set; }

    [Required]
    [Range(0.0001, 1)]
    [Display(Name = "Tax base ratio")]
    public decimal? TaxBaseRatio { get; set; } = 1;
}

// ---- Items -------------------------------------------------------------------------------------------------

public sealed class ItemFormViewModel : MasterFormViewModel
{
    [Required]
    [StringLength(30)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public ItemCategory? Category { get; set; }

    [Required]
    [Display(Name = "Base unit")]
    public Guid? BaseUomId { get; set; }

    [Display(Name = "Tax code")]
    public Guid? TaxCodeId { get; set; }

    public List<ItemConversionInput> Conversions { get; set; } = [];

    [BindNever]
    public IReadOnlyList<SelectListItem> Uoms { get; set; } = [];

    [BindNever]
    public IReadOnlyList<SelectListItem> TaxCodes { get; set; } = [];
}

public sealed class ItemConversionInput
{
    [Required]
    [Display(Name = "Unit")]
    public Guid? UomId { get; set; }

    [Required]
    [Range(0.000001, 1_000_000)]
    public decimal? Factor { get; set; }
}

// ---- Warehouses --------------------------------------------------------------------------------------------

public sealed class WarehouseFormViewModel : MasterFormViewModel
{
    [Required]
    [StringLength(30)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Branch")]
    public Guid? BranchId { get; set; }

    [StringLength(250)]
    public string? Address { get; set; }

    [BindNever]
    public string? Type { get; set; }

    [BindNever]
    public string? CoopCode { get; set; }

    [BindNever]
    public IReadOnlyList<SelectListItem> Branches { get; set; } = [];
}

// ---- Vendors & customers -----------------------------------------------------------------------------------

public sealed class VendorFormViewModel : MasterFormViewModel
{
    [Required]
    [StringLength(30)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    public TaxIdentityInput TaxIdentity { get; set; } = new();

    [StringLength(250)]
    public string? Address { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    [EmailAddress]
    [StringLength(150)]
    public string? Email { get; set; }

    [Required]
    [Range(0, 365)]
    [Display(Name = "Payment term (days)")]
    public int? PaymentTermDays { get; set; }

    public BankAccountInput BankAccount { get; set; } = new();

    [Range(0, 100)]
    [Display(Name = "Price tolerance %")]
    public decimal? PriceTolerancePercent { get; set; }
}

public sealed class CustomerFormViewModel : MasterFormViewModel
{
    [Required]
    [StringLength(30)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    public TaxIdentityInput TaxIdentity { get; set; } = new();

    [StringLength(250)]
    public string? Address { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    [EmailAddress]
    [StringLength(150)]
    public string? Email { get; set; }

    [Required]
    [Range(0, 365)]
    [Display(Name = "Payment term (days)")]
    public int? PaymentTermDays { get; set; }

    [Required]
    [Range(0, 1_000_000_000_000)]
    [Display(Name = "Credit limit (Rp)")]
    public decimal? CreditLimit { get; set; }
}

/// <summary>
/// Enum options for dropdowns (value = enum name, as the use cases expect).
/// </summary>
public static class EnumOptions
{
    public static IReadOnlyList<SelectListItem> For<TEnum>(TEnum? selected = null)
        where TEnum : struct, Enum =>
        [.. Enum.GetValues<TEnum>().Select(v => new SelectListItem(Label(v.ToString()), v.ToString(), selected?.Equals(v) == true))];

    /// <summary>"Pph4Ayat2" → "Pph4 Ayat2", "OpenHouse" → "Open House"; "Coop" is shown as "Farm".</summary>
    public static string Label(string value) =>
        value == "Coop" ? "Farm" : System.Text.RegularExpressions.Regex.Replace(value, "(?<=[a-z0-9])(?=[A-Z])", " ");
}
