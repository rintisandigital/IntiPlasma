using SharedKernel;

namespace Domain.Inventory.GoodsReceipts;

public sealed record GoodsReceiptPostedDomainEvent(Guid GoodsReceiptId) : DomainEvent;
