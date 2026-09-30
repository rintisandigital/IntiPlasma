using Application.Abstractions.Authorization;
using Application.Abstractions.Numbering;
using Application.Cycles.Plan;
using Application.UnitTests.Abstractions;
using Domain.Common;
using Domain.MasterData.Branches;
using Domain.MasterData.Coops;
using Domain.MasterData.Farmers;
using Domain.Partnership.Cycles;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Cycles;

public sealed class PlanCycleCommandHandlerTests : BaseHandlerTest
{
    private static readonly DateOnly ChickInDate = new(2026, 10, 5);

    [Fact]
    public async Task Handle_Should_PlanCycleWithDocumentNumber_WhenValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Coop coop = await SeedIntiCoopAsync(context);

        IDocumentNumberGenerator numbers = Substitute.For<IDocumentNumberGenerator>();
        numbers.NextAsync("SKL", "BDG", ChickInDate, Arg.Any<CancellationToken>()).Returns("SKL/BDG/2026/X/0001");

        var handler = new PlanCycleCommandHandler(context, AllBranches(), numbers);

        // Act
        Result<PlanCycleResponse> result = await handler.Handle(
            new PlanCycleCommand(coop.Id, null, ChickInDate, 9_000, null), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Number.ShouldBe("SKL/BDG/2026/X/0001");
        ProductionCycle cycle = await context.ProductionCycles.SingleAsync();
        cycle.Status.ShouldBe(CycleStatus.Planned);
        cycle.CoopId.ShouldBe(coop.Id);
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenCoopAlreadyHasOpenCycle()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Coop coop = await SeedIntiCoopAsync(context);
        IDocumentNumberGenerator numbers = Substitute.For<IDocumentNumberGenerator>();
        numbers.NextAsync(default!, default!, default, default).ReturnsForAnyArgs("SKL/BDG/2026/X/0001", "SKL/BDG/2026/X/0002");

        var handler = new PlanCycleCommandHandler(context, AllBranches(), numbers);
        await handler.Handle(new PlanCycleCommand(coop.Id, null, ChickInDate, 9_000, null), CancellationToken.None);

        // Act
        Result<PlanCycleResponse> result = await handler.Handle(
            new PlanCycleCommand(coop.Id, null, ChickInDate.AddDays(40), 9_000, null), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(CycleErrors.CoopHasOpenCycle(coop.Id));
    }

    [Fact]
    public async Task Handle_Should_ReturnForbidden_WhenUserHasNoAccessToBranch()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Coop coop = await SeedIntiCoopAsync(context);

        IBranchAccess otherBranchOnly = Substitute.For<IBranchAccess>();
        otherBranchOnly.GetScopeAsync(Arg.Any<CancellationToken>()).Returns(new BranchScope(false, [Guid.NewGuid()]));

        IDocumentNumberGenerator numbers = Substitute.For<IDocumentNumberGenerator>();
        var handler = new PlanCycleCommandHandler(context, otherBranchOnly, numbers);

        // Act
        Result<PlanCycleResponse> result = await handler.Handle(
            new PlanCycleCommand(coop.Id, null, ChickInDate, 9_000, null), CancellationToken.None);

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        await numbers.DidNotReceiveWithAnyArgs().NextAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task Handle_Should_NotConsumeDocumentNumber_WhenDomainRejectsPlan()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Coop coop = await SeedIntiCoopAsync(context);
        IDocumentNumberGenerator numbers = Substitute.For<IDocumentNumberGenerator>();
        var handler = new PlanCycleCommandHandler(context, AllBranches(), numbers);

        // Act: population above the coop capacity of 10,000.
        Result<PlanCycleResponse> result = await handler.Handle(
            new PlanCycleCommand(coop.Id, null, ChickInDate, 12_000, null), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(CycleErrors.InvalidPopulation(10_000));
        await numbers.DidNotReceiveWithAnyArgs().NextAsync(default!, default!, default, default);
    }

    private static IBranchAccess AllBranches()
    {
        IBranchAccess branchAccess = Substitute.For<IBranchAccess>();
        branchAccess.GetScopeAsync(Arg.Any<CancellationToken>()).Returns(BranchScope.All);

        return branchAccess;
    }

    private static async Task<Coop> SeedIntiCoopAsync(TestDbContext context)
    {
        var branch = Branch.Create("BDG", "Cabang Bandung", null, null);
        Farmer farmer = Farmer.Create("INT-01", "Farm Inti", FarmerType.Inti, branch.Id, null,
            TaxIdentity.None, null, null, BankAccount.None).Value;
        Coop coop = Coop.Create(farmer, "KDG-01", "Kandang 1", 10_000, HouseType.ClosedHouse, null, null, null).Value;

        context.Branches.Add(branch);
        context.Farmers.Add(farmer);
        context.Coops.Add(coop);
        await context.SaveChangesAsync();

        return coop;
    }
}
