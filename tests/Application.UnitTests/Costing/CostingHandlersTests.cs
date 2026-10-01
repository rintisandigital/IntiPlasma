using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Numbering;
using Application.Costing;
using Application.Cycles.Start;
using Application.Production;
using Application.Sales;
using Application.UnitTests.Abstractions;
using Domain.Common;
using Domain.Finance.FiscalPeriods;
using Domain.Inventory.Stock;
using Domain.MasterData.Branches;
using Domain.MasterData.Coops;
using Domain.MasterData.Customers;
using Domain.MasterData.Farmers;
using Domain.MasterData.Items;
using Domain.MasterData.Uoms;
using Domain.MasterData.Warehouses;
using Domain.Partnership.Cycles;
using Domain.Sales.SalesInvoices;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Costing;

public sealed class CostingHandlersTests : BaseHandlerTest
{
    private static readonly DateOnly ChickIn = new(2026, 9, 1);
    private static readonly DateOnly HarvestDate = ChickIn.AddDays(30);

    [Fact]
    public async Task Invoice_Should_RecognizeEstimatedHpp_AndClosingShouldFreezeTheFinalCost()
    {
        await using TestDbContext context = CreateDbContext();
        Setup s = await SeedAsync(context);

        // Chick-in places 1.000 DOC @ Rp 7.500 from the coop warehouse: the cycle's cost so far is Rp 7.500.000.
        await new StartCycleCommandHandler(context, AllBranches())
            .Handle(new StartCycleCommand(s.Cycle.Id, ChickIn, [new ChickInLine(s.Doc.Id, 1_000)]), CancellationToken.None);

        // Half of the birds harvested (1.000 kg); the rest still in the coop at a recorded BW of 2.000 g.
        await new CreateDailyRecordingCommandHandler(context, AllBranches(), Clock())
            .Handle(new CreateDailyRecordingCommand(null, s.Cycle.Id, HarvestDate, 0, 0, 2_000m, null, []), CancellationToken.None);
        Guid harvest = (await new RecordHarvestCommandHandler(context, AllBranches(), Clock())
            .Handle(new RecordHarvestCommand(s.Cycle.Id, HarvestDate, 500, 1_000m, null), CancellationToken.None)).Value;

        CycleCostResponse running = (await new GetCycleCostQueryHandler(context, AllBranches())
            .Handle(new GetCycleCostQuery(s.Cycle.Id), CancellationToken.None)).Value;

        // 7.500.000 / (1.000 kg harvested + 500 birds × 2 kg) = 3.750 per kg
        running.IsFinal.ShouldBeFalse();
        running.TotalCost.ShouldBe(7_500_000m);
        running.LiveWeightKg.ShouldBe(2_000m);
        running.CostPerKg.ShouldBe(3_750m);

        Guid invoiceId = await SellAsync(context, s, harvest, birds: 500);

        SalesInvoice invoice = await context.SalesInvoices.Include(i => i.Lines).SingleAsync(i => i.Id == invoiceId);
        invoice.CostAmount.ShouldBe(new Money(3_750_000m));

        // The rest is harvested lighter than recorded: 500 birds / 900 kg; final cost per kg = 7.500.000 / 1.900.
        Guid second = (await new RecordHarvestCommandHandler(context, AllBranches(), Clock())
            .Handle(new RecordHarvestCommand(s.Cycle.Id, HarvestDate.AddDays(1), 500, 900m, null), CancellationToken.None)).Value;
        await SellAsync(context, s, second, birds: 500);

        Result<CyclePerformance> closed = await new CloseCycleCommandHandler(context, AllBranches())
            .Handle(new CloseCycleCommand(s.Cycle.Id), CancellationToken.None);

        closed.IsSuccess.ShouldBeTrue();
        CycleCostSummary cost = (await context.ProductionCycles.SingleAsync(c => c.Id == s.Cycle.Id)).ClosingCost!;
        cost.TotalCost.ShouldBe(7_500_000m);
        cost.CostPerKg.ShouldBe(3_947.37m);

        // Recognized: 1.000 kg × 3.750 + 900 kg × (7.500.000 / (1.000 + 900)) rounded = 3.750.000 + 3.552.633
        cost.RecognizedCost.ShouldBe(7_302_633m);
        cost.Adjustment.ShouldBe(197_367m);
        (await context.ProductionCycles.SingleAsync(c => c.Id == s.Cycle.Id)).DomainEvents
            .OfType<CycleClosedDomainEvent>().ShouldHaveSingleItem();
    }

    private static async Task<Guid> SellAsync(TestDbContext context, Setup s, Guid harvestId, int birds)
    {
        Guid orderId = (await new CreateSalesOrderCommandHandler(context, AllBranches(), Numbers()).Handle(
            new CreateSalesOrderCommand(s.BranchId, s.Customer.Id, HarvestDate, null, null,
                [new SalesOrderLineRequest(s.LiveBird.Id, birds, birds * 2m, 20_000m, null)]),
            CancellationToken.None)).Value.Id;

        await new ApproveSalesOrderCommandHandler(context, AllBranches(), User(), Clock())
            .Handle(new ApproveSalesOrderCommand(orderId, null), CancellationToken.None);

        Guid delivery = (await new CreateDeliveryOrderCommandHandler(context, AllBranches(), Numbers(), Clock()).Handle(
            new CreateDeliveryOrderCommand(orderId, HarvestDate.AddDays(1), null, null, null, [new DeliveryOrderLineRequest(1, harvestId)]),
            CancellationToken.None)).Value.Id;

        Guid invoiceId = (await new CreateSalesInvoiceCommandHandler(context, AllBranches(), Clock())
            .Handle(new CreateSalesInvoiceCommand([delivery], HarvestDate.AddDays(1), null), CancellationToken.None)).Value;

        (await new PostSalesInvoiceCommandHandler(context, AllBranches(), Numbers(), User(), Clock())
            .Handle(new PostSalesInvoiceCommand(invoiceId), CancellationToken.None)).IsSuccess.ShouldBeTrue();

        return invoiceId;
    }

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
        numbers.NextAsync(default!, default!, default, default)
            .ReturnsForAnyArgs(call => $"{call.ArgAt<string>(0)}/BDG/{Guid.NewGuid():N}");

        return numbers;
    }

    /// <summary>
    /// An inti coop with a planned cycle and 1.000 DOC (Rp 7.500.000) in its warehouse; a customer with ample credit.
    /// </summary>
    private static async Task<Setup> SeedAsync(TestDbContext context)
    {
        var branch = Branch.Create("BDG", "Bandung", null, null);
        var kg = Uom.Create("KG", "Kilogram");
        var ekor = Uom.Create("EKOR", "Ekor");
        var doc = Item.Create("DOC", "DOC", ItemCategory.Doc, ekor.Id, null);
        var liveBird = Item.Create("AYM", "Ayam Hidup", ItemCategory.LiveBird, kg.Id, null);
        var customer = Customer.Create("RPA-01", "RPA", TaxIdentity.None, null, null, null, 14, new Money(1_000_000_000m));

        Farmer farmer = Farmer.Create("INT", "Farm Inti", FarmerType.Inti, branch.Id, null, TaxIdentity.None, null, null, BankAccount.None).Value;
        Coop coop = Coop.Create(farmer, "KDG-A", "Kandang A", 5_000, HouseType.ClosedHouse, null, null, null).Value;
        var warehouse = Warehouse.CreateForCoop("GK-A", "Gudang A", branch.Id, coop.Id, null);
        ProductionCycle cycle = ProductionCycle.Plan("SKL/A", coop, farmer, null, ChickIn, 1_000, null).Value;

        var docBalance = StockBalance.Open(warehouse.Id, doc.Id);
        docBalance.Receive(
            new StockMovement(ChickIn, StockMovementType.Receipt, "Seed", Guid.NewGuid(), "SEED", cycle.Id), 1_000m, new Money(7_500_000m));

        context.Branches.Add(branch);
        context.Uoms.AddRange(kg, ekor);
        context.Items.AddRange(doc, liveBird);
        context.Customers.Add(customer);
        context.Farmers.Add(farmer);
        context.Coops.Add(coop);
        context.Warehouses.Add(warehouse);
        context.ProductionCycles.Add(cycle);
        context.FiscalPeriods.AddRange(FiscalPeriod.CreateYear(2026));
        context.StockBalances.Add(docBalance);
        await context.SaveChangesAsync();

        return new Setup(branch.Id, doc, liveBird, customer, cycle);
    }

    private sealed record Setup(Guid BranchId, Item Doc, Item LiveBird, Customer Customer, ProductionCycle Cycle);
}
