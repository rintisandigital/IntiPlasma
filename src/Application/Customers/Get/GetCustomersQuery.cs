using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using SharedKernel;

namespace Application.Customers.Get;

public sealed record GetCustomersQuery(PageRequest Paging) : IQuery<PagedList<CustomerResponse>>;
