using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Numbering;
using Application.Documents;
using Application.Inventory.StockTransfers;
using Domain.Common;
using Domain.Documents.Attachments;
using Domain.Inventory.StockReturns;
using Domain.Inventory.StockTransfers;
using Domain.MasterData.Items;
using Domain.MasterData.Warehouses;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Inventory.StockReturns;

/// <summary>
/// Retur sapronak: leftover feed/OVK back from a coop warehouse to a central warehouse. Taken out of the coop's open
/// cycle at the coop warehouse's moving average cost (auto journal persediaan / ayam dalam proses via the outbox).
/// </summary>
public sealed record CreateStockReturnCommand(
    Guid FromWarehouseId,
    Guid ToWarehouseId,
    DateOnly ReturnDate,
    string Reason,
    string? Notes,
    IReadOnlyList<StockTransferLineRequest> Lines,
    IReadOnlyList<Guid>? Documents = null) : ICommand<CreateStockReturnResponse>;

public sealed record CreateStockReturnResponse(Guid Id, string Number, Guid CycleId);

/// <summary>
/// Mutasi pakan antar kandang. Feed never goes directly from coop to coop: it is returned to the central warehouse
/// and transferred from there to the other coop, as two documents posted together. The central warehouse's stock
/// card shows both movements and the transfer leaves at the central warehouse's moving average cost.
/// </summary>
public sealed record CreateFeedMutationCommand(
    Guid FromCoopWarehouseId,
    Guid ViaCentralWarehouseId,
    Guid ToCoopWarehouseId,
    DateOnly MutationDate,
    string Reason,
    IReadOnlyList<StockTransferLineRequest> Lines,
    IReadOnlyList<Guid>? Documents = null) : ICommand<CreateFeedMutationResponse>;

public sealed record CreateFeedMutationResponse(
    Guid ReturnId,
    string ReturnNumber,
    Guid FromCycleId,
    Guid TransferId,
    string TransferNumber,
    Guid ToCycleId);

internal sealed class CreateStockReturnCommandValidator : AbstractValidator<CreateStockReturnCommand>
{
    public CreateStockReturnCommandValidator()
    {
        RuleFor(c => c.FromWarehouseId).NotEmpty();
        RuleFor(c => c.ToWarehouseId).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(300);
        RuleFor(c => c.Notes).MaximumLength(1000);
        RuleFor(c => c.Lines).NotEmpty();
        RuleForEach(c => c.Lines).SetValidator(new StockTransferLineRequestValidator());
        RuleFor(c => c.Documents).ValidDocuments();
    }
}

internal sealed class CreateFeedMutationCommandValidator : AbstractValidator<CreateFeedMutationCommand>
{
    public CreateFeedMutationCommandValidator()
    {
        RuleFor(c => c.FromCoopWarehouseId).NotEmpty();
        RuleFor(c => c.ViaCentralWarehouseId).NotEmpty();
        RuleFor(c => c.ToCoopWarehouseId).NotEmpty().NotEqual(c => c.FromCoopWarehouseId);
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(300);
        RuleFor(c => c.Lines).NotEmpty();
        RuleForEach(c => c.Lines).SetValidator(new StockTransferLineRequestValidator());
        RuleFor(c => c.Documents).ValidDocuments();
    }
}

internal sealed class CreateStockReturnCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IDocumentNumberGenerator numberGenerator,
    IAttachmentService attachments) : ICommandHandler<CreateStockReturnCommand, CreateStockReturnResponse>
{
    public const string DocumentPrefix = "RTR";

    public async Task<Result<CreateStockReturnResponse>> Handle(CreateStockReturnCommand command, CancellationToken cancellationToken)
    {
        Result<StockReturnDraft> draft = await PrepareAsync(
            context, branchAccess, command.FromWarehouseId, command.ToWarehouseId, command.ReturnDate, command.Reason,
            command.Notes, command.Lines, cancellationToken);

        if (draft.IsFailure)
        {
            return Result.Failure<CreateStockReturnResponse>(draft.Error);
        }

        Result attachable = await attachments.EnsureAttachableAsync(draft.Value.From.BranchId, command.Documents, cancellationToken);
        if (attachable.IsFailure)
        {
            return Result.Failure<CreateStockReturnResponse>(attachable.Error);
        }

        StockReturn stockReturn = await draft.Value.CreateAsync(context, numberGenerator, cancellationToken);

        Result moved = await StockPosting.PostReturnAsync(context, new StockBalances(context), stockReturn, cancellationToken);
        if (moved.IsFailure)
        {
            return Result.Failure<CreateStockReturnResponse>(moved.Error);
        }

        Result documents = await attachments.ApplyDocumentsAsync(
            stockReturn,
            AttachmentOwner.Of(AttachmentOwnerTypes.StockReturn, stockReturn.Id),
            stockReturn.BranchId,
            command.Documents,
            cancellationToken);
        if (documents.IsFailure)
        {
            return Result.Failure<CreateStockReturnResponse>(documents.Error);
        }

        context.StockReturns.Add(stockReturn);

        await context.SaveChangesAsync(cancellationToken);

        return new CreateStockReturnResponse(stockReturn.Id, stockReturn.Number, stockReturn.CycleId);
    }

    internal static async Task<Result<StockReturnDraft>> PrepareAsync(
        IApplicationDbContext context,
        IBranchAccess branchAccess,
        Guid fromWarehouseId,
        Guid toWarehouseId,
        DateOnly returnDate,
        string reason,
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
            return Result.Failure<StockReturnDraft>(WarehouseErrors.NotFound(from is null ? fromWarehouseId : toWarehouseId));
        }

        Result access = await branchAccess.EnsureAccessAsync(from.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<StockReturnDraft>(access.Error);
        }

        if (from.Type != WarehouseType.Coop)
        {
            return Result.Failure<StockReturnDraft>(StockReturnErrors.SourceMustBeCoop);
        }

        Result<Guid?> cycleId = await InventorySupport.ResolveCoopCycleAsync(context, from, cancellationToken);
        if (cycleId.IsFailure)
        {
            return Result.Failure<StockReturnDraft>(cycleId.Error);
        }

        Dictionary<Guid, Item> items = await InventorySupport.LoadItemsAsync(context, requests.Select(r => r.ItemId), cancellationToken);

        Result<List<BaseQuantityLine>> converted = StockPosting.ToBaseQuantities(
            items,
            requests.Select(r => (r.ItemId, r.UomId, r.Quantity)),
            InventorySupport.UsableCategories,
            StockReturnErrors.OnlyFeedAndOvk);

        if (converted.IsFailure)
        {
            return Result.Failure<StockReturnDraft>(converted.Error);
        }

        List<StockReturnLineInput> lines =
            [.. converted.Value.Select(l => new StockReturnLineInput(l.ItemId, l.UomId, l.Quantity, l.BaseQuantity))];

        Result<StockReturn> validation = StockReturn.Create(
            string.Empty, from, to, cycleId.Value!.Value, returnDate, reason, notes, lines);

        if (validation.IsFailure)
        {
            return Result.Failure<StockReturnDraft>(validation.Error);
        }

        return new StockReturnDraft(from, to, cycleId.Value.Value, returnDate, reason, notes, lines);
    }
}

internal sealed record StockReturnDraft(
    Warehouse From,
    Warehouse To,
    Guid CycleId,
    DateOnly ReturnDate,
    string Reason,
    string? Notes,
    List<StockReturnLineInput> Lines)
{
    public async Task<StockReturn> CreateAsync(
        IApplicationDbContext context,
        IDocumentNumberGenerator numberGenerator,
        CancellationToken cancellationToken)
    {
        string number = await numberGenerator.NextForBranchAsync(
            context, CreateStockReturnCommandHandler.DocumentPrefix, From.BranchId, ReturnDate, cancellationToken);

        return StockReturn.Create(number, From, To, CycleId, ReturnDate, Reason, Notes, Lines).Value;
    }
}

internal sealed class CreateFeedMutationCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IDocumentNumberGenerator numberGenerator,
    IAttachmentService attachments) : ICommandHandler<CreateFeedMutationCommand, CreateFeedMutationResponse>
{
    public async Task<Result<CreateFeedMutationResponse>> Handle(CreateFeedMutationCommand command, CancellationToken cancellationToken)
    {
        string notes = $"Mutasi pakan: {command.Reason}";

        Result<StockReturnDraft> returnDraft = await CreateStockReturnCommandHandler.PrepareAsync(
            context, branchAccess, command.FromCoopWarehouseId, command.ViaCentralWarehouseId, command.MutationDate,
            command.Reason, notes, command.Lines, cancellationToken);

        if (returnDraft.IsFailure)
        {
            return Result.Failure<CreateFeedMutationResponse>(returnDraft.Error);
        }

        Result<StockTransferDraft> transferDraft = await CreateStockTransferCommandHandler.PrepareAsync(
            context, branchAccess, command.ViaCentralWarehouseId, command.ToCoopWarehouseId, command.MutationDate,
            notes, command.Lines, cancellationToken);

        if (transferDraft.IsFailure)
        {
            return Result.Failure<CreateFeedMutationResponse>(transferDraft.Error);
        }

        if (transferDraft.Value.CycleId is null)
        {
            return Result.Failure<CreateFeedMutationResponse>(StockReturnErrors.MutationTargetMustBeCoop);
        }

        Result attachable = await attachments.EnsureAttachableAsync(returnDraft.Value.From.BranchId, command.Documents, cancellationToken);
        if (attachable.IsFailure)
        {
            return Result.Failure<CreateFeedMutationResponse>(attachable.Error);
        }

        StockReturn stockReturn = await returnDraft.Value.CreateAsync(context, numberGenerator, cancellationToken);
        StockTransfer transfer = await transferDraft.Value.CreateAsync(context, numberGenerator, cancellationToken);

        // One set of balances: the transfer draws on the central stock the return has just added.
        var balances = new StockBalances(context);

        Result returned = await StockPosting.PostReturnAsync(context, balances, stockReturn, cancellationToken);
        if (returned.IsFailure)
        {
            return Result.Failure<CreateFeedMutationResponse>(returned.Error);
        }

        Result transferred = await StockPosting.PostTransferAsync(context, balances, transfer, cancellationToken);
        if (transferred.IsFailure)
        {
            return Result.Failure<CreateFeedMutationResponse>(transferred.Error);
        }

        // Both documents of the mutation carry the same attachments.
        foreach ((IHasDocuments document, AttachmentOwner owner, Guid branchId) in new (IHasDocuments, AttachmentOwner, Guid)[]
                 {
                     (stockReturn, AttachmentOwner.Of(AttachmentOwnerTypes.StockReturn, stockReturn.Id), stockReturn.BranchId),
                     (transfer, AttachmentOwner.Of(AttachmentOwnerTypes.StockTransfer, transfer.Id), transfer.BranchId)
                 })
        {
            Result documents = await attachments.ApplyDocumentsAsync(document, owner, branchId, command.Documents, cancellationToken);
            if (documents.IsFailure)
            {
                return Result.Failure<CreateFeedMutationResponse>(documents.Error);
            }
        }

        context.StockReturns.Add(stockReturn);
        context.StockTransfers.Add(transfer);

        await context.SaveChangesAsync(cancellationToken);

        return new CreateFeedMutationResponse(
            stockReturn.Id, stockReturn.Number, stockReturn.CycleId, transfer.Id, transfer.Number, transfer.CycleId!.Value);
    }
}
