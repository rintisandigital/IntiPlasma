using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Numbering;
using Domain.Inventory.Stock;
using Domain.Inventory.StockTransfers;
using Domain.MasterData.Items;
using Domain.MasterData.Warehouses;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Inventory.StockTransfers;

public sealed record StockTransferLineRequest(Guid ItemId, Guid UomId, decimal Quantity);

/// <summary>
/// Posts a stock transfer out of a central warehouse. Stock leaves the source at moving average cost and arrives
/// at the destination with exactly that value. Transfers to a coop are charged to its open cycle (auto journal
/// ayam dalam proses / persediaan follows through the outbox).
/// </summary>
public sealed record CreateStockTransferCommand(
    Guid FromWarehouseId,
    Guid ToWarehouseId,
    DateOnly TransferDate,
    string? Notes,
    IReadOnlyList<StockTransferLineRequest> Lines) : ICommand<CreateStockTransferResponse>;

public sealed record CreateStockTransferResponse(Guid Id, string Number, Guid? CycleId);

internal sealed class CreateStockTransferCommandValidator : AbstractValidator<CreateStockTransferCommand>
{
    public CreateStockTransferCommandValidator()
    {
        RuleFor(c => c.FromWarehouseId).NotEmpty();
        RuleFor(c => c.ToWarehouseId).NotEmpty();
        RuleFor(c => c.Notes).MaximumLength(1000);
        RuleFor(c => c.Lines).NotEmpty();
        RuleForEach(c => c.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ItemId).NotEmpty();
            l.RuleFor(x => x.UomId).NotEmpty();
            l.RuleFor(x => x.Quantity).GreaterThan(0);
        });
    }
}

internal sealed class CreateStockTransferCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IDocumentNumberGenerator numberGenerator) : ICommandHandler<CreateStockTransferCommand, CreateStockTransferResponse>
{
    public const string DocumentPrefix = "TRF";

    public async Task<Result<CreateStockTransferResponse>> Handle(CreateStockTransferCommand command, CancellationToken cancellationToken)
    {
        List<Warehouse> warehouses = await context.Warehouses.AsNoTracking()
            .Where(w => w.Id == command.FromWarehouseId || w.Id == command.ToWarehouseId)
            .ToListAsync(cancellationToken);

        Warehouse? from = warehouses.Find(w => w.Id == command.FromWarehouseId);
        Warehouse? to = warehouses.Find(w => w.Id == command.ToWarehouseId);

        if (from is null || to is null)
        {
            return Result.Failure<CreateStockTransferResponse>(
                WarehouseErrors.NotFound(from is null ? command.FromWarehouseId : command.ToWarehouseId));
        }

        Result access = await branchAccess.EnsureAccessAsync(from.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<CreateStockTransferResponse>(access.Error);
        }

        Result<Guid?> cycleId = await InventorySupport.ResolveCoopCycleAsync(context, to, cancellationToken);
        if (cycleId.IsFailure)
        {
            return Result.Failure<CreateStockTransferResponse>(cycleId.Error);
        }

        Result<List<StockTransferLineInput>> lines = await ToBaseQuantitiesAsync(command.Lines, cycleId.Value is not null, cancellationToken);
        if (lines.IsFailure)
        {
            return Result.Failure<CreateStockTransferResponse>(lines.Error);
        }

        Result<StockTransfer> validation = StockTransfer.Create(
            string.Empty, from, to, cycleId.Value, command.TransferDate, command.Notes, lines.Value);

        if (validation.IsFailure)
        {
            return Result.Failure<CreateStockTransferResponse>(validation.Error);
        }

        string number = await numberGenerator.NextForBranchAsync(
            context, DocumentPrefix, from.BranchId, command.TransferDate, cancellationToken);

        StockTransfer transfer = StockTransfer.Create(
            number, from, to, cycleId.Value, command.TransferDate, command.Notes, lines.Value).Value;

        Result moved = await MoveStockAsync(transfer, cancellationToken);
        if (moved.IsFailure)
        {
            return Result.Failure<CreateStockTransferResponse>(moved.Error);
        }

        context.StockTransfers.Add(transfer);

        await context.SaveChangesAsync(cancellationToken);

        return new CreateStockTransferResponse(transfer.Id, transfer.Number, transfer.CycleId);
    }

    private async Task<Result> MoveStockAsync(StockTransfer transfer, CancellationToken cancellationToken)
    {
        var balances = new StockBalances(context);
        var outMovement = new StockMovement(
            transfer.TransferDate, StockMovementType.TransferOut, nameof(StockTransfer), transfer.Id, transfer.Number, transfer.CycleId);
        StockMovement inMovement = outMovement with { Type = StockMovementType.TransferIn };

        foreach (StockTransferLine line in transfer.Lines)
        {
            StockBalance source = await balances.GetAsync(transfer.FromWarehouseId, line.ItemId, cancellationToken);

            Result<StockLedgerEntry> issued = source.Issue(outMovement, line.BaseQuantity);
            if (issued.IsFailure)
            {
                return issued;
            }

            Money value = -issued.Value.Value;
            transfer.RecordCost(line.LineNumber, issued.Value.UnitCost, value);

            StockBalance destination = await balances.GetAsync(transfer.ToWarehouseId, line.ItemId, cancellationToken);

            Result<StockLedgerEntry> received = destination.Receive(inMovement, line.BaseQuantity, value);
            if (received.IsFailure)
            {
                return received;
            }

            context.StockLedgerEntries.AddRange(issued.Value, received.Value);
        }

        return Result.Success();
    }

    private async Task<Result<List<StockTransferLineInput>>> ToBaseQuantitiesAsync(
        IReadOnlyList<StockTransferLineRequest> requests,
        bool toCycle,
        CancellationToken cancellationToken)
    {
        Dictionary<Guid, Item> items = await InventorySupport.LoadItemsAsync(context, requests.Select(r => r.ItemId), cancellationToken);

        var lines = new List<StockTransferLineInput>();

        foreach (StockTransferLineRequest request in requests)
        {
            if (!items.TryGetValue(request.ItemId, out Item? item))
            {
                return Result.Failure<List<StockTransferLineInput>>(ItemErrors.NotFound(request.ItemId));
            }

            // Only sapronak can be charged to a production cycle; the auto journal has no account for other items.
            if (toCycle && !InventorySupport.SapronakCategories.Contains(item.Category))
            {
                return Result.Failure<List<StockTransferLineInput>>(StockTransferErrors.OnlySapronakToCoop);
            }

            Result<decimal> baseQuantity = item.ConvertToBase(request.UomId, request.Quantity);
            if (baseQuantity.IsFailure)
            {
                return Result.Failure<List<StockTransferLineInput>>(baseQuantity.Error);
            }

            lines.Add(new StockTransferLineInput(request.ItemId, request.UomId, request.Quantity, baseQuantity.Value));
        }

        return lines;
    }
}
