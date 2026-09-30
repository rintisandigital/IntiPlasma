using SharedKernel;

namespace Domain.Sales.SalesInvoices;

public sealed record SalesInvoicePostedDomainEvent(Guid SalesInvoiceId) : DomainEvent;
