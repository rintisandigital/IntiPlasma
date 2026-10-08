using System.ComponentModel.DataAnnotations;
using Application.Contracts;
using Application.Documents;
using Domain.MasterData.Coops;
using Domain.MasterData.Farmers;
using Domain.Partnership.Contracts;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Web.App.Models.Shared;

namespace Web.App.Areas.Partnership.Models;

// ---- Farmers -----------------------------------------------------------------------------------------------

public sealed class FarmerFormViewModel : MasterFormViewModel
{
    [Required]
    [StringLength(30)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public FarmerType? Type { get; set; }

    [Required]
    [Display(Name = "Branch")]
    public Guid? BranchId { get; set; }

    [StringLength(16)]
    [Display(Name = "NIK (required for plasma)")]
    public string? Nik { get; set; }

    public TaxIdentityInput TaxIdentity { get; set; } = new();

    [StringLength(250)]
    public string? Address { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    public BankAccountInput BankAccount { get; set; } = new();

    [BindNever]
    public int CoopCount { get; set; }

    [BindNever]
    public IReadOnlyList<SelectListItem> Branches { get; set; } = [];
}

// ---- Coops -------------------------------------------------------------------------------------------------

public sealed class CoopFormViewModel : MasterFormViewModel
{
    [Required]
    [Display(Name = "Farmer")]
    public Guid? FarmerId { get; set; }

    [Required]
    [StringLength(30)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Range(1, 1_000_000)]
    [Display(Name = "Capacity (birds)")]
    public int? Capacity { get; set; }

    [Required]
    [Display(Name = "House type")]
    public HouseType? HouseType { get; set; }

    [StringLength(250)]
    public string? Address { get; set; }

    [Range(-90, 90)]
    public decimal? Latitude { get; set; }

    [Range(-180, 180)]
    public decimal? Longitude { get; set; }

    /// <summary>
    /// Survey data (data kandang); validated by the use case.
    /// </summary>
    public CoopProfile Profile { get; set; } = new();

    [BindNever]
    public string? FarmerLabel { get; set; }

    [BindNever]
    public string? BranchCode { get; set; }

    [BindNever]
    public Guid? OpenCycleId { get; set; }
}

// ---- Contracts ---------------------------------------------------------------------------------------------

public sealed class ContractFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(30)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Branch")]
    public Guid? BranchId { get; set; }

    [Required]
    public ContractScheme? Scheme { get; set; }

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Valid from")]
    public DateOnly? ValidFrom { get; set; }

    [Display(Name = "Valid to")]
    public DateOnly? ValidTo { get; set; }

    [Range(0, 100)]
    [Display(Name = "Plasma profit share %")]
    public decimal? PlasmaProfitSharePercent { get; set; }

    [Display(Name = "Income tax (PPh) on settlements")]
    public Guid? IncomeTaxCodeId { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public List<InputPriceInput> InputPrices { get; set; } = [];

    public List<LiveBirdPriceInput> LiveBirdPrices { get; set; } = [];

    public List<IncentiveInput> Incentives { get; set; } = [];

    public List<Guid> Documents { get; set; } = [];

    [BindNever]
    public bool IsNew => Id is null;

    [BindNever]
    public IReadOnlyList<SelectListItem> Branches { get; set; } = [];

    [BindNever]
    public IReadOnlyList<SelectListItem> IncomeTaxCodes { get; set; } = [];

    [BindNever]
    public IReadOnlyList<AttachmentResponse> Attachments { get; set; } = [];

    public ContractTermsRequest ToTerms() => new(
        Name,
        ValidFrom!.Value,
        ValidTo,
        Scheme == ContractScheme.ProfitSharing ? PlasmaProfitSharePercent : null,
        IncomeTaxCodeId,
        Notes,
        [.. InputPrices.Select(p => new ContractTermsRequest.InputPrice(p.ItemId!.Value, p.Price!.Value))],
        [.. LiveBirdPrices.Select(p => new ContractTermsRequest.LiveBirdPrice(p.MinWeightKg!.Value, p.MaxWeightKg!.Value, p.PricePerKg!.Value))],
        [.. Incentives.Select(i => new ContractTermsRequest.Incentive(
            i.Name, i.Kind!.Value, i.Metric!.Value, i.RangeFrom, i.RangeTo, i.Amount!.Value, i.Basis!.Value))]);

    public static ContractFormViewModel From(ContractResponse contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        return new ContractFormViewModel
        {
            Id = contract.Id,
            Code = contract.Code,
            BranchId = contract.BranchId,
            Scheme = Enum.Parse<ContractScheme>(contract.Scheme),
            Name = contract.Name,
            ValidFrom = contract.ValidFrom,
            ValidTo = contract.ValidTo,
            PlasmaProfitSharePercent = contract.PlasmaProfitSharePercent,
            IncomeTaxCodeId = contract.IncomeTaxCodeId,
            Notes = contract.Notes,
            InputPrices = [.. (contract.InputPrices ?? []).Select(p => new InputPriceInput
            {
                ItemId = p.ItemId,
                ItemLabel = $"{p.ItemCode} — {p.ItemName} ({p.UomCode})",
                Price = p.Price
            })],
            LiveBirdPrices = [.. (contract.LiveBirdPrices ?? []).Select(p => new LiveBirdPriceInput
            {
                MinWeightKg = p.MinWeightKg,
                MaxWeightKg = p.MaxWeightKg,
                PricePerKg = p.PricePerKg
            })],
            Incentives = [.. (contract.Incentives ?? []).Select(i => new IncentiveInput
            {
                Name = i.Name,
                Kind = Enum.Parse<IncentiveKind>(i.Kind),
                Metric = Enum.Parse<IncentiveMetric>(i.Metric),
                RangeFrom = i.RangeFrom,
                RangeTo = i.RangeTo,
                Amount = i.Amount,
                Basis = Enum.Parse<IncentiveBasis>(i.Basis)
            })],
            Documents = [.. contract.Documents]
        };
    }
}

public sealed class InputPriceInput
{
    [Required]
    public Guid? ItemId { get; set; }

    /// <summary>
    /// Shown for an existing row (posted back so a re-rendered form keeps the label).
    /// </summary>
    public string? ItemLabel { get; set; }

    [Required]
    [Range(0.01, 1_000_000_000)]
    public decimal? Price { get; set; }
}

public sealed class LiveBirdPriceInput
{
    [Required]
    [Range(0, 100)]
    public decimal? MinWeightKg { get; set; }

    [Required]
    [Range(0, 100)]
    public decimal? MaxWeightKg { get; set; }

    [Required]
    [Range(0.01, 1_000_000)]
    public decimal? PricePerKg { get; set; }
}

public sealed class IncentiveInput
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public IncentiveKind? Kind { get; set; }

    [Required]
    public IncentiveMetric? Metric { get; set; }

    public decimal? RangeFrom { get; set; }

    public decimal? RangeTo { get; set; }

    [Required]
    [Range(0.01, 1_000_000_000)]
    public decimal? Amount { get; set; }

    [Required]
    public IncentiveBasis? Basis { get; set; }
}

public sealed record ContractDetailsViewModel(
    ContractResponse Contract,
    IReadOnlyList<AttachmentResponse> Attachments,
    bool CanEditDraft,
    bool CanChangeStatus,
    bool CanManageAttachments);
