using SharedKernel;

namespace Domain.Finance.Receivables;

public static class CustomerReceiptErrors
{
    public static Error NotFound(Guid customerReceiptId) => Error.NotFound(
        "CustomerReceipts.NotFound",
        $"The customer receipt with the Id = '{customerReceiptId}' was not found");

    public static Error InvoiceMismatch(Guid salesInvoiceId) => Error.Problem(
        "CustomerReceipts.InvoiceMismatch",
        $"The sales invoice with the Id = '{salesInvoiceId}' belongs to another customer or branch");

    public static Error BeforeInvoiceDate(string invoiceNumber) => Error.Problem(
        "CustomerReceipts.BeforeInvoiceDate",
        $"The receipt date cannot be before the date of invoice {invoiceNumber}");

    public static Error Voided(Guid customerReceiptId) => Error.Problem(
        "CustomerReceipts.Voided",
        $"The customer receipt with the Id = '{customerReceiptId}' is voided");

    public static Error AdvanceExceeded(Money unapplied) => Error.Problem(
        "CustomerReceipts.AdvanceExceeded",
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Only {unapplied} of the advance is left to apply"));

    public static readonly Error NoAllocations = Error.Problem(
        "CustomerReceipts.NoAllocations",
        "A customer receipt must be allocated to at least one invoice or carry an advance");

    public static readonly Error AdvanceAlreadyApplied = Error.Problem(
        "CustomerReceipts.AdvanceAlreadyApplied",
        "A receipt whose advance has been applied to invoices cannot be voided");

    public static readonly Error ApplicationBeforeReceipt = Error.Problem(
        "CustomerReceipts.ApplicationBeforeReceipt",
        "An advance cannot be applied before the receipt date");

    public static readonly Error VoidBeforeReceipt = Error.Problem(
        "CustomerReceipts.VoidBeforeReceipt",
        "A receipt cannot be voided before its receipt date");

    public static readonly Error DuplicateInvoice = Error.Problem(
        "CustomerReceipts.DuplicateInvoice",
        "An invoice can only appear once on a receipt");

}
