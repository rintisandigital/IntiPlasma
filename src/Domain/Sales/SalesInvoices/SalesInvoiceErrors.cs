using System.Globalization;
using SharedKernel;

namespace Domain.Sales.SalesInvoices;

public static class SalesInvoiceErrors
{
    public static Error NotFound(Guid salesInvoiceId) => Error.NotFound(
        "SalesInvoices.NotFound",
        $"The sales invoice with the Id = '{salesInvoiceId}' was not found");

    public static Error InvalidTransition(SalesInvoiceStatus from, SalesInvoiceStatus to) => Error.Problem(
        "SalesInvoices.InvalidTransition",
        $"A sales invoice cannot move from {from} to {to}");

    public static Error NotPayable(Guid salesInvoiceId) => Error.Problem(
        "SalesInvoices.NotPayable",
        $"The sales invoice with the Id = '{salesInvoiceId}' is not posted or already paid");

    public static Error OverPayment(string number, Money outstanding) => Error.Problem(
        "SalesInvoices.OverPayment",
        string.Create(CultureInfo.InvariantCulture, $"The payment for invoice {number} must be positive and cannot exceed the outstanding {outstanding}"));

    public static Error NoTaxRate(string taxCode, DateOnly date) => Error.Problem(
        "SalesInvoices.NoTaxRate",
        $"The VAT code {taxCode} has no rate in effect on {date:yyyy-MM-dd}");

    public static readonly Error NoDeliveries = Error.Problem(
        "SalesInvoices.NoDeliveries",
        "A sales invoice needs at least one delivery order");

    public static readonly Error DuplicateDelivery = Error.Problem(
        "SalesInvoices.DuplicateDelivery",
        "A delivery order can only appear once on an invoice");

    public static readonly Error MixedDeliveries = Error.Problem(
        "SalesInvoices.MixedDeliveries",
        "All delivery orders on an invoice must belong to the same customer and branch");

    public static readonly Error BeforeDeliveryDate = Error.Problem(
        "SalesInvoices.BeforeDeliveryDate",
        "The invoice date cannot be before a delivery date");
}
