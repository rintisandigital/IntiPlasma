using Application.Abstractions.Data;
using Domain.MasterData.Coops;
using Domain.MasterData.Warehouses;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Coops.Create;

/// <summary>
/// Every approved coop gets its own coop warehouse (gudang kandang) that receives sapronak transfers for its cycles
/// (PLAN-MOBILE M-87: created on approval, no longer on creation). Idempotent: the outbox may deliver an event more
/// than once. <see cref="CoopCreatedDomainEvent"/> is only still handled for messages raised before M6.
/// </summary>
internal sealed class CoopWarehouseDomainEventHandler(IApplicationDbContext context)
    : IDomainEventHandler<CoopApprovedDomainEvent>, IDomainEventHandler<CoopCreatedDomainEvent>
{
    private const string CodePrefix = "GK-";

    public Task Handle(CoopApprovedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        CreateWarehouseAsync(domainEvent.CoopId, cancellationToken);

    public Task Handle(CoopCreatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        CreateWarehouseAsync(domainEvent.CoopId, cancellationToken);

    private async Task CreateWarehouseAsync(Guid coopId, CancellationToken cancellationToken)
    {
        if (await context.Warehouses.AnyAsync(w => w.CoopId == coopId, cancellationToken))
        {
            return;
        }

        Coop coop = await context.Coops.AsNoTracking()
            .SingleAsync(c => c.Id == coopId, cancellationToken);

        if (coop.RegistrationStatus != Domain.MasterData.RegistrationStatus.Approved)
        {
            return;
        }

        string code = CodePrefix + coop.Code;
        if (await context.Warehouses.AnyAsync(w => w.Code == code, cancellationToken))
        {
            code = $"{code}-{coop.Id.ToString("N")[^6..]}";
        }

        context.Warehouses.Add(Warehouse.CreateForCoop(code, $"Gudang {coop.Name}", coop.BranchId, coop.Id, coop.Address));

        await context.SaveChangesAsync(cancellationToken);
    }
}
