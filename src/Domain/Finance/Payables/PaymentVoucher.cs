using System.Globalization;
using SharedKernel;

namespace Domain.Finance.Payables;

/// <summary>
/// Payment voucher (bukti kas/bank keluar) paying posted documents of one payee from a cash/bank account: vendor
/// invoices of a vendor, or plasma settlements of a farmer. Maker-checker: Draft → Approved (by someone other than
/// the creator) → Paid. Paying registers the allocations on the documents (partial payment allowed, never above the
/// outstanding) and journals Dr hutang usaha / hutang plasma, Cr kas/bank.
/// </summary>
public sealed class PaymentVoucher : AggregateRoot
{
    private readonly List<PaymentVoucherAllocation> _allocations = [];
    private readonly List<PaymentVoucherSettlementAllocation> _settlementAllocations = [];

    private PaymentVoucher(Guid id)
        : base(id)
    {
    }

    private PaymentVoucher()
    {
    }

    public string Number { get; private set; }
    public Guid BranchId { get; private set; }
    public PayeeType PayeeType { get; private set; }

    /// <summary>
    /// Set when the payee is a vendor.
    /// </summary>
    public Guid? VendorId { get; private set; }

    /// <summary>
    /// Set when the payee is a plasma farmer.
    /// </summary>
    public Guid? FarmerId { get; private set; }

    public Guid CashBankAccountId { get; private set; }

    /// <summary>
    /// Planned payment date; replaced by the actual date when paid.
    /// </summary>
    public DateOnly PaymentDate { get; private set; }

    public string? Reference { get; private set; }
    public string? Notes { get; private set; }
    public Money Amount { get; private set; } = new(0m);
    public PaymentVoucherStatus Status { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public Guid? PaidBy { get; private set; }
    public DateTime? PaidAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }

    /// <summary>
    /// Vendor invoices paid (payee = vendor).
    /// </summary>
    public IReadOnlyCollection<PaymentVoucherAllocation> Allocations => [.. _allocations];

    /// <summary>
    /// Plasma settlements paid (payee = farmer).
    /// </summary>
    public IReadOnlyCollection<PaymentVoucherSettlementAllocation> SettlementAllocations => [.. _settlementAllocations];

    public Guid PayeeId => PayeeType == PayeeType.Vendor ? VendorId!.Value : FarmerId!.Value;

    /// <summary>
    /// The documents paid and their amounts.
    /// </summary>
    public IReadOnlyList<(Guid DocumentId, Money Amount)> DocumentAmounts => PayeeType == PayeeType.Vendor
        ? [.. _allocations.Select(a => (a.VendorInvoiceId, a.Amount))]
        : [.. _settlementAllocations.Select(a => (a.PlasmaSettlementId, a.Amount))];

    /// <param name="allocations">Vendor invoices of the vendor, or plasma settlements of the farmer.</param>
    public static Result<PaymentVoucher> Create(
        string number,
        Guid branchId,
        PayeeType payeeType,
        Guid payeeId,
        Guid cashBankAccountId,
        DateOnly paymentDate,
        string? reference,
        string? notes,
        IReadOnlyList<(IPayable Document, Money Amount)> allocations)
    {
        Result validation = ValidateAllocations(branchId, payeeId, paymentDate, allocations);
        if (validation.IsFailure)
        {
            return Result.Failure<PaymentVoucher>(validation.Error);
        }

        var voucher = new PaymentVoucher(Guid.CreateVersion7())
        {
            Number = number,
            BranchId = branchId,
            PayeeType = payeeType,
            VendorId = payeeType == PayeeType.Vendor ? payeeId : null,
            FarmerId = payeeType == PayeeType.Farmer ? payeeId : null,
            CashBankAccountId = cashBankAccountId,
            PaymentDate = paymentDate,
            Reference = reference,
            Notes = notes,
            Status = PaymentVoucherStatus.Draft,
            Amount = allocations.Aggregate(new Money(0m), (total, a) => total + a.Amount)
        };

        if (payeeType == PayeeType.Vendor)
        {
            voucher._allocations.AddRange(allocations.Select(a => new PaymentVoucherAllocation(voucher.Id, a.Document.Id, a.Amount)));
        }
        else
        {
            voucher._settlementAllocations.AddRange(
                allocations.Select(a => new PaymentVoucherSettlementAllocation(voucher.Id, a.Document.Id, a.Amount)));
        }

        return voucher;
    }

    public Result Approve(Guid approverId, DateTime utcNow)
    {
        if (Status != PaymentVoucherStatus.Draft)
        {
            return Result.Failure(PaymentVoucherErrors.InvalidTransition(Status, PaymentVoucherStatus.Approved));
        }

        if (CreatedBy == approverId)
        {
            return Result.Failure(PaymentVoucherErrors.SelfApprovalNotAllowed);
        }

        Status = PaymentVoucherStatus.Approved;
        ApprovedBy = approverId;
        ApprovedAtUtc = utcNow;

        return Result.Success();
    }

    /// <summary>
    /// Pays an approved voucher on the actual payment date and registers the payment on each document.
    /// </summary>
    /// <param name="documents">The documents of the allocations (tracked).</param>
    public Result Pay(DateOnly paymentDate, IReadOnlyList<IPayable> documents, Guid? userId, DateTime utcNow)
    {
        if (Status != PaymentVoucherStatus.Approved)
        {
            return Result.Failure(PaymentVoucherErrors.InvalidTransition(Status, PaymentVoucherStatus.Paid));
        }

        List<(IPayable Document, Money Amount)> allocations =
            [.. DocumentAmounts.Select(a => (documents.Single(d => d.Id == a.DocumentId), a.Amount))];

        Result validation = ValidateAllocations(BranchId, PayeeId, paymentDate, allocations);
        if (validation.IsFailure)
        {
            return validation;
        }

        foreach ((IPayable document, Money amount) in allocations)
        {
            document.RegisterPayment(amount);
        }

        PaymentDate = paymentDate;
        Status = PaymentVoucherStatus.Paid;
        PaidBy = userId;
        PaidAtUtc = utcNow;

        Raise(new PaymentVoucherPaidDomainEvent(Id));

        return Result.Success();
    }

    public Result Cancel(string reason)
    {
        if (Status is not (PaymentVoucherStatus.Draft or PaymentVoucherStatus.Approved))
        {
            return Result.Failure(PaymentVoucherErrors.InvalidTransition(Status, PaymentVoucherStatus.Cancelled));
        }

        Status = PaymentVoucherStatus.Cancelled;
        CancellationReason = reason;

        return Result.Success();
    }

    private static Result ValidateAllocations(
        Guid branchId,
        Guid payeeId,
        DateOnly paymentDate,
        IReadOnlyList<(IPayable Document, Money Amount)> allocations)
    {
        if (allocations.Count == 0)
        {
            return Result.Failure(PaymentVoucherErrors.NoAllocations);
        }

        if (allocations.GroupBy(a => a.Document.Id).Any(g => g.Count() > 1))
        {
            return Result.Failure(PaymentVoucherErrors.DuplicateInvoice);
        }

        foreach ((IPayable document, Money amount) in allocations)
        {
            if (document.BranchId != branchId || document.PayeeId != payeeId)
            {
                return Result.Failure(PaymentVoucherErrors.InvoiceMismatch(document.Id));
            }

            if (!document.IsPayable)
            {
                return Result.Failure(PaymentVoucherErrors.DocumentNotPayable(document.Id));
            }

            if (amount.IsNegative || amount.IsZero || amount > document.Outstanding)
            {
                return Result.Failure(PaymentVoucherErrors.OverPayment(document.Number!, document.Outstanding));
            }

            if (paymentDate < document.DocumentDate)
            {
                return Result.Failure(PaymentVoucherErrors.BeforeInvoiceDate(document.Number!));
            }
        }

        return Result.Success();
    }
}

public sealed class PaymentVoucherAllocation
{
    internal PaymentVoucherAllocation(Guid paymentVoucherId, Guid vendorInvoiceId, Money amount)
    {
        PaymentVoucherId = paymentVoucherId;
        VendorInvoiceId = vendorInvoiceId;
        Amount = amount with { };
    }

    private PaymentVoucherAllocation()
    {
    }

    public Guid PaymentVoucherId { get; private set; }
    public Guid VendorInvoiceId { get; private set; }
    public Money Amount { get; private set; }
}

public sealed class PaymentVoucherSettlementAllocation
{
    internal PaymentVoucherSettlementAllocation(Guid paymentVoucherId, Guid plasmaSettlementId, Money amount)
    {
        PaymentVoucherId = paymentVoucherId;
        PlasmaSettlementId = plasmaSettlementId;
        Amount = amount with { };
    }

    private PaymentVoucherSettlementAllocation()
    {
    }

    public Guid PaymentVoucherId { get; private set; }
    public Guid PlasmaSettlementId { get; private set; }
    public Money Amount { get; private set; }
}

public enum PayeeType
{
    Vendor = 1,

    /// <summary>
    /// Peternak plasma (settlement payment).
    /// </summary>
    Farmer = 2
}

public enum PaymentVoucherStatus
{
    Draft = 1,
    Approved = 2,
    Paid = 3,
    Cancelled = 9
}

public sealed record PaymentVoucherPaidDomainEvent(Guid PaymentVoucherId) : DomainEvent;

public static class PaymentVoucherErrors
{
    public static Error NotFound(Guid paymentVoucherId) => Error.NotFound(
        "PaymentVouchers.NotFound",
        $"The payment voucher with the Id = '{paymentVoucherId}' was not found");

    public static Error InvalidTransition(PaymentVoucherStatus from, PaymentVoucherStatus to) => Error.Problem(
        "PaymentVouchers.InvalidTransition",
        $"A payment voucher cannot move from {from} to {to}");

    public static Error InvoiceMismatch(Guid documentId) => Error.Problem(
        "PaymentVouchers.InvoiceMismatch",
        $"The document with the Id = '{documentId}' belongs to another payee or branch");

    public static Error DocumentNotPayable(Guid documentId) => Error.Problem(
        "PaymentVouchers.DocumentNotPayable",
        $"The document with the Id = '{documentId}' is not posted/approved, or has nothing left to pay");

    public static Error OverPayment(string number, Money outstanding) => Error.Problem(
        "PaymentVouchers.OverPayment",
        string.Create(CultureInfo.InvariantCulture, $"The payment for {number} must be positive and cannot exceed the outstanding {outstanding}"));

    public static Error BeforeInvoiceDate(string number) => Error.Problem(
        "PaymentVouchers.BeforeInvoiceDate",
        $"The payment date cannot be before the date of {number}");

    public static readonly Error SelfApprovalNotAllowed = Error.Problem(
        "PaymentVouchers.SelfApprovalNotAllowed",
        "A payment voucher must be approved by someone other than its creator");

    public static readonly Error NoAllocations = Error.Problem(
        "PaymentVouchers.NoAllocations",
        "A payment voucher must pay at least one document");

    public static readonly Error DuplicateInvoice = Error.Problem(
        "PaymentVouchers.DuplicateInvoice",
        "A document can only appear once on a payment voucher");
}
