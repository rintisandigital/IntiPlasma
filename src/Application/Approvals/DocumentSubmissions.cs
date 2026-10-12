using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Approvals;
using Domain.MasterData.Coops;
using Domain.MasterData.Farmers;
using Domain.Partnership.Contracts;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Approvals;

/// <summary>
/// Submits a draft or rejected farmer again (M-83); a new request starts at level 1. Returns the request id.
/// </summary>
public sealed record SubmitFarmerCommand(Guid FarmerId) : ICommand<Guid>;

/// <summary>
/// Submits a draft or rejected coop again; its farmer must be approved or waiting for approval (M-87).
/// </summary>
public sealed record SubmitCoopCommand(Guid CoopId) : ICommand<Guid>;

/// <summary>
/// The approval timeline of a farmer (new farmer and NIK/bank account changes).
/// </summary>
public sealed record GetFarmerApprovalsQuery(Guid FarmerId) : IQuery<IReadOnlyList<ApprovalDetailResponse>>;

public sealed record GetCoopApprovalsQuery(Guid CoopId) : IQuery<IReadOnlyList<ApprovalDetailResponse>>;

public sealed record GetContractApprovalsQuery(Guid ContractId) : IQuery<IReadOnlyList<ApprovalDetailResponse>>;

internal sealed class SubmitFarmerCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IFieldScope fieldScope,
    IApprovalService approvals) : ICommandHandler<SubmitFarmerCommand, Guid>
{
    public async Task<Result<Guid>> Handle(SubmitFarmerCommand command, CancellationToken cancellationToken)
    {
        Farmer? farmer = await context.Farmers.SingleOrDefaultAsync(f => f.Id == command.FarmerId, cancellationToken);

        if (farmer is null || !await fieldScope.CanAccessFarmerAsync(farmer.Id, cancellationToken))
        {
            return Result.Failure<Guid>(FarmerErrors.NotFound(command.FarmerId));
        }

        Result access = await branchAccess.EnsureAccessAsync(farmer.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<Guid>(access.Error);
        }

        Result submitted = farmer.Submit();
        if (submitted.IsFailure)
        {
            return Result.Failure<Guid>(submitted.Error);
        }

        Result<ApprovalRequest> request = await approvals.SubmitAsync(ApprovalSubmissions.ForFarmer(farmer), cancellationToken);
        if (request.IsFailure)
        {
            return Result.Failure<Guid>(request.Error);
        }

        await context.SaveChangesAsync(cancellationToken);

        return request.Value.Id;
    }
}

internal sealed class SubmitCoopCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IFieldScope fieldScope,
    IApprovalService approvals) : ICommandHandler<SubmitCoopCommand, Guid>
{
    public async Task<Result<Guid>> Handle(SubmitCoopCommand command, CancellationToken cancellationToken)
    {
        Coop? coop = await context.Coops.SingleOrDefaultAsync(c => c.Id == command.CoopId, cancellationToken);

        if (coop is null || !await fieldScope.CanAccessCoopAsync(coop.Id, cancellationToken))
        {
            return Result.Failure<Guid>(CoopErrors.NotFound(command.CoopId));
        }

        Result access = await branchAccess.EnsureAccessAsync(coop.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<Guid>(access.Error);
        }

        Farmer farmer = await context.Farmers.AsNoTracking().SingleAsync(f => f.Id == coop.FarmerId, cancellationToken);

        Result submitted = coop.Submit(farmer);
        if (submitted.IsFailure)
        {
            return Result.Failure<Guid>(submitted.Error);
        }

        Result<ApprovalRequest> request = await approvals.SubmitAsync(ApprovalSubmissions.ForCoop(coop, farmer), cancellationToken);
        if (request.IsFailure)
        {
            return Result.Failure<Guid>(request.Error);
        }

        await context.SaveChangesAsync(cancellationToken);

        return request.Value.Id;
    }
}

internal sealed class GetFarmerApprovalsQueryHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IFieldScope fieldScope,
    IQueryHandler<GetDocumentApprovalsQuery, IReadOnlyList<ApprovalDetailResponse>> approvals)
    : IQueryHandler<GetFarmerApprovalsQuery, IReadOnlyList<ApprovalDetailResponse>>
{
    public async Task<Result<IReadOnlyList<ApprovalDetailResponse>>> Handle(
        GetFarmerApprovalsQuery query,
        CancellationToken cancellationToken)
    {
        Guid? branchId = await context.Farmers
            .Where(f => f.Id == query.FarmerId)
            .Select(f => (Guid?)f.BranchId)
            .SingleOrDefaultAsync(cancellationToken);

        if (branchId is null
            || !(await branchAccess.GetScopeAsync(cancellationToken)).CanAccess(branchId.Value)
            || !await fieldScope.CanAccessFarmerAsync(query.FarmerId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<ApprovalDetailResponse>>(FarmerErrors.NotFound(query.FarmerId));
        }

        return await approvals.Handle(
            new GetDocumentApprovalsQuery(query.FarmerId, [ApprovalDocumentType.Farmer, ApprovalDocumentType.FarmerChange]),
            cancellationToken);
    }
}

internal sealed class GetCoopApprovalsQueryHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IFieldScope fieldScope,
    IQueryHandler<GetDocumentApprovalsQuery, IReadOnlyList<ApprovalDetailResponse>> approvals)
    : IQueryHandler<GetCoopApprovalsQuery, IReadOnlyList<ApprovalDetailResponse>>
{
    public async Task<Result<IReadOnlyList<ApprovalDetailResponse>>> Handle(
        GetCoopApprovalsQuery query,
        CancellationToken cancellationToken)
    {
        Guid? branchId = await context.Coops
            .Where(c => c.Id == query.CoopId)
            .Select(c => (Guid?)c.BranchId)
            .SingleOrDefaultAsync(cancellationToken);

        if (branchId is null
            || !(await branchAccess.GetScopeAsync(cancellationToken)).CanAccess(branchId.Value)
            || !await fieldScope.CanAccessCoopAsync(query.CoopId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<ApprovalDetailResponse>>(CoopErrors.NotFound(query.CoopId));
        }

        return await approvals.Handle(new GetDocumentApprovalsQuery(query.CoopId, [ApprovalDocumentType.Coop]), cancellationToken);
    }
}

internal sealed class GetContractApprovalsQueryHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IQueryHandler<GetDocumentApprovalsQuery, IReadOnlyList<ApprovalDetailResponse>> approvals)
    : IQueryHandler<GetContractApprovalsQuery, IReadOnlyList<ApprovalDetailResponse>>
{
    public async Task<Result<IReadOnlyList<ApprovalDetailResponse>>> Handle(
        GetContractApprovalsQuery query,
        CancellationToken cancellationToken)
    {
        Guid? branchId = await context.Contracts
            .Where(c => c.Id == query.ContractId)
            .Select(c => (Guid?)c.BranchId)
            .SingleOrDefaultAsync(cancellationToken);

        if (branchId is null || !(await branchAccess.GetScopeAsync(cancellationToken)).CanAccess(branchId.Value))
        {
            return Result.Failure<IReadOnlyList<ApprovalDetailResponse>>(ContractErrors.NotFound(query.ContractId));
        }

        return await approvals.Handle(
            new GetDocumentApprovalsQuery(query.ContractId, [ApprovalDocumentType.Contract]), cancellationToken);
    }
}
