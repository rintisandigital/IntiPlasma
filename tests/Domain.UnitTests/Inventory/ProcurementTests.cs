using Domain.Inventory.GoodsReceipts;
using Domain.Inventory.StockTransfers;
using Domain.MasterData.Items;
using Domain.MasterData.Warehouses;
using Domain.Procurement.PurchaseOrders;
using SharedKernel;

namespace Domain.UnitTests.Inventory;

public sealed class ProcurementTests
{
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid Feed = Guid.NewGuid();
    private static readonly Guid Doc = Guid.NewGuid();
    private static readonly Guid Sak = Guid.NewGuid();
    private static readonly Guid Ekor = Guid.NewGuid();
    private static readonly DateOnly OrderDate = new(2026, 10, 1);

    private static readonly Dictionary<Guid, ItemCategory> Categories = new()
    {
        [Feed] = ItemCategory.Feed,
        [Doc] = ItemCategory.Doc
    };

    private static PurchaseOrder ApprovedOrder()
    {
        PurchaseOrder order = PurchaseOrder.Create("PO/1", BranchId, Guid.NewGuid(), OrderDate, null, null,
        [
            new PurchaseOrderLineInput(Feed, Sak, 100m, new Money(430_000m), null),
            new PurchaseOrderLineInput(Doc, Ekor, 10_000m, new Money(7_500m), null)
        ]).Value;

        order.Approve(Guid.NewGuid(), DateTime.UtcNow);

        return order;
    }

    private static Warehouse Central() => Warehouse.CreateCentral("GI", "Gudang Induk", BranchId, null);

    private static Warehouse CoopWarehouse() => Warehouse.CreateForCoop("GK", "Gudang Kandang", BranchId, Guid.NewGuid(), null);

    [Fact]
    public void PurchaseOrder_Should_TrackPartialAndFullReceipt()
    {
        PurchaseOrder order = ApprovedOrder();

        order.RegisterReceipt(1, 60m).IsSuccess.ShouldBeTrue();
        order.Status.ShouldBe(PurchaseOrderStatus.PartiallyReceived);

        order.RegisterReceipt(1, 41m).Error.ShouldBe(PurchaseOrderErrors.OverReceipt(1, 40m));

        order.RegisterReceipt(1, 40m);
        order.RegisterReceipt(2, 10_000m);
        order.Status.ShouldBe(PurchaseOrderStatus.Received);
        order.RegisterReceipt(1, 1m).Error.ShouldBe(PurchaseOrderErrors.NotReceivable(order.Id));
    }

    [Fact]
    public void PurchaseOrder_Should_NotBeReceivedBeforeApproval()
    {
        PurchaseOrder order = PurchaseOrder.Create("PO/1", BranchId, Guid.NewGuid(), OrderDate, null, null,
            [new PurchaseOrderLineInput(Feed, Sak, 1m, new Money(1m), null)]).Value;

        order.RegisterReceipt(1, 1m).Error.ShouldBe(PurchaseOrderErrors.NotReceivable(order.Id));
    }

    [Fact]
    public void GoodsReceipt_Should_ValueAtOrderPricePerBaseUnit()
    {
        // 20 SAK @ Rp 430.000 = 1.000 KG at Rp 8.600/KG.
        GoodsReceipt receipt = GoodsReceipt.Create("BPB/1", ApprovedOrder(), Central(), null, OrderDate, "SJ-01", null,
            [new GoodsReceiptLineInput(1, 20m, 1_000m)], Categories).Value;

        GoodsReceiptLine line = receipt.Lines.Single();
        line.Value.ShouldBe(new Money(8_600_000m));
        line.UnitCost.ShouldBe(8_600m);
        receipt.DomainEvents.ShouldContain(e => e is GoodsReceiptPostedDomainEvent);
    }

    [Fact]
    public void GoodsReceipt_Should_AllowOnlyDocIntoCoopWarehouse()
    {
        Warehouse coop = CoopWarehouse();

        GoodsReceipt.Create("BPB/1", ApprovedOrder(), coop, Guid.NewGuid(), OrderDate, null, null,
                [new GoodsReceiptLineInput(1, 20m, 1_000m)], Categories)
            .Error.ShouldBe(GoodsReceiptErrors.OnlyDocToCoop);

        GoodsReceipt doc = GoodsReceipt.Create("BPB/2", ApprovedOrder(), coop, Guid.NewGuid(), OrderDate, null, null,
            [new GoodsReceiptLineInput(2, 10_000m, 10_000m)], Categories).Value;

        doc.CycleId.ShouldNotBeNull();
    }

    [Fact]
    public void GoodsReceipt_Should_RejectOverReceiptAndWrongBranch()
    {
        GoodsReceipt.Create("BPB/1", ApprovedOrder(), Central(), null, OrderDate, null, null,
                [new GoodsReceiptLineInput(1, 101m, 5_050m)], Categories)
            .Error.ShouldBe(PurchaseOrderErrors.OverReceipt(1, 100m));

        var otherBranch = Warehouse.CreateCentral("GX", "Gudang X", Guid.NewGuid(), null);
        GoodsReceipt.Create("BPB/1", ApprovedOrder(), otherBranch, null, OrderDate, null, null,
                [new GoodsReceiptLineInput(1, 1m, 50m)], Categories)
            .Error.ShouldBe(GoodsReceiptErrors.InvalidWarehouse);
    }

    [Fact]
    public void StockTransfer_Should_RequireCentralSource_AndCycleForCoop()
    {
        StockTransferLineInput[] lines = [new(Feed, Sak, 10m, 500m)];

        StockTransfer.Create("TRF/1", CoopWarehouse(), Central(), null, OrderDate, null, lines)
            .Error.ShouldBe(StockTransferErrors.SourceMustBeCentral);

        Warehouse coop = CoopWarehouse();
        StockTransfer.Create("TRF/1", Central(), coop, null, OrderDate, null, lines)
            .Error.ShouldBe(GoodsReceiptErrors.CoopHasNoOpenCycle(coop.CoopId!.Value));

        var cycleId = Guid.NewGuid();
        StockTransfer transfer = StockTransfer.Create("TRF/1", Central(), coop, cycleId, OrderDate, null, lines).Value;
        transfer.IsToCycle.ShouldBeTrue();
        transfer.CycleId.ShouldBe(cycleId);
    }

    [Fact]
    public void StockTransfer_Should_NotCrossBranches()
    {
        var otherBranch = Warehouse.CreateCentral("GX", "Gudang X", Guid.NewGuid(), null);

        StockTransfer.Create("TRF/1", Central(), otherBranch, null, OrderDate, null, [new(Feed, Sak, 1m, 50m)])
            .Error.ShouldBe(StockTransferErrors.CrossBranch);
    }
}
