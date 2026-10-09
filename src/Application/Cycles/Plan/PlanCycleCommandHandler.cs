using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Numbering;
using Domain.MasterData.Coops;
using Domain.MasterData.Farmers;
using Domain.Partnership.Contracts;
using Domain.Partnership.Cycles;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cycles.Plan;

internal sealed class PlanCycleCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IFieldScope fieldScope,
    IDocumentNumberGenerator documentNumberGenerator)
    : ICommandHandler<PlanCycleCommand, PlanCycleResponse>
{
    public const string DocumentPrefix = "SKL";

    public async Task<Result<PlanCycleResponse>> Handle(PlanCycleCommand command, CancellationToken cancellationToken)
    {
        Coop? coop = await context.Coops.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == command.CoopId, cancellationToken);

        if (coop is null || !await fieldScope.CanAccessCoopAsync(coop.Id, cancellationToken))
        {
            return Result.Failure<PlanCycleResponse>(CoopErrors.NotFound(command.CoopId));
        }

        Result access = await branchAccess.EnsureAccessAsync(coop.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<PlanCycleResponse>(access.Error);
        }

        if (await context.ProductionCycles.AnyAsync(
                c => c.CoopId == coop.Id && ProductionCycle.OpenStatuses.Contains(c.Status), cancellationToken))
        {
            return Result.Failure<PlanCycleResponse>(CycleErrors.CoopHasOpenCycle(coop.Id));
        }

        Farmer farmer = await context.Farmers.AsNoTracking().SingleAsync(f => f.Id == coop.FarmerId, cancellationToken);

        PartnershipContract? contract = null;
        if (command.ContractId is Guid contractId)
        {
            contract = await context.Contracts.AsNoTracking()
                .Include(c => c.InputPrices)
                .Include(c => c.LiveBirdPrices)
                .Include(c => c.Incentives)
                .SingleOrDefaultAsync(c => c.Id == contractId, cancellationToken);

            if (contract is null)
            {
                return Result.Failure<PlanCycleResponse>(ContractErrors.NotFound(contractId));
            }
        }

        string branchCode = await context.Branches
            .Where(b => b.Id == coop.BranchId)
            .Select(b => b.Code)
            .SingleAsync(cancellationToken);

        // Validate with a placeholder number first so a rejected plan does not consume a document number.
        Result<ProductionCycle> validation = ProductionCycle.Plan(
            string.Empty, coop, farmer, contract, command.PlannedChickInDate, command.PlannedPopulation, command.Notes);

        if (validation.IsFailure)
        {
            return Result.Failure<PlanCycleResponse>(validation.Error);
        }

        string number = await documentNumberGenerator.NextAsync(
            DocumentPrefix, branchCode, command.PlannedChickInDate, cancellationToken);

        ProductionCycle cycle = ProductionCycle.Plan(
            number, coop, farmer, contract, command.PlannedChickInDate, command.PlannedPopulation, command.Notes).Value;

        context.ProductionCycles.Add(cycle);

        await context.SaveChangesAsync(cancellationToken);

        return new PlanCycleResponse(cycle.Id, cycle.Number);
    }
}
