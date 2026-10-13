using Application.Abstractions.Authentication;
using Application.Abstractions.Numbering;
using Application.Finance.AutoJournal;
using Application.Finance.FiscalPeriods;
using Application.UnitTests.Abstractions;
using Domain.Finance.Accounts;
using Domain.Finance.FiscalPeriods;
using Domain.Finance.JournalMappings;
using Domain.Finance.Journals;
using Domain.MasterData.Branches;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Finance;

public sealed class YearEndClosingTests : BaseHandlerTest
{
    [Fact]
    public async Task ClosingJournals_Should_UseTheBranchRetainedEarningsMapping_ElseTheDefault()
    {
        // Arrange: Bandung uses the company default, Garut has its own retained earnings account.
        await using TestDbContext context = CreateDbContext();
        var bandung = Branch.Create("BDG", "Bandung", null, null);
        var garut = Branch.Create("GRT", "Garut", null, null);
        Account receivable = Account.Create("1-1301", "Piutang", AccountType.Asset, null, isPostable: true).Value;
        Account sales = Account.Create("4-1101", "Penjualan", AccountType.Revenue, null, isPostable: true).Value;
        Account companyRetained = Account.Create("3-2101", "Laba Ditahan", AccountType.Equity, null, isPostable: true).Value;
        Account garutRetained = Account.Create("3-2102", "Laba Ditahan Garut", AccountType.Equity, null, isPostable: true).Value;
        Account summary = Account.Create("3-3101", "Ikhtisar L/R", AccountType.Equity, null, isPostable: true).Value;

        context.Branches.AddRange(bandung, garut);
        context.Accounts.AddRange(receivable, sales, companyRetained, garutRetained, summary);
        context.JournalMappings.AddRange(
            JournalMapping.Create(AccountingEvents.SalesInvoice, null, null, [("LiveBirdSales", receivable.Id, sales.Id, null)]).Value,
            JournalMapping.Create(AccountingEvents.YearEndClosing, null, null, [("NetIncome", summary.Id, companyRetained.Id, null)]).Value,
            JournalMapping.Create(AccountingEvents.YearEndClosing, garut.Id, null, [("NetIncome", summary.Id, garutRetained.Id, null)]).Value);
        context.FiscalPeriods.AddRange(FiscalPeriod.CreateYear(2026));
        await context.SaveChangesAsync();

        IDocumentNumberGenerator numbers = Substitute.For<IDocumentNumberGenerator>();
        numbers.NextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns("JO/1");
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTime.UtcNow);
        var autoJournal = new AutoJournalService(context, numbers, Substitute.For<IUserContext>(), clock);

        foreach ((Branch branch, decimal amount) in new[] { (bandung, 1_000_000m), (garut, 400_000m) })
        {
            (await autoJournal.PostAsync(
                new AccountingEntry(AccountingEvents.SalesInvoice, Guid.NewGuid(), branch.Id, new DateOnly(2026, 6, 1), "Sales",
                    [new AccountingAmount("LiveBirdSales", new Money(amount))]),
                CancellationToken.None)).IsSuccess.ShouldBeTrue();

            // The in-memory provider tracks the shared Money.Zero as an owned entity; start each journal untracked.
            context.ChangeTracker.Clear();
        }

        FiscalPeriod december = await context.FiscalPeriods.SingleAsync(p => p.Month == 12);

        // Act
        Result<IReadOnlyList<JournalEntry>> result = await YearEndClosing.CreateClosingJournalsAsync(
            context, numbers, december, null, DateTime.UtcNow, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Single(j => j.BranchId == bandung.Id).Lines
            .Single(l => l.AccountId == companyRetained.Id).Credit.ShouldBe(new Money(1_000_000m));
        result.Value.Single(j => j.BranchId == garut.Id).Lines
            .Single(l => l.AccountId == garutRetained.Id).Credit.ShouldBe(new Money(400_000m));
    }
}
