using SharedKernel;

namespace Domain.Finance.Payables;

/// <summary>
/// Payment voucher (bukti kas/bank keluar) paying posted invoices of one vendor from a cash/bank account.
/// Maker-checker: Draft → Approved (by someone other than the creator) → Paid. Paying registers the allocations on the
/// invoices (partial payment allowed, never above the outstanding) and journals Dr hutang usaha / Cr kas/bank.
/// </summary>
public sealed class PaymentVoucher : AggregateRoot
{
    private readonly List<PaymentVoucherAllocation> _allocations = [];

    private PaymentVoucher(Guid id)
        : base(id)
    {
    }

    private PaymentVoucher()
    {
    }

    public string Number { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid VendorId { get; private set; }
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
    public IReadOnlyCollection<PaymentVoucherAllocation> Allocations => [.. _allocations];

    public static Result<PaymentVoucher> Create(
        string number,
        Guid branchId,
        Guid vendorId,
        Guid cashBankAccountId,
        DateOnly paymentDate,
        string? reference,
        string? notes,
        IReadOnlyList<(VendorInvoice Invoice, Money Amount)> allocations)
    {
        Result validation = ValidateAllocations(branchId, vendorId, paymentDate, allocations);
        if (validation.IsFailure)
        {
            return Result.Failure<PaymentVoucher>(validation.Error);
        }

        var voucher = new PaymentVoucher(Guid.CreateVersion7())
        {
            Number = number,
            BranchId = branchId,
            VendorId = vendorId,
            CashBankAccountId = cashBankAccountId,
            PaymentDate = paymentDate,
            Reference = reference,
            Notes = notes,
            Status = PaymentVoucherStatus.Draft,
            Amount = allocations.Aggregate(new Money(0m), (total, a) => total + a.Amount)
        };

        voucher._allocations.AddRange(allocations.Select(a => new PaymentVoucherAllocation(voucher.Id, a.Invoice.Id, a.Amount)));

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
    /// Pays an approved voucher on the actual payment date and registers the payment on each invoice.
    /// </summary>
    /// <param name="invoices">The invoices of the allocations (tracked).</param>
    public Result Pay(DateOnly paymentDate, IReadOnlyList<VendorInvoice> invoices, Guid? userId, DateTime utcNow)
    {
        if (Status != PaymentVoucherStatus.Approved)
        {
            return Result.Failure(PaymentVoucherErrors.InvalidTransition(Status, PaymentVoucherStatus.Paid));
        }

        List<(VendorInvoice Invoice, Money Amount)> allocations =
            [.. _allocations.Select(a => (invoices.Single(i => i.Id == a.VendorInvoiceId), a.Amount))];

        Result validation = ValidateAllocations(BranchId, VendorId, paymentDate, allocations);
        if (validation.IsFailure)
        {
            return validation;
        }

        foreach ((VendorInvoice invoice, Money amount) in allocations)
        {
            invoice.RegisterPayment(amount);
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
        Guid vendorId,
        DateOnly paymentDate,
        IReadOnlyList<(VendorInvoice Invoice, Money Amount)> allocations)
    {
        if (allocations.Count == 0)
        {
            return Result.Failure(PaymentVoucherErrors.NoAllocations);
        }

        if (allocations.GroupBy(a => a.Invoice.Id).Any(g => g.Count() > 1))
        {
            return Result.Failure(PaymentVoucherErrors.DuplicateInvoice);
        }

        foreach ((VendorInvoice invoice, Money amount) in allocations)
        {
            if (invoice.BranchId != branchId || invoice.VendorId != vendorId)
            {
                return Result.Failure(PaymentVoucherErrors.InvoiceMismatch(invoice.Id));
            }

            if (!invoice.IsPayable)
            {
                return Result.Failure(VendorInvoiceErrors.NotPayable(invoice.Id));
            }

            if (amount.IsNegative || amount.IsZero || amount > invoice.Outstanding)
            {
                return Result.Failure(VendorInvoiceErrors.OverPayment(invoice.Number!, invoice.Outstanding));
            }

            if (paymentDate < invoice.InvoiceDate)
            {
                return Result.Failure(PaymentVoucherErrors.BeforeInvoiceDate(invoice.Number!));
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

public enum PaymentVoucherStatus
{
    Draft = 1,
    Approved = 2,
    Paid = 3,
    Cancelled = 9
}

public sealed record PaymentVoucherPaidDomainEvent(Guid PaymentVoucherId) : DomainEvent;
