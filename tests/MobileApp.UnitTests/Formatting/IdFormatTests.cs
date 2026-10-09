using MobileApp.Core.Contracts;
using MobileApp.Core.Formatting;

namespace MobileApp.UnitTests.Formatting;

public sealed class IdFormatTests
{
    [Theory]
    [InlineData(1234567.891, 2, "1.234.567,89")]
    [InlineData(1234567, 0, "1.234.567")]
    [InlineData(-1500.5, 1, "-1.500,5")]
    [InlineData(0.125, 2, "0,13")]
    public void Number_Should_UseIndonesianSeparators(double value, int decimals, string expected) =>
        IdFormat.Number((decimal)value, decimals).ShouldBe(expected);

    [Fact]
    public void Money_Should_PrefixRupiah()
    {
        IdFormat.Money(1250000m).ShouldBe("Rp 1.250.000");
        IdFormat.Money(-5000m).ShouldBe("-Rp 5.000");
    }

    [Fact]
    public void Tons_Should_ConvertKilograms() => IdFormat.Tons(12345m).ShouldBe("12,35 ton");

    [Fact]
    public void Dates_Should_UseDayMonthYear()
    {
        IdFormat.Date(new DateOnly(2026, 10, 8)).ShouldBe("08/10/2026");
        IdFormat.DateTime(new DateTime(2026, 10, 8, 7, 5, 0, DateTimeKind.Local)).ShouldBe("08/10/2026 07:05");
        IdFormat.LongDate(new DateOnly(2026, 10, 8)).ShouldBe("Kamis, 8 Oktober 2026");
    }

    [Theory]
    [InlineData(6, "Selamat pagi")]
    [InlineData(12, "Selamat siang")]
    [InlineData(16, "Selamat sore")]
    [InlineData(20, "Selamat malam")]
    public void Greeting_Should_FollowTheTimeOfDay(int hour, string expected) =>
        IdFormat.Greeting(new DateTime(2026, 10, 8, hour, 0, 0, DateTimeKind.Local)).ShouldBe(expected);

    [Fact]
    public void Feed_Should_AddPacks_OnlyWhenEveryItemHasTheSameUnit()
    {
        DashboardFeedStock starter = new(Guid.NewGuid(), "PK1", "Starter", 900m, "KG", "SAK", 50m);
        DashboardFeedStock finisher = new(Guid.NewGuid(), "PK2", "Finisher", 25m, "KG", "SAK", 50m);

        IdFormat.Feed(925m, [starter, finisher]).ShouldBe("925 kg (18,5 SAK)");
        IdFormat.Feed(925m, [starter, finisher with { PackUomCode = null, PackFactor = null }]).ShouldBe("925 kg");
        IdFormat.Feed(0m, []).ShouldBe("0 kg");
        IdFormat.Optional(null).ShouldBe("–");
        IdFormat.Optional(1.5413m, 3).ShouldBe("1,541");
    }
}
