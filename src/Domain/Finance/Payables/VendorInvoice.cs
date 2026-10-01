using Domain.Common;
using Domain.Inventory.GoodsReceipts;
using Domain.MasterData.TaxCodes;
using Domain.Procurement.PurchaseOrders;
using SharedKernel;

namespace Domain.Finance.Payables;

/// <summary>
/// Tagihan vendor (vendor invoice) matched three ways: every line bills received goods (a goods receipt line of the
/// vendor's purchase order) for at most the quantity not billed yet, at a price compared with the order price.
/// Draft → Posted → (Partially) Paid; a draft can be cancelled, which releases the billed quantities.
/// Posting clears hutang belum ditagih at the receipt value, books the price difference, input VAT and the income tax
/// withheld, and gives the invoice its number. A price difference above the vendor's tolerance needs an approval reason.
/// </summary>
public sealed class VendorInvoice : AggregateRoot, IPayable, IHasDocuments
{
    private readonly List<VendorInvoiceLine> _lines = [];

    private VendorInvoice(Guid id)
        : base(id)
    {
    }

    private VendorInvoice()
    {
    }

    /// <summary>
    /// Internal number, given when posting; null while the invoice is a draft.
    /// </summary>
    public string? Number { get; private set; }

    public Guid BranchId { get; private set; }
    public Guid VendorId { get; private set; }

    /// <summary>
    /// The vendor's own invoice number.
    /// </summary>
    public string VendorInvoiceNumber { get; private set; }

    /// <summary>
    /// Nomor faktur pajak (input VAT document).
    /// </summary>
    public string? TaxInvoiceNumber { get; private set; }

    public DateOnly InvoiceDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public VendorInvoiceStatus Status { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>
    /// Invoiced amount excluding VAT (DPP).
    /// </summary>
    public Money Subtotal { get; private set; } = new(0m);

    /// <summary>
    /// Receipt value of the billed goods: what is cleared from hutang belum ditagih (GRNI).
    /// </summary>
    public Money GoodsValue { get; private set; } = new(0m);

    public Money VatAmount { get; private set; } = new(0m);

    /// <summary>
    /// PPh withheld from the vendor (e.g. PPh 22/23), deducted from what is paid.
    /// </summary>
    public Guid? IncomeTaxCodeId { get; private set; }

    public decimal IncomeTaxRatePercent { get; private set; }
    public Money IncomeTaxAmount { get; private set; } = new(0m);

    /// <summary>
    /// Payable to the vendor: subtotal + VAT − income tax withheld.
    /// </summary>
    public Money Total { get; private set; } = new(0m);

    public Money PaidAmount { get; private set; } = new(0m);

    /// <summary>
    /// Largest difference between an invoice price and its order price, in percent of the order price.
    /// </summary>
    public decimal MaxPriceDeviationPercent { get; private set; }

    /// <summary>
    /// Set when the invoice was posted with a price difference above the vendor's tolerance.
    /// </summary>
    public string? PriceVarianceApprovalReason { get; private set; }

    public Guid? PostedBy { get; private set; }
    public DateTime? PostedAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }
    public IReadOnlyCollection<VendorInvoiceLine> Lines => [.. _lines];

    /// <summary>
    /// Invoice amount − receipt value (positive: billed above the order price).
    /// </summary>
    public Money PriceVariance => Subtotal - GoodsValue;

    public Money Outstanding => Total - PaidAmount;

    public bool IsPayable => Status is VendorInvoiceStatus.Posted or VendorInvoiceStatus.PartiallyPaid;

    Guid IPayable.PayeeId => VendorId;

    DateOnly IPayable.DocumentDate => InvoiceDate;

    /// <summary>
    /// Lampiran: ids of the attached photos and documents.
    /// </summary>
    public Guid[] Documents { get; private set; } = [];

    public Result SetDocuments(IEnumerable<Guid>? documents) =>
        Status == VendorInvoiceStatus.Cancelled
            ? Result.Failure(DocumentErrors.OwnerCancelled)
            : DocumentList.Apply(documents, value => Documents = value);

    /// <summary>
    /// Creates a draft and registers the billed quantities on the goods receipts (so they cannot be billed twice).
    /// </summary>
    /// <param name="taxCodes">Every VAT code of the order lines and the income tax code, with their rates.</param>
    public static Result<VendorInvoice> CreateDraft(
        Guid branchId,
        Guid vendorId,
        int paymentTermDays,
        string vendorInvoiceNumber,
        string? taxInvoiceNumber,
        DateOnly invoiceDate,
        string? notes,
        Guid? incomeTaxCodeId,
        IReadOnlyList<VendorInvoiceLineInput> lines,
        IReadOnlyDictionary<Guid, TaxCode> taxCodes)
    {
        Result validation = Validate(branchId, vendorId, invoiceDate, lines);
        if (validation.IsFailure)
        {
            return Result.Failure<VendorInvoice>(validation.Error);
        }

        // Every tax code must have a rate before anything is registered on the goods receipts.
        IEnumerable<Guid> usedTaxCodes = lines
            .Where(l => l.OrderLine.TaxCodeId is not null)
            .Select(l => l.OrderLine.TaxCodeId!.Value)
            .Append(incomeTaxCodeId ?? Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct();

        foreach (Guid taxCodeId in usedTaxCodes)
        {
            Result<TaxCalculation> probe = taxCodes[taxCodeId].Calculate(Money.Zero, invoiceDate);
            if (probe.IsFailure)
            {
                return Result.Failure<VendorInvoice>(probe.Error);
            }
        }

        var invoice = new VendorInvoice(Guid.CreateVersion7())
        {
            BranchId = branchId,
            VendorId = vendorId,
            VendorInvoiceNumber = vendorInvoiceNumber.Trim(),
            TaxInvoiceNumber = taxInvoiceNumber,
            InvoiceDate = invoiceDate,
            DueDate = invoiceDate.AddDays(paymentTermDays),
            Notes = notes,
            IncomeTaxCodeId = incomeTaxCodeId,
            Status = VendorInvoiceStatus.Draft
        };

        foreach (VendorInvoiceLineInput input in lines)
        {
            PurchaseOrderLine orderLine = input.OrderLine;
            Money amount = input.UnitPrice * input.Quantity;

            Result<TaxCalculation> vat = orderLine.TaxCodeId is null
                ? TaxCalculation.None
                : taxCodes[orderLine.TaxCodeId.Value].Calculate(amount, invoiceDate);

            if (vat.IsFailure)
            {
                return Result.Failure<VendorInvoice>(vat.Error);
            }

            Result<Money> goodsValue = input.Receipt.RegisterInvoice(input.ReceiptLineNumber, input.Quantity);
            if (goodsValue.IsFailure)
            {
                return Result.Failure<VendorInvoice>(goodsValue.Error);
            }

            invoice._lines.Add(new VendorInvoiceLine(
                invoice.Id, invoice._lines.Count + 1, input, amount, goodsValue.Value, vat.Value));
        }

        invoice.Subtotal = invoice._lines.Aggregate(new Money(0m), (total, line) => total + line.Amount);
        invoice.GoodsValue = invoice._lines.Aggregate(new Money(0m), (total, line) => total + line.GoodsValue);
        invoice.VatAmount = invoice._lines.Aggregate(new Money(0m), (total, line) => total + line.VatAmount);
        invoice.MaxPriceDeviationPercent = invoice._lines.Max(l => l.PriceDeviationPercent);

        if (incomeTaxCodeId is not null)
        {
            Result<TaxCalculation> incomeTax = taxCodes[incomeTaxCodeId.Value].Calculate(invoice.Subtotal, invoiceDate);
            if (incomeTax.IsFailure)
            {
                return Result.Failure<VendorInvoice>(incomeTax.Error);
            }

            invoice.IncomeTaxRatePercent = incomeTax.Value.RatePercent;
            invoice.IncomeTaxAmount = incomeTax.Value.TaxAmount with { };
        }

        invoice.Total = invoice.Subtotal + invoice.VatAmount - invoice.IncomeTaxAmount;

        return invoice;
    }

    /// <summary>
    /// Checks the state and the price variance before a number is taken.
    /// </summary>
    /// <param name="tolerancePercent">The vendor's price tolerance.</param>
    /// <param name="approvalReason">Required when a line's price differs from the order price by more than the tolerance.</param>
    public Result EnsurePostable(decimal tolerancePercent, string? approvalReason)
    {
        if (Status != VendorInvoiceStatus.Draft)
        {
            return Result.Failure(VendorInvoiceErrors.InvalidTransition(Status, VendorInvoiceStatus.Posted));
        }

        return MaxPriceDeviationPercent > tolerancePercent && string.IsNullOrWhiteSpace(approvalReason)
            ? Result.Failure(VendorInvoiceErrors.PriceVarianceAboveTolerance(MaxPriceDeviationPercent, tolerancePercent))
            : Result.Success();
    }

    public Result Post(string number, decimal tolerancePercent, string? approvalReason, Guid? userId, DateTime utcNow)
    {
        Result postable = EnsurePostable(tolerancePercent, approvalReason);
        if (postable.IsFailure)
        {
            return postable;
        }

        Number = number;
        Status = VendorInvoiceStatus.Posted;
        PriceVarianceApprovalReason = MaxPriceDeviationPercent > tolerancePercent ? approvalReason!.Trim() : null;
        PostedBy = userId;
        PostedAtUtc = utcNow;

        Raise(new VendorInvoicePostedDomainEvent(Id));

        return Result.Success();
    }

    /// <summary>
    /// Cancels a draft and releases the billed quantities on its goods receipts.
    /// </summary>
    public Result Cancel(string reason, IReadOnlyList<GoodsReceipt> receipts)
    {
        if (Status != VendorInvoiceStatus.Draft)
        {
            return Result.Failure(VendorInvoiceErrors.InvalidTransition(Status, VendorInvoiceStatus.Cancelled));
        }

        foreach (VendorInvoiceLine line in _lines)
        {
            receipts.Single(r => r.Id == line.GoodsReceiptId)
                .ReleaseInvoice(line.GoodsReceiptLineNumber, line.Quantity, line.GoodsValue);
        }

        Status = VendorInvoiceStatus.Cancelled;
        CancellationReason = reason;

        return Result.Success();
    }

    public Result RegisterPayment(Money amount)
    {
        if (!IsPayable)
        {
            return Result.Failure(VendorInvoiceErrors.NotPayable(Id));
        }

        if (amount.IsNegative || amount.IsZero || amount > Outstanding)
        {
            return Result.Failure(VendorInvoiceErrors.OverPayment(Number!, Outstanding));
        }

        PaidAmount += amount;
        Status = Outstanding.IsZero ? VendorInvoiceStatus.Paid : VendorInvoiceStatus.PartiallyPaid;

        return Result.Success();
    }

    private static Result Validate(Guid branchId, Guid vendorId, DateOnly invoiceDate, IReadOnlyList<VendorInvoiceLineInput> lines)
    {
        if (lines.Count == 0)
        {
            return Result.Failure(VendorInvoiceErrors.NoLines);
        }

        if (lines.GroupBy(l => (l.Receipt.Id, l.ReceiptLineNumber)).Any(g => g.Count() > 1))
        {
            return Result.Failure(VendorInvoiceErrors.DuplicateLine);
        }

        foreach (VendorInvoiceLineInput line in lines)
        {
            if (line.Receipt.VendorId != vendorId || line.Receipt.BranchId != branchId)
            {
                return Result.Failure(VendorInvoiceErrors.ReceiptMismatch(line.Receipt.Number));
            }

            if (line.Quantity <= 0 || line.UnitPrice.IsNegative || line.UnitPrice.IsZero)
            {
                return Result.Failure(VendorInvoiceErrors.InvalidLine);
            }

            if (invoiceDate < line.Receipt.ReceiptDate)
            {
                return Result.Failure(VendorInvoiceErrors.BeforeReceiptDate(line.Receipt.Number));
            }
        }

        return Result.Success();
    }
}

/// <param name="Receipt">The goods receipt (tracked: the billed quantity is registered on it).</param>
/// <param name="OrderLine">The purchase order line the receipt line was received against.</param>
/// <param name="Quantity">Billed quantity in the order line's unit.</param>
/// <param name="UnitPrice">Invoiced price per order unit, excluding VAT.</param>
public sealed record VendorInvoiceLineInput(
    GoodsReceipt Receipt,
    int ReceiptLineNumber,
    PurchaseOrderLine OrderLine,
    decimal Quantity,
    Money UnitPrice);

public enum VendorInvoiceStatus
{
    Draft = 1,
    Posted = 2,
    PartiallyPaid = 3,
    Paid = 4,
    Cancelled = 9
}

public sealed record VendorInvoicePostedDomainEvent(Guid VendorInvoiceId) : DomainEvent;
