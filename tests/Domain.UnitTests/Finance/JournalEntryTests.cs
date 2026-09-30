using Domain.Finance.FiscalPeriods;
using Domain.Finance.Journals;
using SharedKernel;

namespace Domain.UnitTests.Finance;

public sealed class JournalEntryTests
{
    private static readonly Guid Cash = Guid.NewGuid();
    private static readonly Guid Revenue = Guid.NewGuid();
    private static readonly DateOnly Date = new(2026, 10, 15);
    private static readonly FiscalPeriod October = FiscalPeriod.CreateYear(2026).Single(p => p.Month == 10);

    private static JournalLineInput[] BalancedLines(decimal amount = 1_000_000m) =>
    [
        JournalLineInput.DebitLine(Cash, new Money(amount)),
        JournalLineInput.CreditLine(Revenue, new Money(amount))
    ];

    [Fact]
    public void Create_Should_Fail_WhenNotBalanced()
    {
        Result<JournalEntry> result = JournalEntry.CreateManual(Guid.NewGuid(), Date, "Test",
        [
            JournalLineInput.DebitLine(Cash, new Money(1_000m)),
            JournalLineInput.CreditLine(Revenue, new Money(900m))
        ]);

        result.Error.Code.ShouldBe("Journals.NotBalanced");
    }

    [Fact]
    public void Create_Should_Fail_WhenLineHasBothSidesOrNone()
    {
        JournalEntry.CreateManual(Guid.NewGuid(), Date, "Test",
        [
            new JournalLineInput(Cash, null, null, new Money(100m), new Money(100m)),
            JournalLineInput.CreditLine(Revenue, Money.Zero)
        ]).Error.ShouldBe(JournalErrors.InvalidLineAmount);
    }

    [Fact]
    public void Create_Should_RequireTwoLines()
    {
        JournalEntry.CreateManual(Guid.NewGuid(), Date, "Test", [JournalLineInput.DebitLine(Cash, new Money(1m))])
            .Error.ShouldBe(JournalErrors.TooFewLines);
    }

    [Fact]
    public void ManualJournal_Should_RequireApprovalBeforePosting()
    {
        JournalEntry journal = JournalEntry.CreateManual(Guid.NewGuid(), Date, "Test", BalancedLines()).Value;

        journal.Post("JU/1", October, null, DateTime.UtcNow)
            .Error.ShouldBe(JournalErrors.InvalidTransition(JournalStatus.Draft, JournalStatus.Posted));

        journal.Approve(Guid.NewGuid(), DateTime.UtcNow).IsSuccess.ShouldBeTrue();
        journal.Post("JU/1", October, null, DateTime.UtcNow).IsSuccess.ShouldBeTrue();

        journal.Status.ShouldBe(JournalStatus.Posted);
        journal.Number.ShouldBe("JU/1");
        journal.TotalDebit.ShouldBe(journal.TotalCredit);
        journal.DomainEvents.ShouldContain(e => e is JournalPostedDomainEvent);
    }

    [Fact]
    public void Post_Should_Fail_WhenPeriodIsClosed()
    {
        FiscalPeriod closed = FiscalPeriod.CreateYear(2026).Single(p => p.Month == 10);
        closed.Close(null, DateTime.UtcNow);
        JournalEntry journal = JournalEntry.CreateAutomatic(Guid.NewGuid(), Date, "Auto", "PurchaseReceipt", Guid.NewGuid(), BalancedLines()).Value;

        journal.Post("JO/1", closed, null, DateTime.UtcNow).Error.ShouldBe(FiscalPeriodErrors.Closed(Date));
    }

    [Fact]
    public void AutomaticJournal_Should_PostWithoutApproval()
    {
        JournalEntry journal = JournalEntry.CreateAutomatic(Guid.NewGuid(), Date, "Auto", "PurchaseReceipt", Guid.NewGuid(), BalancedLines()).Value;

        journal.Post("JO/1", October, null, DateTime.UtcNow).IsSuccess.ShouldBeTrue();
        journal.Update(Date, "changed", BalancedLines()).Error.ShouldBe(JournalErrors.NotEditable(journal.Id));
    }

    [Fact]
    public void Reverse_Should_SwapSides_AndMarkOriginalReversed()
    {
        JournalEntry journal = JournalEntry.CreateAutomatic(Guid.NewGuid(), Date, "Auto", "SalesInvoice", Guid.NewGuid(), BalancedLines(500m)).Value;
        journal.Post("JO/1", October, null, DateTime.UtcNow);

        Result<JournalEntry> reversal = journal.Reverse(Date.AddDays(1), "salah akun", "JU/9", October, null, DateTime.UtcNow);

        reversal.IsSuccess.ShouldBeTrue();
        journal.Status.ShouldBe(JournalStatus.Reversed);
        journal.ReversedById.ShouldBe(reversal.Value.Id);
        reversal.Value.ReversalOfId.ShouldBe(journal.Id);
        reversal.Value.Status.ShouldBe(JournalStatus.Posted);
        reversal.Value.Lines.Single(l => l.AccountId == Cash).Credit.ShouldBe(new Money(500m));
        reversal.Value.Lines.Single(l => l.AccountId == Revenue).Debit.ShouldBe(new Money(500m));
        journal.Reverse(Date.AddDays(2), "lagi", "JU/10", October, null, DateTime.UtcNow).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Reverse_Should_NotBeDatedBeforeOriginal()
    {
        JournalEntry journal = JournalEntry.CreateAutomatic(Guid.NewGuid(), Date, "Auto", "SalesInvoice", Guid.NewGuid(), BalancedLines()).Value;
        journal.Post("JO/1", October, null, DateTime.UtcNow);

        journal.Reverse(Date.AddDays(-1), "x", "JU/9", October, null, DateTime.UtcNow)
            .Error.ShouldBe(JournalErrors.ReversalBeforeOriginal);
    }
}
