using Domain.Common;
using Domain.MasterData;
using Domain.MasterData.Coops;
using Domain.MasterData.Farmers;
using Domain.Partnership.Contracts;
using SharedKernel;

namespace Domain.UnitTests.Approvals;

/// <summary>
/// Status of farmers, coops and contracts going through the approval module (PLAN-MOBILE M-81 s.d. M-87).
/// </summary>
public sealed class RegistrationTests
{
    [Fact]
    public void Farmer_Should_OnlyBecomeActive_WhenApproved()
    {
        Farmer farmer = TestData.DraftPlasmaFarmer();
        farmer.RegistrationStatus.ShouldBe(RegistrationStatus.Draft);
        farmer.IsActive.ShouldBeFalse();

        farmer.Update(farmer.Name, farmer.Nik, farmer.TaxIdentity, null, null, farmer.BankAccount, isActive: true);
        farmer.IsActive.ShouldBeFalse();

        farmer.Approve().Error.ShouldBe(FarmerErrors.NotPendingApproval(farmer.Id));
        farmer.Submit().IsSuccess.ShouldBeTrue();
        farmer.Submit().Error.ShouldBe(FarmerErrors.NotSubmittable(farmer.Id));
        farmer.Approve().IsSuccess.ShouldBeTrue();

        farmer.RegistrationStatus.ShouldBe(RegistrationStatus.Approved);
        farmer.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void PendingFarmer_Should_BeLocked_UntilCancelled()
    {
        Farmer farmer = TestData.DraftPlasmaFarmer();
        farmer.Submit();

        farmer.Update("Nama Baru", farmer.Nik, farmer.TaxIdentity, null, null, farmer.BankAccount, isActive: false)
            .Error.ShouldBe(FarmerErrors.PendingApproval(farmer.Id));

        farmer.ReturnToDraft().IsSuccess.ShouldBeTrue();
        farmer.Update("Nama Baru", farmer.Nik, farmer.TaxIdentity, null, null, farmer.BankAccount, isActive: false)
            .IsSuccess.ShouldBeTrue();
        farmer.Name.ShouldBe("Nama Baru");
    }

    [Fact]
    public void RejectedFarmer_Should_BeEditable_AndSubmittedAgain()
    {
        Farmer farmer = TestData.DraftPlasmaFarmer();
        farmer.Submit();
        farmer.Reject();

        farmer.Update(farmer.Name, "3201010101010009", farmer.TaxIdentity, null, null, farmer.BankAccount, false)
            .Value.ShouldBeFalse();
        farmer.Nik.ShouldBe("3201010101010009");
        farmer.Submit().IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void ApprovedFarmer_Should_ProposeANikOrBankAccountChange_InsteadOfApplyingIt()
    {
        Farmer farmer = TestData.PlasmaFarmer();
        BankAccount account = BankAccount.Create("BRI", "123456", "Budi Santoso").Value;

        Result<bool> result = farmer.Update("Budi S.", "3201010101010002", farmer.TaxIdentity, "Bandung", null, account, true);

        result.Value.ShouldBeTrue();
        farmer.Name.ShouldBe("Budi S.");
        farmer.Address.ShouldBe("Bandung");
        farmer.Nik.ShouldBe("3201010101010001");
        farmer.BankAccount.ShouldBe(BankAccount.None);
        farmer.PendingChange.ShouldBe(new FarmerPendingChange("3201010101010002", "BRI", "123456", "Budi Santoso"));

        farmer.Update("Budi S.", "3201010101010003", farmer.TaxIdentity, "Bandung", null, account, true)
            .Error.ShouldBe(FarmerErrors.ChangePending);
        farmer.Update("Budi Santoso", farmer.Nik, farmer.TaxIdentity, "Bandung", null, farmer.BankAccount, true)
            .Value.ShouldBeFalse();

        farmer.ApplyPendingChange();

        farmer.Nik.ShouldBe("3201010101010002");
        farmer.BankAccount.ShouldBe(account);
        farmer.PendingChange.ShouldBeNull();
    }

    [Fact]
    public void ProposedChange_Should_StillBeValidated()
    {
        Farmer farmer = TestData.PlasmaFarmer();

        farmer.Update(farmer.Name, null, farmer.TaxIdentity, null, null, farmer.BankAccount, true)
            .Error.ShouldBe(FarmerErrors.PlasmaRequiresNik);
        farmer.PendingChange.ShouldBeNull();
    }

    [Fact]
    public void Coop_Should_NeedASubmittedFarmer()
    {
        Farmer draft = TestData.DraftPlasmaFarmer();
        Coop.Create(draft, "KDG-9", "Kandang 9", 5_000, HouseType.OpenHouse, null, null, null)
            .Error.ShouldBe(CoopErrors.FarmerNotSubmitted);

        draft.Submit();
        Coop coop = TestData.DraftCoop(draft);
        coop.Submit(draft).IsSuccess.ShouldBeTrue();

        coop.Approve(draft).Error.ShouldBe(CoopErrors.FarmerNotApproved);
        coop.RegistrationStatus.ShouldBe(RegistrationStatus.PendingApproval);

        draft.Approve();
        coop.Approve(draft).IsSuccess.ShouldBeTrue();
        coop.IsActive.ShouldBeTrue();
        coop.DomainEvents.ShouldContain(e => e is CoopApprovedDomainEvent);
    }

    [Fact]
    public void Coop_Should_NotRaiseTheWarehouseEvent_WhenCreated()
    {
        Coop coop = TestData.DraftCoop(TestData.PlasmaFarmer());

        coop.DomainEvents.ShouldBeEmpty();
        coop.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Contract_Should_BecomeActive_OnlyThroughApproval()
    {
        PartnershipContract contract = PartnershipContract
            .Create("KTR-1", TestData.BranchId, ContractScheme.PriceContract, TestData.Terms()).Value;

        contract.Approve().Error.ShouldBe(ContractErrors.NotPendingApproval(contract.Id));
        contract.Submit().IsSuccess.ShouldBeTrue();
        contract.UpdateTerms(TestData.Terms()).Error.ShouldBe(ContractErrors.NotDraft(contract.Id));

        contract.Reject().IsSuccess.ShouldBeTrue();
        contract.Status.ShouldBe(ContractStatus.Rejected);
        contract.UpdateTerms(TestData.Terms()).IsSuccess.ShouldBeTrue();

        contract.Submit().IsSuccess.ShouldBeTrue();
        contract.ReturnToDraft().IsSuccess.ShouldBeTrue();
        contract.Status.ShouldBe(ContractStatus.Draft);

        contract.Submit();
        contract.Approve().IsSuccess.ShouldBeTrue();
        contract.Status.ShouldBe(ContractStatus.Active);
        contract.DomainEvents.ShouldContain(e => e is ContractActivatedDomainEvent);
    }
}
