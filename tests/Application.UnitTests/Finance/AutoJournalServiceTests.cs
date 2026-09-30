using Application.Abstractions.Authentication;
using Application.Abstractions.Numbering;
using Application.Finance.AutoJournal;
using Application.UnitTests.Abstractions;
using Domain.Finance.Accounts;
using Domain.Finance.FiscalPeriods;
using Domain.Finance.JournalMappings;
using Domain.Finance.Journals;
using Domain.MasterData.Branches;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Finance;

public sealed class AutoJournalServiceTests : BaseHandlerTest
{
    private static readonly DateOnly Date = new(2026, 10, 20);

    [Fact]
    public async Task PostAsync_Should_PostBalancedJournal_UsingDefaultMapping()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Setup setup = await SeedAsync(context);
        AutoJournalService service = CreateService(context);

        // Act
        Result<Guid> result = await service.PostAsync(Receipt(setup.Branch.Id, Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        JournalEntry journal = await context.JournalEntries.Include(j => j.Lines).SingleAsync();
        journal.Status.ShouldBe(JournalStatus.Posted);
        journal.Source.ShouldBe(JournalSource.Automatic);
        journal.Number.ShouldBe("JO/BDG/2026/X/0001");
        journal.Lines.Single(l => l.AccountId == setup.Inventory.Id).Debit.ShouldBe(new Money(12_000_000m));
        journal.Lines.Single(l => l.AccountId == setup.Grni.Id).Credit.ShouldBe(new Money(12_000_000m));
    }

    [Fact]
    public async Task PostAsync_Should_BeIdempotent_ForSameSourceDocument()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Setup setup = await SeedAsync(context);
        AutoJournalService service = CreateService(context);
        AccountingEntry entry = Receipt(setup.Branch.Id, Guid.NewGuid());

        // Act
        Result<Guid> first = await service.PostAsync(entry, CancellationToken.None);
        Result<Guid> second = await service.PostAsync(entry, CancellationToken.None);

        // Assert
        second.Value.ShouldBe(first.Value);
        (await context.JournalEntries.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task PostAsync_Should_PreferBranchSpecificMapping()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Setup setup = await SeedAsync(context);

        Account branchGrni = Account.Create("2-1199", "GRNI Bandung", AccountType.Liability, null, isPostable: true).Value;
        context.Accounts.Add(branchGrni);
        context.JournalMappings.Add(JournalMapping.Create(AccountingEvents.PurchaseReceipt, setup.Branch.Id, null,
            [("FeedReceived", setup.Inventory.Id, branchGrni.Id, null)]).Value);
        await context.SaveChangesAsync();

        AutoJournalService service = CreateService(context);

        // Act
        await service.PostAsync(Receipt(setup.Branch.Id, Guid.NewGuid()), CancellationToken.None);

        // Assert
        JournalEntry journal = await context.JournalEntries.Include(j => j.Lines).SingleAsync();
        journal.Lines.ShouldContain(l => l.AccountId == branchGrni.Id);
    }

    [Fact]
    public async Task PostAsync_Should_Fail_WhenPeriodIsClosed()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Setup setup = await SeedAsync(context);
        FiscalPeriod october = await context.FiscalPeriods.SingleAsync(p => p.Month == 10);
        october.Close(null, DateTime.UtcNow);
        await context.SaveChangesAsync();

        // Act
        Result<Guid> result = await CreateService(context).PostAsync(Receipt(setup.Branch.Id, Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(FiscalPeriodErrors.Closed(Date));
    }

    [Fact]
    public async Task PostAsync_Should_Fail_WhenEventIsNotMapped()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Setup setup = await SeedAsync(context);
        var entry = new AccountingEntry(AccountingEvents.SalesInvoice, Guid.NewGuid(), setup.Branch.Id, Date, "Sales",
            [new AccountingAmount("LiveBirdSales", new Money(1_000m))]);

        // Act
        Result<Guid> result = await CreateService(context).PostAsync(entry, CancellationToken.None);

        // Assert
        result.Error.ShouldBe(JournalMappingErrors.NotConfigured(AccountingEvents.SalesInvoice, setup.Branch.Id));
    }

    private static AccountingEntry Receipt(Guid branchId, Guid sourceId) =>
        new(AccountingEvents.PurchaseReceipt, sourceId, branchId, Date, "Penerimaan pakan",
            [new AccountingAmount("FeedReceived", new Money(12_000_000m))]);

    private static AutoJournalService CreateService(TestDbContext context)
    {
        IDocumentNumberGenerator numbers = Substitute.For<IDocumentNumberGenerator>();
        numbers.NextAsync("JO", "BDG", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns("JO/BDG/2026/X/0001");

        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTime.UtcNow);

        return new AutoJournalService(context, numbers, Substitute.For<IUserContext>(), clock);
    }

    private static async Task<Setup> SeedAsync(TestDbContext context)
    {
        var branch = Branch.Create("BDG", "Bandung", null, null);
        Account inventory = Account.Create("1-1402", "Persediaan Pakan", AccountType.Asset, null, isPostable: true).Value;
        Account grni = Account.Create("2-1102", "GRNI", AccountType.Liability, null, isPostable: true).Value;
        JournalMapping mapping = JournalMapping.Create(AccountingEvents.PurchaseReceipt, null, null,
            [("FeedReceived", inventory.Id, grni.Id, null)]).Value;

        context.Branches.Add(branch);
        context.Accounts.AddRange(inventory, grni);
        context.JournalMappings.Add(mapping);
        context.FiscalPeriods.AddRange(FiscalPeriod.CreateYear(2026));
        await context.SaveChangesAsync();

        return new Setup(branch, inventory, grni);
    }

    private sealed record Setup(Branch Branch, Account Inventory, Account Grni);
}
