using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Numbering;
using Application.Cycles.Start;
using Application.Inventory.StockReturns;
using Application.Inventory.StockTransfers;
using Application.Production;
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
using Domain.Production.DailyRecordings;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Production;

public sealed class ProductionHandlersTests : BaseHandlerTest
{
    private static readonly DateOnly ChickIn = new(2026, 10, 1);

    [Fact]
    public async Task ChickIn_Should_PlaceDocFromCoopWarehouse()
    {
        await using TestDbContext context = CreateDbContext();
        Setup s = await SeedAsync(context);

        Result<int> result = await new StartCycleCommandHandler(context, AllBranches(), NoFieldScope(), CreateAttachments(context))
            .Handle(new StartCycleCommand(s.CycleA.Id, ChickIn, [new ChickInLine(s.Doc.Id, 1_000)]), CancellationToken.None);

        result.Value.ShouldBe(1_000);
        (await Balance(context, s.CoopA, s.Doc)).Quantity.ShouldBe(0m);
        (await context.ProductionCycles.SingleAsync(c => c.Id == s.CycleA.Id)).Status.ShouldBe(CycleStatus.Active);
    }

    [Fact]
    public async Task ChickIn_Should_Fail_WithoutEnoughDoc()
    {
        await using TestDbContext context = CreateDbContext();
        Setup s = await SeedAsync(context);

        Result<int> result = await new StartCycleCommandHandler(context, AllBranches(), NoFieldScope(), CreateAttachments(context))
            .Handle(new StartCycleCommand(s.CycleA.Id, ChickIn, [new ChickInLine(s.Doc.Id, 1_001)]), CancellationToken.None);

        result.Error.ShouldBe(CycleErrors.InsufficientDoc(1_001, 1_000m));
    }

    [Fact]
    public async Task Recording_Should_UseFeed_BeIdempotent_AndRevisionShouldCorrectStock()
    {
        await using TestDbContext context = CreateDbContext();
        Setup s = await SeedAsync(context);
        await StartAsync(context, s);
        var create = new CreateDailyRecordingCommandHandler(context, AllBranches(), NoFieldScope(), Clock(), CreateAttachments(context));
        var clientId = Guid.CreateVersion7();

        // Day 1: 3 dead, 2 SAK (100 kg) feed.
        var command = new CreateDailyRecordingCommand(
            clientId, s.CycleA.Id, ChickIn.AddDays(1), 3, 0, 45m, null, [new UsageRequest(s.Feed.Id, s.Sak.Id, 2m)]);

        (await create.Handle(command, CancellationToken.None)).Value.ShouldBe(clientId);
        (await create.Handle(command, CancellationToken.None)).Value.ShouldBe(clientId); // offline retry

        (await Balance(context, s.CoopA, s.Feed)).Quantity.ShouldBe(400m);
        (await context.ProductionCycles.SingleAsync(c => c.Id == s.CycleA.Id)).TotalMortality.ShouldBe(3);

        // Revision: it was actually 5 dead and 1 SAK.
        Result revised = await new ReviseDailyRecordingCommandHandler(context, AllBranches(), NoFieldScope(), User(), Clock(), CreateAttachments(context)).Handle(
            new ReviseDailyRecordingCommand(clientId, "koreksi PPL", 5, 0, 45m, null, [new UsageRequest(s.Feed.Id, s.Sak.Id, 1m)]),
            CancellationToken.None);

        revised.IsSuccess.ShouldBeTrue();
        StockBalance feed = await Balance(context, s.CoopA, s.Feed);
        feed.Quantity.ShouldBe(450m);
        feed.Value.ShouldBe(new Money(4_500_000m));
        (await context.ProductionCycles.SingleAsync(c => c.Id == s.CycleA.Id)).TotalMortality.ShouldBe(5);
        (await context.DailyRecordings.Include(r => r.Revisions).SingleAsync()).Revisions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Recording_Should_Fail_WhenSameDayRecordedTwice()
    {
        await using TestDbContext context = CreateDbContext();
        Setup s = await SeedAsync(context);
        await StartAsync(context, s);
        var create = new CreateDailyRecordingCommandHandler(context, AllBranches(), NoFieldScope(), Clock(), CreateAttachments(context));

        await create.Handle(new CreateDailyRecordingCommand(null, s.CycleA.Id, ChickIn, 1, 0, null, null, []), CancellationToken.None);
        Result<Guid> second = await create.Handle(
            new CreateDailyRecordingCommand(null, s.CycleA.Id, ChickIn, 1, 0, null, null, []), CancellationToken.None);

        second.Error.ShouldBe(DailyRecordingErrors.AlreadyRecorded(ChickIn));
    }

    [Fact]
    public async Task FeedMutation_Should_GoThroughCentralWarehouse()
    {
        await using TestDbContext context = CreateDbContext();
        Setup s = await SeedAsync(context);

        Result<CreateFeedMutationResponse> result = await new CreateFeedMutationCommandHandler(context, AllBranches(), Numbers(), CreateAttachments(context))
            .Handle(
                new CreateFeedMutationCommand(s.CoopA.Id, s.Central.Id, s.CoopB.Id, ChickIn, "kandang B kekurangan pakan",
                    [new StockTransferLineRequest(s.Feed.Id, s.Sak.Id, 4m)]),
                CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.FromCycleId.ShouldBe(s.CycleA.Id);
        result.Value.ToCycleId.ShouldBe(s.CycleB.Id);

        (await Balance(context, s.CoopA, s.Feed)).Quantity.ShouldBe(300m);
        (await Balance(context, s.Central, s.Feed)).Quantity.ShouldBe(0m);
        (await Balance(context, s.CoopB, s.Feed)).Quantity.ShouldBe(200m);

        // The central warehouse's stock card shows the feed passing through: return in, then transfer out.
        List<StockMovementType> centralMovements = await context.StockLedgerEntries
            .Where(e => e.WarehouseId == s.Central.Id)
            .Select(e => e.Type)
            .ToListAsync();

        centralMovements.ShouldBe([StockMovementType.ReturnIn, StockMovementType.TransferOut], ignoreOrder: true);
    }

    [Fact]
    public async Task Close_Should_RequireLeftoverFeedToBeReturned()
    {
        await using TestDbContext context = CreateDbContext();
        Setup s = await SeedAsync(context);
        await StartAsync(context, s);

        await new RecordHarvestCommandHandler(context, AllBranches(), NoFieldScope(), Clock(), CreateAttachments(context))
            .Handle(new RecordHarvestCommand(s.CycleA.Id, ChickIn.AddDays(35), 1_000, 2_000m, null), CancellationToken.None);

        var close = new CloseCycleCommandHandler(context, AllBranches(), NoFieldScope());
        (await close.Handle(new CloseCycleCommand(s.CycleA.Id), CancellationToken.None)).Error.Code.ShouldBe("Cycles.LeftoverStock");

        await new CreateStockReturnCommandHandler(context, AllBranches(), Numbers(), CreateAttachments(context)).Handle(
            new CreateStockReturnCommand(s.CoopA.Id, s.Central.Id, ChickIn.AddDays(35), "sisa pakan", null,
                [new StockTransferLineRequest(s.Feed.Id, s.Kg.Id, 500m)]),
            CancellationToken.None);

        // With the stock returned, the harvest still has to be sold (see SalesHandlersTests for a successful close).
        Result<CyclePerformance> closed = await close.Handle(new CloseCycleCommand(s.CycleA.Id), CancellationToken.None);

        closed.Error.ShouldBe(CycleErrors.UnsoldHarvest(1));
        (await context.ProductionCycles.SingleAsync(c => c.Id == s.CycleA.Id)).Status.ShouldBe(CycleStatus.Harvesting);
    }

    private static async Task StartAsync(TestDbContext context, Setup s) =>
        await new StartCycleCommandHandler(context, AllBranches(), NoFieldScope(), CreateAttachments(context))
            .Handle(new StartCycleCommand(s.CycleA.Id, ChickIn, [new ChickInLine(s.Doc.Id, 1_000)]), CancellationToken.None);

    private static Task<StockBalance> Balance(TestDbContext context, Warehouse warehouse, Item item) =>
        context.StockBalances.SingleAsync(b => b.WarehouseId == warehouse.Id && b.ItemId == item.Id);

    private static IBranchAccess AllBranches()
    {
        IBranchAccess branchAccess = Substitute.For<IBranchAccess>();
        branchAccess.GetScopeAsync(Arg.Any<CancellationToken>()).Returns(BranchScope.All);

        return branchAccess;
    }

    private static IDateTimeProvider Clock()
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc));

        return clock;
    }

    private static IUserContext User()
    {
        IUserContext user = Substitute.For<IUserContext>();
        user.UserId.Returns(Guid.NewGuid());

        return user;
    }

    private static IDocumentNumberGenerator Numbers()
    {
        IDocumentNumberGenerator numbers = Substitute.For<IDocumentNumberGenerator>();
        numbers.NextAsync(default!, default!, default, default).ReturnsForAnyArgs(_ => $"DOC/{Guid.NewGuid():N}");

        return numbers;
    }

    /// <summary>
    /// Two inti coops with a planned cycle each. Coop A holds 1.000 DOC and 500 kg feed (Rp 10.000/kg); coop B holds none.
    /// </summary>
    private static async Task<Setup> SeedAsync(TestDbContext context)
    {
        var branch = Branch.Create("BDG", "Bandung", null, null);
        var kg = Uom.Create("KG", "Kilogram");
        var sak = Uom.Create("SAK", "Sak");
        var ekor = Uom.Create("EKOR", "Ekor");
        var feed = Item.Create("PKN", "Pakan", ItemCategory.Feed, kg.Id, null);
        feed.SetConversions([(sak.Id, 50m)]);
        var doc = Item.Create("DOC", "DOC", ItemCategory.Doc, ekor.Id, null);

        Farmer farmer = Farmer.Create("INT", "Farm Inti", FarmerType.Inti, branch.Id, null, TaxIdentity.None, null, null, BankAccount.None).Value;
        Coop coopA = Coop.Create(farmer, "KDG-A", "Kandang A", 5_000, HouseType.ClosedHouse, null, null, null).Value;
        Coop coopB = Coop.Create(farmer, "KDG-B", "Kandang B", 5_000, HouseType.ClosedHouse, null, null, null).Value;
        ProductionCycle cycleA = ProductionCycle.Plan("SKL/A", coopA, farmer, null, ChickIn, 1_000, null).Value;
        ProductionCycle cycleB = ProductionCycle.Plan("SKL/B", coopB, farmer, null, ChickIn, 1_000, null).Value;

        var central = Warehouse.CreateCentral("GI", "Gudang Induk", branch.Id, null);
        var warehouseA = Warehouse.CreateForCoop("GK-A", "Gudang A", branch.Id, coopA.Id, null);
        var warehouseB = Warehouse.CreateForCoop("GK-B", "Gudang B", branch.Id, coopB.Id, null);

        var movement = new StockMovement(ChickIn, StockMovementType.TransferIn, "Seed", Guid.NewGuid(), "SEED", cycleA.Id);
        var docBalance = StockBalance.Open(warehouseA.Id, doc.Id);
        var feedBalance = StockBalance.Open(warehouseA.Id, feed.Id);
        docBalance.Receive(movement, 1_000m, new Money(7_500_000m));
        feedBalance.Receive(movement, 500m, new Money(5_000_000m));

        context.Branches.Add(branch);
        context.Uoms.AddRange(kg, sak, ekor);
        context.Items.AddRange(feed, doc);
        context.Farmers.Add(farmer);
        context.Coops.AddRange(coopA, coopB);
        context.ProductionCycles.AddRange(cycleA, cycleB);
        context.Warehouses.AddRange(central, warehouseA, warehouseB);
        context.StockBalances.AddRange(docBalance, feedBalance);
        await context.SaveChangesAsync();

        return new Setup(feed, doc, kg, sak, central, warehouseA, warehouseB, cycleA, cycleB);
    }

    private sealed record Setup(
        Item Feed,
        Item Doc,
        Uom Kg,
        Uom Sak,
        Warehouse Central,
        Warehouse CoopA,
        Warehouse CoopB,
        ProductionCycle CycleA,
        ProductionCycle CycleB);
}
