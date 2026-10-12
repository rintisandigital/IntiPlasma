using Application.Abstractions.Auditing;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Approvals;
using Domain.Auditing;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Approvals;

/// <summary>
/// Replaces a departing approver everywhere at once (M-99): in every flow (for new requests) and in the levels still
/// to be decided of pending requests. With <paramref name="BranchId"/> only that branch's flows and requests change
/// (the default flows are left alone).
/// </summary>
public sealed record ReplaceApproverCommand(Guid FromUserId, Guid ToUserId, Guid? BranchId = null)
    : ICommand<ReplaceApproverResult>;

public sealed record ReplaceApproverResult(int Flows, int Requests);

/// <summary>
/// Where a user is an approver: for the Replace Approver preview and the warning when deactivating a user (M-99).
/// </summary>
public sealed record GetApproverUsageQuery(Guid UserId, Guid? BranchId = null) : IQuery<ApproverUsageResponse>;

public sealed record ApproverUsageResponse(
    IReadOnlyList<ApproverUsageFlow> Flows,
    IReadOnlyList<ApproverUsageRequest> PendingRequests)
{
    public bool IsUsed => Flows.Count > 0 || PendingRequests.Count > 0;
}

public sealed record ApproverUsageFlow(Guid FlowId, string DocumentType, string? BranchName, string Name, IReadOnlyList<int> Levels);

public sealed record ApproverUsageRequest(
    Guid RequestId,
    string DocumentType,
    string DocumentCode,
    string DocumentTitle,
    string BranchName,
    int CurrentLevel,
    IReadOnlyList<int> Levels);

internal sealed class ReplaceApproverCommandValidator : AbstractValidator<ReplaceApproverCommand>
{
    public ReplaceApproverCommandValidator()
    {
        RuleFor(c => c.FromUserId).NotEmpty();
        RuleFor(c => c.ToUserId).NotEmpty().NotEqual(c => c.FromUserId);
    }
}

internal sealed class ReplaceApproverCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IAuditTrail auditTrail) : ICommandHandler<ReplaceApproverCommand, ReplaceApproverResult>
{
    public async Task<Result<ReplaceApproverResult>> Handle(ReplaceApproverCommand command, CancellationToken cancellationToken)
    {
        Result eligible = await ApproverRules.EnsureEligibleAsync(context, [command.ToUserId], cancellationToken);
        if (eligible.IsFailure)
        {
            return Result.Failure<ReplaceApproverResult>(eligible.Error);
        }

        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        List<ApprovalFlow> flows = await context.ApprovalFlows
            .Include(f => f.Levels)
            .Where(f => command.BranchId == null || f.BranchId == command.BranchId)
            .ToListAsync(cancellationToken);

        List<ApprovalRequest> requests = await context.ApprovalRequests
            .Include(r => r.Levels)
            .Where(r => r.Status == ApprovalStatus.Pending)
            .Where(r => command.BranchId == null || r.BranchId == command.BranchId)
            .ToListAsync(cancellationToken);

        int changedFlows = flows
            .Where(f => f.BranchId is not { } b || scope.CanAccess(b))
            .Count(f => f.ReplaceApprover(command.FromUserId, command.ToUserId));

        var changedRequests = requests
            .Where(r => scope.CanAccess(r.BranchId))
            .Where(r => r.ReplaceApprover(command.FromUserId, command.ToUserId))
            .ToList();

        if (changedFlows == 0 && changedRequests.Count == 0)
        {
            return new ReplaceApproverResult(0, 0);
        }

        auditTrail.Record(new AuditEntry(
            AuditCategory.Access,
            "ReplaceApprover",
            $"Approver replaced in {changedFlows} flow(s) and {changedRequests.Count} pending request(s)")
        {
            EntityType = "ApprovalFlow",
            EntityId = command.ToUserId,
            Details = new
            {
                command.FromUserId,
                command.ToUserId,
                command.BranchId,
                Requests = changedRequests.Select(r => $"{r.DocumentType} {r.DocumentCode}").ToArray()
            }
        });

        await context.SaveChangesAsync(cancellationToken);

        return new ReplaceApproverResult(changedFlows, changedRequests.Count);
    }
}

internal sealed class GetApproverUsageQueryHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : IQueryHandler<GetApproverUsageQuery, ApproverUsageResponse>
{
    public async Task<Result<ApproverUsageResponse>> Handle(GetApproverUsageQuery query, CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        List<ApprovalFlow> flows = await context.ApprovalFlows
            .AsNoTracking()
            .Include(f => f.Levels)
            .Where(f => query.BranchId == null || f.BranchId == query.BranchId)
            .ToListAsync(cancellationToken);

        List<ApprovalRequest> requests = await context.ApprovalRequests
            .AsNoTracking()
            .Include(r => r.Levels)
            .Where(r => r.Status == ApprovalStatus.Pending)
            .Where(r => query.BranchId == null || r.BranchId == query.BranchId)
            .ToListAsync(cancellationToken);

        Dictionary<Guid, string> branches = await context.Branches
            .AsNoTracking()
            .Select(b => new { b.Id, b.Name })
            .ToDictionaryAsync(b => b.Id, b => b.Name, cancellationToken);

        List<ApproverUsageFlow> usedFlows = [.. flows
            .Where(f => f.BranchId is not { } b || scope.CanAccess(b))
            .Where(f => f.HasApprover(query.UserId))
            .Select(f => new ApproverUsageFlow(
                f.Id,
                f.DocumentType.ToString(),
                f.BranchId is { } b ? branches.GetValueOrDefault(b) : null,
                f.Name,
                [.. f.Levels.Where(l => l.ApproverUserIds.Contains(query.UserId)).Select(l => l.LevelNo)]))];

        List<ApproverUsageRequest> usedRequests = [.. requests
            .Where(r => scope.CanAccess(r.BranchId))
            .Select(r => new
            {
                Request = r,
                Levels = r.Levels
                    .Where(l => l.LevelNo >= r.CurrentLevel && l.ApproverUserIds.Contains(query.UserId))
                    .Select(l => l.LevelNo)
                    .ToList()
            })
            .Where(x => x.Levels.Count > 0)
            .Select(x => new ApproverUsageRequest(
                x.Request.Id,
                x.Request.DocumentType.ToString(),
                x.Request.DocumentCode,
                x.Request.DocumentTitle,
                branches.GetValueOrDefault(x.Request.BranchId) ?? string.Empty,
                x.Request.CurrentLevel,
                x.Levels))];

        return new ApproverUsageResponse(usedFlows, usedRequests);
    }
}
