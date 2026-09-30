using Domain.Finance.Accounts;
using Domain.Finance.FiscalPeriods;
using Domain.Finance.JournalMappings;
using Domain.Finance.Journals;
using SharedKernel;

namespace Domain.UnitTests.Finance;

public sealed class FinanceSetupTests
{
    [Fact]
    public void Account_Should_DeriveNormalBalanceFromType_UnlessOverridden()
    {
        Account header = Account.Create("1-2", "Aset Tetap", AccountType.Asset, null, isPostable: false).Value;

        Account building = Account.Create("1-2201", "Bangunan", AccountType.Asset, header, isPostable: true).Value;
        Account depreciation = Account.Create("1-2901", "Akumulasi Penyusutan", AccountType.Asset, header, true, BalanceSide.Credit).Value;

        building.NormalBalance.ShouldBe(BalanceSide.Debit);
        depreciation.NormalBalance.ShouldBe(BalanceSide.Credit);
        building.ParentId.ShouldBe(header.Id);
    }

    [Fact]
    public void Account_Should_RejectPostableParentOrDifferentType()
    {
        Account postable = Account.Create("1-1101", "Kas", AccountType.Asset, null, isPostable: true).Value;
        Account header = Account.Create("2", "Liabilitas", AccountType.Liability, null, isPostable: false).Value;

        Account.Create("1-1101-01", "Kas A", AccountType.Asset, postable, true).Error.ShouldBe(AccountErrors.ParentMustBeHeader(postable.Id));
        Account.Create("2-1101", "Hutang", AccountType.Asset, header, true).Error.ShouldBe(AccountErrors.ParentTypeMismatch);
    }

    [Fact]
    public void FiscalYear_Should_HaveTwelveContiguousMonths()
    {
        IReadOnlyList<FiscalPeriod> periods = FiscalPeriod.CreateYear(2028);

        periods.Count.ShouldBe(12);
        periods[1].StartDate.ShouldBe(new DateOnly(2028, 2, 1));
        periods[1].EndDate.ShouldBe(new DateOnly(2028, 2, 29));
        periods[11].EndDate.ShouldBe(new DateOnly(2028, 12, 31));
    }

    [Fact]
    public void Mapping_Should_RejectUnknownEventOrComponent()
    {
        JournalMapping.Create("Nope", null, null, []).Error.ShouldBe(JournalMappingErrors.UnknownEvent("Nope"));

        JournalMapping.Create(AccountingEvents.PurchaseReceipt, null, null, [("Bogus", Guid.NewGuid(), Guid.NewGuid(), null)])
            .Error.ShouldBe(JournalMappingErrors.UnknownComponent(AccountingEvents.PurchaseReceipt, "Bogus"));
    }

    [Fact]
    public void Mapping_Should_BuildBalancedLines_WithOverridesAndNegativeAmounts()
    {
        var inventory = Guid.NewGuid();
        var grni = Guid.NewGuid();
        var feedInventory = Guid.NewGuid();
        var otherGrni = Guid.NewGuid();

        JournalMapping mapping = JournalMapping.Create(AccountingEvents.PurchaseReceipt, null, null,
        [
            ("DocReceived", inventory, grni, null),
            ("FeedReceived", feedInventory, grni, null)
        ]).Value;

        IReadOnlyList<JournalLineInput> lines = mapping.BuildLines(
        [
            new AccountingAmount("DocReceived", new Money(7_500_000m)),
            new AccountingAmount("FeedReceived", new Money(-250_000m), CreditAccountId: otherGrni),
            new AccountingAmount("OvkReceived", Money.Zero)
        ]).Value;

        lines.Count.ShouldBe(4);
        lines.Aggregate(Money.Zero, (t, l) => t + l.Debit).ShouldBe(lines.Aggregate(Money.Zero, (t, l) => t + l.Credit));
        lines.Single(l => l.AccountId == inventory).Debit.ShouldBe(new Money(7_500_000m));

        // Negative amount: sides swap, so the (overridden) GRNI account is debited and feed inventory credited.
        lines.Single(l => l.AccountId == otherGrni).Debit.ShouldBe(new Money(250_000m));
        lines.Single(l => l.AccountId == feedInventory).Credit.ShouldBe(new Money(250_000m));
    }

    [Fact]
    public void Mapping_Should_Fail_WhenComponentHasNoAccounts()
    {
        JournalMapping mapping = JournalMapping.Create(AccountingEvents.PurchaseReceipt, null, null,
            [("DocReceived", Guid.NewGuid(), Guid.NewGuid(), null)]).Value;

        mapping.BuildLines([new AccountingAmount("OvkReceived", new Money(10m))])
            .Error.ShouldBe(JournalMappingErrors.ComponentNotMapped(AccountingEvents.PurchaseReceipt, "OvkReceived"));
    }
}
