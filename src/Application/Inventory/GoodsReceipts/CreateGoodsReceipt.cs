using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Numbering;
using Application.Procurement;
using Domain.Inventory.GoodsReceipts;
using Domain.Inventory.Stock;
using Domain.MasterData.Items;
using Domain.MasterData.Warehouses;
using Domain.Procurement.PurchaseOrders;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Inventory.GoodsReceipts;

public sealed record GoodsReceiptLineRequest(int PurchaseOrderLineNumber, decimal Quantity);

/// <summary>
/// Posts a goods receipt: stock goes into the warehouse at the order price, the order's received quantities are
/// updated, and the auto journal (persediaan / hutang belum ditagih) follows through the outbox.
/// </summary>
public sealed record CreateGoodsReceiptCommand(
    Guid PurchaseOrderId,
    Guid WarehouseId,
    DateOnly ReceiptDate,
    string? DeliveryNoteNumber,
    string? Notes,
    IReadOnlyList<GoodsReceiptLineRequest> Lines) : ICommand<CreateGoodsReceiptResponse>;

public sealed record CreateGoodsReceiptResponse(Guid Id, string Number);

internal sealed class CreateGoodsReceiptCommandValidator : AbstractValidator<CreateGoodsReceiptCommand>
{
    public CreateGoodsReceiptCommandValidator()
    {
        RuleFor(c => c.PurchaseOrderId).NotEmpty();
        RuleFor(c => c.WarehouseId).NotEmpty();
        RuleFor(c => c.DeliveryNoteNumber).MaximumLength(50);
        RuleFor(c => c.Notes).MaximumLength(1000);
        RuleFor(c => c.Lines).NotEmpty();
        RuleForEach(c => c.Lines).ChildRules(l => l.RuleFor(x => x.Quantity).GreaterThan(0));
    }
}

internal sealed class CreateGoodsReceiptCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IDocumentNumberGenerator numberGenerator) : ICommandHandler<CreateGoodsReceiptCommand, CreateGoodsReceiptResponse>
{
    public const string DocumentPrefix = "BPB";

    public async Task<Result<CreateGoodsReceiptResponse>> Handle(CreateGoodsReceiptCommand command, CancellationToken cancellationToken)
    {
        Result<PurchaseOrder> order = await PurchaseOrderSupport.LoadAsync(context, branchAccess, command.PurchaseOrderId, cancellationToken);
        if (order.IsFailure)
        {
            return Result.Failure<CreateGoodsReceiptResponse>(order.Error);
        }

        Warehouse? warehouse = await context.Warehouses.AsNoTracking()
            .SingleOrDefaultAsync(w => w.Id == command.WarehouseId, cancellationToken);

        if (warehouse is null)
        {
            return Result.Failure<CreateGoodsReceiptResponse>(WarehouseErrors.NotFound(command.WarehouseId));
        }

        Result<Guid?> cycleId = await InventorySupport.ResolveCoopCycleAsync(context, warehouse, cancellationToken);
        if (cycleId.IsFailure)
        {
            return Result.Failure<CreateGoodsReceiptResponse>(cycleId.Error);
        }

        Dictionary<Guid, Item> items = await InventorySupport.LoadItemsAsync(
            context, order.Value.Lines.Select(l => l.ItemId), cancellationToken);
        var categories = items.ToDictionary(i => i.Key, i => i.Value.Category);

        Result<List<GoodsReceiptLineInput>> lines = ToBaseQuantities(order.Value, items, command.Lines);
        if (lines.IsFailure)
        {
            return Result.Failure<CreateGoodsReceiptResponse>(lines.Error);
        }

        Result<GoodsReceipt> validation = GoodsReceipt.Create(
            string.Empty, order.Value, warehouse, cycleId.Value, command.ReceiptDate,
            command.DeliveryNoteNumber, command.Notes, lines.Value, categories);

        // Validated with a placeholder number first so a rejected receipt does not consume a document number.
        if (validation.IsFailure)
        {
            return Result.Failure<CreateGoodsReceiptResponse>(validation.Error);
        }

        string number = await numberGenerator.NextForBranchAsync(
            context, DocumentPrefix, order.Value.BranchId, command.ReceiptDate, cancellationToken);

        GoodsReceipt receipt = GoodsReceipt.Create(
            number, order.Value, warehouse, cycleId.Value, command.ReceiptDate,
            command.DeliveryNoteNumber, command.Notes, lines.Value, categories).Value;

        foreach (GoodsReceiptLineInput line in lines.Value)
        {
            Result registered = order.Value.RegisterReceipt(line.PurchaseOrderLineNumber, line.Quantity);
            if (registered.IsFailure)
            {
                return Result.Failure<CreateGoodsReceiptResponse>(registered.Error);
            }
        }

        var balances = new StockBalances(context);
        var movement = new StockMovement(
            receipt.ReceiptDate, StockMovementType.Receipt, nameof(GoodsReceipt), receipt.Id, receipt.Number, receipt.CycleId);

        foreach (GoodsReceiptLine line in receipt.Lines)
        {
            StockBalance balance = await balances.GetAsync(receipt.WarehouseId, line.ItemId, cancellationToken);

            Result<StockLedgerEntry> entry = balance.Receive(movement, line.BaseQuantity, line.Value);
            if (entry.IsFailure)
            {
                return Result.Failure<CreateGoodsReceiptResponse>(entry.Error);
            }

            context.StockLedgerEntries.Add(entry.Value);
        }

        context.GoodsReceipts.Add(receipt);

        await context.SaveChangesAsync(cancellationToken);

        return new CreateGoodsReceiptResponse(receipt.Id, receipt.Number);
    }

    private static Result<List<GoodsReceiptLineInput>> ToBaseQuantities(
        PurchaseOrder order,
        Dictionary<Guid, Item> items,
        IReadOnlyList<GoodsReceiptLineRequest> requests)
    {
        var lines = new List<GoodsReceiptLineInput>();

        foreach (GoodsReceiptLineRequest request in requests)
        {
            PurchaseOrderLine? orderLine = order.Lines.FirstOrDefault(l => l.LineNumber == request.PurchaseOrderLineNumber);
            if (orderLine is null)
            {
                return Result.Failure<List<GoodsReceiptLineInput>>(PurchaseOrderErrors.LineNotFound(request.PurchaseOrderLineNumber));
            }

            Result<decimal> baseQuantity = items[orderLine.ItemId].ConvertToBase(orderLine.UomId, request.Quantity);
            if (baseQuantity.IsFailure)
            {
                return Result.Failure<List<GoodsReceiptLineInput>>(baseQuantity.Error);
            }

            lines.Add(new GoodsReceiptLineInput(request.PurchaseOrderLineNumber, request.Quantity, baseQuantity.Value));
        }

        return lines;
    }
}
