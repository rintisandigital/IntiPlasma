using Application.Abstractions.Messaging;

namespace Application.Farmers.GetById;

public sealed record GetFarmerByIdQuery(Guid FarmerId) : IQuery<FarmerResponse>;
