using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Domain.MasterData.Farmers;
using SharedKernel;

namespace Application.Farmers.Get;

public sealed record GetFarmersQuery(PageRequest Paging, Guid? BranchId, FarmerType? Type)
    : IQuery<PagedList<FarmerResponse>>;
