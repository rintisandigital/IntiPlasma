using System.ComponentModel.DataAnnotations;
using Application.Documents;
using Application.Finance.Payables;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Web.App.Areas.Finance.Models;

// ---- Vendor invoices ---------------------------------------------------------------------------------------

public sealed class VendorInvoiceFormViewModel
{
    [Required]
    [Display(Name = "Vendor")]
    public Guid? VendorId { get; set; }

    public string? VendorLabel { get; set; }

    [Required]
    [Display(Name = "Branch")]
    public Guid? BranchId { get; set; }

    [Required]
    [StringLength(50)]
    [Display(Name = "Vendor's invoice number")]
    public string VendorInvoiceNumber { get; set; } = string.Empty;

    [StringLength(50)]
    [Display(Name = "Tax invoice (faktur pajak) number")]
    public string? TaxInvoiceNumber { get; set; }

    [Required]
    [Display(Name = "Invoice date")]
    public DateOnly? InvoiceDate { get; set; }

    [Display(Name = "Income tax withheld (PPh)")]
    public Guid? IncomeTaxCodeId { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public List<BillLineInput> Lines { get; set; } = [];

    public List<Guid> Documents { get; set; } = [];

    /// <summary>
    /// Received goods of the vendor in the branch that are not billed yet.
    /// </summary>
    [BindNever]
    public IReadOnlyList<UninvoicedReceiptLineResponse> Receipts { get; set; } = [];

    [BindNever]
    public IReadOnlyList<SelectListItem> Branches { get; set; } = [];

    [BindNever]
    public IReadOnlyList<SelectListItem> IncomeTaxCodes { get; set; } = [];

    [BindNever]
    public IReadOnlyList<AttachmentResponse> Attachments { get; set; } = [];

    public VendorInvoiceLineRequest[] ToLines() =>
        [.. Lines
            .Where(l => l.Selected == true && l.Quantity > 0)
            .Select(l => new VendorInvoiceLineRequest(l.GoodsReceiptId, l.GoodsReceiptLineNumber, l.Quantity!.Value, l.UnitPrice ?? 0))];
}

/// <summary>
/// One not yet billed receipt line; unchecked = not on this invoice.
/// </summary>
public sealed class BillLineInput
{
    public Guid GoodsReceiptId { get; set; }

    public int GoodsReceiptLineNumber { get; set; }

    public bool? Selected { get; set; }

    [Range(0, 1_000_000_000)]
    public decimal? Quantity { get; set; }

    [Range(0, 100_000_000_000)]
    public decimal? UnitPrice { get; set; }
}

public sealed record VendorInvoiceDetailsViewModel(
    VendorInvoiceResponse Invoice,
    IReadOnlyList<AttachmentResponse> Attachments,
    string? VarianceBlocked,
    bool CanEdit,
    bool CanPay)
{
    public bool IsDraft => Invoice.Status == "Draft";

    public bool IsPayable => Invoice.Status is "Posted" or "PartiallyPaid" && Invoice.Outstanding > 0;

    public bool HasVariance => Invoice.PriceVariance != 0;
}

// ---- Payment vouchers --------------------------------------------------------------------------------------

/// <summary>
/// A payment voucher to a vendor (posted invoices) or to a plasma farmer (approved settlements).
/// </summary>
public sealed class PaymentVoucherFormViewModel
{
    /// <summary>
    /// "Vendor" or "Farmer".
    /// </summary>
    public string PayeeType { get; set; } = "Vendor";

    [Display(Name = "Payee")]
    public Guid? PayeeId { get; set; }

    public string? PayeeLabel { get; set; }

    [Required]
    [Display(Name = "Pay from (cash/bank)")]
    public Guid? CashBankAccountId { get; set; }

    [Required]
    [Display(Name = "Payment date")]
    public DateOnly? PaymentDate { get; set; }

    [StringLength(100)]
    [Display(Name = "Reference (transfer / cheque no.)")]
    public string? Reference { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public List<PayableAllocationInput> Allocations { get; set; } = [];

    public List<Guid> Documents { get; set; } = [];

    [BindNever]
    public IReadOnlyList<OpenPayable> OpenDocuments { get; set; } = [];

    [BindNever]
    public IReadOnlyList<SelectListItem> CashBankAccounts { get; set; } = [];

    [BindNever]
    public IReadOnlyList<AttachmentResponse> Attachments { get; set; } = [];

    public bool IsPlasma => PayeeType == "Farmer";

    public (Guid DocumentId, decimal Amount)[] ToAllocations() =>
        [.. Allocations.Where(a => a.Amount > 0 && a.DocumentId is not null).Select(a => (a.DocumentId!.Value, a.Amount!.Value))];
}

/// <param name="Amount">Paid on this document; empty or zero = not allocated.</param>
public sealed class PayableAllocationInput
{
    [Required]
    public Guid? DocumentId { get; set; }

    [Range(0, 100_000_000_000)]
    public decimal? Amount { get; set; }
}

/// <summary>
/// A document still to be paid: a posted vendor invoice or an approved plasma settlement.
/// </summary>
/// <param name="Reference">The vendor's invoice number, or the cycle number of a settlement.</param>
public sealed record OpenPayable(
    Guid DocumentId,
    string Number,
    string Reference,
    string BranchCode,
    DateOnly Date,
    DateOnly? DueDate,
    int DaysOverdue,
    decimal Total,
    decimal Outstanding);

public sealed record PaymentVoucherDetailsViewModel(
    PaymentVoucherResponse Voucher,
    IReadOnlyList<AttachmentResponse> Attachments,
    bool CanEdit,
    bool CanPrint)
{
    public bool IsDraft => Voucher.Status == "Draft";

    public bool IsApproved => Voucher.Status == "Approved";

    public bool IsPlasma => Voucher.PayeeType == "Farmer";
}

// ---- Payables (ledger & aging) -----------------------------------------------------------------------------

public sealed class PayablesViewModel
{
    public string Tab { get; init; } = "aging";

    public Guid? VendorId { get; init; }

    public string? VendorLabel { get; init; }

    public string? Branch { get; init; }

    public IReadOnlyList<SelectListItem> BranchOptions { get; init; } = [];

    public DateOnly From { get; init; }

    public DateOnly To { get; init; }

    public DateOnly AsOf { get; init; }

    public PayableLedgerResponse? Ledger { get; init; }

    public PayableAgingResponse? Aging { get; init; }
}
