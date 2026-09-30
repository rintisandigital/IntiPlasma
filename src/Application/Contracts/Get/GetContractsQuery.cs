using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Domain.Partnership.Contracts;
using SharedKernel;

namespace Application.Contracts.Get;

public sealed record GetContractsQuery(PageRequest Paging, Guid? BranchId, ContractStatus? Status)
    : IQuery<PagedList<ContractResponse>>;
