using Application.Abstractions.Messaging;

namespace Application.Branches.GetById;

public sealed record GetBranchByIdQuery(Guid BranchId) : IQuery<BranchResponse>;
