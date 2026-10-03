using System.ComponentModel.DataAnnotations;
using Application.Documents;
using Application.Finance.Journals;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Web.App.Areas.Finance.Models;

/// <summary>
/// Manual journal draft (create or edit): branch, date, description and balanced debit/credit lines.
/// </summary>
public sealed class JournalFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [Display(Name = "Branch")]
    public Guid? BranchId { get; set; }

    [Required]
    [Display(Name = "Journal date")]
    public DateOnly? Date { get; set; }

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    public List<JournalFormLineInput> Lines { get; set; } = [];

    public List<Guid> Documents { get; set; } = [];

    [BindNever]
    public IReadOnlyList<SelectListItem> Branches { get; set; } = [];

    [BindNever]
    public IReadOnlyList<SelectListItem> CostCenters { get; set; } = [];

    [BindNever]
    public IReadOnlyList<SelectListItem> Templates { get; set; } = [];

    [BindNever]
    public IReadOnlyList<AttachmentResponse> Attachments { get; set; } = [];

    public bool IsNew => Id is null;

    public JournalLineRequest[] ToLines() =>
        [.. Lines
            .Where(l => l.AccountId is not null || l.Debit > 0 || l.Credit > 0)
            .Select(l => new JournalLineRequest(l.AccountId ?? Guid.Empty, l.CostCenterId, l.Description, l.Debit ?? 0, l.Credit ?? 0))];
}

public sealed class JournalFormLineInput
{
    public Guid? AccountId { get; set; }

    /// <summary>
    /// Label of the chosen account, posted back so a re-rendered form keeps it.
    /// </summary>
    public string? AccountLabel { get; set; }

    public Guid? CostCenterId { get; set; }

    [StringLength(250)]
    public string? Description { get; set; }

    [Range(0, 100_000_000_000)]
    public decimal? Debit { get; set; }

    [Range(0, 100_000_000_000)]
    public decimal? Credit { get; set; }
}

/// <param name="SourceUrl">Detail page of the business document an automatic journal comes from.</param>
public sealed record JournalDetailsViewModel(
    JournalResponse Journal,
    IReadOnlyList<AttachmentResponse> Attachments,
    string? SourceUrl,
    string? ReversalNumber,
    bool CanEdit,
    bool CanDelete,
    bool CanPrint)
{
    public bool IsManual => Journal.Source == "Manual";

    public bool IsDraft => Journal.Status == "Draft";

    public bool IsApproved => Journal.Status == "Approved";

    public bool IsPosted => Journal.Status == "Posted";
}


/// <summary>
/// Readable names of automatic journal sources and where their document is shown.
/// </summary>
public static class JournalSources
{
    public static string Label(string? sourceType) => sourceType switch
    {
        null => "Manual",
        "PurchaseReceipt" => "Goods receipt",
        "StockTransferToCycle" => "Sapronak to cycle",
        "StockReturnFromCycle" => "Return from cycle",
        "VendorInvoice" => "Vendor invoice",
        "VendorPayment" => "Vendor payment",
        "PlasmaPayment" => "Plasma payment",
        "SalesInvoice" => "Sales invoice",
        "SalesCreditNote" => "Credit note",
        "CustomerReceipt" => "Customer receipt",
        "CustomerAdvanceApplied" => "Advance applied",
        "PlasmaSettlement" => "Plasma settlement",
        "CycleCostAdjustment" => "Cycle closing (HPP)",
        "CashTransaction" => "Cash in/out",
        "BankTransfer" => "Bank transfer",
        "YearEndClosing" => "Year-end closing",
        "YearEndClosing.Reversal" => "Year-end closing reversed",
        _ => sourceType
    };

    /// <summary>
    /// (area, controller, action) of the source document; StockTransferToCycle may be a transfer or a goods receipt
    /// into a coop warehouse and is resolved by the Journals controller.
    /// </summary>
    public static (string Area, string Controller, string Action)? Route(string? sourceType) => sourceType switch
    {
        "PurchaseReceipt" => ("Inventory", "GoodsReceipts", "Details"),
        "StockReturnFromCycle" => ("Inventory", "StockReturns", "Details"),
        "VendorInvoice" => ("Finance", "VendorInvoices", "Details"),
        "VendorPayment" or "PlasmaPayment" => ("Finance", "PaymentVouchers", "Details"),
        "SalesInvoice" => ("Sales", "SalesInvoices", "Details"),
        "CustomerReceipt" => ("Sales", "Receipts", "Details"),
        "PlasmaSettlement" => ("Costing", "Settlements", "Details"),
        "CycleCostAdjustment" => ("Production", "Cycles", "Details"),
        "CashTransaction" => ("Finance", "CashTransactions", "Details"),
        _ => null
    };
}
