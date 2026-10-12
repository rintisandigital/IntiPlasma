using Domain.Approvals;
using SharedKernel;

namespace Domain.UnitTests.Approvals;

public sealed class ApprovalTests
{
    private static readonly Guid Requester = Guid.NewGuid();
    private static readonly Guid Susi = Guid.NewGuid();
    private static readonly Guid Budi = Guid.NewGuid();
    private static readonly Guid Tommy = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 10, 12, 3, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Flow_Should_HaveOneToFiveLevels(int count)
    {
        ApprovalLevelDefinition[] levels = [.. Enumerable.Range(1, count).Select(i => new ApprovalLevelDefinition($"L{i}", [Susi]))];

        ApprovalFlow.Create(ApprovalDocumentType.Farmer, null, "Farmer", levels).Error.ShouldBe(ApprovalErrors.InvalidLevelCount);
    }

    [Fact]
    public void Flow_Should_RequireANameAndAnApproverPerLevel()
    {
        ApprovalFlow.Create(ApprovalDocumentType.Coop, null, "Coop", [new(" ", [Susi])])
            .Error.ShouldBe(ApprovalErrors.LevelNameRequired(1));
        ApprovalFlow.Create(ApprovalDocumentType.Coop, null, "Coop", [new("Verifikasi", [Susi]), new("Kepala Cabang", [])])
            .Error.ShouldBe(ApprovalErrors.LevelWithoutApprover(2));
    }

    [Fact]
    public void Flow_Should_AddAndRemoveLevels_AndReplaceAnApprover()
    {
        ApprovalFlow flow = ApprovalFlow.Create(
            ApprovalDocumentType.Farmer, Guid.NewGuid(), "Farmer BDG", [new("Verifikasi", [Susi]), new("Kepala Cabang", [Tommy])]).Value;

        flow.Update("Farmer BDG", true, [new("Verifikasi", [Susi, Susi]), new("Kepala Produksi", [Budi]), new("Kepala Cabang", [Tommy])])
            .IsSuccess.ShouldBeTrue();
        flow.Levels.Select(l => l.Name).ShouldBe(["Verifikasi", "Kepala Produksi", "Kepala Cabang"]);
        flow.Levels[0].ApproverUserIds.ShouldBe([Susi]);

        flow.Update("Farmer BDG", true, [new("Kepala Cabang", [Tommy, Budi])]).IsSuccess.ShouldBeTrue();
        flow.Levels.Count.ShouldBe(1);

        flow.ReplaceApprover(Tommy, Budi).ShouldBeTrue();
        flow.Levels[0].ApproverUserIds.ShouldBe([Budi]);
        flow.ReplaceApprover(Susi, Budi).ShouldBeFalse();
    }

    [Fact]
    public void Request_Should_BeDecidedLevelByLevel_AndApprovedWhenEveryLevelApproves()
    {
        ApprovalRequest request = Submit();

        request.Approve(new ApprovalActor(Budi), null, Now).Error.ShouldBe(ApprovalErrors.NotApprover);

        request.Approve(new ApprovalActor(Susi), "ok", Now).IsSuccess.ShouldBeTrue();
        request.Status.ShouldBe(ApprovalStatus.Pending);
        request.CurrentLevel.ShouldBe(2);
        request.DomainEvents.ShouldContain(e => e is ApprovalLevelApprovedDomainEvent);

        request.Approve(new ApprovalActor(Budi), null, Now.AddMinutes(5)).IsSuccess.ShouldBeTrue();
        request.Status.ShouldBe(ApprovalStatus.Approved);
        request.CompletedAtUtc.ShouldBe(Now.AddMinutes(5));
        request.Decisions.Count.ShouldBe(2);
        request.DomainEvents.ShouldContain(e => e is ApprovalRequestApprovedDomainEvent);

        request.Approve(new ApprovalActor(Budi), null, Now).Error.ShouldBe(ApprovalErrors.NotPending);
    }

    [Fact]
    public void Request_Should_BeRejected_ByAnyLevel_WithAReason()
    {
        ApprovalRequest request = Submit();
        request.Approve(new ApprovalActor(Susi), null, Now);

        request.Reject(new ApprovalActor(Budi), " ", Now).Error.ShouldBe(ApprovalErrors.ReasonRequired);
        request.Reject(new ApprovalActor(Budi), "Data kurang", Now).IsSuccess.ShouldBeTrue();

        request.Status.ShouldBe(ApprovalStatus.Rejected);
        request.CurrentLevel.ShouldBe(2);
        request.Decisions[^1].Note.ShouldBe("Data kurang");
    }

    [Fact]
    public void Requester_Should_NotDecide_EvenWhenListedAsApprover()
    {
        ApprovalRequest request = Submit(firstLevel: [Requester, Susi]);

        request.Approve(new ApprovalActor(Requester), null, Now).Error.ShouldBe(ApprovalErrors.RequesterCannotDecide);
    }

    [Fact]
    public void A_User_Should_DecideAtMostOneLevel()
    {
        ApprovalRequest request = Submit(firstLevel: [Susi], secondLevel: [Susi, Budi]);
        request.Approve(new ApprovalActor(Susi), null, Now);

        request.Approve(new ApprovalActor(Susi), null, Now).Error.ShouldBe(ApprovalErrors.AlreadyDecidedOtherLevel);
        request.Approve(new ApprovalActor(Budi), null, Now).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void A_Delegate_Should_DecideOnBehalfOfTheApprover()
    {
        var delegateUser = Guid.NewGuid();
        ApprovalRequest request = Submit(secondLevel: [Susi, Budi]);
        request.Approve(new ApprovalActor(Susi), null, Now);

        request.Approve(new ApprovalActor(delegateUser, Susi), null, Now).Error.ShouldBe(ApprovalErrors.AlreadyDecidedOtherLevel);
        request.Approve(new ApprovalActor(delegateUser, Budi), null, Now).IsSuccess.ShouldBeTrue();

        request.Decisions[^1].DecidedBy.ShouldBe(delegateUser);
        request.Decisions[^1].OnBehalfOf.ShouldBe(Budi);
    }

    [Fact]
    public void Requester_Should_NotDecide_AsADelegate()
    {
        ApprovalRequest request = Submit();

        request.Approve(new ApprovalActor(Requester, Susi), null, Now).Error.ShouldBe(ApprovalErrors.RequesterCannotDecide);
    }

    [Fact]
    public void Only_TheRequester_Should_CancelAPendingRequest()
    {
        ApprovalRequest request = Submit();

        request.Cancel(Susi, Now).Error.ShouldBe(ApprovalErrors.OnlyRequesterCanCancel);
        request.Cancel(Requester, Now).IsSuccess.ShouldBeTrue();
        request.Status.ShouldBe(ApprovalStatus.Cancelled);
        request.Cancel(Requester, Now).Error.ShouldBe(ApprovalErrors.NotPending);
    }

    [Fact]
    public void Reassign_Should_OnlyChangeLevelsStillToBeDecided()
    {
        ApprovalRequest request = Submit();
        request.Approve(new ApprovalActor(Susi), null, Now);

        request.ReassignApprover(1, Susi, Tommy).Error.ShouldBe(ApprovalErrors.LevelAlreadyDecided);
        request.ReassignApprover(2, Susi, Tommy).Error.ShouldBe(ApprovalErrors.ApproverNotInLevel);
        request.ReassignApprover(2, Budi, Tommy).IsSuccess.ShouldBeTrue();

        request.IsApproverOfCurrentLevel(Tommy).ShouldBeTrue();
        request.IsApproverOfCurrentLevel(Budi).ShouldBeFalse();
    }

    [Fact]
    public void ReplaceApprover_Should_SkipDecidedLevels()
    {
        ApprovalRequest request = Submit(firstLevel: [Susi], secondLevel: [Susi, Budi]);
        request.Approve(new ApprovalActor(Susi), null, Now);

        request.ReplaceApprover(Susi, Tommy).ShouldBeTrue();

        request.Levels[0].ApproverUserIds.ShouldBe([Susi]);
        request.Levels[1].ApproverUserIds.ShouldBe([Tommy, Budi]);
    }

    [Fact]
    public void Snapshot_Should_NotFollowLaterChangesOfTheFlow()
    {
        ApprovalFlow flow = ApprovalFlow.Create(ApprovalDocumentType.Farmer, null, "Farmer", [new("Verifikasi", [Susi])]).Value;
        var request = ApprovalRequest.Submit(
            ApprovalDocumentType.Farmer, Guid.NewGuid(), Guid.NewGuid(), "PTN-1", "Cecep", null, null, flow.Id,
            flow.ToDefinitions(), Requester, Now);

        flow.Update("Farmer", true, [new("Verifikasi", [Budi]), new("Kepala Cabang", [Tommy])]);

        request.LevelCount.ShouldBe(1);
        request.Levels[0].ApproverUserIds.ShouldBe([Susi]);
    }

    [Fact]
    public void Delegation_Should_ValidateItsPeriod()
    {
        var today = new DateOnly(2026, 10, 12);

        ApprovalDelegation.Create(Tommy, Tommy, today, today, null, today).Error.ShouldBe(ApprovalErrors.DelegateToSelf);
        ApprovalDelegation.Create(Tommy, Budi, today, today.AddDays(-1), null, today).Error.ShouldBe(ApprovalErrors.InvalidDelegationPeriod);
        ApprovalDelegation.Create(Tommy, Budi, today.AddDays(-5), today.AddDays(-1), null, today).Error.ShouldBe(ApprovalErrors.InvalidDelegationPeriod);
        ApprovalDelegation.Create(Tommy, Budi, today, today.AddDays(90), null, today).Error.ShouldBe(ApprovalErrors.InvalidDelegationPeriod);

        ApprovalDelegation delegation = ApprovalDelegation.Create(Tommy, Budi, today, today.AddDays(89), "Cuti", today).Value;
        delegation.IsActiveOn(today).ShouldBeTrue();
        delegation.Overlaps(today.AddDays(89), today.AddDays(95)).ShouldBeTrue();
        delegation.Overlaps(today.AddDays(90), today.AddDays(95)).ShouldBeFalse();
    }

    [Fact]
    public void Delegation_Should_EndWhenRevoked()
    {
        var today = new DateOnly(2026, 10, 12);
        ApprovalDelegation delegation = ApprovalDelegation.Create(Tommy, Budi, today, today.AddDays(5), null, today).Value;

        delegation.Revoke(Tommy, Now, today).IsSuccess.ShouldBeTrue();

        delegation.IsActiveOn(today).ShouldBeFalse();
        delegation.Overlaps(today, today).ShouldBeFalse();
        delegation.Revoke(Tommy, Now, today).Error.ShouldBe(ApprovalErrors.DelegationEnded);
    }

    private static ApprovalRequest Submit(Guid[]? firstLevel = null, Guid[]? secondLevel = null) =>
        ApprovalRequest.Submit(
            ApprovalDocumentType.Farmer,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "PTN-1",
            "Cecep",
            null,
            null,
            null,
            [new ApprovalLevelDefinition("Verifikasi", firstLevel ?? [Susi]), new ApprovalLevelDefinition("Kepala Cabang", secondLevel ?? [Budi])],
            Requester,
            Now);
}
