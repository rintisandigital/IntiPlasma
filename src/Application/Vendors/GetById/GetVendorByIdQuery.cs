using Application.Abstractions.Messaging;

namespace Application.Vendors.GetById;

public sealed record GetVendorByIdQuery(Guid VendorId) : IQuery<VendorResponse>;
