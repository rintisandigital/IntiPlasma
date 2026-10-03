using System.ComponentModel.DataAnnotations;
using Application.Costing;
using Application.Cycles;
using Application.Documents;
using Application.Inventory;
using Application.Production;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Web.App.Areas.Inventory.Models;

namespace Web.App.Areas.Production.Models;

// ---- Cycles ------------------------------------------------------------------------------------------------

public sealed class PlanCycleFormViewModel
{
    [Required]
    [Display(Name = "Coop")]
    public Guid? CoopId { get; set; }

    public string? CoopLabel { get; set; }

    /// <summary>
    /// Required for a plasma coop, empty for an inti coop.
    /// </summary>
    [Display(Name = "Partnership contract")]
    public Guid? ContractId { get; set; }

    public string? ContractLabel { get; set; }

    [Required]
    [Display(Name = "Planned chick-in date")]
    public DateOnly? PlannedChickInDate { get; set; }

    [Required]
    [Range(1, 1_000_000)]
    [Display(Name = "Planned population (birds)")]
    public int? PlannedPopulation { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }
}

public sealed class ChickInFormViewModel
{
    [Required]
    [Display(Name = "Chick-in date")]
    public DateOnly? ChickInDate { get; set; }

    public List<ChickInLineInput> Lines { get; set; } = [];

    public List<Guid> Documents { get; set; } = [];

    [BindNever]
    public CycleResponse? Cycle { get; set; }

    [BindNever]
    public Guid? CoopWarehouseId { get; set; }

    /// <summary>
    /// DOC on hand in the coop warehouse (what can be placed).
    /// </summary>
    [BindNever]
    public IReadOnlyList<StockBalanceResponse> DocStock { get; set; } = [];

    [BindNever]
    public IReadOnlyList<AttachmentResponse> Attachments { get; set; } = [];
}

public sealed class ChickInLineInput
{
    public Guid ItemId { get; set; }

    /// <summary>
    /// Birds placed from this DOC item; empty or zero = not placed.
    /// </summary>
    [Range(0, 1_000_000)]
    public int? Quantity { get; set; }
}

/// <summary>
/// The cycle page: header, progress steps and the tabs (overview, recordings, harvests, coop stock, performance,
/// cost, attachments).
/// </summary>
public sealed class CycleDetailsViewModel
{
    public required CycleResponse Cycle { get; init; }

    public CyclePerformanceResponse? Performance { get; init; }

    public CycleCostResponse? Cost { get; init; }

    public IReadOnlyList<DailyRecordingResponse> Recordings { get; init; } = [];

    public IReadOnlyList<StockBalanceResponse> CoopStock { get; init; } = [];

    public Guid? CoopWarehouseId { get; init; }

    public IReadOnlyList<AttachmentResponse> Attachments { get; init; } = [];

    public string Tab { get; init; } = "overview";

    public bool CanEdit { get; init; }

    public bool CanRecord { get; init; }

    public bool CanHarvest { get; init; }

    public bool CanPrint { get; init; }

    public bool CanTransfer { get; init; }

    /// <summary>
    /// The plasma cycle's active settlement (id, number), if any.
    /// </summary>
    public (Guid Id, string Number)? Settlement { get; init; }

    public bool CanCreateSettlement { get; init; }

    public bool CanViewCost { get; init; }

    public bool IsSettleable => Cycle.Status == "Closed" && Cycle.ContractId is not null && Settlement is null;

    public bool IsPlanned => Cycle.Status == "Planned";

    public bool IsRecordable => Cycle.Status is "Active" or "Harvesting";

    public bool IsClosed => Cycle.Status is "Closed" or "Settled";

    public decimal DocInCoop => CoopStock.Where(s => s.ItemCategory == "Doc").Sum(s => s.Quantity);

    /// <summary>
    /// 1 planned, 2 DOC in the coop warehouse, 3 chick-in done, 4 harvesting, 5 closed (0 = cancelled).
    /// </summary>
    public int Step => Cycle.Status switch
    {
        "Planned" => DocInCoop > 0 ? 2 : 1,
        "Active" => 3,
        "Harvesting" => 4,
        "Closed" or "Settled" => 5,
        _ => 0
    };
}

// ---- Daily recordings ----------------------------------------------------------------------------------------

public sealed class RecordingFormViewModel
{
    /// <summary>
    /// The recording being revised (empty for a new recording).
    /// </summary>
    public Guid? Id { get; set; }

    [Required]
    public Guid? CycleId { get; set; }

    [Required]
    public DateOnly? Date { get; set; }

    [Required]
    [Range(0, 1_000_000)]
    public int? Mortality { get; set; } = 0;

    [Required]
    [Range(0, 1_000_000)]
    public int? Culling { get; set; } = 0;

    [Range(1, 9_999)]
    [Display(Name = "Average body weight (gram)")]
    public decimal? AverageBodyWeightGram { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Revision only: why the recording is corrected.
    /// </summary>
    [StringLength(300)]
    public string? Reason { get; set; }

    public List<StockLineInput> Usages { get; set; } = [];

    public List<Guid> Documents { get; set; } = [];

    [BindNever]
    public bool IsRevision => Id is not null;

    [BindNever]
    public CycleResponse? Cycle { get; set; }

    [BindNever]
    public Guid? CoopWarehouseId { get; set; }

    [BindNever]
    public int RevisionNumber { get; set; }

    [BindNever]
    public IReadOnlyList<AttachmentResponse> Attachments { get; set; } = [];

    public UsageRequest[] ToUsages() =>
        [.. Usages.Select(u => new UsageRequest(u.ItemId!.Value, u.UomId!.Value, u.Quantity!.Value))];
}

public sealed record RecordingDetailsViewModel(
    DailyRecordingResponse Recording,
    CycleResponse Cycle,
    IReadOnlyList<AttachmentResponse> Attachments,
    bool CanRevise);

/// <summary>
/// Daily recordings or harvests page: the chosen cycle and its rows.
/// </summary>
public sealed class CycleWorkspaceViewModel
{
    public CycleResponse? Cycle { get; init; }

    public string? CycleLabel { get; init; }

    public IReadOnlyList<DailyRecordingResponse> Recordings { get; init; } = [];

    public IReadOnlyList<HarvestResponse> Harvests { get; init; } = [];

    public bool CanCreate { get; init; }

    public bool CanEdit { get; init; }

    public HarvestFormViewModel Harvest { get; init; } = new();

    /// <summary>
    /// Metadata of the attachments of every harvest of the cycle.
    /// </summary>
    public IReadOnlyList<AttachmentResponse> HarvestAttachments { get; init; } = [];

    public IReadOnlyList<AttachmentResponse> AttachmentsOf(Guid[] ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return [.. HarvestAttachments.Where(a => ids.Contains(a.Id))];
    }
}

// ---- Harvests ------------------------------------------------------------------------------------------------

public sealed class HarvestFormViewModel
{
    [Required]
    public Guid? CycleId { get; set; }

    [Required]
    [Display(Name = "Harvest date")]
    public DateOnly? Date { get; set; }

    [Required]
    [Range(1, 1_000_000)]
    public int? Birds { get; set; }

    [Required]
    [Range(0.001, 1_000_000)]
    [Display(Name = "Weight (kg)")]
    public decimal? WeightKg { get; set; }

    [StringLength(500)]
    [Display(Name = "Truck / notes")]
    public string? Notes { get; set; }

    public List<Guid> Documents { get; set; } = [];
}
