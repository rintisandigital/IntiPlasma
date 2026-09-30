using Application.Abstractions.Messaging;

namespace Application.Customers.GetById;

public sealed record GetCustomerByIdQuery(Guid CustomerId) : IQuery<CustomerResponse>;
