using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using SharedKernel;

namespace Application.Coops.Get;

public sealed record GetCoopsQuery(PageRequest Paging, Guid? BranchId, Guid? FarmerId, Guid? FieldOfficerId = null)
    : IQuery<PagedList<CoopResponse>>;
