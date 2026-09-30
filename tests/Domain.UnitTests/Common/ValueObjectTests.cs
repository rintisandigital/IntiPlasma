using Domain.Common;
using SharedKernel;

namespace Domain.UnitTests.Common;

public sealed class ValueObjectTests
{
    [Fact]
    public void TaxIdentity_Should_NormalizeFormattedNpwp()
    {
        Result<TaxIdentity> result = TaxIdentity.Create("01.234.567.8-901.000", null, isPkp: true);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Npwp.ShouldBe("012345678901000");
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("12.345.678.9-012.34X")]
    public void TaxIdentity_Should_RejectInvalidNpwp(string npwp)
    {
        TaxIdentity.Create(npwp, null, false).Error.ShouldBe(CommonErrors.InvalidNpwp);
    }

    [Fact]
    public void TaxIdentity_Should_RequireNpwp_ForPkp()
    {
        TaxIdentity.Create(null, null, isPkp: true).Error.ShouldBe(CommonErrors.PkpRequiresNpwp);
    }

    [Fact]
    public void BankAccount_Should_BeNone_WhenEverythingIsEmpty()
    {
        BankAccount.Create(" ", null, "").Value.ShouldBe(BankAccount.None);
    }

    [Fact]
    public void BankAccount_Should_RejectPartialData()
    {
        BankAccount.Create("BRI", null, "Budi").Error.ShouldBe(CommonErrors.IncompleteBankAccount);
    }
}
