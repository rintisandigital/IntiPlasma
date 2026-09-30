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

    public static readonly Error NoAllocations = Error.Problem(
        "CustomerReceipts.NoAllocations",
        "A customer receipt must be allocated to at least one invoice");

    public static readonly Error DuplicateInvoice = Error.Problem(
        "CustomerReceipts.DuplicateInvoice",
        "An invoice can only appear once on a receipt");

    public static readonly Error InvalidCashAccount = Error.Problem(
        "CustomerReceipts.InvalidCashAccount",
        "The cash/bank account must be an active, postable asset account");
}
