using MobileApp.Core.Formatting;

namespace MobileApp.UnitTests.Formatting;

public sealed class LabelsTests
{
    [Theory]
    [InlineData("3201010101010001", "3201********0001")]
    [InlineData("12345", "*****")]
    [InlineData(null, "—")]
    public void MaskNik_Should_HideTheMiddleDigits(string? nik, string expected) =>
        Labels.MaskNik(nik).ShouldBe(expected);

    [Theory]
    [InlineData("0812-3456-7890", "6281234567890")]
    [InlineData("+62 812 3456 7890", "6281234567890")]
    [InlineData("123", null)]
    [InlineData(null, null)]
    public void WhatsAppNumber_Should_UseTheInternationalForm(string? phone, string? expected) =>
        Labels.WhatsAppNumber(phone).ShouldBe(expected);

    [Theory]
    [InlineData("+62 812-3456", "+628123456")]
    [InlineData("(022) 123", "022123")]
    public void TelNumber_Should_KeepDigitsAndPlus(string phone, string expected) =>
        Labels.TelNumber(phone).ShouldBe(expected);

    [Fact]
    public void Enums_Should_BeTranslated_AndUnknownValuesKept()
    {
        Labels.CycleStatus("Harvesting").ShouldBe("Panen");
        Labels.ContractStatus("Draft").ShouldBe("Draf");
        Labels.Scheme("ProfitSharing").ShouldBe("Bagi hasil");
        Labels.HouseType("ClosedHouse").ShouldBe("Kandang tertutup");
        Labels.IncentiveBasis("PerBird").ShouldBe("per ekor");
        Labels.CycleStatus("Something").ShouldBe("Something");
    }
}

public sealed class ExternalLinksTests
{
    [Fact]
    public void Links_Should_UseTheNormalizedNumbers()
    {
        MobileApp.Core.Formatting.ExternalLinks.WhatsApp("0812-345-678")!.ToString().ShouldBe("https://wa.me/62812345678");
        MobileApp.Core.Formatting.ExternalLinks.Phone("0812 345 678")!.ToString().ShouldBe("tel:0812345678");
        MobileApp.Core.Formatting.ExternalLinks.Phone(null).ShouldBeNull();
    }

    [Fact]
    public void Map_Should_UseInvariantCoordinates() =>
        MobileApp.Core.Formatting.ExternalLinks.Map(-6.914744m, 107.609810m).AbsoluteUri
            .ShouldBe("https://www.google.com/maps/search/?api=1&query=-6.914744%2C107.609810");
}
