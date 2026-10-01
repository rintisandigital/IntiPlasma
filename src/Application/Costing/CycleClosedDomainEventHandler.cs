using Application.Abstractions.Data;
using Application.Finance.AutoJournal;
using Application.Inventory;
using Domain.Finance.JournalMappings;
using Domain.Partnership.Cycles;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Costing;

/// <summary>
/// Trues up the estimated HPP when a cycle closes: the final cycle cost minus the cost already recognized on its
/// sales invoices is journaled Dr HPP / Cr ayam dalam proses (sides swapped when the estimate was too high), so the
/// cycle's ayam dalam proses ends at zero.
/// </summary>
internal sealed class CycleClosedDomainEventHandler(IApplicationDbContext context, IAutoJournalService autoJournal)
    : IDomainEventHandler<CycleClosedDomainEvent>
{
    public async Task Handle(CycleClosedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ProductionCycle cycle = await context.ProductionCycles.AsNoTracking()
            .SingleAsync(c => c.Id == domainEvent.CycleId, cancellationToken);

        if (cycle.ClosingCost is null || cycle.ClosingCost.Adjustment == 0m)
        {
            return;
        }

        await InventoryAccounting.PostAsync(autoJournal, new AccountingEntry(
            AccountingEvents.CycleCostAdjustment,
            cycle.Id,
            cycle.BranchId,
            cycle.ClosedDate!.Value,
            $"Penyesuaian HPP tutup siklus {cycle.Number}",
            [new AccountingAmount("CostOfGoodsSold", new Money(cycle.ClosingCost.Adjustment))]),
            cancellationToken);
    }
}
