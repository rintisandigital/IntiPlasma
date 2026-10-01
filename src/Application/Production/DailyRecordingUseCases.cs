using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Cycles;
using Application.Documents;
using Application.Inventory;
using Domain.Documents.Attachments;
using Domain.Inventory.Stock;
using Domain.MasterData.Items;
using Domain.MasterData.Warehouses;
using Domain.Partnership.Cycles;
using Domain.Production.DailyRecordings;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Production;

public sealed record UsageRequest(Guid ItemId, Guid UomId, decimal Quantity);

/// <param name="Id">Optional client-generated id (Guid v7) for offline entry on the mobile app. Sending the same
/// recording again returns the existing one instead of failing.</param>
public sealed record CreateDailyRecordingCommand(
    Guid? Id,
    Guid CycleId,
    DateOnly Date,
    int Mortality,
    int Culling,
    decimal? AverageBodyWeightGram,
    string? Notes,
    IReadOnlyList<UsageRequest> Usages,
    IReadOnlyList<Guid>? Documents = null) : ICommand<Guid>;

/// <summary>
/// Revisi daily recording: replaces all values; the reason and the previous values are kept in the history.
/// Stock is corrected by reversing the previous usage and issuing the new usage.
/// </summary>
public sealed record ReviseDailyRecordingCommand(
    Guid DailyRecordingId,
    string Reason,
    int Mortality,
    int Culling,
    decimal? AverageBodyWeightGram,
    string? Notes,
    IReadOnlyList<UsageRequest> Usages,
    IReadOnlyList<Guid>? Documents = null) : ICommand;

internal sealed class UsageRequestValidator : AbstractValidator<UsageRequest>
{
    public UsageRequestValidator()
    {
        RuleFor(u => u.ItemId).NotEmpty();
        RuleFor(u => u.UomId).NotEmpty();
        RuleFor(u => u.Quantity).GreaterThan(0);
    }
}

internal sealed class CreateDailyRecordingCommandValidator : AbstractValidator<CreateDailyRecordingCommand>
{
    public CreateDailyRecordingCommandValidator()
    {
        RuleFor(c => c.CycleId).NotEmpty();
        RuleFor(c => c.Mortality).GreaterThanOrEqualTo(0);
        RuleFor(c => c.Culling).GreaterThanOrEqualTo(0);
        RuleFor(c => c.AverageBodyWeightGram).GreaterThan(0).LessThan(10_000);
        RuleFor(c => c.Notes).MaximumLength(1000);
        RuleFor(c => c.Usages).NotNull();
        RuleForEach(c => c.Usages).SetValidator(new UsageRequestValidator());
        RuleFor(c => c.Documents).ValidDocuments();
    }
}

internal sealed class ReviseDailyRecordingCommandValidator : AbstractValidator<ReviseDailyRecordingCommand>
{
    public ReviseDailyRecordingCommandValidator()
    {
        RuleFor(c => c.DailyRecordingId).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(300);
        RuleFor(c => c.Mortality).GreaterThanOrEqualTo(0);
        RuleFor(c => c.Culling).GreaterThanOrEqualTo(0);
        RuleFor(c => c.AverageBodyWeightGram).GreaterThan(0).LessThan(10_000);
        RuleFor(c => c.Notes).MaximumLength(1000);
        RuleFor(c => c.Usages).NotNull();
        RuleForEach(c => c.Usages).SetValidator(new UsageRequestValidator());
        RuleFor(c => c.Documents).ValidDocuments();
    }
}

internal static class DailyRecordingSupport
{
    /// <summary>
    /// Recordings may be entered at most one day ahead of the server date (time zone slack for WIB/WITA/WIT).
    /// </summary>
    public static Result EnsureNotFuture(DateOnly date, IDateTimeProvider dateTimeProvider)
    {
        DateOnly latest = DateOnly.FromDateTime(dateTimeProvider.UtcNow).AddDays(1);

        return date > latest ? Result.Failure(DailyRecordingErrors.FutureDate(date)) : Result.Success();
    }

    public static async Task<Result<DailyRecordingValues>> ToValuesAsync(
        IApplicationDbContext context,
        int mortality,
        int culling,
        decimal? averageBodyWeightGram,
        string? notes,
        IReadOnlyList<UsageRequest> usages,
        CancellationToken cancellationToken)
    {
        Dictionary<Guid, Item> items = await InventorySupport.LoadItemsAsync(context, usages.Select(u => u.ItemId), cancellationToken);

        Result<List<BaseQuantityLine>> converted = StockPosting.ToBaseQuantities(
            items,
            usages.Select(u => (u.ItemId, u.UomId, u.Quantity)),
            InventorySupport.UsableCategories,
            DailyRecordingErrors.UsageItemNotAllowed);

        if (converted.IsFailure)
        {
            return Result.Failure<DailyRecordingValues>(converted.Error);
        }

        return new DailyRecordingValues(
            mortality,
            culling,
            averageBodyWeightGram,
            notes,
            [.. converted.Value.Select(l => new DailyRecordingUsageInput(l.ItemId, l.UomId, l.Quantity, l.BaseQuantity))]);
    }

    /// <summary>
    /// Takes the recorded usage out of the coop warehouse and remembers the value of each line.
    /// </summary>
    public static async Task<Result> IssueUsagesAsync(
        IApplicationDbContext context,
        StockBalances balances,
        Warehouse coopWarehouse,
        ProductionCycle cycle,
        DailyRecording recording,
        CancellationToken cancellationToken)
    {
        StockMovement movement = Movement(StockMovementType.Usage, cycle, recording);

        foreach (DailyRecordingUsage usage in recording.Usages)
        {
            StockBalance balance = await balances.GetAsync(coopWarehouse.Id, usage.ItemId, cancellationToken);

            Result<StockLedgerEntry> issued = balance.Issue(movement, usage.BaseQuantity);
            if (issued.IsFailure)
            {
                return issued;
            }

            context.StockLedgerEntries.Add(issued.Value);
            recording.RecordUsageValue(usage.ItemId, -issued.Value.Value);
        }

        return Result.Success();
    }

    /// <summary>
    /// Puts previously recorded usage back into the coop warehouse at the value it left with.
    /// </summary>
    public static async Task ReverseUsagesAsync(
        IApplicationDbContext context,
        StockBalances balances,
        Warehouse coopWarehouse,
        ProductionCycle cycle,
        DailyRecording recording,
        IReadOnlyList<(Guid ItemId, decimal BaseQuantity, Money Value)> usages,
        CancellationToken cancellationToken)
    {
        StockMovement movement = Movement(StockMovementType.UsageReversal, cycle, recording);

        foreach ((Guid itemId, decimal quantity, Money value) in usages)
        {
            StockBalance balance = await balances.GetAsync(coopWarehouse.Id, itemId, cancellationToken);

            context.StockLedgerEntries.Add(balance.Receive(movement, quantity, value).Value);
        }
    }

    private static StockMovement Movement(StockMovementType type, ProductionCycle cycle, DailyRecording recording) =>
        new(recording.Date, type, nameof(DailyRecording), recording.Id, $"{cycle.Number} {recording.Date:yyyy-MM-dd}", cycle.Id);
}

internal sealed class CreateDailyRecordingCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IDateTimeProvider dateTimeProvider,
    IAttachmentService attachments) : ICommandHandler<CreateDailyRecordingCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateDailyRecordingCommand command, CancellationToken cancellationToken)
    {
        if (command.Id is Guid clientId)
        {
            var existing = await context.DailyRecordings
                .Where(r => r.Id == clientId)
                .Select(r => new { r.CycleId, r.Date })
                .SingleOrDefaultAsync(cancellationToken);

            if (existing is not null)
            {
                return existing.CycleId == command.CycleId && existing.Date == command.Date
                    ? clientId
                    : Result.Failure<Guid>(DailyRecordingErrors.IdBelongsToOtherRecording);
            }
        }

        Result<ProductionCycle> cycle = await CycleLoader.LoadAsync(context, branchAccess, command.CycleId, cancellationToken);
        if (cycle.IsFailure)
        {
            return Result.Failure<Guid>(cycle.Error);
        }

        Result notFuture = DailyRecordingSupport.EnsureNotFuture(command.Date, dateTimeProvider);
        if (notFuture.IsFailure)
        {
            return Result.Failure<Guid>(notFuture.Error);
        }

        if (await context.DailyRecordings.AnyAsync(r => r.CycleId == command.CycleId && r.Date == command.Date, cancellationToken))
        {
            return Result.Failure<Guid>(DailyRecordingErrors.AlreadyRecorded(command.Date));
        }

        Result<DailyRecordingValues> values = await DailyRecordingSupport.ToValuesAsync(
            context, command.Mortality, command.Culling, command.AverageBodyWeightGram, command.Notes, command.Usages, cancellationToken);

        if (values.IsFailure)
        {
            return Result.Failure<Guid>(values.Error);
        }

        Result<DailyRecording> recording = DailyRecording.Create(command.Id, cycle.Value, command.Date, values.Value);
        if (recording.IsFailure)
        {
            return Result.Failure<Guid>(recording.Error);
        }

        Result depletion = cycle.Value.ApplyDepletion(command.Mortality, command.Culling);
        if (depletion.IsFailure)
        {
            return Result.Failure<Guid>(depletion.Error);
        }

        Result<Warehouse> warehouse = await InventorySupport.FindCoopWarehouseAsync(context, cycle.Value.CoopId, cancellationToken);
        if (warehouse.IsFailure)
        {
            return Result.Failure<Guid>(warehouse.Error);
        }

        Result issued = await DailyRecordingSupport.IssueUsagesAsync(
            context, new StockBalances(context), warehouse.Value, cycle.Value, recording.Value, cancellationToken);

        if (issued.IsFailure)
        {
            return Result.Failure<Guid>(issued.Error);
        }

        Result documents = await attachments.ApplyDocumentsAsync(
            recording.Value,
            AttachmentOwner.Of(AttachmentOwnerTypes.DailyRecording, recording.Value.Id),
            recording.Value.BranchId,
            command.Documents,
            cancellationToken);
        if (documents.IsFailure)
        {
            return Result.Failure<Guid>(documents.Error);
        }

        context.DailyRecordings.Add(recording.Value);

        await context.SaveChangesAsync(cancellationToken);

        return recording.Value.Id;
    }
}

internal sealed class ReviseDailyRecordingCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider,
    IAttachmentService attachments) : ICommandHandler<ReviseDailyRecordingCommand>
{
    public async Task<Result> Handle(ReviseDailyRecordingCommand command, CancellationToken cancellationToken)
    {
        DailyRecording? recording = await context.DailyRecordings
            .Include(r => r.Usages)
            .Include(r => r.Revisions)
            .SingleOrDefaultAsync(r => r.Id == command.DailyRecordingId, cancellationToken);

        if (recording is null)
        {
            return Result.Failure(DailyRecordingErrors.NotFound(command.DailyRecordingId));
        }

        Result<ProductionCycle> cycle = await CycleLoader.LoadAsync(context, branchAccess, recording.CycleId, cancellationToken);
        if (cycle.IsFailure)
        {
            return cycle;
        }

        Result<DailyRecordingValues> values = await DailyRecordingSupport.ToValuesAsync(
            context, command.Mortality, command.Culling, command.AverageBodyWeightGram, command.Notes, command.Usages, cancellationToken);

        if (values.IsFailure)
        {
            return values;
        }

        Result depletion = cycle.Value.ApplyDepletion(
            command.Mortality - recording.Mortality, command.Culling - recording.Culling);

        if (depletion.IsFailure)
        {
            return depletion;
        }

        List<(Guid ItemId, decimal BaseQuantity, Money Value)> previousUsages =
            [.. recording.Usages.Select(u => (u.ItemId, u.BaseQuantity, u.Value))];

        Result revised = recording.Revise(values.Value, command.Reason, userContext.UserId, dateTimeProvider.UtcNow);
        if (revised.IsFailure)
        {
            return revised;
        }

        // The attachments of a revision document the correction; they belong to the new revision entry.
        Result documents = await attachments.ApplyDocumentsAsync(
            ids => recording.SetRevisionDocuments(recording.RevisionNumber, ids),
            () => recording.Revisions.Single(r => r.RevisionNumber == recording.RevisionNumber).Documents,
            AttachmentOwner.OfRevision(recording.Id, recording.RevisionNumber),
            recording.BranchId,
            command.Documents,
            cancellationToken);
        if (documents.IsFailure)
        {
            return documents;
        }

        Result<Warehouse> warehouse = await InventorySupport.FindCoopWarehouseAsync(context, cycle.Value.CoopId, cancellationToken);
        if (warehouse.IsFailure)
        {
            return warehouse;
        }

        var balances = new StockBalances(context);

        await DailyRecordingSupport.ReverseUsagesAsync(
            context, balances, warehouse.Value, cycle.Value, recording, previousUsages, cancellationToken);

        Result issued = await DailyRecordingSupport.IssueUsagesAsync(
            context, balances, warehouse.Value, cycle.Value, recording, cancellationToken);

        if (issued.IsFailure)
        {
            return issued;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
