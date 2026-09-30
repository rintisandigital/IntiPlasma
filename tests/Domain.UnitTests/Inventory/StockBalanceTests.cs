using Domain.Inventory.Stock;
using SharedKernel;

namespace Domain.UnitTests.Inventory;

public sealed class StockBalanceTests
{
    private static readonly StockMovement Movement = new(
        new DateOnly(2026, 10, 1), StockMovementType.Receipt, "Test", Guid.NewGuid(), "T/1", null);

    [Fact]
    public void Receive_Should_RecalculateMovingAverage()
    {
        var balance = StockBalance.Open(Guid.NewGuid(), Guid.NewGuid());

        balance.Receive(Movement, 1_000m, new Money(8_500_000m));
        balance.Receive(Movement, 500m, new Money(4_400_000m));

        balance.Quantity.ShouldBe(1_500m);
        balance.Value.ShouldBe(new Money(12_900_000m));
        balance.AverageCost.ShouldBe(8_600m);
    }

    [Fact]
    public void Issue_Should_UseAverageCost_AndProduceLedgerEntry()
    {
        var balance = StockBalance.Open(Guid.NewGuid(), Guid.NewGuid());
        balance.Receive(Movement, 1_000m, new Money(8_500_000m));
        balance.Receive(Movement, 500m, new Money(4_400_000m));

        StockLedgerEntry entry = balance.Issue(Movement with { Type = StockMovementType.TransferOut }, 300m).Value;

        entry.Quantity.ShouldBe(-300m);
        entry.UnitCost.ShouldBe(8_600m);
        entry.Value.ShouldBe(new Money(-2_580_000m));
        entry.BalanceQuantity.ShouldBe(1_200m);
        balance.Value.ShouldBe(new Money(10_320_000m));
    }

    [Fact]
    public void IssuingEverything_Should_LeaveNoRoundingResidue()
    {
        var balance = StockBalance.Open(Guid.NewGuid(), Guid.NewGuid());
        balance.Receive(Movement, 3m, new Money(100m));

        balance.Issue(Movement, 1m);
        balance.Issue(Movement, 2m);

        balance.Quantity.ShouldBe(0m);
        balance.Value.ShouldBe(Money.Zero);
    }

    [Fact]
    public void Issue_Should_NeverGoNegative()
    {
        var balance = StockBalance.Open(Guid.NewGuid(), Guid.NewGuid());
        balance.Receive(Movement, 10m, new Money(100m));

        balance.Issue(Movement, 11m).Error.Code.ShouldBe("Stock.Insufficient");
        balance.Quantity.ShouldBe(10m);
    }
}
