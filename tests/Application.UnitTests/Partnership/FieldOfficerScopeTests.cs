using Application.Abstractions.Authorization;
using Application.Common;
using Application.Coops.Create;
using Application.Coops.Update;
using Application.Farmers.Create;
using Application.UnitTests.Abstractions;
using Domain.Access;
using Domain.Common;
using Domain.MasterData.Branches;
using Domain.MasterData.Coops;
using Domain.MasterData.Farmers;
using Domain.Roles;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Partnership;

/// <summary>
/// PPL assignment and scope on farmer and coop commands (PLAN-MOBILE M-36 s.d. M-38).
/// </summary>
public sealed class FieldOfficerScopeTests : BaseHandlerTest
{
    [Fact]
    public async Task CreateFarmer_Should_AssignCreator_WhenUserIsLimitedToAssignedData()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Branch branch = await SeedBranchAsync(context);
        User ppl = await SeedFieldOfficerAsync(context, branch);
        User otherPpl = await SeedFieldOfficerAsync(context, branch);

        var handler = new CreateFarmerCommandHandler(
            context, AllBranches(), new TestFieldScope(context, ppl.Id), CreateAttachments(context));

        // Act
        Result<Guid> result = await handler.Handle(CreateFarmer(branch.Id, otherPpl.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.Farmers.SingleAsync()).FieldOfficerUserId.ShouldBe(ppl.Id);
    }

    [Fact]
    public async Task CreateFarmer_Should_AssignRequestedFieldOfficer_WhenUserIsNotLimited()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Branch branch = await SeedBranchAsync(context);
        User ppl = await SeedFieldOfficerAsync(context, branch);

        var handler = new CreateFarmerCommandHandler(context, AllBranches(), NoFieldScope(), CreateAttachments(context));

        // Act
        Result<Guid> result = await handler.Handle(CreateFarmer(branch.Id, ppl.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.Farmers.SingleAsync()).FieldOfficerUserId.ShouldBe(ppl.Id);
    }

    [Fact]
    public async Task CreateFarmer_Should_RejectFieldOfficer_WhenUserLacksPplPermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Branch branch = await SeedBranchAsync(context);
        User staff = await SeedUserAsync(context, branch, Role.Create("Staff", null, [Permissions.FarmersRead]).Value);

        var handler = new CreateFarmerCommandHandler(context, AllBranches(), NoFieldScope(), CreateAttachments(context));

        // Act
        Result<Guid> result = await handler.Handle(CreateFarmer(branch.Id, staff.Id), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(UserErrors.NotFieldOfficer(staff.Id));
    }

    [Fact]
    public async Task CreateFarmer_Should_RejectFieldOfficer_WhenUserIsAdministrator()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Branch branch = await SeedBranchAsync(context);
        User admin = await SeedUserAsync(context, branch, Role.CreateAdministrator());

        var handler = new CreateFarmerCommandHandler(context, AllBranches(), NoFieldScope(), CreateAttachments(context));

        // Act
        Result<Guid> result = await handler.Handle(CreateFarmer(branch.Id, admin.Id), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(UserErrors.NotFieldOfficer(admin.Id));
    }

    [Fact]
    public async Task CreateFarmer_Should_RejectFieldOfficer_WhenBranchIsNotCovered()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Branch branch = await SeedBranchAsync(context);
        Branch otherBranch = await SeedBranchAsync(context, "JKT");
        User ppl = await SeedFieldOfficerAsync(context, otherBranch);

        var handler = new CreateFarmerCommandHandler(context, AllBranches(), NoFieldScope(), CreateAttachments(context));

        // Act
        Result<Guid> result = await handler.Handle(CreateFarmer(branch.Id, ppl.Id), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(UserErrors.NotFieldOfficer(ppl.Id));
    }

    [Fact]
    public async Task CreateCoop_Should_ReturnNotFound_WhenFarmerIsOutsideScope()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Branch branch = await SeedBranchAsync(context);
        User ppl = await SeedFieldOfficerAsync(context, branch);
        User otherPpl = await SeedFieldOfficerAsync(context, branch);
        Farmer farmer = await SeedFarmerAsync(context, branch, otherPpl.Id);

        var handler = new CreateCoopCommandHandler(
            context, AllBranches(), new TestFieldScope(context, ppl.Id), CreateAttachments(context));

        // Act
        Result<Guid> result = await handler.Handle(CreateCoop(farmer.Id), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(FarmerErrors.NotFound(farmer.Id));
    }

    [Fact]
    public async Task CreateCoop_Should_AssignCreator_WhenFarmerIsInScope()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Branch branch = await SeedBranchAsync(context);
        User ppl = await SeedFieldOfficerAsync(context, branch);
        Farmer farmer = await SeedFarmerAsync(context, branch, ppl.Id);

        var handler = new CreateCoopCommandHandler(
            context, AllBranches(), new TestFieldScope(context, ppl.Id), CreateAttachments(context));

        // Act
        Result<Guid> result = await handler.Handle(CreateCoop(farmer.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.Coops.SingleAsync()).FieldOfficerUserId.ShouldBe(ppl.Id);
    }

    [Fact]
    public async Task UpdateCoop_Should_ReturnNotFound_WhenCoopIsAssignedToAnotherPpl()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Branch branch = await SeedBranchAsync(context);
        User ppl = await SeedFieldOfficerAsync(context, branch);
        User otherPpl = await SeedFieldOfficerAsync(context, branch);
        Coop coop = await SeedCoopAsync(context, branch, otherPpl.Id);

        var handler = new UpdateCoopCommandHandler(
            context, AllBranches(), new TestFieldScope(context, ppl.Id), CreateAttachments(context));

        // Act
        Result result = await handler.Handle(UpdateCoop(coop.Id, ppl.Id), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(CoopErrors.NotFound(coop.Id));
    }

    [Fact]
    public async Task UpdateCoop_Should_KeepAssignment_WhenUserIsLimitedToAssignedData()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Branch branch = await SeedBranchAsync(context);
        User ppl = await SeedFieldOfficerAsync(context, branch);
        User otherPpl = await SeedFieldOfficerAsync(context, branch);
        Coop coop = await SeedCoopAsync(context, branch, ppl.Id);

        var handler = new UpdateCoopCommandHandler(
            context, AllBranches(), new TestFieldScope(context, ppl.Id), CreateAttachments(context));

        // Act
        Result result = await handler.Handle(UpdateCoop(coop.Id, otherPpl.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.Coops.SingleAsync()).FieldOfficerUserId.ShouldBe(ppl.Id);
    }

    [Fact]
    public async Task UpdateCoop_Should_Reassign_WhenUserIsNotLimited()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Branch branch = await SeedBranchAsync(context);
        User ppl = await SeedFieldOfficerAsync(context, branch);
        User otherPpl = await SeedFieldOfficerAsync(context, branch);
        Coop coop = await SeedCoopAsync(context, branch, ppl.Id);

        var handler = new UpdateCoopCommandHandler(context, AllBranches(), NoFieldScope(), CreateAttachments(context));

        // Act
        Result result = await handler.Handle(UpdateCoop(coop.Id, otherPpl.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.Coops.SingleAsync()).FieldOfficerUserId.ShouldBe(otherPpl.Id);
    }

    private static CreateFarmerCommand CreateFarmer(Guid branchId, Guid? fieldOfficerUserId) => new(
        "INT-01",
        "Farm Inti",
        FarmerType.Inti,
        branchId,
        null,
        new TaxIdentityRequest(null, null, false),
        null,
        null,
        new BankAccountRequest(null, null, null),
        FieldOfficerUserId: fieldOfficerUserId);

    private static CreateCoopCommand CreateCoop(Guid farmerId) =>
        new(farmerId, "KDG-01", "Kandang 1", 10_000, HouseType.ClosedHouse, null, null, null);

    private static UpdateCoopCommand UpdateCoop(Guid coopId, Guid? fieldOfficerUserId) => new(
        coopId, "Kandang 1", 10_000, HouseType.ClosedHouse, null, null, null, true, FieldOfficerUserId: fieldOfficerUserId);

    private static IBranchAccess AllBranches()
    {
        IBranchAccess branchAccess = Substitute.For<IBranchAccess>();
        branchAccess.GetScopeAsync(Arg.Any<CancellationToken>()).Returns(BranchScope.All);

        return branchAccess;
    }

    private static async Task<Branch> SeedBranchAsync(TestDbContext context, string code = "BDG")
    {
        var branch = Branch.Create(code, $"Cabang {code}", null, null);
        context.Branches.Add(branch);
        await context.SaveChangesAsync();

        return branch;
    }

    private static Task<User> SeedFieldOfficerAsync(TestDbContext context, Branch branch) =>
        SeedUserAsync(context, branch, Role.Create($"PPL {Guid.NewGuid():N}", null, [Permissions.PartnershipAssignedOnly]).Value);

    private static async Task<User> SeedUserAsync(TestDbContext context, Branch branch, Role role)
    {
        BranchAccessProfile profile = BranchAccessProfile.Create($"Area {Guid.NewGuid():N}", null, false, [branch.Id]).Value;
        var user = User.Create($"{Guid.NewGuid():N}@example.com", "Petugas", "Lapangan", "hash");
        user.SetAccess(null, profile.Id, branch.Id);
        user.SetRoles([role.Id]);

        context.Roles.Add(role);
        context.BranchAccessProfiles.Add(profile);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    private static async Task<Farmer> SeedFarmerAsync(TestDbContext context, Branch branch, Guid? fieldOfficerUserId)
    {
        Farmer farmer = Farmer.Create($"INT-{Guid.NewGuid():N}"[..20], "Farm Inti", FarmerType.Inti, branch.Id, null,
            TaxIdentity.None, null, null, BankAccount.None).Value;
        farmer.AssignFieldOfficer(fieldOfficerUserId);

        context.Farmers.Add(farmer);
        await context.SaveChangesAsync();

        return farmer;
    }

    private static async Task<Coop> SeedCoopAsync(TestDbContext context, Branch branch, Guid? fieldOfficerUserId)
    {
        Farmer farmer = await SeedFarmerAsync(context, branch, null);
        Coop coop = Coop.Create(farmer, "KDG-01", "Kandang 1", 10_000, HouseType.ClosedHouse, null, null, null).Value;
        coop.AssignFieldOfficer(fieldOfficerUserId);

        context.Coops.Add(coop);
        await context.SaveChangesAsync();

        return coop;
    }

    /// <summary>
    /// A user limited to assigned data, with the same rules as the Infrastructure implementation.
    /// </summary>
    private sealed class TestFieldScope(TestDbContext context, Guid userId) : IFieldScope
    {
        public Task<FieldScope> GetScopeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new FieldScope(true, userId));

        public Task<bool> CanAccessFarmerAsync(Guid farmerId, CancellationToken cancellationToken = default) =>
            context.Farmers.AnyAsync(
                f => f.Id == farmerId &&
                     (f.FieldOfficerUserId == userId ||
                      context.Coops.Any(c => c.FarmerId == f.Id && c.FieldOfficerUserId == userId)),
                cancellationToken);

        public Task<bool> CanAccessCoopAsync(Guid coopId, CancellationToken cancellationToken = default) =>
            context.Coops.AnyAsync(c => c.Id == coopId && c.FieldOfficerUserId == userId, cancellationToken);

        public Task<bool> CanAccessCycleAsync(Guid cycleId, CancellationToken cancellationToken = default) =>
            context.ProductionCycles
                .Where(pc => pc.Id == cycleId)
                .Join(context.Coops, pc => pc.CoopId, c => c.Id, (_, c) => c)
                .AnyAsync(c => c.FieldOfficerUserId == userId, cancellationToken);
    }
}
