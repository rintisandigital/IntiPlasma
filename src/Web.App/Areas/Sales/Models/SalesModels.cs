using System.ComponentModel.DataAnnotations;
using Application.Documents;
using Application.Finance.Receivables;
using Application.Sales;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Web.App.Areas.Sales.Models;

// ---- Sales orders ------------------------------------------------------------------------------------------

public sealed class SalesOrderFormViewModel
{
    public Guid? Id { get; set; }

    public string? Number { get; set; }

    [Required]
    [Display(Name = "Branch")]
    public Guid? BranchId { get; set; }

    [Required]
    [Display(Name = "Customer")]
    public Guid? CustomerId { get; set; }

    public string? CustomerLabel { get; set; }

    [Required]
    [Display(Name = "Order date")]
    public DateOnly? OrderDate { get; set; }

    [Display(Name = "Delivery date")]
    public DateOnly? DeliveryDate { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public List<SalesOrderLineFormInput> Lines { get; set; } = [];

    public List<Guid> Documents { get; set; } = [];

    [BindNever]
    public bool IsNew => Id is null;

    [BindNever]
    public IReadOnlyList<SelectListItem> Branches { get; set; } = [];

    [BindNever]
    public IReadOnlyList<SelectListItem> VatCodes { get; set; } = [];

    [BindNever]
    public IReadOnlyList<AttachmentResponse> Attachments { get; set; } = [];

    public SalesOrderLineRequest[] ToLines() =>
        [.. Lines.Select(l => new SalesOrderLineRequest(l.ItemId!.Value, l.Birds!.Value, l.EstimatedWeightKg!.Value, l.PricePerKg!.Value, l.TaxCodeId))];
}

public sealed class SalesOrderLineFormInput
{
    [Required]
    public Guid? ItemId { get; set; }

    public string? ItemLabel { get; set; }

    [Required]
    [Range(1, 10_000_000)]
    public int? Birds { get; set; }

    [Required]
    [Range(0.001, 100_000_000)]
    public decimal? EstimatedWeightKg { get; set; }

    [Required]
    [Range(0.01, 100_000_000)]
    public decimal? PricePerKg { get; set; }

    public Guid? TaxCodeId { get; set; }
}

/// <param name="Credit">Credit position of the customer (limit, exposure excluding this order, available).</param>
/// <param name="CreditBlocked">Set when approval was refused for the credit limit: offers the approval with a reason.</param>
public sealed record SalesOrderDetailsViewModel(
    SalesOrderResponse Order,
    IReadOnlyList<AttachmentResponse> Attachments,
    CustomerCreditResponse? Credit,
    string? CreditBlocked,
    bool CanEdit,
    bool CanDeliver)
{
    public bool IsDraft => Order.Status == "Draft";

    public bool IsDeliverable => Order.Status is "Approved" or "PartiallyDelivered";

    public bool CanBeCancelled => Order.Status is "Draft" or "Approved";

    public bool CanBeClosed => Order.Status == "PartiallyDelivered";

    public bool ExceedsCredit => Credit is not null && Order.EstimatedAmount > Credit.Available;
}

// ---- Delivery orders ---------------------------------------------------------------------------------------------

public sealed class DeliveryOrderFormViewModel
{
    [Required]
    [Display(Name = "Sales order")]
    public Guid? SalesOrderId { get; set; }

    [Required]
    [Display(Name = "Delivery date")]
    public DateOnly? DeliveryDate { get; set; }

    [StringLength(20)]
    [Display(Name = "Vehicle number")]
    public string? VehicleNumber { get; set; }

    [StringLength(100)]
    [Display(Name = "Driver")]
    public string? DriverName { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public List<DeliveryLineInput> Lines { get; set; } = [];

    public List<Guid> Documents { get; set; } = [];

    [BindNever]
    public SalesOrderResponse? Order { get; set; }

    [BindNever]
    public IReadOnlyList<UndeliveredHarvestResponse> Harvests { get; set; } = [];

    [BindNever]
    public IReadOnlyList<AttachmentResponse> Attachments { get; set; } = [];
}

/// <summary>
/// One undelivered harvest (truck) and the order line it is sold on; unchecked = not on this delivery.
/// </summary>
public sealed class DeliveryLineInput
{
    public Guid HarvestId { get; set; }

    public bool? Selected { get; set; }

    public int? SalesOrderLineNumber { get; set; }
}

public sealed record DeliveryOrderDetailsViewModel(
    DeliveryOrderResponse Delivery,
    IReadOnlyList<AttachmentResponse> Attachments,
    bool CanCancel,
    bool CanPrint,
    bool CanInvoice);

// ---- Sales invoices ----------------------------------------------------------------------------------------------

public sealed class SalesInvoiceFormViewModel
{
    [Required]
    [Display(Name = "Customer")]
    public Guid? CustomerId { get; set; }

    public string? CustomerLabel { get; set; }

    [Required]
    [Display(Name = "Invoice date")]
    public DateOnly? InvoiceDate { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public List<Guid> DeliveryOrderIds { get; set; } = [];

    /// <summary>
    /// Delivered, not yet invoiced delivery orders of the customer.
    /// </summary>
    [BindNever]
    public IReadOnlyList<DeliveryOrderResponse> Deliveries { get; set; } = [];
}

public sealed record SalesInvoiceDetailsViewModel(
    SalesInvoiceResponse Invoice,
    IReadOnlyList<SalesCreditNoteResponse> CreditNotes,
    bool CanEdit,
    bool CanPrint,
    bool CanCredit,
    bool CanReceive)
{
    public bool IsDraft => Invoice.Status == "Draft";

    public bool IsPosted => Invoice.Status is "Posted" or "PartiallyPaid" or "Paid";

    public bool IsOpen => Invoice.Status is "Posted" or "PartiallyPaid" && Invoice.Outstanding > 0;
}

// ---- Credit notes ------------------------------------------------------------------------------------------------

public sealed class CreditNoteFormViewModel
{
    [Required]
    public Guid? SalesInvoiceId { get; set; }

    [Required]
    public DateOnly? Date { get; set; }

    [Required]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    public List<CreditLineInput> Lines { get; set; } = [];

    [BindNever]
    public SalesInvoiceResponse? Invoice { get; set; }
}

/// <param name="Amount">Reduction of the invoice line excluding VAT; empty = no change.</param>
public sealed class CreditLineInput
{
    public int InvoiceLineNumber { get; set; }

    [Range(0, 100_000_000_000)]
    public decimal? Amount { get; set; }
}

// ---- Customer receipts ---------------------------------------------------------------------------------------------

public sealed class ReceiptFormViewModel
{
    [Required]
    [Display(Name = "Customer")]
    public Guid? CustomerId { get; set; }

    public string? CustomerLabel { get; set; }

    [Required]
    [Display(Name = "Cash/bank account")]
    public Guid? CashBankAccountId { get; set; }

    [Required]
    [Display(Name = "Receipt date")]
    public DateOnly? ReceiptDate { get; set; }

    [StringLength(100)]
    [Display(Name = "Reference (transfer / giro no.)")]
    public string? Reference { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [Range(0, 100_000_000_000)]
    [Display(Name = "Advance (uang muka)")]
    public decimal? AdvanceAmount { get; set; }

    public List<AllocationInput> Allocations { get; set; } = [];

    public List<Guid> Documents { get; set; } = [];

    /// <summary>
    /// Open posted invoices of the customer.
    /// </summary>
    [BindNever]
    public IReadOnlyList<InvoiceAging> Invoices { get; set; } = [];

    [BindNever]
    public IReadOnlyList<SelectListItem> CashBankAccounts { get; set; } = [];

    [BindNever]
    public IReadOnlyList<AttachmentResponse> Attachments { get; set; } = [];

    public ReceiptAllocationRequest[] ToAllocations() =>
        [.. Allocations.Where(a => a.Amount > 0 && a.SalesInvoiceId is not null).Select(a => new ReceiptAllocationRequest(a.SalesInvoiceId!.Value, a.Amount!.Value))];
}

/// <param name="Amount">Paid on this invoice; empty or zero = not allocated.</param>
public sealed class AllocationInput
{
    [Required]
    public Guid? SalesInvoiceId { get; set; }

    [Range(0, 100_000_000_000)]
    public decimal? Amount { get; set; }
}

public sealed record ReceiptDetailsViewModel(
    CustomerReceiptResponse Receipt,
    IReadOnlyList<AttachmentResponse> Attachments,
    IReadOnlyList<InvoiceAging> OpenInvoices,
    bool CanEdit,
    bool CanPrint)
{
    public bool IsPosted => Receipt.Status == "Posted";

    public bool HasAdvanceLeft => IsPosted && Receipt.UnappliedAdvance > 0;

    /// <summary>
    /// A receipt whose advance was already applied to invoices cannot be voided.
    /// </summary>
    public bool CanBeVoided => IsPosted && (Receipt.Applications ?? []).Count == 0;
}

// ---- Receivables -----------------------------------------------------------------------------------------------------

public sealed class ReceivablesViewModel
{
    public string Tab { get; init; } = "aging";

    public Guid? CustomerId { get; init; }

    public string? CustomerLabel { get; init; }

    public string? Branch { get; init; }

    public IReadOnlyList<SelectListItem> BranchOptions { get; init; } = [];

    public DateOnly From { get; init; }

    public DateOnly To { get; init; }

    public DateOnly AsOf { get; init; }

    public ReceivableLedgerResponse? Ledger { get; init; }

    public ReceivableAgingResponse? Aging { get; init; }
}
