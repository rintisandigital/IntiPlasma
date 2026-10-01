using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Numbering;
using Application.Documents;
using Domain.Documents.Attachments;
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
    IReadOnlyList<StockTransferLineRequest> Lines,
    IReadOnlyList<Guid>? Documents = null) : ICommand<CreateStockTransferResponse>;

public sealed record CreateStockTransferResponse(Guid Id, string Number, Guid? CycleId);

internal sealed class StockTransferLineRequestValidator : AbstractValidator<StockTransferLineRequest>
{
    public StockTransferLineRequestValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.UomId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}

internal sealed class CreateStockTransferCommandValidator : AbstractValidator<CreateStockTransferCommand>
{
    public CreateStockTransferCommandValidator()
    {
        RuleFor(c => c.FromWarehouseId).NotEmpty();
        RuleFor(c => c.ToWarehouseId).NotEmpty();
        RuleFor(c => c.Notes).MaximumLength(1000);
        RuleFor(c => c.Lines).NotEmpty();
        RuleForEach(c => c.Lines).SetValidator(new StockTransferLineRequestValidator());
        RuleFor(c => c.Documents).ValidDocuments();
    }
}

internal sealed class CreateStockTransferCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IDocumentNumberGenerator numberGenerator,
    IAttachmentService attachments) : ICommandHandler<CreateStockTransferCommand, CreateStockTransferResponse>
{
    public const string DocumentPrefix = "TRF";

    public async Task<Result<CreateStockTransferResponse>> Handle(CreateStockTransferCommand command, CancellationToken cancellationToken)
    {
        Result<StockTransferDraft> draft = await PrepareAsync(
            context, branchAccess, command.FromWarehouseId, command.ToWarehouseId, command.TransferDate, command.Notes,
            command.Lines, cancellationToken);

        if (draft.IsFailure)
        {
            return Result.Failure<CreateStockTransferResponse>(draft.Error);
        }

        Result attachable = await attachments.EnsureAttachableAsync(draft.Value.From.BranchId, command.Documents, cancellationToken);
        if (attachable.IsFailure)
        {
            return Result.Failure<CreateStockTransferResponse>(attachable.Error);
        }

        StockTransfer transfer = await draft.Value.CreateAsync(context, numberGenerator, cancellationToken);

        Result moved = await StockPosting.PostTransferAsync(context, new StockBalances(context), transfer, cancellationToken);
        if (moved.IsFailure)
        {
            return Result.Failure<CreateStockTransferResponse>(moved.Error);
        }

        Result documents = await attachments.ApplyDocumentsAsync(
            transfer,
            AttachmentOwner.Of(AttachmentOwnerTypes.StockTransfer, transfer.Id),
            transfer.BranchId,
            command.Documents,
            cancellationToken);
        if (documents.IsFailure)
        {
            return Result.Failure<CreateStockTransferResponse>(documents.Error);
        }

        context.StockTransfers.Add(transfer);

        await context.SaveChangesAsync(cancellationToken);

        return new CreateStockTransferResponse(transfer.Id, transfer.Number, transfer.CycleId);
    }

    /// <summary>
    /// Validates a transfer completely (without consuming a document number) so it can also be combined with a
    /// return into a feed mutation.
    /// </summary>
    internal static async Task<Result<StockTransferDraft>> PrepareAsync(
        IApplicationDbContext context,
        IBranchAccess branchAccess,
        Guid fromWarehouseId,
        Guid toWarehouseId,
        DateOnly transferDate,
        string? notes,
        IReadOnlyList<StockTransferLineRequest> requests,
        CancellationToken cancellationToken)
    {
        List<Warehouse> warehouses = await context.Warehouses.AsNoTracking()
            .Where(w => w.Id == fromWarehouseId || w.Id == toWarehouseId)
            .ToListAsync(cancellationToken);

        Warehouse? from = warehouses.Find(w => w.Id == fromWarehouseId);
        Warehouse? to = warehouses.Find(w => w.Id == toWarehouseId);

        if (from is null || to is null)
        {
            return Result.Failure<StockTransferDraft>(WarehouseErrors.NotFound(from is null ? fromWarehouseId : toWarehouseId));
        }

        Result access = await branchAccess.EnsureAccessAsync(from.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<StockTransferDraft>(access.Error);
        }

        Result<Guid?> cycleId = await InventorySupport.ResolveCoopCycleAsync(context, to, cancellationToken);
        if (cycleId.IsFailure)
        {
            return Result.Failure<StockTransferDraft>(cycleId.Error);
        }

        Dictionary<Guid, Item> items = await InventorySupport.LoadItemsAsync(context, requests.Select(r => r.ItemId), cancellationToken);

        // Only sapronak can be charged to a production cycle; the auto journal has no account for other items.
        Result<List<BaseQuantityLine>> converted = StockPosting.ToBaseQuantities(
            items,
            requests.Select(r => (r.ItemId, r.UomId, r.Quantity)),
            cycleId.Value is null ? null : InventorySupport.SapronakCategories,
            StockTransferErrors.OnlySapronakToCoop);

        if (converted.IsFailure)
        {
            return Result.Failure<StockTransferDraft>(converted.Error);
        }

        List<StockTransferLineInput> lines =
            [.. converted.Value.Select(l => new StockTransferLineInput(l.ItemId, l.UomId, l.Quantity, l.BaseQuantity))];

        Result<StockTransfer> validation = StockTransfer.Create(string.Empty, from, to, cycleId.Value, transferDate, notes, lines);
        if (validation.IsFailure)
        {
            return Result.Failure<StockTransferDraft>(validation.Error);
        }

        return new StockTransferDraft(from, to, cycleId.Value, transferDate, notes, lines);
    }
}

internal sealed record StockTransferDraft(
    Warehouse From,
    Warehouse To,
    Guid? CycleId,
    DateOnly TransferDate,
    string? Notes,
    List<StockTransferLineInput> Lines)
{
    public async Task<StockTransfer> CreateAsync(
        IApplicationDbContext context,
        IDocumentNumberGenerator numberGenerator,
        CancellationToken cancellationToken)
    {
        string number = await numberGenerator.NextForBranchAsync(
            context, CreateStockTransferCommandHandler.DocumentPrefix, From.BranchId, TransferDate, cancellationToken);

        return StockTransfer.Create(number, From, To, CycleId, TransferDate, Notes, Lines).Value;
    }
}
