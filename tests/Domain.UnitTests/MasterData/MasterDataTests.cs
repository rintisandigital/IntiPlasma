using Domain.Common;
using Domain.MasterData.Coops;
using Domain.MasterData.Farmers;
using Domain.MasterData.Items;
using Domain.MasterData.TaxCodes;
using SharedKernel;

namespace Domain.UnitTests.MasterData;

public sealed class MasterDataTests
{
    [Fact]
    public void Farmer_Should_RequireNik_WhenPlasma()
    {
        Result<Farmer> result = Farmer.Create("PLM-002", "Siti", FarmerType.Plasma, TestData.BranchId, null,
            TaxIdentity.None, null, null, BankAccount.None);

        result.Error.ShouldBe(FarmerErrors.PlasmaRequiresNik);
    }

    [Fact]
    public void Farmer_Should_RejectInvalidNik()
    {
        Result<Farmer> result = Farmer.Create("PLM-002", "Siti", FarmerType.Plasma, TestData.BranchId, "32010101",
            TaxIdentity.None, null, null, BankAccount.None);

        result.Error.ShouldBe(CommonErrors.InvalidNik);
    }

    [Fact]
    public void Coop_Should_InheritBranchOfFarmer_AndRaiseCreatedEvent()
    {
        Farmer farmer = TestData.PlasmaFarmer();

        Coop coop = TestData.Coop(farmer);

        coop.BranchId.ShouldBe(farmer.BranchId);
        coop.FarmerId.ShouldBe(farmer.Id);
        coop.DomainEvents.ShouldContain(e => e is CoopCreatedDomainEvent);
    }

    [Fact]
    public void FieldOfficer_Should_BeAssignedAndCleared()
    {
        Farmer farmer = TestData.PlasmaFarmer();
        Coop coop = TestData.Coop(farmer);
        var ppl = Guid.NewGuid();

        farmer.AssignFieldOfficer(ppl);
        coop.AssignFieldOfficer(ppl);

        farmer.FieldOfficerUserId.ShouldBe(ppl);
        coop.FieldOfficerUserId.ShouldBe(ppl);

        farmer.AssignFieldOfficer(Guid.Empty);
        coop.AssignFieldOfficer(null);

        farmer.FieldOfficerUserId.ShouldBeNull();
        coop.FieldOfficerUserId.ShouldBeNull();
    }

    [Fact]
    public void Coop_Should_NotBeCreated_ForInactiveFarmer()
    {
        Farmer farmer = TestData.PlasmaFarmer();
        farmer.Update(farmer.Name, farmer.Nik, farmer.TaxIdentity, null, null, farmer.BankAccount, isActive: false);

        Result<Coop> result = Coop.Create(farmer, "KDG-9", "Kandang 9", 5_000, HouseType.OpenHouse, null, null, null);

        result.Error.ShouldBe(FarmerErrors.Inactive(farmer.Id));
    }

    [Fact]
    public void TaxCode_Should_ReturnRateEffectiveOnDate()
    {
        var ppn = TaxCode.CreateVat("PPN", "PPN Keluaran", VatTreatment.Taxable);
        ppn.SetRates(
        [
            (new DateOnly(2022, 4, 1), 11m, 1m),
            (new DateOnly(2025, 1, 1), 12m, 11m / 12m)
        ]);

        ppn.GetRateOn(new DateOnly(2024, 12, 31))!.RatePercent.ShouldBe(11m);
        ppn.GetRateOn(new DateOnly(2025, 6, 1))!.RatePercent.ShouldBe(12m);
        ppn.GetRateOn(new DateOnly(2020, 1, 1)).ShouldBeNull();
    }

    [Fact]
    public void TaxCode_Should_RejectDuplicateEffectiveDates()
    {
        var pph = TaxCode.CreateIncomeTax("PPH23", "PPh 23", IncomeTaxArticle.Pph23);

        Result result = pph.SetRates([(TestData.Today, 2m, 1m), (TestData.Today, 4m, 1m)]);

        result.Error.ShouldBe(TaxCodeErrors.DuplicateEffectiveDate);
    }

    [Fact]
    public void Item_Should_ConvertAlternativeUnitsToBase()
    {
        var kg = Guid.NewGuid();
        var sak = Guid.NewGuid();
        var feed = Item.Create("PKN-BR1", "Pakan BR-1", ItemCategory.Feed, kg, null);
        feed.SetConversions([(sak, 50m)]);

        feed.ConvertToBase(sak, 3m).Value.ShouldBe(150m);
        feed.ConvertToBase(kg, 3m).Value.ShouldBe(3m);
        feed.ConvertToBase(Guid.NewGuid(), 1m).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Item_Should_RejectConversionToBaseUnit()
    {
        var kg = Guid.NewGuid();
        var feed = Item.Create("PKN-BR1", "Pakan BR-1", ItemCategory.Feed, kg, null);

        feed.SetConversions([(kg, 1m)]).Error.ShouldBe(ItemErrors.ConversionToBaseUom);
    }
}
