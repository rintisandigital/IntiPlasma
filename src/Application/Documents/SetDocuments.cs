using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Costing.PlasmaSettlements;
using Domain.Documents.Attachments;
using Domain.Finance.CashBank;
using Domain.Finance.Journals;
using Domain.Finance.Payables;
using Domain.Finance.Receivables;
using Domain.Inventory.GoodsReceipts;
using Domain.Inventory.StockReturns;
using Domain.Inventory.StockTransfers;
using Domain.MasterData.Coops;
using Domain.MasterData.Customers;
using Domain.MasterData.Farmers;
using Domain.MasterData.Vendors;
using Domain.Partnership.Contracts;
using Domain.Partnership.Cycles;
using Domain.Procurement.PurchaseOrders;
using Domain.Production.DailyRecordings;
using Domain.Sales.DeliveryOrders;
using Domain.Sales.SalesOrders;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Documents;

/// <summary>
/// <c>PUT …/{id}/documents</c>: replaces the attachments of a master or transaction in any status (also approved,
/// posted, paid or closed documents, because only the attachments change), except cancelled or voided documents.
/// </summary>
/// <param name="OwnerType">One of <see cref="AttachmentOwnerTypes"/>.</param>
/// <param name="ParentId">The cycle of a harvest (route check).</param>
/// <param name="RevisionNumber">The revision of a daily recording (<see cref="AttachmentOwnerTypes.DailyRecordingRevision"/>).</param>
public sealed record SetDocumentsCommand(
    string OwnerType,
    Guid OwnerId,
    IReadOnlyList<Guid>? Documents,
    Guid? ParentId = null,
    int? RevisionNumber = null) : ICommand;

internal sealed class SetDocumentsCommandValidator : AbstractValidator<SetDocumentsCommand>
{
    public SetDocumentsCommandValidator()
    {
        RuleFor(c => c.OwnerType).NotEmpty();
        RuleFor(c => c.OwnerId).NotEmpty();
        RuleFor(c => c.Documents).NotNull().ValidDocuments();
        RuleFor(c => c.RevisionNumber).NotNull().GreaterThan(0)
            .When(c => c.OwnerType == AttachmentOwnerTypes.DailyRecordingRevision);
    }
}

internal sealed class SetDocumentsCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IFieldScope fieldScope,
    IAttachmentService attachments) : ICommandHandler<SetDocumentsCommand>
{
    public async Task<Result> Handle(SetDocumentsCommand command, CancellationToken cancellationToken)
    {
        Result<DocumentTarget> target = await LoadAsync(command, cancellationToken);
        if (target.IsFailure)
        {
            return target;
        }

        if (!await AttachmentOwnerScope.CanAccessAsync(context, fieldScope, command.OwnerType, command.OwnerId, cancellationToken))
        {
            return Result.Failure(OutOfFieldScope(command));
        }

        if (target.Value.BranchId is { } branchId)
        {
            Result access = await branchAccess.EnsureAccessAsync(branchId, cancellationToken);
            if (access.IsFailure)
            {
                return access;
            }
        }

        Result applied = await attachments.ApplyDocumentsAsync(
            target.Value.SetDocuments,
            target.Value.CurrentDocuments,
            target.Value.Owner,
            target.Value.BranchId,
            command.Documents ?? [],
            cancellationToken);

        if (applied.IsFailure)
        {
            return applied;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<Result<DocumentTarget>> LoadAsync(SetDocumentsCommand command, CancellationToken cancellationToken)
    {
        Guid id = command.OwnerId;
        var owner = AttachmentOwner.Of(command.OwnerType, id);

        switch (command.OwnerType)
        {
            case AttachmentOwnerTypes.Farmer:
                Farmer? farmer = await context.Farmers.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
                return Of(farmer, owner, farmer?.BranchId, FarmerErrors.NotFound(id));

            case AttachmentOwnerTypes.Coop:
                Coop? coop = await context.Coops.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
                return Of(coop, owner, coop?.BranchId, CoopErrors.NotFound(id));

            case AttachmentOwnerTypes.Vendor:
                Vendor? vendor = await context.Vendors.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
                return Of(vendor, owner, null, VendorErrors.NotFound(id));

            case AttachmentOwnerTypes.Customer:
                Customer? customer = await context.Customers.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
                return Of(customer, owner, null, CustomerErrors.NotFound(id));

            case AttachmentOwnerTypes.Contract:
                PartnershipContract? contract = await context.Contracts.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
                return Of(contract, owner, contract?.BranchId, ContractErrors.NotFound(id));

            case AttachmentOwnerTypes.Cycle:
                ProductionCycle? cycle = await context.ProductionCycles.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
                return Of(cycle, owner, cycle?.BranchId, CycleErrors.NotFound(id));

            case AttachmentOwnerTypes.Harvest:
                return await LoadHarvestAsync(command, owner, cancellationToken);

            case AttachmentOwnerTypes.DailyRecording:
                DailyRecording? recording = await context.DailyRecordings.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
                return Of(recording, owner, recording?.BranchId, DailyRecordingErrors.NotFound(id));

            case AttachmentOwnerTypes.DailyRecordingRevision:
                return await LoadRevisionAsync(id, command.RevisionNumber ?? 0, cancellationToken);

            case AttachmentOwnerTypes.GoodsReceipt:
                GoodsReceipt? receipt = await context.GoodsReceipts.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
                return Of(receipt, owner, receipt?.BranchId, GoodsReceiptErrors.NotFound(id));

            case AttachmentOwnerTypes.StockReturn:
                StockReturn? stockReturn = await context.StockReturns.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
                return Of(stockReturn, owner, stockReturn?.BranchId, StockReturnErrors.NotFound(id));

            case AttachmentOwnerTypes.StockTransfer:
                StockTransfer? transfer = await context.StockTransfers.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
                return Of(transfer, owner, transfer?.BranchId, StockTransferErrors.NotFound(id));

            case AttachmentOwnerTypes.PurchaseOrder:
                PurchaseOrder? purchaseOrder = await context.PurchaseOrders.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
                return Of(purchaseOrder, owner, purchaseOrder?.BranchId, PurchaseOrderErrors.NotFound(id));

            case AttachmentOwnerTypes.SalesOrder:
                SalesOrder? salesOrder = await context.SalesOrders.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
                return Of(salesOrder, owner, salesOrder?.BranchId, SalesOrderErrors.NotFound(id));

            case AttachmentOwnerTypes.DeliveryOrder:
                DeliveryOrder? delivery = await context.DeliveryOrders.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
                return Of(delivery, owner, delivery?.BranchId, DeliveryOrderErrors.NotFound(id));

            case AttachmentOwnerTypes.VendorInvoice:
                VendorInvoice? invoice = await context.VendorInvoices.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
                return Of(invoice, owner, invoice?.BranchId, VendorInvoiceErrors.NotFound(id));

            case AttachmentOwnerTypes.PaymentVoucher:
                PaymentVoucher? voucher = await context.PaymentVouchers.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
                return Of(voucher, owner, voucher?.BranchId, PaymentVoucherErrors.NotFound(id));

            case AttachmentOwnerTypes.CashTransaction:
                CashTransaction? cash = await context.CashTransactions.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
                return Of(cash, owner, cash?.BranchId, CashTransactionErrors.NotFound(id));

            case AttachmentOwnerTypes.CustomerReceipt:
                CustomerReceipt? customerReceipt = await context.CustomerReceipts.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
                return Of(customerReceipt, owner, customerReceipt?.BranchId, CustomerReceiptErrors.NotFound(id));

            case AttachmentOwnerTypes.Journal:
                JournalEntry? journal = await context.JournalEntries.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
                return Of(journal, owner, journal?.BranchId, JournalErrors.NotFound(id));

            case AttachmentOwnerTypes.PlasmaSettlement:
                PlasmaSettlement? settlement = await context.PlasmaSettlements.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
                return Of(settlement, owner, settlement?.BranchId, PlasmaSettlementErrors.NotFound(id));

            default:
                throw new ArgumentOutOfRangeException(nameof(command), command.OwnerType, "Unknown attachment owner type");
        }
    }

    private async Task<Result<DocumentTarget>> LoadHarvestAsync(
        SetDocumentsCommand command,
        AttachmentOwner owner,
        CancellationToken cancellationToken)
    {
        Guid harvestId = command.OwnerId;

        ProductionCycle? cycle = await context.ProductionCycles
            .Include(c => c.Harvests)
            .SingleOrDefaultAsync(c => c.Harvests.Any(h => h.Id == harvestId), cancellationToken);

        if (cycle is null || command.ParentId is { } cycleId && cycle.Id != cycleId)
        {
            return Result.Failure<DocumentTarget>(CycleErrors.HarvestNotFound(harvestId));
        }

        return new DocumentTarget(
            owner,
            cycle.BranchId,
            ids => cycle.SetHarvestDocuments(harvestId, ids),
            () => cycle.Harvests.Single(h => h.Id == harvestId).Documents);
    }

    private async Task<Result<DocumentTarget>> LoadRevisionAsync(
        Guid dailyRecordingId,
        int revisionNumber,
        CancellationToken cancellationToken)
    {
        DailyRecording? recording = await context.DailyRecordings
            .Include(r => r.Revisions)
            .SingleOrDefaultAsync(r => r.Id == dailyRecordingId, cancellationToken);

        if (recording is null)
        {
            return Result.Failure<DocumentTarget>(DailyRecordingErrors.NotFound(dailyRecordingId));
        }

        if (recording.Revisions.All(r => r.RevisionNumber != revisionNumber))
        {
            return Result.Failure<DocumentTarget>(DailyRecordingErrors.RevisionNotFound(revisionNumber));
        }

        return new DocumentTarget(
            AttachmentOwner.OfRevision(dailyRecordingId, revisionNumber),
            recording.BranchId,
            ids => recording.SetRevisionDocuments(revisionNumber, ids),
            () => recording.Revisions.Single(r => r.RevisionNumber == revisionNumber).Documents);
    }

    /// <summary>
    /// Owners outside the PPL scope are reported as not found (only owners of <see cref="AttachmentOwnerScope"/>).
    /// </summary>
    private static Error OutOfFieldScope(SetDocumentsCommand command) => command.OwnerType switch
    {
        AttachmentOwnerTypes.Farmer => FarmerErrors.NotFound(command.OwnerId),
        AttachmentOwnerTypes.Coop => CoopErrors.NotFound(command.OwnerId),
        AttachmentOwnerTypes.Cycle => CycleErrors.NotFound(command.OwnerId),
        AttachmentOwnerTypes.Harvest => CycleErrors.HarvestNotFound(command.OwnerId),
        _ => DailyRecordingErrors.NotFound(command.OwnerId)
    };

    private static Result<DocumentTarget> Of(IHasDocuments? entity, AttachmentOwner owner, Guid? branchId, Error notFound) =>
        entity is null
            ? Result.Failure<DocumentTarget>(notFound)
            : new DocumentTarget(owner, branchId, entity.SetDocuments, () => entity.Documents);

    private sealed record DocumentTarget(
        AttachmentOwner Owner,
        Guid? BranchId,
        Func<IEnumerable<Guid>?, Result> SetDocuments,
        Func<Guid[]> CurrentDocuments);
}
