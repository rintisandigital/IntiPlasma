using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Costing;
using Application.Inventory;
using Application.Sales;
using Domain.MasterData.Items;
using Domain.MasterData.Warehouses;
using Domain.Partnership.Cycles;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Production;

/// <summary>
/// Panen: birds caught and weighed. The first harvest moves the cycle to Harvesting.
/// </summary>
public sealed record RecordHarvestCommand(Guid CycleId, DateOnly Date, int Birds, decimal WeightKg, string? Notes) : ICommand<Guid>;

/// <summary>
/// Tutup siklus: requires the whole population to be harvested (or recorded as dead/culled), the coop warehouse
/// to be empty (leftover feed/OVK returned to the central warehouse) and every harvest to be sold (on a delivery order
/// billed by a posted sales invoice). Freezes the performance summary used by the
/// plasma settlement.
/// </summary>
public sealed record CloseCycleCommand(Guid CycleId) : ICommand<CyclePerformance>;

internal sealed class RecordHarvestCommandValidator : AbstractValidator<RecordHarvestCommand>
{
    public RecordHarvestCommandValidator()
    {
        RuleFor(c => c.CycleId).NotEmpty();
        RuleFor(c => c.Birds).GreaterThan(0);
        RuleFor(c => c.WeightKg).GreaterThan(0);
        RuleFor(c => c.Notes).MaximumLength(500);
    }
}

internal sealed class RecordHarvestCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<RecordHarvestCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RecordHarvestCommand command, CancellationToken cancellationToken)
    {
        Result notFuture = DailyRecordingSupport.EnsureNotFuture(command.Date, dateTimeProvider);
        if (notFuture.IsFailure)
        {
            return Result.Failure<Guid>(notFuture.Error);
        }

        Result<ProductionCycle> cycle = await ProductionSupport.LoadWithHarvestsAsync(context, branchAccess, command.CycleId, cancellationToken);
        if (cycle.IsFailure)
        {
            return Result.Failure<Guid>(cycle.Error);
        }

        Result<CycleHarvest> harvest = cycle.Value.RecordHarvest(command.Date, command.Birds, command.WeightKg, command.Notes);
        if (harvest.IsFailure)
        {
            return Result.Failure<Guid>(harvest.Error);
        }

        await context.SaveChangesAsync(cancellationToken);

        return harvest.Value.Id;
    }
}

internal sealed class CloseCycleCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<CloseCycleCommand, CyclePerformance>
{
    public async Task<Result<CyclePerformance>> Handle(CloseCycleCommand command, CancellationToken cancellationToken)
    {
        Result<ProductionCycle> cycle = await ProductionSupport.LoadWithHarvestsAsync(context, branchAccess, command.CycleId, cancellationToken);
        if (cycle.IsFailure)
        {
            return Result.Failure<CyclePerformance>(cycle.Error);
        }

        Result<Warehouse> warehouse = await InventorySupport.FindCoopWarehouseAsync(context, cycle.Value.CoopId, cancellationToken);
        if (warehouse.IsFailure)
        {
            return Result.Failure<CyclePerformance>(warehouse.Error);
        }

        List<string> leftovers = await context.StockBalances
            .Where(b => b.WarehouseId == warehouse.Value.Id && b.Quantity > 0)
            .Join(context.Items, b => b.ItemId, i => i.Id, (b, i) => i.Code)
            .ToListAsync(cancellationToken);

        if (leftovers.Count > 0)
        {
            return Result.Failure<CyclePerformance>(CycleErrors.LeftoverStock(string.Join(", ", leftovers)));
        }

        int unsold = await SalesSupport.UnsoldHarvestCountAsync(
            context, [.. cycle.Value.Harvests.Select(h => h.Id)], cancellationToken);

        if (unsold > 0)
        {
            return Result.Failure<CyclePerformance>(CycleErrors.UnsoldHarvest(unsold));
        }

        decimal feedKg = await ProductionSupport.FeedUsedKgAsync(context, cycle.Value.Id, null, cancellationToken);

        int birds = cycle.Value.HarvestedBirds;
        decimal? averageWeight = birds == 0 ? null : cycle.Value.HarvestedWeightKg / birds;
        decimal? averageAge = birds == 0 ? null : cycle.Value.Harvests.Sum(h => (decimal)h.Birds * h.AgeDays) / birds;

        var performance = CyclePerformance.Calculate(
            cycle.Value.InitialPopulation ?? 0,
            cycle.Value.TotalMortality,
            cycle.Value.TotalCulling,
            birds,
            cycle.Value.HarvestedWeightKg,
            feedKg,
            averageWeight,
            averageAge);

        CycleCostSummary cost = await CycleCosting.SummarizeAsync(context, cycle.Value, cancellationToken);

        Result closed = cycle.Value.Close(performance, cost);
        if (closed.IsFailure)
        {
            return Result.Failure<CyclePerformance>(closed.Error);
        }

        await context.SaveChangesAsync(cancellationToken);

        return performance;
    }
}

internal static class ProductionSupport
{
    public static async Task<Result<ProductionCycle>> LoadWithHarvestsAsync(
        IApplicationDbContext context,
        IBranchAccess branchAccess,
        Guid cycleId,
        CancellationToken cancellationToken)
    {
        ProductionCycle? cycle = await context.ProductionCycles
            .Include(c => c.Harvests)
            .SingleOrDefaultAsync(c => c.Id == cycleId, cancellationToken);

        if (cycle is null)
        {
            return Result.Failure<ProductionCycle>(CycleErrors.NotFound(cycleId));
        }

        Result access = await branchAccess.EnsureAccessAsync(cycle.BranchId, cancellationToken);

        return access.IsSuccess ? cycle : Result.Failure<ProductionCycle>(access.Error);
    }

    /// <summary>
    /// Feed consumed by the cycle (kg, from daily recordings), optionally up to a date.
    /// </summary>
    public static Task<decimal> FeedUsedKgAsync(
        IApplicationDbContext context,
        Guid cycleId,
        DateOnly? upTo,
        CancellationToken cancellationToken) =>
        context.DailyRecordings
            .Where(r => r.CycleId == cycleId && (upTo == null || r.Date <= upTo))
            .SelectMany(r => r.Usages)
            .Join(context.Items.Where(i => i.Category == ItemCategory.Feed), u => u.ItemId, i => i.Id, (u, _) => u.BaseQuantity)
            .SumAsync(cancellationToken);
}
