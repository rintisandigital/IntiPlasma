using Application.Abstractions.Messaging;

namespace Application.Items.GetById;

public sealed record GetItemByIdQuery(Guid ItemId) : IQuery<ItemResponse>;
