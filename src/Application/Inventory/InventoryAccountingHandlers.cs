using Application.Abstractions.Data;
using Application.Finance.AutoJournal;
using Domain.Finance.JournalMappings;
using Domain.Inventory.GoodsReceipts;
using Domain.Inventory.StockReturns;
using Domain.Inventory.StockTransfers;
using Domain.MasterData.Items;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Inventory;

/// <summary>
/// Journals goods receipts: Dr persediaan (per sapronak type) / Cr hutang belum ditagih. DOC delivered straight to a
/// coop is immediately part of the cycle, so it also gets the transfer-to-cycle journal (Dr ayam dalam proses /
/// Cr persediaan DOC). Runs from the outbox; a failure (e.g. closed period, missing mapping) is retried and then
/// dead-lettered with the error.
/// </summary>
internal sealed class GoodsReceiptPostedDomainEventHandler(IApplicationDbContext context, IAutoJournalService autoJournal)
    : IDomainEventHandler<GoodsReceiptPostedDomainEvent>
{
    public async Task Handle(GoodsReceiptPostedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        GoodsReceipt receipt = await context.GoodsReceipts.AsNoTracking()
            .Include(r => r.Lines)
            .SingleAsync(r => r.Id == domainEvent.GoodsReceiptId, cancellationToken);

        string vendor = await context.Vendors
            .Where(v => v.Id == receipt.VendorId)
            .Select(v => v.Name)
            .SingleAsync(cancellationToken);

        IReadOnlyList<(ItemCategory Category, Money Value)> values = await InventoryAccounting.ValuesByCategoryAsync(
            context, receipt.Lines.Select(l => (l.ItemId, l.Value)), cancellationToken);

        await InventoryAccounting.PostAsync(autoJournal, new AccountingEntry(
            AccountingEvents.PurchaseReceipt,
            receipt.Id,
            receipt.BranchId,
            receipt.ReceiptDate,
            $"Penerimaan {receipt.Number} dari {vendor}",
            [.. values.Select(v => new AccountingAmount(InventoryAccounting.ReceivedComponent(v.Category), v.Value))]),
            cancellationToken);

        if (receipt.CycleId is not null)
        {
            await InventoryAccounting.PostAsync(autoJournal, new AccountingEntry(
                AccountingEvents.StockTransferToCycle,
                receipt.Id,
                receipt.BranchId,
                receipt.ReceiptDate,
                $"DOC langsung ke kandang {receipt.Number}",
                [.. values.Select(v => new AccountingAmount(InventoryAccounting.IssuedComponent(v.Category), v.Value))]),
                cancellationToken);
        }
    }
}

/// <summary>
/// Journals sapronak sent to a coop: Dr ayam dalam proses / Cr persediaan. Transfers between central warehouses
/// of the same branch do not change any account and are not journaled.
/// </summary>
internal sealed class StockTransferPostedDomainEventHandler(IApplicationDbContext context, IAutoJournalService autoJournal)
    : IDomainEventHandler<StockTransferPostedDomainEvent>
{
    public async Task Handle(StockTransferPostedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        StockTransfer transfer = await context.StockTransfers.AsNoTracking()
            .Include(t => t.Lines)
            .SingleAsync(t => t.Id == domainEvent.StockTransferId, cancellationToken);

        if (!transfer.IsToCycle)
        {
            return;
        }

        IReadOnlyList<(ItemCategory Category, Money Value)> values = await InventoryAccounting.ValuesByCategoryAsync(
            context, transfer.Lines.Select(l => (l.ItemId, l.Value)), cancellationToken);

        await InventoryAccounting.PostAsync(autoJournal, new AccountingEntry(
            AccountingEvents.StockTransferToCycle,
            transfer.Id,
            transfer.BranchId,
            transfer.TransferDate,
            $"Kirim sapronak {transfer.Number}",
            [.. values.Select(v => new AccountingAmount(InventoryAccounting.IssuedComponent(v.Category), v.Value))]),
            cancellationToken);
    }
}

/// <summary>
/// Journals leftover sapronak returned from a coop: Dr persediaan / Cr ayam dalam proses.
/// </summary>
internal sealed class StockReturnPostedDomainEventHandler(IApplicationDbContext context, IAutoJournalService autoJournal)
    : IDomainEventHandler<StockReturnPostedDomainEvent>
{
    public async Task Handle(StockReturnPostedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        StockReturn stockReturn = await context.StockReturns.AsNoTracking()
            .Include(r => r.Lines)
            .SingleAsync(r => r.Id == domainEvent.StockReturnId, cancellationToken);

        IReadOnlyList<(ItemCategory Category, Money Value)> values = await InventoryAccounting.ValuesByCategoryAsync(
            context, stockReturn.Lines.Select(l => (l.ItemId, l.Value)), cancellationToken);

        await InventoryAccounting.PostAsync(autoJournal, new AccountingEntry(
            AccountingEvents.StockReturnFromCycle,
            stockReturn.Id,
            stockReturn.BranchId,
            stockReturn.ReturnDate,
            $"Retur sapronak {stockReturn.Number}: {stockReturn.Reason}",
            [.. values.Select(v => new AccountingAmount(InventoryAccounting.ReturnedComponent(v.Category), v.Value))]),
            cancellationToken);
    }
}

internal static class InventoryAccounting
{
    public static string ReturnedComponent(ItemCategory category) => category switch
    {
        ItemCategory.Feed => "FeedReturned",
        ItemCategory.Ovk => "OvkReturned",
        _ => throw new InvalidOperationException($"Items of category {category} cannot be returned.")
    };

    public static string ReceivedComponent(ItemCategory category) => category switch
    {
        ItemCategory.Doc => "DocReceived",
        ItemCategory.Feed => "FeedReceived",
        ItemCategory.Ovk => "OvkReceived",
        _ => throw new InvalidOperationException($"Items of category {category} are not journaled as sapronak.")
    };

    public static string IssuedComponent(ItemCategory category) => category switch
    {
        ItemCategory.Doc => "DocIssued",
        ItemCategory.Feed => "FeedIssued",
        ItemCategory.Ovk => "OvkIssued",
        _ => throw new InvalidOperationException($"Items of category {category} are not journaled as sapronak.")
    };

    public static async Task<IReadOnlyList<(ItemCategory Category, Money Value)>> ValuesByCategoryAsync(
        IApplicationDbContext context,
        IEnumerable<(Guid ItemId, Money Value)> lines,
        CancellationToken cancellationToken)
    {
        var list = lines.ToList();
        var itemIds = list.Select(l => l.ItemId).Distinct().ToList();

        Dictionary<Guid, ItemCategory> categories = await context.Items
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Category, cancellationToken);

        return
        [
            .. list
                .GroupBy(l => categories[l.ItemId])
                .Select(g => (g.Key, g.Aggregate(Money.Zero, (total, l) => total + l.Value)))
        ];
    }

    /// <summary>
    /// Throws on failure so the outbox retries the event and finally records the error.
    /// </summary>
    public static async Task PostAsync(IAutoJournalService autoJournal, AccountingEntry entry, CancellationToken cancellationToken)
    {
        Result<Guid> result = await autoJournal.PostAsync(entry, cancellationToken);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Auto journal for {entry.EventType} '{entry.SourceId}' failed: {result.Error.Code} - {result.Error.Description}");
        }
    }
}
