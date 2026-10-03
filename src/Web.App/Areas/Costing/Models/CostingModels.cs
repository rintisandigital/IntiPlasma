using System.ComponentModel.DataAnnotations;
using Application.Costing;
using Application.Cycles;
using Application.Documents;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Web.App.Areas.Costing.Models;

// ---- Cycle cost --------------------------------------------------------------------------------------------

/// <param name="Settlement">The cycle's active settlement, if any (plasma cycles).</param>
public sealed record CycleCostDetailsViewModel(
    CycleCostResponse Cost,
    CycleResponse Cycle,
    PlasmaSettlementResponse? Settlement,
    Guid? CoopWarehouseId,
    bool CanPrint,
    bool CanCreateSettlement)
{
    public bool IsPlasma => Cycle.ContractId is not null;

    public bool IsSettleable => IsPlasma && Settlement is null && Cycle.Status == "Closed";
}

// ---- Plasma settlements ------------------------------------------------------------------------------------

/// <summary>
/// Create (cycle chosen first) or recalculate (<see cref="Id"/> set) a draft settlement.
/// </summary>
public sealed class SettlementFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [Display(Name = "Cycle")]
    public Guid? CycleId { get; set; }

    [Required]
    [Display(Name = "Settlement date")]
    public DateOnly? SettlementDate { get; set; }

    [Required]
    [Range(0, 100_000_000_000)]
    [Display(Name = "Debt deduction (potongan hutang)")]
    public decimal? DebtDeduction { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public List<Guid> Documents { get; set; } = [];

    [BindNever]
    public string? Number { get; set; }

    [BindNever]
    public CycleResponse? Cycle { get; set; }

    [BindNever]
    public FarmerPlasmaDebtResponse? FarmerDebt { get; set; }

    [BindNever]
    public IReadOnlyList<AttachmentResponse> Attachments { get; set; } = [];

    public bool IsRecalculation => Id is not null;
}

/// <param name="FarmerDebt">The farmer's plasma debt known from settlements (shown while the settlement is a draft).</param>
public sealed record SettlementDetailsViewModel(
    PlasmaSettlementResponse Settlement,
    IReadOnlyList<AttachmentResponse> Attachments,
    FarmerPlasmaDebtResponse? FarmerDebt,
    bool CanEdit,
    bool CanPrint,
    bool CanPay)
{
    public bool IsDraft => Settlement.Status == "Draft";

    public bool IsPayable => Settlement.Status is "Approved" or "PartiallyPaid" && Settlement.Outstanding > 0;

    public bool IsLoss => Settlement.GrossIncome < 0;

    /// <summary>
    /// Calculation lines grouped by type, in the order of the slip.
    /// </summary>
    public IEnumerable<IGrouping<string, PlasmaSettlementLineResponse>> LineGroups =>
        (Settlement.Lines ?? []).GroupBy(l => l.Type).OrderBy(g => SettlementLines.Order(g.Key));
}

/// <summary>
/// Display names and order of the settlement line types.
/// </summary>
public static class SettlementLines
{
    private static readonly string[] Types = ["LiveBirdValue", "ProfitShare", "InputCharge", "Bonus", "Penalty"];

    public static int Order(string type) => Array.IndexOf(Types, type) is var index and >= 0 ? index : Types.Length;

    public static string Title(string type) => type switch
    {
        "LiveBirdValue" => "Live birds at the guaranteed price",
        "InputCharge" => "Sapronak at the contract price",
        "ProfitShare" => "Profit share",
        "Bonus" => "Bonus / incentive",
        "Penalty" => "Penalty",
        _ => type
    };

    public static string Scheme(string scheme) => scheme switch
    {
        "PriceContract" => "Price contract",
        "ProfitSharing" => "Profit sharing",
        _ => scheme
    };
}
