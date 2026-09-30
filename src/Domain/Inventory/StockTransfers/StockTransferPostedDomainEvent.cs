using SharedKernel;

namespace Domain.Inventory.StockTransfers;

public sealed record StockTransferPostedDomainEvent(Guid StockTransferId) : DomainEvent;
