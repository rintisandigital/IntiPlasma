using System.ComponentModel.DataAnnotations;
using Application.Finance.CashBank;
using Application.Finance.FiscalPeriods;
using Domain.Finance.Accounts;
using Domain.Finance.CashBank;
using Domain.Finance.JournalMappings;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Web.App.Areas.Finance.Models;

// ---- Chart of accounts -------------------------------------------------------------------------------------

public sealed class AccountFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(20)]
    [RegularExpression("^[A-Za-z0-9.-]+$", ErrorMessage = "Use letters, digits, '.' or '-' only.")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public AccountType? Type { get; set; }

    [Display(Name = "Parent (header account)")]
    public Guid? ParentId { get; set; }

    [Display(Name = "Postable (detail) account")]
    public bool? IsPostable { get; set; }

    /// <summary>
    /// Empty = the default of the type; set only for contra accounts (e.g. accumulated depreciation).
    /// </summary>
    [Display(Name = "Normal balance")]
    public BalanceSide? NormalBalance { get; set; }

    [Display(Name = "Cash flow category")]
    public CashFlowCategory? CashFlowCategory { get; set; }

    public bool? IsActive { get; set; }

    [BindNever]
    public bool IsNew => Id is null;

    [BindNever]
    public bool CanSave { get; set; } = true;

    [BindNever]
    public string? ParentLabel { get; set; }

    /// <summary>
    /// Header accounts that may become the parent, with their type (the form only offers those of the chosen type).
    /// </summary>
    [BindNever]
    public IReadOnlyList<HeaderAccountOption> HeaderAccounts { get; set; } = [];
}

public sealed record HeaderAccountOption(Guid Id, string Label, string Type);

public sealed class AccountListViewModel
{
    public required IReadOnlyList<Application.Finance.Accounts.AccountResponse> Accounts { get; init; }

    public string? Search { get; init; }

    public AccountType? Type { get; init; }

    public string? Status { get; init; }

    public IReadOnlyList<SelectListItem> TypeOptions { get; init; } = [];

    public static bool IsContra(Application.Finance.Accounts.AccountResponse account)
    {
        ArgumentNullException.ThrowIfNull(account);

        string normal = account.Type is nameof(AccountType.Asset) or nameof(AccountType.Expense)
            ? nameof(BalanceSide.Debit)
            : nameof(BalanceSide.Credit);

        return account.NormalBalance != normal;
    }
}

// ---- Cost centers ------------------------------------------------------------------------------------------

public sealed class CostCenterFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public bool? IsActive { get; set; }

    [BindNever]
    public bool IsNew => Id is null;

    [BindNever]
    public bool CanSave { get; set; } = true;
}

// ---- Fiscal periods ----------------------------------------------------------------------------------------

public sealed class FiscalPeriodListViewModel
{
    public required IReadOnlyList<FiscalPeriodResponse> Periods { get; init; }

    public int? Year { get; init; }

    public IReadOnlyList<int> Years { get; init; } = [];

    public bool CanOpenYear { get; init; }

    public bool CanChangeStatus { get; init; }

    public int SuggestedYear { get; init; }
}

public sealed record FiscalPeriodChecklistViewModel(PeriodClosingChecklist Checklist, bool CanChangeStatus);

/// <summary>
/// English text of the period closing checks (the API returns Indonesian descriptions).
/// </summary>
public static class ClosingCheckLabels
{
    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        ["UnpostedJournals"] = "Manual journals still draft/approved (not posted)",
        ["PendingAutoJournals"] = "Auto journal events not processed yet (wait a moment and refresh)",
        ["FailedAutoJournals"] = "Failed auto journals (dead letter) — fix the cause and retry",
        ["DraftSalesInvoices"] = "Sales invoices still draft",
        ["UninvoicedDeliveries"] = "Delivered DOs not invoiced yet",
        ["DraftVendorInvoices"] = "Vendor invoices still draft",
        ["OpenPaymentVouchers"] = "Payment vouchers draft/approved but not paid",
        ["OpenCashTransactions"] = "Cash in/out draft/approved but not posted",
        ["DraftSettlements"] = "Plasma settlements still draft",
        ["UnreconciledBanks"] = "Bank accounts not reconciled up to the period end"
    };

    public static string For(ClosingCheck check)
    {
        ArgumentNullException.ThrowIfNull(check);
        return Labels.GetValueOrDefault(check.Code, check.Description);
    }
}

// ---- Journal templates -------------------------------------------------------------------------------------

public sealed class JournalTemplateFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool? IsActive { get; set; }

    public List<TemplateLineInput> Lines { get; set; } = [];

    [BindNever]
    public bool IsNew => Id is null;

    [BindNever]
    public bool CanSave { get; set; } = true;

    [BindNever]
    public IReadOnlyList<SelectListItem> CostCenters { get; set; } = [];
}

public sealed class TemplateLineInput
{
    [Required]
    public Guid? AccountId { get; set; }

    /// <summary>
    /// Label of the chosen account, posted back so a re-rendered form keeps it.
    /// </summary>
    public string? AccountLabel { get; set; }

    public Guid? CostCenterId { get; set; }

    [Required]
    public BalanceSide? Side { get; set; }

    [StringLength(250)]
    public string? Description { get; set; }
}

// ---- Auto journal mappings ---------------------------------------------------------------------------------

public sealed class JournalMappingFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [Display(Name = "Accounting event")]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Empty = company-wide default; a branch overrides the default for that branch.
    /// </summary>
    [Display(Name = "Branch")]
    public Guid? BranchId { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    public bool? IsActive { get; set; }

    public List<MappingLineInput> Lines { get; set; } = [];

    [BindNever]
    public bool IsNew => Id is null;

    [BindNever]
    public bool CanSave { get; set; } = true;

    [BindNever]
    public string? BranchCode { get; set; }

    [BindNever]
    public IReadOnlyList<SelectListItem> Events { get; set; } = [];

    [BindNever]
    public IReadOnlyList<SelectListItem> Branches { get; set; } = [];

    [BindNever]
    public IReadOnlyList<SelectListItem> CostCenters { get; set; } = [];
}

/// <summary>
/// One component of the event (fixed rows); a component left without accounts is not mapped.
/// </summary>
public sealed class MappingLineInput
{
    public string Component { get; set; } = string.Empty;

    public Guid? DebitAccountId { get; set; }

    public string? DebitAccountLabel { get; set; }

    public Guid? CreditAccountId { get; set; }

    public string? CreditAccountLabel { get; set; }

    public Guid? CostCenterId { get; set; }
}

public sealed record JournalMappingEventRow(
    AccountingEventDefinition Event,
    Application.Finance.JournalMappings.JournalMappingResponse? Default,
    IReadOnlyList<Application.Finance.JournalMappings.JournalMappingResponse> Overrides);

/// <summary>
/// English names of the accounting events and their amount components (the catalog describes them in Indonesian).
/// </summary>
public static class AccountingEventLabels
{
    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        [AccountingEvents.PurchaseReceipt] = "Sapronak receipt (DOC, feed, OVK) into a warehouse",
        ["PurchaseReceipt.DocReceived"] = "DOC received",
        ["PurchaseReceipt.FeedReceived"] = "Feed received",
        ["PurchaseReceipt.OvkReceived"] = "OVK received",
        [AccountingEvents.VendorInvoice] = "Vendor invoice",
        ["VendorInvoice.GoodsValue"] = "Invoiced goods value (clears goods received not invoiced)",
        ["VendorInvoice.PriceVariance"] = "Invoice vs PO price variance (negative = cheaper)",
        ["VendorInvoice.InputVat"] = "Input VAT",
        ["VendorInvoice.IncomeTaxWithheld"] = "Income tax withheld",
        [AccountingEvents.VendorPayment] = "Payment to a vendor",
        ["VendorPayment.Paid"] = "Amount paid (cash/bank account can be chosen per transaction)",
        [AccountingEvents.StockTransferToCycle] = "Sapronak transfer to a coop (running cycle)",
        ["StockTransferToCycle.DocIssued"] = "DOC transferred",
        ["StockTransferToCycle.FeedIssued"] = "Feed transferred",
        ["StockTransferToCycle.OvkIssued"] = "OVK transferred",
        [AccountingEvents.SalesInvoice] = "Live bird sales invoice",
        ["SalesInvoice.LiveBirdSales"] = "Live bird sales (tax base)",
        ["SalesInvoice.OutputVat"] = "Output VAT",
        ["SalesInvoice.CostOfGoodsSold"] = "Cost of goods sold (estimated cycle cost per kg at posting)",
        [AccountingEvents.CustomerReceipt] = "Customer payment received",
        ["CustomerReceipt.Received"] = "Amount received for invoices (cash/bank account can be chosen per transaction)",
        ["CustomerReceipt.Advance"] = "Sales advance (receipt not allocated to an invoice)",
        [AccountingEvents.CustomerAdvanceApplied] = "Sales advance applied to an invoice",
        ["CustomerAdvanceApplied.Applied"] = "Advance applied",
        [AccountingEvents.SalesCreditNote] = "Sales credit note / return",
        ["SalesCreditNote.SalesReturn"] = "Sales return / discount (tax base)",
        ["SalesCreditNote.OutputVat"] = "Output VAT correction",
        [AccountingEvents.StockReturnFromCycle] = "Sapronak returned from a coop to the central warehouse",
        ["StockReturnFromCycle.FeedReturned"] = "Feed returned",
        ["StockReturnFromCycle.OvkReturned"] = "OVK returned",
        [AccountingEvents.PlasmaSettlement] = "Plasma settlement (partnership result)",
        ["PlasmaSettlement.PlasmaIncome"] = "Plasma income (plasma's share)",
        ["PlasmaSettlement.IncomeTaxWithheld"] = "Income tax withheld from plasma income",
        ["PlasmaSettlement.Deduction"] = "Plasma debt/penalty deduction",
        ["PlasmaSettlement.PlasmaDeficit"] = "Plasma deficit (negative result) booked as plasma receivable",
        [AccountingEvents.CycleCostAdjustment] = "COGS adjustment when a cycle is closed (final cost − estimated COGS)",
        ["CycleCostAdjustment.CostOfGoodsSold"] = "COGS difference (negative = estimate was too high)",
        [AccountingEvents.YearEndClosing] = "Year-end closing journal (when December is closed)",
        ["YearEndClosing.NetIncome"] = "Net income to retained earnings (credit account = retained earnings)",
        [AccountingEvents.PlasmaPayment] = "Payment to a plasma farmer",
        ["PlasmaPayment.Paid"] = "Amount paid (cash/bank account can be chosen per transaction)"
    };

    public static string Event(AccountingEventDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return Labels.GetValueOrDefault(definition.Code, definition.Description);
    }

    public static string Event(string eventType) =>
        AccountingEvents.Find(eventType) is { } definition ? Event(definition) : eventType;

    public static string Component(string eventType, AccountingComponentDefinition component)
    {
        ArgumentNullException.ThrowIfNull(component);
        return Labels.GetValueOrDefault($"{eventType}.{component.Code}", component.Description);
    }
}

// ---- Cash/bank accounts ------------------------------------------------------------------------------------

public sealed class CashBankAccountFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public CashBankAccountType? Type { get; set; }

    [Required]
    [Display(Name = "Branch")]
    public Guid? BranchId { get; set; }

    [Required]
    [Display(Name = "Ledger account (COA)")]
    public Guid? AccountId { get; set; }

    [StringLength(100)]
    [Display(Name = "Bank name")]
    public string? BankName { get; set; }

    [StringLength(40)]
    [Display(Name = "Account number")]
    public string? AccountNumber { get; set; }

    public bool? IsActive { get; set; }

    [BindNever]
    public bool IsNew => Id is null;

    [BindNever]
    public bool CanSave { get; set; } = true;

    [BindNever]
    public string? AccountLabel { get; set; }

    [BindNever]
    public string? BranchCode { get; set; }

    [BindNever]
    public decimal? Balance { get; set; }

    [BindNever]
    public IReadOnlyList<SelectListItem> Branches { get; set; } = [];

    public static CashBankAccountFormViewModel From(CashBankAccountResponse account)
    {
        ArgumentNullException.ThrowIfNull(account);

        return new CashBankAccountFormViewModel
        {
            Id = account.Id,
            Code = account.Code,
            Name = account.Name,
            Type = Enum.Parse<CashBankAccountType>(account.Type),
            BranchId = account.BranchId,
            BranchCode = account.BranchCode,
            AccountId = account.AccountId,
            AccountLabel = $"{account.AccountCode} — {account.AccountName}",
            BankName = account.BankName,
            AccountNumber = account.AccountNumber,
            IsActive = account.IsActive,
            Balance = account.Balance
        };
    }
}
