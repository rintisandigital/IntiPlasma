using System.Globalization;
using SharedKernel;

namespace Domain.Finance.Payables;

public static class VendorInvoiceErrors
{
    public static Error NotFound(Guid vendorInvoiceId) => Error.NotFound(
        "VendorInvoices.NotFound",
        $"The vendor invoice with the Id = '{vendorInvoiceId}' was not found");

    public static Error InvalidTransition(VendorInvoiceStatus from, VendorInvoiceStatus to) => Error.Problem(
        "VendorInvoices.InvalidTransition",
        $"A vendor invoice cannot move from {from} to {to}");

    public static Error NotPayable(Guid vendorInvoiceId) => Error.Problem(
        "VendorInvoices.NotPayable",
        $"The vendor invoice with the Id = '{vendorInvoiceId}' is not posted or already paid");

    public static Error OverPayment(string number, Money outstanding) => Error.Problem(
        "VendorInvoices.OverPayment",
        string.Create(CultureInfo.InvariantCulture, $"The payment for vendor invoice {number} must be positive and cannot exceed the outstanding {outstanding}"));

    public static Error PriceVarianceAboveTolerance(decimal deviationPercent, decimal tolerancePercent) => Error.Problem(
        "VendorInvoices.PriceVarianceAboveTolerance",
        string.Create(
            CultureInfo.InvariantCulture,
            $"An invoice price differs {deviationPercent:0.##}% from the order price, above the vendor's tolerance of {tolerancePercent:0.##}%; post with a variance approval"));

    public static Error ReceiptMismatch(string receiptNumber) => Error.Problem(
        "VendorInvoices.ReceiptMismatch",
        $"Goods receipt {receiptNumber} belongs to another vendor or branch");

    public static Error BeforeReceiptDate(string receiptNumber) => Error.Problem(
        "VendorInvoices.BeforeReceiptDate",
        $"The invoice date cannot be before the date of goods receipt {receiptNumber}");

    public static Error DuplicateVendorInvoiceNumber(string number) => Error.Conflict(
        "VendorInvoices.DuplicateVendorInvoiceNumber",
        $"The vendor's invoice number {number} is already registered");

    public static readonly Error NoLines = Error.Problem(
        "VendorInvoices.NoLines",
        "A vendor invoice needs at least one goods receipt line");

    public static readonly Error DuplicateLine = Error.Problem(
        "VendorInvoices.DuplicateLine",
        "A goods receipt line can only appear once on an invoice");

    public static readonly Error InvalidLine = Error.Problem(
        "VendorInvoices.InvalidLine",
        "Each line needs a positive quantity and a positive unit price");

    public static readonly Error InvalidIncomeTaxCode = Error.Problem(
        "VendorInvoices.InvalidIncomeTaxCode",
        "The withholding tax code must be an active income tax (PPh) code");
}
