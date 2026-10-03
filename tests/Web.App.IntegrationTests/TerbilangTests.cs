using Web.App.Infrastructure.Formatting;

namespace Web.App.IntegrationTests;

/// <summary>
/// PhaseW6: amounts in words in Indonesian on printed documents (W-17).
/// </summary>
public sealed class TerbilangTests
{
    [Theory]
    [InlineData(0, "Nol rupiah")]
    [InlineData(1, "Satu rupiah")]
    [InlineData(11, "Sebelas rupiah")]
    [InlineData(15, "Lima belas rupiah")]
    [InlineData(100, "Seratus rupiah")]
    [InlineData(110, "Seratus sepuluh rupiah")]
    [InlineData(1000, "Seribu rupiah")]
    [InlineData(1500, "Seribu lima ratus rupiah")]
    [InlineData(21_000, "Dua puluh satu ribu rupiah")]
    [InlineData(100_000, "Seratus ribu rupiah")]
    [InlineData(1_250_500, "Satu juta dua ratus lima puluh ribu lima ratus rupiah")]
    [InlineData(41_400_000, "Empat puluh satu juta empat ratus ribu rupiah")]
    [InlineData(2_000_000_000, "Dua miliar rupiah")]
    [InlineData(1_000_000_000_000, "Satu triliun rupiah")]
    public void Rupiah_Should_SpellWholeAmounts(long amount, string expected) =>
        Terbilang.Rupiah(amount).ShouldBe(expected);

    [Fact]
    public void Rupiah_Should_SpellCents() =>
        Terbilang.Rupiah(1_250_500.75m).ShouldBe("Satu juta dua ratus lima puluh ribu lima ratus rupiah tujuh puluh lima sen");

    [Fact]
    public void Rupiah_Should_SpellNegativeAmounts() =>
        Terbilang.Rupiah(-1_500m).ShouldBe("Minus seribu lima ratus rupiah");
}
