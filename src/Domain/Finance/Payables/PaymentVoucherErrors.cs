using SharedKernel;

namespace Domain.Finance.Payables;

public static class PaymentVoucherErrors
{
    public static Error NotFound(Guid paymentVoucherId) => Error.NotFound(
        "PaymentVouchers.NotFound",
        $"The payment voucher with the Id = '{paymentVoucherId}' was not found");

    public static Error InvalidTransition(PaymentVoucherStatus from, PaymentVoucherStatus to) => Error.Problem(
        "PaymentVouchers.InvalidTransition",
        $"A payment voucher cannot move from {from} to {to}");

    public static Error InvoiceMismatch(Guid vendorInvoiceId) => Error.Problem(
        "PaymentVouchers.InvoiceMismatch",
        $"The vendor invoice with the Id = '{vendorInvoiceId}' belongs to another vendor or branch");

    public static Error BeforeInvoiceDate(string invoiceNumber) => Error.Problem(
        "PaymentVouchers.BeforeInvoiceDate",
        $"The payment date cannot be before the date of vendor invoice {invoiceNumber}");

    public static readonly Error SelfApprovalNotAllowed = Error.Problem(
        "PaymentVouchers.SelfApprovalNotAllowed",
        "A payment voucher must be approved by someone other than its creator");

    public static readonly Error NoAllocations = Error.Problem(
        "PaymentVouchers.NoAllocations",
        "A payment voucher must pay at least one vendor invoice");

    public static readonly Error DuplicateInvoice = Error.Problem(
        "PaymentVouchers.DuplicateInvoice",
        "An invoice can only appear once on a payment voucher");
}
