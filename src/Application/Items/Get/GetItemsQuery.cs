using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Domain.MasterData.Items;
using SharedKernel;

namespace Application.Items.Get;

public sealed record GetItemsQuery(PageRequest Paging, ItemCategory? Category) : IQuery<PagedList<ItemResponse>>;
