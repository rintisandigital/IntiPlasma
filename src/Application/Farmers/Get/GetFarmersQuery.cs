using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Domain.MasterData.Farmers;
using SharedKernel;

namespace Application.Farmers.Get;

/// <param name="FieldOfficerId">Farmers of this PPL: assigned directly or through one of their coops.</param>
public sealed record GetFarmersQuery(PageRequest Paging, Guid? BranchId, FarmerType? Type, Guid? FieldOfficerId = null)
    : IQuery<PagedList<FarmerResponse>>;
