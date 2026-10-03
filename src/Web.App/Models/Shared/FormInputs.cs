using System.ComponentModel.DataAnnotations;
using Application.Common;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Web.App.Models.Shared;

/// <summary>
/// NPWP / NITKU / PKP fields of vendors, customers and farmers (validated by the domain).
/// </summary>
public sealed class TaxIdentityInput
{
    [StringLength(30)]
    [Display(Name = "NPWP")]
    public string? Npwp { get; set; }

    [StringLength(40)]
    [Display(Name = "NITKU")]
    public string? Nitku { get; set; }

    [Display(Name = "PKP (VAT registered)")]
    public bool? IsPkp { get; set; }

    public static TaxIdentityInput From(TaxIdentityResponse? response) => new()
    {
        Npwp = response?.Npwp,
        Nitku = response?.Nitku,
        IsPkp = response?.IsPkp
    };

    public TaxIdentityRequest ToRequest() => new(Npwp, Nitku, IsPkp == true);
}

public sealed class BankAccountInput
{
    [StringLength(100)]
    [Display(Name = "Bank")]
    public string? BankName { get; set; }

    [StringLength(50)]
    [Display(Name = "Account number")]
    public string? AccountNumber { get; set; }

    [StringLength(150)]
    [Display(Name = "Account holder")]
    public string? AccountHolderName { get; set; }

    public static BankAccountInput From(BankAccountResponse? response) => new()
    {
        BankName = response?.BankName,
        AccountNumber = response?.AccountNumber,
        AccountHolderName = response?.AccountHolderName
    };

    public BankAccountRequest ToRequest() => new(BankName, AccountNumber, AccountHolderName);
}

/// <summary>
/// Base of the master data forms: id (null = create), active flag, attachments and whether the user may save.
/// </summary>
public abstract class MasterFormViewModel
{
    public Guid? Id { get; set; }

    [Display(Name = "Active")]
    public bool? IsActive { get; set; }

    public List<Guid> Documents { get; set; } = [];

    [BindNever]
    public bool IsNew => Id is null;

    /// <summary>
    /// False when the user only has View: the form is shown read-only.
    /// </summary>
    [BindNever]
    public bool CanSave { get; set; } = true;

    [BindNever]
    public IReadOnlyList<Application.Documents.AttachmentResponse> Attachments { get; set; } = [];
}

