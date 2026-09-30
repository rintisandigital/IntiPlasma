using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Domain.Partnership.Cycles;
using SharedKernel;

namespace Application.Cycles.Get;

public sealed record GetCyclesQuery(
    PageRequest Paging,
    Guid? BranchId,
    Guid? FarmerId,
    Guid? CoopId,
    CycleStatus? Status) : IQuery<PagedList<CycleResponse>>;
