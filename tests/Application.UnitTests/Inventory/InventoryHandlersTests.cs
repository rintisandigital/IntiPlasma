using Application.Abstractions.Authorization;
using Application.Abstractions.Numbering;
using Application.Inventory.GoodsReceipts;
using Application.Inventory.StockTransfers;
using Application.UnitTests.Abstractions;
using Domain.Common;
using Domain.Inventory.Stock;
using Domain.MasterData.Branches;
using Domain.MasterData.Coops;
using Domain.MasterData.Farmers;
using Domain.MasterData.Items;
using Domain.MasterData.Uoms;
using Domain.MasterData.Warehouses;
using Domain.Partnership.Cycles;
using Domain.Procurement.PurchaseOrders;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Inventory;

public sealed class InventoryHandlersTests : BaseHandlerTest
{
    private static readonly DateOnly Date = new(2026, 10, 3);

    [Fact]
    public async Task GoodsReceipt_Then_Transfer_Should_MoveStockAtMovingAverageCost()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Setup s = await SeedAsync(context);
        IDocumentNumberGenerator numbers = Numbers();

        var receive = new CreateGoodsReceiptCommandHandler(context, AllBranches(), numbers);
        var transfer = new CreateStockTransferCommandHandler(context, AllBranches(), numbers);

        // Act: receive 20 SAK (1.000 KG) at Rp 430.000/SAK, then send 10 SAK (500 KG) to the coop.
        Result<CreateGoodsReceiptResponse> received = await receive.Handle(
            new CreateGoodsReceiptCommand(s.Order.Id, s.Central.Id, Date, "SJ-1", null, [new GoodsReceiptLineRequest(1, 20m)]),
            CancellationToken.None);

        Result<CreateStockTransferResponse> sent = await transfer.Handle(
            new CreateStockTransferCommand(s.Central.Id, s.CoopWarehouse.Id, Date, null, [new StockTransferLineRequest(s.Feed.Id, s.Sak.Id, 10m)]),
            CancellationToken.None);

        // Assert
        received.IsSuccess.ShouldBeTrue();
        sent.IsSuccess.ShouldBeTrue();
        sent.Value.CycleId.ShouldBe(s.Cycle.Id);

        StockBalance central = await context.StockBalances.SingleAsync(b => b.WarehouseId == s.Central.Id);
        StockBalance coop = await context.StockBalances.SingleAsync(b => b.WarehouseId == s.CoopWarehouse.Id);

        central.Quantity.ShouldBe(500m);
        central.Value.ShouldBe(new Money(4_300_000m));
        coop.Quantity.ShouldBe(500m);
        coop.Value.ShouldBe(new Money(4_300_000m));

        (await context.StockLedgerEntries.CountAsync()).ShouldBe(3);
        (await context.PurchaseOrders.SingleAsync()).Status.ShouldBe(PurchaseOrderStatus.PartiallyReceived);
    }

    [Fact]
    public async Task Transfer_Should_Fail_WhenStockIsInsufficient()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Setup s = await SeedAsync(context);
        var transfer = new CreateStockTransferCommandHandler(context, AllBranches(), Numbers());

        // Act
        Result<CreateStockTransferResponse> result = await transfer.Handle(
            new CreateStockTransferCommand(s.Central.Id, s.CoopWarehouse.Id, Date, null, [new StockTransferLineRequest(s.Feed.Id, s.Kg.Id, 1m)]),
            CancellationToken.None);

        // Assert
        result.Error.Code.ShouldBe("Stock.Insufficient");
        (await context.StockTransfers.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task GoodsReceipt_Should_Fail_WhenQuantityExceedsOutstanding()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Setup s = await SeedAsync(context);
        IDocumentNumberGenerator numbers = Numbers();
        var receive = new CreateGoodsReceiptCommandHandler(context, AllBranches(), numbers);

        // Act
        Result<CreateGoodsReceiptResponse> result = await receive.Handle(
            new CreateGoodsReceiptCommand(s.Order.Id, s.Central.Id, Date, null, null, [new GoodsReceiptLineRequest(1, 101m)]),
            CancellationToken.None);

        // Assert
        result.Error.ShouldBe(PurchaseOrderErrors.OverReceipt(1, 100m));
        await numbers.DidNotReceiveWithAnyArgs().NextAsync(default!, default!, default, default);
    }

    private static IBranchAccess AllBranches()
    {
        IBranchAccess branchAccess = Substitute.For<IBranchAccess>();
        branchAccess.GetScopeAsync(Arg.Any<CancellationToken>()).Returns(BranchScope.All);

        return branchAccess;
    }

    private static IDocumentNumberGenerator Numbers()
    {
        IDocumentNumberGenerator numbers = Substitute.For<IDocumentNumberGenerator>();
        numbers.NextAsync(default!, default!, default, default)
            .ReturnsForAnyArgs(call => $"{call.ArgAt<string>(0)}/BDG/2026/X/0001");

        return numbers;
    }

    private static async Task<Setup> SeedAsync(TestDbContext context)
    {
        var branch = Branch.Create("BDG", "Bandung", null, null);
        var kg = Uom.Create("KG", "Kilogram");
        var sak = Uom.Create("SAK", "Sak");
        var feed = Item.Create("PKN-1", "Pakan BR-1", ItemCategory.Feed, kg.Id, null);
        feed.SetConversions([(sak.Id, 50m)]);

        Farmer farmer = Farmer.Create("INT-1", "Farm Inti", FarmerType.Inti, branch.Id, null,
            TaxIdentity.None, null, null, BankAccount.None).Value;
        Coop coop = Coop.Create(farmer, "KDG-1", "Kandang 1", 10_000, HouseType.ClosedHouse, null, null, null).Value;
        ProductionCycle cycle = ProductionCycle.Plan("SKL/1", coop, farmer, null, Date, 9_000, null).Value;

        var central = Warehouse.CreateCentral("GI-BDG", "Gudang Induk", branch.Id, null);
        var coopWarehouse = Warehouse.CreateForCoop("GK-KDG-1", "Gudang Kandang 1", branch.Id, coop.Id, null);

        PurchaseOrder order = PurchaseOrder.Create("PO/1", branch.Id, Guid.NewGuid(), Date, null, null,
            [new PurchaseOrderLineInput(feed.Id, sak.Id, 100m, new Money(430_000m), null)]).Value;
        order.Approve(Guid.NewGuid(), DateTime.UtcNow);

        context.Branches.Add(branch);
        context.Uoms.AddRange(kg, sak);
        context.Items.Add(feed);
        context.Farmers.Add(farmer);
        context.Coops.Add(coop);
        context.ProductionCycles.Add(cycle);
        context.Warehouses.AddRange(central, coopWarehouse);
        context.PurchaseOrders.Add(order);
        await context.SaveChangesAsync();

        return new Setup(order, feed, kg, sak, central, coopWarehouse, cycle);
    }

    private sealed record Setup(
        PurchaseOrder Order,
        Item Feed,
        Uom Kg,
        Uom Sak,
        Warehouse Central,
        Warehouse CoopWarehouse,
        ProductionCycle Cycle);
}
