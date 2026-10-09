using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Domain.Documents.Attachments;
using Microsoft.EntityFrameworkCore;

namespace Application.Documents;

/// <summary>
/// PPL scope (<see cref="IFieldScope"/>) for attachment owners: farmers, coops and what belongs to a coop's cycles.
/// Other owners only follow the branch scope.
/// </summary>
internal static class AttachmentOwnerScope
{
    public static async Task<bool> CanAccessAsync(
        IApplicationDbContext context,
        IFieldScope fieldScope,
        string ownerType,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        switch (ownerType)
        {
            case AttachmentOwnerTypes.Farmer:
                return await fieldScope.CanAccessFarmerAsync(ownerId, cancellationToken);

            case AttachmentOwnerTypes.Coop:
                return await fieldScope.CanAccessCoopAsync(ownerId, cancellationToken);

            case AttachmentOwnerTypes.Cycle:
                return await fieldScope.CanAccessCycleAsync(ownerId, cancellationToken);

            case AttachmentOwnerTypes.Harvest:
                Guid harvestCycleId = await context.ProductionCycles
                    .Where(c => c.Harvests.Any(h => h.Id == ownerId))
                    .Select(c => c.Id)
                    .SingleOrDefaultAsync(cancellationToken);
                return harvestCycleId != Guid.Empty && await fieldScope.CanAccessCycleAsync(harvestCycleId, cancellationToken);

            case AttachmentOwnerTypes.DailyRecording:
            case AttachmentOwnerTypes.DailyRecordingRevision:
                Guid recordingCycleId = await context.DailyRecordings
                    .Where(r => r.Id == ownerId)
                    .Select(r => r.CycleId)
                    .SingleOrDefaultAsync(cancellationToken);
                return recordingCycleId != Guid.Empty && await fieldScope.CanAccessCycleAsync(recordingCycleId, cancellationToken);

            default:
                return true;
        }
    }
}
