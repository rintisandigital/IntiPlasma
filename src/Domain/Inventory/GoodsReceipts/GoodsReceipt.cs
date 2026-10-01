using Domain.MasterData.Items;
using Domain.MasterData.Warehouses;
using Domain.Procurement.PurchaseOrders;
using SharedKernel;

namespace Domain.Inventory.GoodsReceipts;

/// <summary>
/// Bukti penerimaan barang (BPB) against a purchase order. Feed and OVK are received into a central warehouse;
/// DOC can also be delivered directly to a coop warehouse, in which case it belongs to the coop's open cycle.
/// Stock is valued at the order price (excluding VAT) per base unit.
/// </summary>
public sealed class GoodsReceipt : AggregateRoot
{
    private readonly List<GoodsReceiptLine> _lines = [];

    private GoodsReceipt(Guid id)
        : base(id)
    {
    }

    private GoodsReceipt()
    {
    }

    public string Number { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid PurchaseOrderId { get; private set; }
    public Guid VendorId { get; private set; }
    public Guid WarehouseId { get; private set; }

    /// <summary>
    /// Set when DOC is delivered directly to a coop: the cycle it is placed into.
    /// </summary>
    public Guid? CycleId { get; private set; }

    public DateOnly ReceiptDate { get; private set; }

    /// <summary>
    /// Vendor's delivery note (surat jalan) number.
    /// </summary>
    public string? DeliveryNoteNumber { get; private set; }

    public string? Notes { get; private set; }
    public IReadOnlyCollection<GoodsReceiptLine> Lines => [.. _lines];

    public Money TotalValue => _lines.Aggregate(Money.Zero, (total, line) => total + line.Value);

    /// <param name="lines">Received quantities per order line, with the quantity converted to the item's base unit.</param>
    /// <param name="itemCategories">Category of every item on the order.</param>
    public static Result<GoodsReceipt> Create(
        string number,
        PurchaseOrder order,
        Warehouse warehouse,
        Guid? coopCycleId,
        DateOnly receiptDate,
        string? deliveryNoteNumber,
        string? notes,
        IReadOnlyList<GoodsReceiptLineInput> lines,
        IReadOnlyDictionary<Guid, ItemCategory> itemCategories)
    {
        Result validation = Validate(order, warehouse, coopCycleId, receiptDate, lines, itemCategories);
        if (validation.IsFailure)
        {
            return Result.Failure<GoodsReceipt>(validation.Error);
        }

        var receipt = new GoodsReceipt(Guid.CreateVersion7())
        {
            Number = number,
            BranchId = order.BranchId,
            PurchaseOrderId = order.Id,
            VendorId = order.VendorId,
            WarehouseId = warehouse.Id,
            CycleId = warehouse.Type == WarehouseType.Coop ? coopCycleId : null,
            ReceiptDate = receiptDate,
            DeliveryNoteNumber = deliveryNoteNumber,
            Notes = notes
        };

        foreach ((GoodsReceiptLineInput input, int index) in lines.Select((l, i) => (l, i)))
        {
            PurchaseOrderLine orderLine = order.Lines.Single(l => l.LineNumber == input.PurchaseOrderLineNumber);
            Money value = orderLine.UnitPrice * input.Quantity;

            receipt._lines.Add(new GoodsReceiptLine(
                receipt.Id,
                index + 1,
                orderLine.LineNumber,
                orderLine.ItemId,
                orderLine.UomId,
                input.Quantity,
                input.BaseQuantity,
                decimal.Round(value.Amount / input.BaseQuantity, Stock.StockLedgerEntry.UnitCostDecimals),
                value));
        }

        receipt.Raise(new GoodsReceiptPostedDomainEvent(receipt.Id));

        return receipt;
    }

    /// <summary>
    /// 3-way match: bills a quantity (order unit) of a receipt line on a vendor invoice and returns its receipt value
    /// (the amount cleared from hutang belum ditagih).
    /// </summary>
    public Result<Money> RegisterInvoice(int lineNumber, decimal quantity)
    {
        GoodsReceiptLine? line = _lines.Find(l => l.LineNumber == lineNumber);
        if (line is null)
        {
            return Result.Failure<Money>(GoodsReceiptErrors.LineNotFound(lineNumber));
        }

        if (quantity <= 0 || quantity > line.UninvoicedQuantity)
        {
            return Result.Failure<Money>(GoodsReceiptErrors.OverInvoiced(Number, lineNumber, line.UninvoicedQuantity));
        }

        return line.Invoice(quantity);
    }

    /// <summary>
    /// Takes back a billed quantity when its draft vendor invoice is cancelled.
    /// </summary>
    public void ReleaseInvoice(int lineNumber, decimal quantity, Money value)
    {
        _lines.Single(l => l.LineNumber == lineNumber).ReleaseInvoice(quantity, value);
    }

    private static Result Validate(
        PurchaseOrder order,
        Warehouse warehouse,
        Guid? coopCycleId,
        DateOnly receiptDate,
        IReadOnlyList<GoodsReceiptLineInput> lines,
        IReadOnlyDictionary<Guid, ItemCategory> itemCategories)
    {
        if (!order.CanReceive)
        {
            return Result.Failure(PurchaseOrderErrors.NotReceivable(order.Id));
        }

        if (!warehouse.IsActive || warehouse.BranchId != order.BranchId)
        {
            return Result.Failure(GoodsReceiptErrors.InvalidWarehouse);
        }

        if (receiptDate < order.OrderDate)
        {
            return Result.Failure(GoodsReceiptErrors.BeforeOrderDate);
        }

        if (lines.Count == 0 || lines.Any(l => l.Quantity <= 0 || l.BaseQuantity <= 0))
        {
            return Result.Failure(GoodsReceiptErrors.InvalidLines);
        }

        if (lines.GroupBy(l => l.PurchaseOrderLineNumber).Any(g => g.Count() > 1))
        {
            return Result.Failure(GoodsReceiptErrors.InvalidLines);
        }

        GoodsReceiptLineInput? unknownLine = lines.FirstOrDefault(
            l => order.Lines.All(ol => ol.LineNumber != l.PurchaseOrderLineNumber));

        if (unknownLine is not null)
        {
            return Result.Failure(PurchaseOrderErrors.LineNotFound(unknownLine.PurchaseOrderLineNumber));
        }

        foreach (GoodsReceiptLineInput line in lines)
        {
            PurchaseOrderLine orderLine = order.Lines.Single(l => l.LineNumber == line.PurchaseOrderLineNumber);
            if (line.Quantity > orderLine.OutstandingQuantity)
            {
                return Result.Failure(PurchaseOrderErrors.OverReceipt(orderLine.LineNumber, orderLine.OutstandingQuantity));
            }
        }

        if (warehouse.Type == WarehouseType.Coop)
        {
            bool onlyDoc = lines
                .Select(l => order.Lines.Single(ol => ol.LineNumber == l.PurchaseOrderLineNumber).ItemId)
                .All(itemId => itemCategories[itemId] == ItemCategory.Doc);

            if (!onlyDoc)
            {
                return Result.Failure(GoodsReceiptErrors.OnlyDocToCoop);
            }

            if (coopCycleId is null)
            {
                return Result.Failure(GoodsReceiptErrors.CoopHasNoOpenCycle(warehouse.CoopId!.Value));
            }
        }

        return Result.Success();
    }
}

/// <param name="Quantity">Received quantity in the order line's unit.</param>
/// <param name="BaseQuantity">The same quantity converted to the item's base unit.</param>
public sealed record GoodsReceiptLineInput(int PurchaseOrderLineNumber, decimal Quantity, decimal BaseQuantity);
