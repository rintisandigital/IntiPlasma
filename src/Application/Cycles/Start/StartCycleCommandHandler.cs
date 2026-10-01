using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Documents;
using Application.Inventory;
using Application.Abstractions.Messaging;
using Domain.Documents.Attachments;
using Domain.Inventory.Stock;
using Domain.MasterData.Items;
using Domain.MasterData.Warehouses;
using Domain.Partnership.Cycles;
using SharedKernel;

namespace Application.Cycles.Start;

internal sealed class StartCycleCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IAttachmentService attachments)
    : ICommandHandler<StartCycleCommand, int>
{
    public async Task<Result<int>> Handle(StartCycleCommand command, CancellationToken cancellationToken)
    {
        Result<ProductionCycle> cycle = await CycleLoader.LoadAsync(context, branchAccess, command.CycleId, cancellationToken);
        if (cycle.IsFailure)
        {
            return Result.Failure<int>(cycle.Error);
        }

        Dictionary<Guid, Item> items = await InventorySupport.LoadItemsAsync(context, command.Lines.Select(l => l.ItemId), cancellationToken);
        if (command.Lines.Any(l => !items.TryGetValue(l.ItemId, out Item? item) || item.Category != ItemCategory.Doc) ||
            command.Lines.GroupBy(l => l.ItemId).Any(g => g.Count() > 1))
        {
            return Result.Failure<int>(InventoryErrors.ChickInNeedsDoc);
        }

        int population = command.Lines.Sum(l => l.Quantity);

        Result started = cycle.Value.Start(command.ChickInDate, population);
        if (started.IsFailure)
        {
            return Result.Failure<int>(started.Error);
        }

        Result<Warehouse> warehouse = await InventorySupport.FindCoopWarehouseAsync(context, cycle.Value.CoopId, cancellationToken);
        if (warehouse.IsFailure)
        {
            return Result.Failure<int>(warehouse.Error);
        }

        var balances = new StockBalances(context);
        var movement = new StockMovement(
            command.ChickInDate, StockMovementType.ChickIn, nameof(ProductionCycle), cycle.Value.Id, cycle.Value.Number, cycle.Value.Id);

        foreach (ChickInLine line in command.Lines)
        {
            StockBalance balance = await balances.GetAsync(warehouse.Value.Id, line.ItemId, cancellationToken);

            if (balance.Quantity < line.Quantity)
            {
                return Result.Failure<int>(CycleErrors.InsufficientDoc(line.Quantity, balance.Quantity));
            }

            context.StockLedgerEntries.Add(balance.Issue(movement, line.Quantity).Value);
        }

        Result documents = await attachments.ApplyDocumentsAsync(
            cycle.Value,
            AttachmentOwner.Of(AttachmentOwnerTypes.Cycle, cycle.Value.Id),
            cycle.Value.BranchId,
            command.Documents,
            cancellationToken);
        if (documents.IsFailure)
        {
            return Result.Failure<int>(documents.Error);
        }

        await context.SaveChangesAsync(cancellationToken);

        return population;
    }
}
