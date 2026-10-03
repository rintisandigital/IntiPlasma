using System.ComponentModel.DataAnnotations;
using Application.Documents;
using Application.Finance.CashBank;
using Domain.Finance.CashBank;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Web.App.Areas.Finance.Models;

// ---- Cash in/out -------------------------------------------------------------------------------------------

public sealed class CashTransactionFormViewModel
{
    [Required]
    public CashDirection? Direction { get; set; }

    [Required]
    [Display(Name = "Cash/bank account")]
    public Guid? CashBankAccountId { get; set; }

    [Required]
    public DateOnly? Date { get; set; }

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Reference { get; set; }

    public List<CashLineInput> Lines { get; set; } = [];

    public List<Guid> Documents { get; set; } = [];

    [BindNever]
    public IReadOnlyList<SelectListItem> CashBankAccounts { get; set; } = [];

    [BindNever]
    public IReadOnlyList<SelectListItem> CostCenters { get; set; } = [];

    [BindNever]
    public IReadOnlyList<AttachmentResponse> Attachments { get; set; } = [];

    public bool IsIn => Direction == CashDirection.In;

    public CashTransactionLineRequest[] ToLines() =>
        [.. Lines
            .Where(l => l.AccountId is not null || l.Amount is not null)
            .Select(l => new CashTransactionLineRequest(l.AccountId ?? Guid.Empty, l.CostCenterId, l.Description, l.Amount ?? 0))];
}

/// <summary>
/// Counter account line: credited on cash-in (e.g. interest income), debited on cash-out (e.g. electricity).
/// </summary>
public sealed class CashLineInput
{
    public Guid? AccountId { get; set; }

    /// <summary>
    /// Label of the chosen account, kept when the form is shown again.
    /// </summary>
    public string? AccountLabel { get; set; }

    public Guid? CostCenterId { get; set; }

    [StringLength(250)]
    public string? Description { get; set; }

    [Range(0, 100_000_000_000)]
    public decimal? Amount { get; set; }
}

public sealed record CashTransactionDetailsViewModel(
    CashTransactionResponse Transaction,
    IReadOnlyList<AttachmentResponse> Attachments,
    bool CanEdit,
    bool CanPrint)
{
    public bool IsIn => Transaction.Direction == nameof(CashDirection.In);

    public bool IsDraft => Transaction.Status == nameof(CashTransactionStatus.Draft);

    /// <summary>
    /// Cash-out must be approved by someone else before posting.
    /// </summary>
    public bool NeedsApproval => !IsIn && IsDraft;

    public bool CanBePosted => IsIn ? IsDraft : Transaction.Status == nameof(CashTransactionStatus.Approved);

    public bool CanBeCancelled => Transaction.Status is nameof(CashTransactionStatus.Draft) or nameof(CashTransactionStatus.Approved);
}

// ---- Bank transfers ----------------------------------------------------------------------------------------

public sealed class BankTransferFormViewModel
{
    [Required]
    [Display(Name = "From")]
    public Guid? FromCashBankAccountId { get; set; }

    [Required]
    [Display(Name = "To")]
    public Guid? ToCashBankAccountId { get; set; }

    [Required]
    public DateOnly? Date { get; set; }

    [Required]
    [Range(0.01, 100_000_000_000)]
    public decimal? Amount { get; set; }

    [StringLength(100)]
    public string? Reference { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    [BindNever]
    public IReadOnlyList<SelectListItem> CashBankAccounts { get; set; } = [];
}

// ---- Cash/bank ledger --------------------------------------------------------------------------------------

public sealed class CashBankLedgerViewModel
{
    public required CashBankAccountResponse Account { get; init; }

    public DateOnly From { get; init; }

    public DateOnly To { get; init; }

    public CashBankLedgerResponse? Ledger { get; init; }
}

// ---- Bank reconciliations ----------------------------------------------------------------------------------

public sealed class BankReconciliationListViewModel
{
    public IReadOnlyList<BankReconciliationSummary> Rows { get; init; } = [];

    public string? Branch { get; init; }

    public Guid? CashBankAccountId { get; init; }

    public IReadOnlyList<SelectListItem> BranchOptions { get; init; } = [];

    public IReadOnlyList<SelectListItem> CashBankAccounts { get; init; } = [];
}

public sealed class BankReconciliationFormViewModel
{
    [Required]
    [Display(Name = "Bank account")]
    public Guid? CashBankAccountId { get; set; }

    [Required]
    [Display(Name = "Statement date")]
    public DateOnly? StatementDate { get; set; }

    [Required]
    [Range(-100_000_000_000, 100_000_000_000)]
    [Display(Name = "Statement closing balance")]
    public decimal? StatementBalance { get; set; }

    [BindNever]
    public IReadOnlyList<SelectListItem> CashBankAccounts { get; set; } = [];
}

public sealed record BankReconciliationDetailsViewModel(BankReconciliationResponse Reconciliation, bool CanEdit)
{
    public bool IsOpen => Reconciliation.Status == nameof(BankReconciliationStatus.InProgress);

    public bool CanComplete => IsOpen && Reconciliation.UnmatchedStatementLines == 0 && Reconciliation.Difference == 0;

    /// <summary>
    /// Uncleared ledger lines a statement line may be matched with: same amount first, then the closest date.
    /// </summary>
    public IEnumerable<UnclearedEntryResponse> Candidates(StatementLineResponse line)
    {
        ArgumentNullException.ThrowIfNull(line);

        return Reconciliation.UnclearedEntries
            .OrderBy(e => e.Amount == line.Amount ? 0 : 1)
            .ThenBy(e => Math.Abs(e.Date.DayNumber - line.Date.DayNumber));
    }
}
