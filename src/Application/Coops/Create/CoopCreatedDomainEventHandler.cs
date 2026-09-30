using Application.Abstractions.Data;
using Domain.MasterData.Coops;
using Domain.MasterData.Warehouses;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Coops.Create;

/// <summary>
/// Every coop gets its own coop warehouse (gudang kandang) that receives sapronak transfers for its cycles.
/// Idempotent: the outbox may deliver the event more than once.
/// </summary>
internal sealed class CoopCreatedDomainEventHandler(IApplicationDbContext context)
    : IDomainEventHandler<CoopCreatedDomainEvent>
{
    private const string CodePrefix = "GK-";

    public async Task Handle(CoopCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await context.Warehouses.AnyAsync(w => w.CoopId == domainEvent.CoopId, cancellationToken))
        {
            return;
        }

        Coop coop = await context.Coops.AsNoTracking()
            .SingleAsync(c => c.Id == domainEvent.CoopId, cancellationToken);

        string code = CodePrefix + coop.Code;
        if (await context.Warehouses.AnyAsync(w => w.Code == code, cancellationToken))
        {
            code = $"{code}-{coop.Id.ToString("N")[^6..]}";
        }

        context.Warehouses.Add(Warehouse.CreateForCoop(code, $"Gudang {coop.Name}", coop.BranchId, coop.Id, coop.Address));

        await context.SaveChangesAsync(cancellationToken);
    }
}
