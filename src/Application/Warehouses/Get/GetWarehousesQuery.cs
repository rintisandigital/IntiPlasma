using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Domain.MasterData.Warehouses;
using SharedKernel;

namespace Application.Warehouses.Get;

public sealed record GetWarehousesQuery(PageRequest Paging, Guid? BranchId, WarehouseType? Type)
    : IQuery<PagedList<WarehouseResponse>>;
