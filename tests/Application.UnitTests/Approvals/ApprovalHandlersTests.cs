using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Approvals;
using Application.Common;
using Application.Contracts.ChangeStatus;
using Application.Farmers.Create;
using Application.Farmers.Update;
using Application.UnitTests.Abstractions;
using Domain.Access;
using Domain.Approvals;
using Domain.Common;
using Domain.MasterData;
using Domain.MasterData.Branches;
using Domain.MasterData.Coops;
using Domain.MasterData.Farmers;
using Domain.Partnership.Contracts;
using Domain.Roles;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Approvals;

/// <summary>
/// Submitting documents and deciding requests (PLAN-MOBILE §5, M-81 s.d. M-99).
/// </summary>
public sealed class ApprovalHandlersTests : BaseHandlerTest
{
    private static readonly DateTime Now = new(2026, 10, 12, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CreateFarmer_Should_SaveNothing_WhenNoFlowIsConfigured()
    {
        await using TestDbContext context = CreateDbContext();
        Branch branch = await SeedBranchAsync(context);

        Result<Guid> result = await CreateFarmerHandler(context).Handle(CreateFarmer(branch.Id), CancellationToken.None);

        result.Error.Code.ShouldBe("Approvals.FlowNotConfigured");
        result.Error.Description.ShouldBe(
            "Alur approval belum dikonfigurasi untuk peternak baru di cabang Cabang BDG. Hubungi administrator.");
        (await context.Farmers.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Submit_Should_PreferTheBranchFlow_OverTheDefault()
    {
        await using TestDbContext context = CreateDbContext();
        Branch branch = await SeedBranchAsync(context);
        User susi = await SeedApproverAsync(context);
        User tommy = await SeedApproverAsync(context);
        await AddFlowAsync(context, null, [susi.Id]);
        await AddFlowAsync(context, branch.Id, [susi.Id], [tommy.Id]);

        Result<Guid> result = await CreateFarmerHandler(context).Handle(CreateFarmer(branch.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        ApprovalRequest request = await context.ApprovalRequests.Include(r => r.Levels).SingleAsync();
        request.LevelCount.ShouldBe(2);
        request.Levels[1].ApproverUserIds.ShouldBe([tommy.Id]);
        (await context.Farmers.SingleAsync()).RegistrationStatus.ShouldBe(RegistrationStatus.PendingApproval);
    }

    [Fact]
    public async Task Approving_EveryLevel_Should_ActivateTheFarmer()
    {
        await using TestDbContext context = CreateDbContext();
        Branch branch = await SeedBranchAsync(context);
        User susi = await SeedApproverAsync(context);
        User tommy = await SeedApproverAsync(context);
        await AddFlowAsync(context, null, [susi.Id], [tommy.Id]);
        await CreateFarmerHandler(context).Handle(CreateFarmer(branch.Id), CancellationToken.None);
        Guid requestId = (await context.ApprovalRequests.SingleAsync()).Id;

        (await Approve(context, tommy.Id, requestId)).Error.ShouldBe(ApprovalErrors.NotApprover);
        (await Approve(context, susi.Id, requestId)).IsSuccess.ShouldBeTrue();
        (await context.Farmers.SingleAsync()).IsActive.ShouldBeFalse();
        (await Approve(context, tommy.Id, requestId)).IsSuccess.ShouldBeTrue();

        Farmer farmer = await context.Farmers.SingleAsync();
        farmer.RegistrationStatus.ShouldBe(RegistrationStatus.Approved);
        farmer.IsActive.ShouldBeTrue();
        (await context.ApprovalRequests.SingleAsync()).Status.ShouldBe(ApprovalStatus.Approved);
    }

    [Fact]
    public async Task Rejecting_Should_LetTheRequesterSubmitAgain_FromLevelOne()
    {
        await using TestDbContext context = CreateDbContext();
        Branch branch = await SeedBranchAsync(context);
        User susi = await SeedApproverAsync(context);
        User tommy = await SeedApproverAsync(context);
        await AddFlowAsync(context, null, [susi.Id], [tommy.Id]);
        await CreateFarmerHandler(context).Handle(CreateFarmer(branch.Id), CancellationToken.None);
        Guid requestId = (await context.ApprovalRequests.SingleAsync()).Id;
        await Approve(context, susi.Id, requestId);

        Result result = await Handler(context, tommy.Id, d => new RejectRequestCommandHandler(context, d, Clock()))
            .Handle(new RejectRequestCommand(requestId, "NIK tidak sesuai KTP"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        Farmer farmer = await context.Farmers.SingleAsync();
        farmer.RegistrationStatus.ShouldBe(RegistrationStatus.Rejected);

        Result<Guid> again = await new SubmitFarmerCommandHandler(context, AllBranches(), NoFieldScope(), CreateApprovals(context))
            .Handle(new SubmitFarmerCommand(farmer.Id), CancellationToken.None);

        again.IsSuccess.ShouldBeTrue();
        ApprovalRequest second = await context.ApprovalRequests.SingleAsync(r => r.Id == again.Value);
        second.CurrentLevel.ShouldBe(1);
        second.Status.ShouldBe(ApprovalStatus.Pending);
    }

    [Fact]
    public async Task Cancelling_Should_ReturnTheFarmerToDraft()
    {
        await using TestDbContext context = CreateDbContext();
        Branch branch = await SeedBranchAsync(context);
        User susi = await SeedApproverAsync(context);
        await AddFlowAsync(context, null, [susi.Id]);
        var requester = Guid.NewGuid();
        await CreateFarmerHandler(context, requester).Handle(CreateFarmer(branch.Id), CancellationToken.None);
        Guid requestId = (await context.ApprovalRequests.SingleAsync()).Id;

        (await Cancel(context, susi.Id, requestId)).Error.ShouldBe(ApprovalErrors.RequestNotFound(requestId));
        (await Cancel(context, requester, requestId)).IsSuccess.ShouldBeTrue();

        (await context.Farmers.SingleAsync()).RegistrationStatus.ShouldBe(RegistrationStatus.Draft);
        (await context.ApprovalRequests.SingleAsync()).Status.ShouldBe(ApprovalStatus.Cancelled);
    }

    [Fact]
    public async Task ApprovingACoop_Should_Fail_AndSaveNothing_WhileItsFarmerIsPending()
    {
        await using TestDbContext context = CreateDbContext();
        Branch branch = await SeedBranchAsync(context);
        User susi = await SeedApproverAsync(context);
        await AddFlowAsync(context, null, [susi.Id]);
        Farmer farmer = Farmer.Create("PLM-9", "Cecep", FarmerType.Inti, branch.Id, null, TaxIdentity.None, null, null, BankAccount.None).Value;
        farmer.Submit();
        Coop coop = Coop.Create(farmer, "KDG-9", "Kandang 9", 3_000, HouseType.OpenHouse, null, null, null).Value;
        context.Farmers.Add(farmer);
        context.Coops.Add(coop);
        await context.SaveChangesAsync();
        Result<Guid> submitted = await new SubmitCoopCommandHandler(context, AllBranches(), NoFieldScope(), CreateApprovals(context))
            .Handle(new SubmitCoopCommand(coop.Id), CancellationToken.None);

        Result result = await Approve(context, susi.Id, submitted.Value);

        result.Error.ShouldBe(CoopErrors.FarmerNotApproved);
        context.ChangeTracker.Clear();
        (await context.ApprovalRequests.SingleAsync()).Status.ShouldBe(ApprovalStatus.Pending);
        (await context.Coops.SingleAsync()).RegistrationStatus.ShouldBe(RegistrationStatus.PendingApproval);
    }

    [Fact]
    public async Task FarmerChange_Should_KeepTheOldValues_UntilApproved()
    {
        await using TestDbContext context = CreateDbContext();
        Branch branch = await SeedBranchAsync(context);
        User susi = await SeedApproverAsync(context);
        await AddFlowAsync(context, null, [susi.Id]);
        Farmer farmer = Farmer.Create("PLM-9", "Cecep", FarmerType.Plasma, branch.Id, "3201010101010001", TaxIdentity.None, null, null, BankAccount.None).Value.Approved();
        context.Farmers.Add(farmer);
        await context.SaveChangesAsync();

        Result update = await new UpdateFarmerCommandHandler(context, AllBranches(), NoFieldScope(), CreateAttachments(context), CreateApprovals(context))
            .Handle(
                new UpdateFarmerCommand(farmer.Id, "Cecep", "3201010101010002", new TaxIdentityRequest(null, null, false), null, null,
                    new BankAccountRequest("BRI", "123", "Cecep"), true),
                CancellationToken.None);

        update.IsSuccess.ShouldBeTrue();
        ApprovalRequest request = await context.ApprovalRequests.SingleAsync();
        request.DocumentType.ShouldBe(ApprovalDocumentType.FarmerChange);
        request.Payload.ShouldNotBeNull();
        request.Payload.ShouldContain("3201010101010002");
        (await context.Farmers.SingleAsync()).Nik.ShouldBe("3201010101010001");

        (await Approve(context, susi.Id, request.Id)).IsSuccess.ShouldBeTrue();

        Farmer changed = await context.Farmers.SingleAsync();
        changed.Nik.ShouldBe("3201010101010002");
        changed.BankAccount.AccountNumber.ShouldBe("123");
        changed.PendingChange.ShouldBeNull();
    }

    [Fact]
    public async Task Contract_Should_BecomeActive_AfterApproval()
    {
        await using TestDbContext context = CreateDbContext();
        Branch branch = await SeedBranchAsync(context);
        User susi = await SeedApproverAsync(context);
        await AddFlowAsync(context, null, [susi.Id]);
        PartnershipContract contract = PartnershipContract.Create(
            "KTR-1", branch.Id, ContractScheme.ProfitSharing,
            new ContractTerms("Bagi hasil", new DateOnly(2026, 10, 1), null, 40m, null, null, [], [], [])).Value;
        context.Contracts.Add(contract);
        await context.SaveChangesAsync();

        Result<Guid> submitted = await new SubmitContractCommandHandler(context, AllBranches(), CreateApprovals(context))
            .Handle(new SubmitContractCommand(contract.Id), CancellationToken.None);
        (await context.Contracts.SingleAsync()).Status.ShouldBe(ContractStatus.PendingApproval);

        (await Approve(context, susi.Id, submitted.Value)).IsSuccess.ShouldBeTrue();

        (await context.Contracts.SingleAsync()).Status.ShouldBe(ContractStatus.Active);
    }

    [Fact]
    public async Task A_Delegate_Should_DecideOnBehalfOfTheApprover_DuringTheDelegation()
    {
        await using TestDbContext context = CreateDbContext();
        Branch branch = await SeedBranchAsync(context);
        User tommy = await SeedApproverAsync(context);
        User budi = await SeedApproverAsync(context);
        await AddFlowAsync(context, null, [tommy.Id]);
        await CreateFarmerHandler(context).Handle(CreateFarmer(branch.Id), CancellationToken.None);
        Guid requestId = (await context.ApprovalRequests.SingleAsync()).Id;

        (await Approve(context, budi.Id, requestId)).Error.ShouldBe(ApprovalErrors.NotApprover);

        var today = DateOnly.FromDateTime(Now.AddHours(7));
        context.ApprovalDelegations.Add(ApprovalDelegation.Create(tommy.Id, budi.Id, today, today.AddDays(3), "Cuti", today).Value);
        await context.SaveChangesAsync();

        (await Approve(context, budi.Id, requestId)).IsSuccess.ShouldBeTrue();

        ApprovalDecision decision = (await context.ApprovalRequests.Include(r => r.Decisions).SingleAsync()).Decisions.Single();
        decision.DecidedBy.ShouldBe(budi.Id);
        decision.OnBehalfOf.ShouldBe(tommy.Id);
    }

    [Fact]
    public async Task An_InactiveApprover_Should_NotDecide()
    {
        await using TestDbContext context = CreateDbContext();
        Branch branch = await SeedBranchAsync(context);
        User susi = await SeedApproverAsync(context);
        await AddFlowAsync(context, null, [susi.Id]);
        await CreateFarmerHandler(context).Handle(CreateFarmer(branch.Id), CancellationToken.None);
        Guid requestId = (await context.ApprovalRequests.SingleAsync()).Id;
        susi.Deactivate();
        await context.SaveChangesAsync();

        (await Approve(context, susi.Id, requestId)).Error.ShouldBe(ApprovalErrors.NotApprover);
    }

    [Fact]
    public async Task ReplaceApprover_Should_ChangeFlowsAndPendingRequests()
    {
        await using TestDbContext context = CreateDbContext();
        Branch branch = await SeedBranchAsync(context);
        User susi = await SeedApproverAsync(context);
        User tommy = await SeedApproverAsync(context);
        await AddFlowAsync(context, null, [susi.Id]);
        await CreateFarmerHandler(context).Handle(CreateFarmer(branch.Id), CancellationToken.None);

        Result<ReplaceApproverResult> result = await new ReplaceApproverCommandHandler(context, AllBranches(), new TestAuditTrail(context))
            .Handle(new ReplaceApproverCommand(susi.Id, tommy.Id), CancellationToken.None);

        result.Value.ShouldBe(new ReplaceApproverResult(4, 1));
        Guid requestId = (await context.ApprovalRequests.SingleAsync()).Id;
        (await Approve(context, susi.Id, requestId)).Error.ShouldBe(ApprovalErrors.NotApprover);
        (await Approve(context, tommy.Id, requestId)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateFlow_Should_RejectApproversWithoutTheDecidePermission()
    {
        await using TestDbContext context = CreateDbContext();
        await SeedBranchAsync(context);
        User staff = await SeedUserAsync(context, Role.Create("Staff", null, [Permissions.FarmersRead]).Value);

        Result<Guid> result = await new CreateApprovalFlowCommandHandler(context, AllBranches())
            .Handle(
                new CreateApprovalFlowCommand(ApprovalDocumentType.Farmer, null, "Farmer", [new("Verifikasi", [staff.Id])]),
                CancellationToken.None);

        result.Error.ShouldBe(ApprovalErrors.InvalidApprover(staff.Id));
    }

    [Fact]
    public async Task CreateFlow_Should_AllowOneActiveFlowPerTypeAndBranch()
    {
        await using TestDbContext context = CreateDbContext();
        await SeedBranchAsync(context);
        User susi = await SeedApproverAsync(context);
        var handler = new CreateApprovalFlowCommandHandler(context, AllBranches());
        var command = new CreateApprovalFlowCommand(ApprovalDocumentType.Coop, null, "Coop", [new("Verifikasi", [susi.Id])]);

        (await handler.Handle(command, CancellationToken.None)).IsSuccess.ShouldBeTrue();
        (await handler.Handle(command, CancellationToken.None)).Error.ShouldBe(ApprovalErrors.FlowAlreadyExists);
    }

    [Fact]
    public async Task SetUpBranchFlows_Should_CreateAFlowPerType_AndGrantBranchAccess()
    {
        await using TestDbContext context = CreateDbContext();
        Branch branch = await SeedBranchAsync(context);
        Branch newBranch = await SeedBranchAsync(context, "SMD");
        User susi = await SeedApproverAsync(context, allBranches: false, branch.Id);

        Result<int> result = await new SetUpBranchApprovalFlowsCommandHandler(context, AllBranches(), new TestAuditTrail(context))
            .Handle(
                new SetUpBranchApprovalFlowsCommand(
                    newBranch.Id,
                    [ApprovalDocumentType.Farmer, ApprovalDocumentType.Coop],
                    [new("Kepala Cabang", [susi.Id])],
                    GrantBranchAccess: true),
                CancellationToken.None);

        result.Value.ShouldBe(2);
        (await context.ApprovalFlows.CountAsync(f => f.BranchId == newBranch.Id)).ShouldBe(2);
        BranchAccessProfile profile = await context.BranchAccessProfiles.Include(p => p.Branches).SingleAsync();
        profile.Covers(newBranch.Id).ShouldBeTrue();
    }

    private static CreateFarmerCommandHandler CreateFarmerHandler(TestDbContext context, Guid? requester = null) =>
        new(context, AllBranches(), NoFieldScope(), CreateAttachments(context), CreateApprovals(context, requester));

    private static Task<Result> Approve(TestDbContext context, Guid userId, Guid requestId) =>
        Handler(context, userId, d => new ApproveRequestCommandHandler(context, d, Clock()))
            .Handle(new ApproveRequestCommand(requestId), CancellationToken.None);

    private static Task<Result> Cancel(TestDbContext context, Guid userId, Guid requestId) =>
        new CancelRequestCommandHandler(context, Decider(context, userId), UserContext(userId), Clock())
            .Handle(new CancelRequestCommand(requestId), CancellationToken.None);

    private static T Handler<T>(TestDbContext context, Guid userId, Func<ApprovalDecider, T> create) =>
        create(Decider(context, userId));

    private static ApprovalDecider Decider(TestDbContext context, Guid userId) =>
        new(
            context,
            AllBranches(),
            UserContext(userId),
            Clock(),
            new ApprovalHandlerRegistry(
            [
                new FarmerApprovalHandler(context),
                new FarmerChangeApprovalHandler(context),
                new CoopApprovalHandler(context),
                new ContractApprovalHandler(context)
            ]));

    private static IUserContext UserContext(Guid userId)
    {
        IUserContext user = Substitute.For<IUserContext>();
        user.UserId.Returns(userId);

        return user;
    }

    private static IDateTimeProvider Clock()
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        return clock;
    }

    private static IBranchAccess AllBranches()
    {
        IBranchAccess branchAccess = Substitute.For<IBranchAccess>();
        branchAccess.GetScopeAsync(Arg.Any<CancellationToken>()).Returns(BranchScope.All);

        return branchAccess;
    }

    private static CreateFarmerCommand CreateFarmer(Guid branchId) => new(
        $"INT-{Guid.NewGuid():N}"[..12],
        "Farm Inti",
        FarmerType.Inti,
        branchId,
        null,
        new TaxIdentityRequest(null, null, false),
        null,
        null,
        new BankAccountRequest(null, null, null));

    private static async Task AddFlowAsync(TestDbContext context, Guid? branchId, params Guid[][] levels)
    {
        foreach (ApprovalDocumentType documentType in Enum.GetValues<ApprovalDocumentType>())
        {
            context.ApprovalFlows.Add(ApprovalFlow.Create(
                documentType,
                branchId,
                documentType.ToString(),
                [.. levels.Select((approvers, i) => new ApprovalLevelDefinition($"Level {i + 1}", approvers))]).Value);
        }

        await context.SaveChangesAsync();
    }

    private static async Task<Branch> SeedBranchAsync(TestDbContext context, string code = "BDG")
    {
        var branch = Branch.Create(code, $"Cabang {code}", null, null);
        context.Branches.Add(branch);
        await context.SaveChangesAsync();

        return branch;
    }

    private static Task<User> SeedApproverAsync(TestDbContext context, bool allBranches = true, params Guid[] branchIds) =>
        SeedUserAsync(context, Role.Create($"Manager {Guid.NewGuid():N}", null, [Permissions.ApprovalsDecide]).Value, allBranches, branchIds);

    private static async Task<User> SeedUserAsync(TestDbContext context, Role role, bool allBranches = true, params Guid[] branchIds)
    {
        BranchAccessProfile profile = BranchAccessProfile.Create($"Area {Guid.NewGuid():N}", null, allBranches, branchIds).Value;
        var user = User.Create($"{Guid.NewGuid():N}@example.com", "Approver", "Satu", "hash");
        user.SetAccess(null, profile.Id, null);
        user.SetRoles([role.Id]);

        context.Roles.Add(role);
        context.BranchAccessProfiles.Add(profile);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }
}
