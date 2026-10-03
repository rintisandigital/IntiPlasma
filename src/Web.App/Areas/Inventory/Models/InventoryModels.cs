using System.ComponentModel.DataAnnotations;
using Application.Documents;
using Application.Inventory;
using Application.Inventory.StockTransfers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Web.App.Areas.Inventory.Models;

/// <summary>
/// A warehouse option; forms filter the destination list by branch and type in the browser.
/// </summary>
public sealed record WarehouseOption(Guid Id, string Label, Guid BranchId, string Type);

// ---- Goods receipts ----------------------------------------------------------------------------------------

public sealed class GoodsReceiptFormViewModel
{
    [Required]
    [Display(Name = "Purchase order")]
    public Guid? PurchaseOrderId { get; set; }

    [Required]
    [Display(Name = "Receiving warehouse")]
    public Guid? WarehouseId { get; set; }

    [Required]
    [Display(Name = "Receipt date")]
    public DateOnly? ReceiptDate { get; set; }

    [StringLength(50)]
    [Display(Name = "Vendor delivery note no.")]
    public string? DeliveryNoteNumber { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public List<GoodsReceiptLineInput> Lines { get; set; } = [];

    public List<Guid> Documents { get; set; } = [];

    [BindNever]
    public Application.Procurement.PurchaseOrderResponse? Order { get; set; }

    [BindNever]
    public IReadOnlyList<WarehouseOption> Warehouses { get; set; } = [];

    [BindNever]
    public IReadOnlyList<AttachmentResponse> Attachments { get; set; } = [];
}

public sealed class GoodsReceiptLineInput
{
    public int PurchaseOrderLineNumber { get; set; }

    /// <summary>
    /// Quantity received now in the order unit; empty or zero = not received in this receipt.
    /// </summary>
    [Range(0, 1_000_000_000)]
    public decimal? Quantity { get; set; }
}

// ---- Stock transfers, returns and feed mutations --------------------------------------------------------------

public enum StockMovementKind
{
    Transfer,
    Return,
    FeedMutation
}

public sealed class StockMovementFormViewModel
{
    [Required]
    [Display(Name = "From warehouse")]
    public Guid? FromWarehouseId { get; set; }

    /// <summary>
    /// Feed mutation only: the central warehouse the feed passes through.
    /// </summary>
    [Display(Name = "Via central warehouse")]
    public Guid? ViaWarehouseId { get; set; }

    [Required]
    [Display(Name = "To warehouse")]
    public Guid? ToWarehouseId { get; set; }

    [Required]
    public DateOnly? Date { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public List<StockLineInput> Lines { get; set; } = [];

    public List<Guid> Documents { get; set; } = [];

    [BindNever]
    public StockMovementKind Kind { get; set; }

    [BindNever]
    public IReadOnlyList<WarehouseOption> Warehouses { get; set; } = [];

    [BindNever]
    public IReadOnlyList<AttachmentResponse> Attachments { get; set; } = [];

    [BindNever]
    public bool CanSave { get; set; } = true;

    public StockTransferLineRequest[] ToLines() =>
        [.. Lines.Select(l => new StockTransferLineRequest(l.ItemId!.Value, l.UomId!.Value, l.Quantity!.Value))];
}

public sealed class StockLineInput
{
    [Required]
    public Guid? ItemId { get; set; }

    public string? ItemLabel { get; set; }

    [Required]
    public Guid? UomId { get; set; }

    [Required]
    [Range(0.000001, 1_000_000_000)]
    public decimal? Quantity { get; set; }

    [BindNever]
    public IReadOnlyList<SelectListItem> Units { get; set; } = [];
}

/// <summary>
/// Details page of a goods receipt (BPB), stock transfer or stock return.
/// </summary>
public sealed record InventoryDocumentViewModel(
    InventoryDocumentResponse Document,
    IReadOnlyList<AttachmentResponse> Attachments,
    string Title,
    string ReferenceLabel,
    bool CanPrint);

// ---- Stock ------------------------------------------------------------------------------------------------

public sealed class StockCardViewModel
{
    public Guid? WarehouseId { get; init; }

    public Guid? ItemId { get; init; }

    public string? ItemLabel { get; init; }

    public DateOnly From { get; init; }

    public DateOnly To { get; init; }

    public StockCardResponse? Card { get; init; }

    public string? WarehouseLabel { get; init; }

    public string? BaseUomCode { get; init; }

    public IReadOnlyList<SelectListItem> Warehouses { get; init; } = [];
}
