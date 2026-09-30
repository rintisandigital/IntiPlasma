using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using SharedKernel;

namespace Application.Vendors.Get;

public sealed record GetVendorsQuery(PageRequest Paging) : IQuery<PagedList<VendorResponse>>;
