using Application.Abstractions.Auditing;
using Application.Abstractions.Authentication;
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
/// Approves the current level (optional note). After the last level the document takes effect in the same save.
/// </summary>
public sealed record ApproveRequestCommand(Guid RequestId, string? Note = null) : ICommand;

/// <summary>
/// Rejects the request at its current level; a reason is required (M-33).
/// </summary>
public sealed record RejectRequestCommand(Guid RequestId, string Reason) : ICommand;

/// <summary>
/// The requester withdraws a pending request; the document returns to draft (M-83).
/// </summary>
public sealed record CancelRequestCommand(Guid RequestId) : ICommand;

/// <summary>
/// Replaces an approver of a level still to be decided on one request (M-32), e.g. while they are on leave.
/// </summary>
public sealed record ReassignApproverCommand(Guid RequestId, int LevelNo, Guid FromUserId, Guid ToUserId) : ICommand;

internal sealed class RejectRequestCommandValidator : AbstractValidator<RejectRequestCommand>
{
    public RejectRequestCommandValidator()
    {
        RuleFor(c => c.RequestId).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(500);
    }
}

internal sealed class ApproveRequestCommandValidator : AbstractValidator<ApproveRequestCommand>
{
    public ApproveRequestCommandValidator()
    {
        RuleFor(c => c.RequestId).NotEmpty();
        RuleFor(c => c.Note).MaximumLength(500);
    }
}

/// <summary>
/// Loads a request for a decision and works out in whose place the current user decides: as a listed approver of the
/// current level, else on behalf of a listed approver who delegated to them today (M-98).
/// </summary>
internal sealed class ApprovalDecider(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider,
    ApprovalHandlerRegistry handlers)
{
    public async Task<Result<ApprovalRequest>> LoadAsync(Guid requestId, CancellationToken cancellationToken)
    {
        ApprovalRequest? request = await context.ApprovalRequests
            .Include(r => r.Levels)
            .Include(r => r.Decisions)
            .SingleOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request is null)
        {
            return Result.Failure<ApprovalRequest>(ApprovalErrors.RequestNotFound(requestId));
        }

        Result access = await branchAccess.EnsureAccessAsync(request.BranchId, cancellationToken);

        return access.IsSuccess ? request : Result.Failure<ApprovalRequest>(access.Error);
    }

    public async Task<Result<ApprovalActor>> ActorAsync(ApprovalRequest request, CancellationToken cancellationToken)
    {
        Guid userId = userContext.UserId;

        if (!await ApproverRules.IsEligibleAsync(context, userId, cancellationToken))
        {
            return Result.Failure<ApprovalActor>(ApprovalErrors.NotApprover);
        }

        if (request.IsApproverOfCurrentLevel(userId) || request.PendingLevel is null)
        {
            return new ApprovalActor(userId);
        }

        Guid[] approvers = request.PendingLevel.ApproverUserIds;
        DateOnly today = ApproverRules.Today(dateTimeProvider.UtcNow);

        Guid? delegator = await context.ApprovalDelegations
            .Where(d => d.DelegateUserId == userId && approvers.Contains(d.DelegatorUserId)
                        && d.RevokedAtUtc == null && d.FromDate <= today && today <= d.ToDate)
            .OrderBy(d => d.FromDate)
            .Select(d => (Guid?)d.DelegatorUserId)
            .FirstOrDefaultAsync(cancellationToken);

        return new ApprovalActor(userId, delegator);
    }

    /// <summary>
    /// Applies a completed request to its document; nothing is saved when that fails.
    /// </summary>
    public Task<Result> ApplyOutcomeAsync(ApprovalRequest request, CancellationToken cancellationToken)
    {
        IApprovalHandler handler = handlers.For(request.DocumentType);

        return request.Status switch
        {
            ApprovalStatus.Approved => handler.OnApprovedAsync(request, cancellationToken),
            ApprovalStatus.Rejected => handler.OnRejectedAsync(request, cancellationToken),
            ApprovalStatus.Cancelled => handler.OnCancelledAsync(request, cancellationToken),
            _ => Task.FromResult(Result.Success())
        };
    }
}

internal sealed class ApproveRequestCommandHandler(
    IApplicationDbContext context,
    ApprovalDecider decider,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<ApproveRequestCommand>
{
    public async Task<Result> Handle(ApproveRequestCommand command, CancellationToken cancellationToken)
    {
        Result<ApprovalRequest> request = await decider.LoadAsync(command.RequestId, cancellationToken);
        if (request.IsFailure)
        {
            return request;
        }

        Result<ApprovalActor> actor = await decider.ActorAsync(request.Value, cancellationToken);
        if (actor.IsFailure)
        {
            return actor;
        }

        Result result = request.Value.Approve(actor.Value, command.Note, dateTimeProvider.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        Result effect = await decider.ApplyOutcomeAsync(request.Value, cancellationToken);
        if (effect.IsFailure)
        {
            return effect;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class RejectRequestCommandHandler(
    IApplicationDbContext context,
    ApprovalDecider decider,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<RejectRequestCommand>
{
    public async Task<Result> Handle(RejectRequestCommand command, CancellationToken cancellationToken)
    {
        Result<ApprovalRequest> request = await decider.LoadAsync(command.RequestId, cancellationToken);
        if (request.IsFailure)
        {
            return request;
        }

        Result<ApprovalActor> actor = await decider.ActorAsync(request.Value, cancellationToken);
        if (actor.IsFailure)
        {
            return actor;
        }

        Result result = request.Value.Reject(actor.Value, command.Reason, dateTimeProvider.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        Result effect = await decider.ApplyOutcomeAsync(request.Value, cancellationToken);
        if (effect.IsFailure)
        {
            return effect;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class CancelRequestCommandHandler(
    IApplicationDbContext context,
    ApprovalDecider decider,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<CancelRequestCommand>
{
    public async Task<Result> Handle(CancelRequestCommand command, CancellationToken cancellationToken)
    {
        ApprovalRequest? request = await context.ApprovalRequests
            .SingleOrDefaultAsync(r => r.Id == command.RequestId, cancellationToken);

        // Only the requester's own requests exist for this command.
        if (request is null || request.RequestedBy != userContext.UserId)
        {
            return Result.Failure(ApprovalErrors.RequestNotFound(command.RequestId));
        }

        Result result = request.Cancel(userContext.UserId, dateTimeProvider.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        Result effect = await decider.ApplyOutcomeAsync(request, cancellationToken);
        if (effect.IsFailure)
        {
            return effect;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class ReassignApproverCommandHandler(
    IApplicationDbContext context,
    ApprovalDecider decider,
    IAuditTrail auditTrail) : ICommandHandler<ReassignApproverCommand>
{
    public async Task<Result> Handle(ReassignApproverCommand command, CancellationToken cancellationToken)
    {
        Result<ApprovalRequest> request = await decider.LoadAsync(command.RequestId, cancellationToken);
        if (request.IsFailure)
        {
            return request;
        }

        Result eligible = await ApproverRules.EnsureEligibleAsync(context, [command.ToUserId], cancellationToken);
        if (eligible.IsFailure)
        {
            return eligible;
        }

        Result result = request.Value.ReassignApprover(command.LevelNo, command.FromUserId, command.ToUserId);
        if (result.IsFailure)
        {
            return result;
        }

        auditTrail.Record(new AuditEntry(
            AuditCategory.Access,
            "ReassignApprover",
            $"Approver of level {command.LevelNo} of {request.Value.DocumentType} {request.Value.DocumentCode} replaced")
        {
            EntityType = "ApprovalRequest",
            EntityId = request.Value.Id,
            Details = new { command.LevelNo, command.FromUserId, command.ToUserId }
        });

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
