using SharedKernel;

namespace Domain.UnitTests.SharedKernel;

public sealed class MoneyTests
{
    [Theory]
    [InlineData(10.005, 10.01)]
    [InlineData(10.004, 10.00)]
    [InlineData(-10.005, -10.01)]
    public void Constructor_Should_RoundAwayFromZeroToTwoDecimals(decimal amount, decimal expected)
    {
        new Money(amount).Amount.ShouldBe(expected);
    }

    [Fact]
    public void Arithmetic_Should_ProduceRoundedMoney()
    {
        var price = new Money(7_250.50m);

        Money total = price * 3.333m;

        total.Amount.ShouldBe(24_165.92m);
        (total - price).Amount.ShouldBe(16_915.42m);
        (price + price).ShouldBe(new Money(14_501m));
    }

    [Fact]
    public void Comparison_Should_UseAmount()
    {
        (new Money(1m) < new Money(2m)).ShouldBeTrue();
        Money.Zero.IsZero.ShouldBeTrue();
        (-new Money(5m)).IsNegative.ShouldBeTrue();
    }
}
