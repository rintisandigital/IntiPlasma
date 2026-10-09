using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Cycles;
using Domain.MasterData.WeightRanges;
using Domain.Partnership.Cycles;
using Domain.Production.LiveBirdStock;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Production;

/// <summary>
/// Stok ayam harian for one weight range (PLAN-MOBILE M-22 s.d. M-27, M-47, M-50). Entering the same cycle, date and
/// range again changes that entry, so the mobile app can resend or correct offline entries freely.
/// </summary>
/// <param name="Id">Client-generated id (Guid v7) of a new entry; ignored when the combination already exists.</param>
public sealed record UpsertLiveBirdStockEntryCommand(
    Guid? Id,
    Guid CycleId,
    DateOnly Date,
    Guid WeightRangeId,
    int Birds,
    decimal WeightKg,
    string? Notes) : ICommand<Guid>;

/// <summary>
/// Removes an entry of today or yesterday, e.g. one entered in the wrong weight range (M-49).
/// </summary>
public sealed record DeleteLiveBirdStockEntryCommand(Guid EntryId) : ICommand;

internal sealed class UpsertLiveBirdStockEntryCommandValidator : AbstractValidator<UpsertLiveBirdStockEntryCommand>
{
    public UpsertLiveBirdStockEntryCommandValidator()
    {
        RuleFor(c => c.CycleId).NotEmpty();
        RuleFor(c => c.WeightRangeId).NotEmpty();
        RuleFor(c => c.Birds).GreaterThan(0);
        RuleFor(c => c.WeightKg).GreaterThan(0).LessThan(100_000_000);
        RuleFor(c => c.Notes).MaximumLength(500);
    }
}

internal sealed class UpsertLiveBirdStockEntryCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IFieldScope fieldScope,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<UpsertLiveBirdStockEntryCommand, Guid>
{
    public async Task<Result<Guid>> Handle(UpsertLiveBirdStockEntryCommand command, CancellationToken cancellationToken)
    {
        if (command.Id is Guid clientId)
        {
            var used = await context.LiveBirdStockEntries
                .Where(e => e.Id == clientId)
                .Select(e => new { e.CycleId, e.Date, e.WeightRangeId })
                .SingleOrDefaultAsync(cancellationToken);

            if (used is not null
                && (used.CycleId != command.CycleId || used.Date != command.Date || used.WeightRangeId != command.WeightRangeId))
            {
                return Result.Failure<Guid>(LiveBirdStockErrors.IdBelongsToOtherEntry);
            }
        }

        Result<ProductionCycle> cycle = await CycleLoader.LoadAsync(context, branchAccess, fieldScope, command.CycleId, cancellationToken);
        if (cycle.IsFailure)
        {
            return Result.Failure<Guid>(cycle.Error);
        }

        // One day of slack for the time zones of Indonesia, as for daily recordings.
        if (command.Date > DateOnly.FromDateTime(dateTimeProvider.UtcNow).AddDays(1))
        {
            return Result.Failure<Guid>(LiveBirdStockErrors.FutureDate(command.Date));
        }

        WeightRange? range = await context.WeightRanges
            .SingleOrDefaultAsync(r => r.Id == command.WeightRangeId, cancellationToken);
        if (range is null)
        {
            return Result.Failure<Guid>(WeightRangeErrors.NotFound(command.WeightRangeId));
        }

        List<LiveBirdStockEntry> sameDay = await context.LiveBirdStockEntries
            .Where(e => e.CycleId == command.CycleId && e.Date == command.Date)
            .ToListAsync(cancellationToken);

        int otherRanges = sameDay.Where(e => e.WeightRangeId != range.Id).Sum(e => e.Birds);
        if (otherRanges + command.Birds > cycle.Value.CurrentPopulation)
        {
            return Result.Failure<Guid>(LiveBirdStockErrors.PopulationExceeded(otherRanges + command.Birds, cycle.Value.CurrentPopulation));
        }

        LiveBirdStockEntry? entry = sameDay.Find(e => e.WeightRangeId == range.Id);
        if (entry is not null)
        {
            Result updated = entry.Update(cycle.Value, range, command.Birds, command.WeightKg, command.Notes);
            if (updated.IsFailure)
            {
                return Result.Failure<Guid>(updated.Error);
            }
        }
        else
        {
            Result<LiveBirdStockEntry> created = LiveBirdStockEntry.Create(
                command.Id, cycle.Value, command.Date, range, command.Birds, command.WeightKg, command.Notes);
            if (created.IsFailure)
            {
                return Result.Failure<Guid>(created.Error);
            }

            entry = created.Value;
            context.LiveBirdStockEntries.Add(entry);
        }

        await context.SaveChangesAsync(cancellationToken);

        return entry.Id;
    }
}

internal sealed class DeleteLiveBirdStockEntryCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IFieldScope fieldScope,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<DeleteLiveBirdStockEntryCommand>
{
    public async Task<Result> Handle(DeleteLiveBirdStockEntryCommand command, CancellationToken cancellationToken)
    {
        LiveBirdStockEntry? entry = await context.LiveBirdStockEntries
            .SingleOrDefaultAsync(e => e.Id == command.EntryId, cancellationToken);

        if (entry is null || !await fieldScope.CanAccessCycleAsync(entry.CycleId, cancellationToken))
        {
            return Result.Failure(LiveBirdStockErrors.NotFound(command.EntryId));
        }

        Result access = await branchAccess.EnsureAccessAsync(entry.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return access;
        }

        if (entry.Date < DateOnly.FromDateTime(dateTimeProvider.UtcNow).AddDays(-1))
        {
            return Result.Failure(LiveBirdStockErrors.TooOldToDelete);
        }

        context.LiveBirdStockEntries.Remove(entry);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
