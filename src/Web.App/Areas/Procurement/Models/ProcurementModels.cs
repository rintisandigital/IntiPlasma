using System.ComponentModel.DataAnnotations;
using Application.Documents;
using Application.Procurement;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Web.App.Areas.Procurement.Models;

public sealed class PurchaseOrderFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [Display(Name = "Branch")]
    public Guid? BranchId { get; set; }

    [Required]
    [Display(Name = "Vendor")]
    public Guid? VendorId { get; set; }

    /// <summary>
    /// Label of the chosen vendor, posted back so a re-rendered form keeps it.
    /// </summary>
    public string? VendorLabel { get; set; }

    [Required]
    [Display(Name = "Order date")]
    public DateOnly? OrderDate { get; set; }

    [Display(Name = "Expected delivery")]
    public DateOnly? ExpectedDate { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public List<PurchaseOrderLineFormInput> Lines { get; set; } = [];

    public List<Guid> Documents { get; set; } = [];

    public string? Number { get; set; }

    [BindNever]
    public bool IsNew => Id is null;

    [BindNever]
    public IReadOnlyList<SelectListItem> Branches { get; set; } = [];

    [BindNever]
    public IReadOnlyList<SelectListItem> VatCodes { get; set; } = [];

    [BindNever]
    public IReadOnlyList<AttachmentResponse> Attachments { get; set; } = [];

    public PurchaseOrderLineRequest[] ToLines() =>
        [.. Lines.Select(l => new PurchaseOrderLineRequest(l.ItemId!.Value, l.UomId!.Value, l.Quantity!.Value, l.UnitPrice!.Value, l.TaxCodeId))];
}

public sealed class PurchaseOrderLineFormInput
{
    [Required]
    public Guid? ItemId { get; set; }

    public string? ItemLabel { get; set; }

    [Required]
    public Guid? UomId { get; set; }

    [Required]
    [Range(0.000001, 1_000_000_000)]
    public decimal? Quantity { get; set; }

    [Required]
    [Range(0.01, 1_000_000_000_000)]
    public decimal? UnitPrice { get; set; }

    public Guid? TaxCodeId { get; set; }

    /// <summary>
    /// Units of the chosen item (base unit and conversions) for the unit dropdown.
    /// </summary>
    [BindNever]
    public IReadOnlyList<SelectListItem> Units { get; set; } = [];
}

/// <param name="Vat">Estimated VAT from the lines' VAT codes at the order date (the vendor invoice books the actual VAT).</param>
public sealed record PurchaseOrderDetailsViewModel(
    PurchaseOrderResponse Order,
    IReadOnlyList<AttachmentResponse> Attachments,
    IReadOnlyDictionary<int, string> LineTaxCodes,
    decimal Vat,
    bool CanEdit,
    bool CanReceive,
    bool CanPrint)
{
    public decimal Total => Order.Subtotal + Vat;

    public bool IsDraft => Order.Status == "Draft";

    public bool CanBeCancelled => Order.Status is "Draft" or "Approved";

    public bool CanBeClosed => Order.Status == "PartiallyReceived";

    public bool IsReceivable => Order.Status is "Approved" or "PartiallyReceived";
}
