using SharedKernel;

namespace Domain.Inventory.StockReturns;

public sealed record StockReturnPostedDomainEvent(Guid StockReturnId) : DomainEvent;
