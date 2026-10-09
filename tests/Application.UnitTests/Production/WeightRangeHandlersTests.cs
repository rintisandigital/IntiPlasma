using Application.UnitTests.Abstractions;
using Application.WeightRanges;
using Domain.MasterData.WeightRanges;
using SharedKernel;

namespace Application.UnitTests.Production;

/// <summary>
/// Master Rentang Bobot (PLAN-MOBILE M-46): active ranges never overlap.
/// </summary>
public sealed class WeightRangeHandlersTests : BaseHandlerTest
{
    [Fact]
    public async Task Create_Should_RejectARangeOverlappingAnActiveOne()
    {
        using TestDbContext context = CreateDbContext();
        var handler = new CreateWeightRangeCommandHandler(context);
        (await handler.Handle(new CreateWeightRangeCommand("B16", "1,6 – 1,8", 1.6m, 1.8m, 2), default)).IsSuccess.ShouldBeTrue();

        Result<Guid> overlapping = await handler.Handle(new CreateWeightRangeCommand("B17", "1,7 – 2,0", 1.7m, 2.0m, 3), default);
        Result<Guid> adjacent = await handler.Handle(new CreateWeightRangeCommand("B18", "1,8 – 2,0", 1.8m, 2.0m, 3), default);

        overlapping.Error.ShouldBe(WeightRangeErrors.Overlap("B16"));
        adjacent.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Update_Should_AllowAnOverlap_OnlyWhileTheRangeIsInactive()
    {
        using TestDbContext context = CreateDbContext();
        var create = new CreateWeightRangeCommandHandler(context);
        await create.Handle(new CreateWeightRangeCommand("B16", "1,6 – 1,8", 1.6m, 1.8m, 1), default);
        Guid old = (await create.Handle(new CreateWeightRangeCommand("B18", "1,8 – 2,0", 1.8m, 2.0m, 2), default)).Value;
        var update = new UpdateWeightRangeCommandHandler(context);

        // Deactivated, the old range may be widened over its neighbour, but it cannot become active like that.
        (await update.Handle(new UpdateWeightRangeCommand(old, "Lama", 1.7m, 2.0m, 2, IsActive: false), default)).IsSuccess.ShouldBeTrue();
        (await update.Handle(new UpdateWeightRangeCommand(old, "Lama", 1.7m, 2.0m, 2, IsActive: true), default))
            .Error.ShouldBe(WeightRangeErrors.Overlap("B16"));
    }
}
