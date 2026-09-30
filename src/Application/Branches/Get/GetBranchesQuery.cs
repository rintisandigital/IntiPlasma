using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using SharedKernel;

namespace Application.Branches.Get;

public sealed record GetBranchesQuery(PageRequest Paging) : IQuery<PagedList<BranchResponse>>;
