using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Numbering;
using Application.Finance.Receivables;
using Application.Production;
using Application.Sales;
using Application.UnitTests.Abstractions;
using Domain.Common;
using Domain.Finance.FiscalPeriods;
using Domain.Finance.Accounts;
using Domain.Finance.CashBank;
using Domain.Finance.Receivables;
using Domain.MasterData.Branches;
using Domain.MasterData.Coops;
using Domain.MasterData.Customers;
using Domain.MasterData.Farmers;
using Domain.MasterData.Items;
using Domain.MasterData.Uoms;
using Domain.MasterData.Warehouses;
using Domain.Partnership.Cycles;
using Domain.Sales.DeliveryOrders;
using Domain.Sales.SalesInvoices;
using Domain.Sales.SalesOrders;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Sales;

public sealed class SalesHandlersTests : BaseHandlerTest
{
    private static readonly DateOnly ChickIn = new(2026, 10, 1);
    private static readonly DateOnly OrderDate = new(2026, 11, 1);
    private static readonly DateOnly HarvestDate = ChickIn.AddDays(35);

    [Fact]
    public async Task Sale_Should_FlowFromOrderToReceipt_AndThenAllowTheCycleToClose()
    {
        await using TestDbContext context = CreateDbContext();
        Setup s = await SeedAsync(context);

        Guid orderId = await CreateApprovedOrderAsync(context, s, birds: 1_000);

        // DO for the first truck; the same harvest cannot be delivered twice.
        Guid firstDelivery = (await Deliver(context, orderId, s.FirstHarvest)).Value.Id;
        (await Deliver(context, orderId, s.FirstHarvest)).Error.ShouldBe(DeliveryOrderErrors.HarvestAlreadyDelivered(s.FirstHarvest));

        var close = new CloseCycleCommandHandler(context, AllBranches());
        (await close.Handle(new CloseCycleCommand(s.Cycle.Id), CancellationToken.None)).Error.ShouldBe(CycleErrors.UnsoldHarvest(2));

        Guid secondDelivery = (await Deliver(context, orderId, s.SecondHarvest)).Value.Id;
        (await context.SalesOrders.SingleAsync()).Status.ShouldBe(SalesOrderStatus.Delivered);

        Result<Guid> invoice = await new CreateSalesInvoiceCommandHandler(context, AllBranches(), Clock())
            .Handle(new CreateSalesInvoiceCommand([firstDelivery, secondDelivery], HarvestDate, null), CancellationToken.None);

        // A draft invoice does not count as sold yet.
        (await close.Handle(new CloseCycleCommand(s.Cycle.Id), CancellationToken.None)).Error.ShouldBe(CycleErrors.UnsoldHarvest(2));

        Result<string> number = await new PostSalesInvoiceCommandHandler(context, AllBranches(), Numbers(), User(), Clock())
            .Handle(new PostSalesInvoiceCommand(invoice.Value), CancellationToken.None);

        number.Value.ShouldStartWith("INV/");

        // 600 birds / 1.200 kg + 400 birds / 820 kg = 2.020 kg @ Rp 20.000 (no VAT code).
        SalesInvoice posted = await context.SalesInvoices.SingleAsync();
        posted.Total.ShouldBe(new Money(40_400_000m));
        posted.DueDate.ShouldBe(HarvestDate.AddDays(14));

        Result<CreateCustomerReceiptResponse> receipt = await new CreateCustomerReceiptCommandHandler(context, AllBranches(), Numbers(), Clock())
            .Handle(
                new CreateCustomerReceiptCommand(s.Bank.Id, s.Customer.Id, HarvestDate.AddDays(3), "TRF-01", null,
                    [new ReceiptAllocationRequest(posted.Id, 30_000_000m)]),
                CancellationToken.None);

        receipt.Value.Amount.ShouldBe(30_000_000m);
        posted.Status.ShouldBe(SalesInvoiceStatus.PartiallyPaid);
        posted.Outstanding.ShouldBe(new Money(10_400_000m));
        (await context.CustomerReceipts.SingleAsync()).DomainEvents.OfType<CustomerReceiptPostedDomainEvent>().ShouldHaveSingleItem();

        Result<CyclePerformance> closed = await close.Handle(new CloseCycleCommand(s.Cycle.Id), CancellationToken.None);

        closed.IsSuccess.ShouldBeTrue();
        closed.Value.HarvestedBirds.ShouldBe(1_000);
        closed.Value.AverageWeightKg.ShouldBe(2.02m);
        (await context.ProductionCycles.SingleAsync(c => c.Id == s.Cycle.Id)).Status.ShouldBe(CycleStatus.Closed);
    }

    [Fact]
    public async Task Approve_Should_CountOpenOrdersInTheCreditExposure()
    {
        await using TestDbContext context = CreateDbContext();
        Setup s = await SeedAsync(context);

        // Credit limit Rp 50.000.000; each order is 1.000 birds / 2.000 kg @ Rp 20.000 = Rp 40.000.000.
        await CreateApprovedOrderAsync(context, s, birds: 1_000);
        Guid second = await CreateOrderAsync(context, s, birds: 1_000);

        var approve = new ApproveSalesOrderCommandHandler(context, AllBranches(), User(), Clock());

        (await approve.Handle(new ApproveSalesOrderCommand(second, null), CancellationToken.None))
            .Error.ShouldBe(SalesOrderErrors.CreditLimitExceeded(
                new Money(50_000_000m), new Money(40_000_000m), new Money(40_000_000m)));

        (await approve.Handle(new ApproveSalesOrderCommand(second, "disetujui direktur"), CancellationToken.None)).IsSuccess.ShouldBeTrue();
        (await context.SalesOrders.SingleAsync(o => o.Id == second)).CreditOverrideReason.ShouldBe("disetujui direktur");
    }

    [Fact]
    public async Task CancelledDelivery_Should_ReturnBirdsToTheOrder_AndFreeTheHarvest()
    {
        await using TestDbContext context = CreateDbContext();
        Setup s = await SeedAsync(context);
        Guid orderId = await CreateApprovedOrderAsync(context, s, birds: 1_000);
        Guid delivery = (await Deliver(context, orderId, s.FirstHarvest)).Value.Id;

        Result cancelled = await new CancelDeliveryOrderCommandHandler(context, AllBranches())
            .Handle(new CancelDeliveryOrderCommand(delivery, "salah customer"), CancellationToken.None);

        cancelled.IsSuccess.ShouldBeTrue();
        SalesOrder order = await context.SalesOrders.Include(o => o.Lines).SingleAsync();
        order.Status.ShouldBe(SalesOrderStatus.Approved);
        order.Lines.Single().DeliveredBirds.ShouldBe(0);

        (await Deliver(context, orderId, s.FirstHarvest)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task CancelledDraftInvoice_Should_MakeTheDeliveryBillableAgain()
    {
        await using TestDbContext context = CreateDbContext();
        Setup s = await SeedAsync(context);
        Guid orderId = await CreateApprovedOrderAsync(context, s, birds: 1_000);
        Guid delivery = (await Deliver(context, orderId, s.FirstHarvest)).Value.Id;
        var create = new CreateSalesInvoiceCommandHandler(context, AllBranches(), Clock());

        Guid draft = (await create.Handle(new CreateSalesInvoiceCommand([delivery], HarvestDate, null), CancellationToken.None)).Value;
        (await create.Handle(new CreateSalesInvoiceCommand([delivery], HarvestDate, null), CancellationToken.None))
            .Error.ShouldBe(DeliveryOrderErrors.NotInvoiceable(delivery));

        await new CancelSalesInvoiceCommandHandler(context, AllBranches())
            .Handle(new CancelSalesInvoiceCommand(draft, "salah tanggal"), CancellationToken.None);

        (await context.DeliveryOrders.SingleAsync()).Status.ShouldBe(DeliveryOrderStatus.Delivered);
        (await create.Handle(new CreateSalesInvoiceCommand([delivery], HarvestDate, null), CancellationToken.None)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Advance_Should_CoverTheCreditExposure_AndBeAppliedToTheInvoice()
    {
        await using TestDbContext context = CreateDbContext();
        Setup s = await SeedAsync(context);

        // An open order of Rp 40.000.000 against a limit of Rp 50.000.000; a second one would exceed it by 30 jt,
        // unless the customer pays Rp 30.000.000 in advance.
        await CreateApprovedOrderAsync(context, s, birds: 1_000);
        Guid second = await CreateOrderAsync(context, s, birds: 1_000);

        Result<CreateCustomerReceiptResponse> advance = await new CreateCustomerReceiptCommandHandler(context, AllBranches(), Numbers(), Clock())
            .Handle(new CreateCustomerReceiptCommand(s.Bank.Id, s.Customer.Id, OrderDate, "DP", null, [], 30_000_000m), CancellationToken.None);

        (await new ApproveSalesOrderCommandHandler(context, AllBranches(), User(), Clock())
            .Handle(new ApproveSalesOrderCommand(second, null), CancellationToken.None)).IsSuccess.ShouldBeTrue();

        Guid orderId = (await context.SalesOrders.OrderBy(o => o.Id).FirstAsync()).Id;
        Guid delivery = (await Deliver(context, orderId, s.FirstHarvest)).Value.Id;
        Guid invoiceId = (await new CreateSalesInvoiceCommandHandler(context, AllBranches(), Clock())
            .Handle(new CreateSalesInvoiceCommand([delivery], HarvestDate, null), CancellationToken.None)).Value;
        await new PostSalesInvoiceCommandHandler(context, AllBranches(), Numbers(), User(), Clock())
            .Handle(new PostSalesInvoiceCommand(invoiceId), CancellationToken.None);

        Result applied = await new ApplyCustomerAdvanceCommandHandler(context, AllBranches(), Clock()).Handle(
            new ApplyCustomerAdvanceCommand(advance.Value.Id, HarvestDate, [new ReceiptAllocationRequest(invoiceId, 20_000_000m)]),
            CancellationToken.None);

        applied.IsSuccess.ShouldBeTrue();
        (await context.SalesInvoices.SingleAsync()).Outstanding.ShouldBe(new Money(4_000_000m));
        (await context.CustomerReceipts.SingleAsync()).UnappliedAdvance.ShouldBe(new Money(10_000_000m));
    }

    [Fact]
    public async Task Receipt_Should_RequireACashBankAccountOfItsBranch()
    {
        await using TestDbContext context = CreateDbContext();
        Setup s = await SeedAsync(context);
        s.Bank.Update(s.Bank.Name, s.Bank.BankName, s.Bank.AccountNumber, isActive: false);
        await context.SaveChangesAsync();

        Result<CreateCustomerReceiptResponse> result = await new CreateCustomerReceiptCommandHandler(context, AllBranches(), Numbers(), Clock())
            .Handle(
                new CreateCustomerReceiptCommand(s.Bank.Id, s.Customer.Id, HarvestDate, null, null, [], 1m),
                CancellationToken.None);

        result.Error.ShouldBe(CashBankErrors.Unusable(s.Bank.Id));
    }

    private static Task<Result<CreateDeliveryOrderResponse>> Deliver(TestDbContext context, Guid orderId, Guid harvestId) =>
        new CreateDeliveryOrderCommandHandler(context, AllBranches(), Numbers(), Clock()).Handle(
            new CreateDeliveryOrderCommand(orderId, HarvestDate, "D 1234 AB", "Asep", null, [new DeliveryOrderLineRequest(1, harvestId)]),
            CancellationToken.None);

    private static async Task<Guid> CreateOrderAsync(TestDbContext context, Setup s, int birds)
    {
        Result<CreateSalesOrderResponse> created = await new CreateSalesOrderCommandHandler(context, AllBranches(), Numbers()).Handle(
            new CreateSalesOrderCommand(s.BranchId, s.Customer.Id, OrderDate, null, null,
                [new SalesOrderLineRequest(s.LiveBird.Id, birds, birds * 2m, 20_000m, null)]),
            CancellationToken.None);

        return created.Value.Id;
    }

    private static async Task<Guid> CreateApprovedOrderAsync(TestDbContext context, Setup s, int birds)
    {
        Guid orderId = await CreateOrderAsync(context, s, birds);

        Result approved = await new ApproveSalesOrderCommandHandler(context, AllBranches(), User(), Clock())
            .Handle(new ApproveSalesOrderCommand(orderId, null), CancellationToken.None);

        approved.IsSuccess.ShouldBeTrue();

        return orderId;
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
    /// An inti cycle of 1.000 birds, fully harvested in two trucks (600 birds / 1.200 kg and 400 birds / 820 kg),
    /// with an empty coop warehouse; a customer with a credit limit of Rp 50.000.000 and 14 days payment term.
    /// </summary>
    private static async Task<Setup> SeedAsync(TestDbContext context)
    {
        var branch = Branch.Create("BDG", "Bandung", null, null);
        var kg = Uom.Create("KG", "Kilogram");
        var liveBird = Item.Create("AYM", "Ayam Hidup", ItemCategory.LiveBird, kg.Id, null);
        var customer = Customer.Create("RPA-01", "RPA Sejahtera", TaxIdentity.None, null, null, null, 14, new Money(50_000_000m));
        Account bankAccount = Account.Create("1-1201", "Bank Operasional", AccountType.Asset, null, true, null).Value;
        CashBankAccount bank = CashBankAccount.Create(
            "BCA", "BCA Operasional", CashBankAccountType.Bank, branch.Id, bankAccount.Id, true, "BCA", "1234567890").Value;

        Farmer farmer = Farmer.Create("INT", "Farm Inti", FarmerType.Inti, branch.Id, null, TaxIdentity.None, null, null, BankAccount.None).Value;
        Coop coop = Coop.Create(farmer, "KDG-A", "Kandang A", 5_000, HouseType.ClosedHouse, null, null, null).Value;
        var warehouse = Warehouse.CreateForCoop("GK-A", "Gudang A", branch.Id, coop.Id, null);
        ProductionCycle cycle = ProductionCycle.Plan("SKL/A", coop, farmer, null, ChickIn, 1_000, null).Value;
        cycle.Start(ChickIn, 1_000);
        CycleHarvest first = cycle.RecordHarvest(HarvestDate, 600, 1_200m, "truk 1").Value;
        CycleHarvest second = cycle.RecordHarvest(HarvestDate, 400, 820m, "truk 2").Value;

        context.Branches.Add(branch);
        context.Uoms.Add(kg);
        context.Items.Add(liveBird);
        context.Customers.Add(customer);
        context.Accounts.Add(bankAccount);
        context.CashBankAccounts.Add(bank);
        context.Farmers.Add(farmer);
        context.Coops.Add(coop);
        context.Warehouses.Add(warehouse);
        context.ProductionCycles.Add(cycle);
        context.FiscalPeriods.AddRange(FiscalPeriod.CreateYear(2026));
        await context.SaveChangesAsync();

        return new Setup(branch.Id, liveBird, customer, bank, cycle, first.Id, second.Id);
    }

    private sealed record Setup(
        Guid BranchId,
        Item LiveBird,
        Customer Customer,
        CashBankAccount Bank,
        ProductionCycle Cycle,
        Guid FirstHarvest,
        Guid SecondHarvest);
}
