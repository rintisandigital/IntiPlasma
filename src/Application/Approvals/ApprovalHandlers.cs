using Application.Abstractions.Data;
using Domain.Approvals;
using Domain.MasterData.Coops;
using Domain.MasterData.Farmers;
using Domain.Partnership.Contracts;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Approvals;

/// <summary>
/// Applies the outcome of a request to its document (PLAN-MOBILE §5.3). Runs in the same unit of work as the decision:
/// when it fails, nothing is saved and the request stays pending at its last level (§5.2).
/// </summary>
internal interface IApprovalHandler
{
    ApprovalDocumentType DocumentType { get; }

    Task<Result> OnApprovedAsync(ApprovalRequest request, CancellationToken cancellationToken);

    Task<Result> OnRejectedAsync(ApprovalRequest request, CancellationToken cancellationToken);

    Task<Result> OnCancelledAsync(ApprovalRequest request, CancellationToken cancellationToken);
}

internal sealed class ApprovalHandlerRegistry(IEnumerable<IApprovalHandler> handlers)
{
    public IApprovalHandler For(ApprovalDocumentType documentType) =>
        handlers.Single(h => h.DocumentType == documentType);
}

internal sealed class FarmerApprovalHandler(IApplicationDbContext context) : IApprovalHandler
{
    public ApprovalDocumentType DocumentType => ApprovalDocumentType.Farmer;

    public Task<Result> OnApprovedAsync(ApprovalRequest request, CancellationToken cancellationToken) =>
        WithFarmer(request, f => f.Approve(), cancellationToken);

    public Task<Result> OnRejectedAsync(ApprovalRequest request, CancellationToken cancellationToken) =>
        WithFarmer(request, f => f.Reject(), cancellationToken);

    public Task<Result> OnCancelledAsync(ApprovalRequest request, CancellationToken cancellationToken) =>
        WithFarmer(request, f => f.ReturnToDraft(), cancellationToken);

    private async Task<Result> WithFarmer(ApprovalRequest request, Func<Farmer, Result> apply, CancellationToken cancellationToken)
    {
        Farmer? farmer = await context.Farmers.SingleOrDefaultAsync(f => f.Id == request.DocumentId, cancellationToken);

        return farmer is null ? Result.Failure(FarmerErrors.NotFound(request.DocumentId)) : apply(farmer);
    }
}

/// <summary>
/// NIK and bank account change of an approved farmer (M-84): applied when approved, discarded otherwise.
/// </summary>
internal sealed class FarmerChangeApprovalHandler(IApplicationDbContext context) : IApprovalHandler
{
    public ApprovalDocumentType DocumentType => ApprovalDocumentType.FarmerChange;

    public Task<Result> OnApprovedAsync(ApprovalRequest request, CancellationToken cancellationToken) =>
        WithFarmer(request, f => f.ApplyPendingChange(), cancellationToken);

    public Task<Result> OnRejectedAsync(ApprovalRequest request, CancellationToken cancellationToken) =>
        WithFarmer(request, f => f.DiscardPendingChange(), cancellationToken);

    public Task<Result> OnCancelledAsync(ApprovalRequest request, CancellationToken cancellationToken) =>
        WithFarmer(request, f => f.DiscardPendingChange(), cancellationToken);

    private async Task<Result> WithFarmer(ApprovalRequest request, Action<Farmer> apply, CancellationToken cancellationToken)
    {
        Farmer? farmer = await context.Farmers.SingleOrDefaultAsync(f => f.Id == request.DocumentId, cancellationToken);
        if (farmer is null)
        {
            return Result.Failure(FarmerErrors.NotFound(request.DocumentId));
        }

        apply(farmer);

        return Result.Success();
    }
}

/// <summary>
/// A coop is approved only once its farmer is (M-87); approving raises the event that creates its warehouse.
/// </summary>
internal sealed class CoopApprovalHandler(IApplicationDbContext context) : IApprovalHandler
{
    public ApprovalDocumentType DocumentType => ApprovalDocumentType.Coop;

    public async Task<Result> OnApprovedAsync(ApprovalRequest request, CancellationToken cancellationToken)
    {
        Coop? coop = await context.Coops.SingleOrDefaultAsync(c => c.Id == request.DocumentId, cancellationToken);
        if (coop is null)
        {
            return Result.Failure(CoopErrors.NotFound(request.DocumentId));
        }

        Farmer? farmer = await context.Farmers.AsNoTracking().SingleOrDefaultAsync(f => f.Id == coop.FarmerId, cancellationToken);

        return farmer is null ? Result.Failure(FarmerErrors.NotFound(coop.FarmerId)) : coop.Approve(farmer);
    }

    public Task<Result> OnRejectedAsync(ApprovalRequest request, CancellationToken cancellationToken) =>
        WithCoop(request, c => c.Reject(), cancellationToken);

    public Task<Result> OnCancelledAsync(ApprovalRequest request, CancellationToken cancellationToken) =>
        WithCoop(request, c => c.ReturnToDraft(), cancellationToken);

    private async Task<Result> WithCoop(ApprovalRequest request, Func<Coop, Result> apply, CancellationToken cancellationToken)
    {
        Coop? coop = await context.Coops.SingleOrDefaultAsync(c => c.Id == request.DocumentId, cancellationToken);

        return coop is null ? Result.Failure(CoopErrors.NotFound(request.DocumentId)) : apply(coop);
    }
}

/// <summary>
/// Contract activation (M-85): approved → Active, rejected → editable again, cancelled → draft.
/// </summary>
internal sealed class ContractApprovalHandler(IApplicationDbContext context) : IApprovalHandler
{
    public ApprovalDocumentType DocumentType => ApprovalDocumentType.Contract;

    public Task<Result> OnApprovedAsync(ApprovalRequest request, CancellationToken cancellationToken) =>
        WithContract(request, c => c.Approve(), cancellationToken);

    public Task<Result> OnRejectedAsync(ApprovalRequest request, CancellationToken cancellationToken) =>
        WithContract(request, c => c.Reject(), cancellationToken);

    public Task<Result> OnCancelledAsync(ApprovalRequest request, CancellationToken cancellationToken) =>
        WithContract(request, c => c.ReturnToDraft(), cancellationToken);

    private async Task<Result> WithContract(
        ApprovalRequest request,
        Func<PartnershipContract, Result> apply,
        CancellationToken cancellationToken)
    {
        PartnershipContract? contract = await context.Contracts
            .Include(c => c.InputPrices)
            .Include(c => c.LiveBirdPrices)
            .SingleOrDefaultAsync(c => c.Id == request.DocumentId, cancellationToken);

        return contract is null ? Result.Failure(ContractErrors.NotFound(request.DocumentId)) : apply(contract);
    }
}
