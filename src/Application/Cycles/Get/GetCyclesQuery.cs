using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Domain.Partnership.Cycles;
using SharedKernel;

namespace Application.Cycles.Get;

/// <param name="FieldOfficerId">Cycles in the coops of this PPL.</param>
public sealed record GetCyclesQuery(
    PageRequest Paging,
    Guid? BranchId,
    Guid? FarmerId,
    Guid? CoopId,
    CycleStatus? Status,
    Guid? FieldOfficerId = null) : IQuery<PagedList<CycleResponse>>;
