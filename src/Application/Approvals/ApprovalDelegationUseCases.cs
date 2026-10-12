using Application.Abstractions.Auditing;
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Approvals;
using Domain.Auditing;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Approvals;

/// <summary>
/// Delegates the signed-in user's approvals for a period (M-98). The WebApp administration page sets
/// <paramref name="DelegatorUserId"/> to delegate for someone else; the API never does.
/// </summary>
public sealed record CreateApprovalDelegationCommand(
    Guid DelegateUserId,
    DateOnly FromDate,
    DateOnly ToDate,
    string? Reason,
    Guid? DelegatorUserId = null) : ICommand<Guid>;

/// <param name="AnyUser">Set by the WebApp administration page: revoke a delegation of another user.</param>
public sealed record RevokeApprovalDelegationCommand(Guid DelegationId, bool AnyUser = false) : ICommand;

/// <summary>
/// Delegations given or received by the signed-in user, or (<paramref name="AllUsers"/>, WebApp administration)
/// by everyone. Ended and revoked ones only with <paramref name="IncludeEnded"/>.
/// </summary>
public sealed record GetApprovalDelegationsQuery(bool AllUsers = false, bool IncludeEnded = false)
    : IQuery<IReadOnlyList<ApprovalDelegationResponse>>;

/// <param name="Status">Active, Scheduled, Ended or Revoked.</param>
public sealed record ApprovalDelegationResponse(
    Guid Id,
    Guid DelegatorUserId,
    string DelegatorName,
    Guid DelegateUserId,
    string DelegateName,
    DateOnly FromDate,
    DateOnly ToDate,
    string? Reason,
    string Status,
    DateTime CreatedAtUtc);

internal sealed class CreateApprovalDelegationCommandValidator : AbstractValidator<CreateApprovalDelegationCommand>
{
    public CreateApprovalDelegationCommandValidator()
    {
        RuleFor(c => c.DelegateUserId).NotEmpty();
        RuleFor(c => c.Reason).MaximumLength(300);
    }
}

internal sealed class CreateApprovalDelegationCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider,
    IAuditTrail auditTrail) : ICommandHandler<CreateApprovalDelegationCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateApprovalDelegationCommand command, CancellationToken cancellationToken)
    {
        Guid delegatorId = command.DelegatorUserId ?? userContext.UserId;
        DateOnly today = ApproverRules.Today(dateTimeProvider.UtcNow);

        if (!await ApproverRules.IsEligibleAsync(context, delegatorId, cancellationToken))
        {
            return Result.Failure<Guid>(ApprovalErrors.InvalidApprover(delegatorId));
        }

        if (!await ApproverRules.IsEligibleAsync(context, command.DelegateUserId, cancellationToken))
        {
            return Result.Failure<Guid>(ApprovalErrors.InvalidDelegate);
        }

        Result<ApprovalDelegation> delegation = ApprovalDelegation.Create(
            delegatorId, command.DelegateUserId, command.FromDate, command.ToDate, command.Reason, today);
        if (delegation.IsFailure)
        {
            return Result.Failure<Guid>(delegation.Error);
        }

        bool overlaps = await context.ApprovalDelegations.AnyAsync(
            d => d.DelegatorUserId == delegatorId && d.RevokedAtUtc == null
                 && d.FromDate <= command.ToDate && command.FromDate <= d.ToDate,
            cancellationToken);
        if (overlaps)
        {
            return Result.Failure<Guid>(ApprovalErrors.DelegationOverlaps);
        }

        context.ApprovalDelegations.Add(delegation.Value);

        auditTrail.Record(new AuditEntry(
            AuditCategory.Access,
            "DelegateApprovals",
            $"Approvals delegated from {command.FromDate:yyyy-MM-dd} to {command.ToDate:yyyy-MM-dd}")
        {
            EntityType = "ApprovalDelegation",
            EntityId = delegation.Value.Id,
            Details = new { DelegatorUserId = delegatorId, command.DelegateUserId, command.FromDate, command.ToDate, command.Reason }
        });

        await context.SaveChangesAsync(cancellationToken);

        return delegation.Value.Id;
    }
}

internal sealed class RevokeApprovalDelegationCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider,
    IAuditTrail auditTrail) : ICommandHandler<RevokeApprovalDelegationCommand>
{
    public async Task<Result> Handle(RevokeApprovalDelegationCommand command, CancellationToken cancellationToken)
    {
        ApprovalDelegation? delegation = await context.ApprovalDelegations
            .SingleOrDefaultAsync(d => d.Id == command.DelegationId, cancellationToken);

        if (delegation is null || !command.AnyUser && delegation.DelegatorUserId != userContext.UserId)
        {
            return Result.Failure(ApprovalErrors.DelegationNotFound(command.DelegationId));
        }

        DateTime utcNow = dateTimeProvider.UtcNow;

        Result result = delegation.Revoke(userContext.UserId, utcNow, ApproverRules.Today(utcNow));
        if (result.IsFailure)
        {
            return result;
        }

        auditTrail.Record(new AuditEntry(AuditCategory.Access, "RevokeApprovalDelegation", "Approval delegation revoked")
        {
            EntityType = "ApprovalDelegation",
            EntityId = delegation.Id
        });

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class GetApprovalDelegationsQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : IQueryHandler<GetApprovalDelegationsQuery, IReadOnlyList<ApprovalDelegationResponse>>
{
    public async Task<Result<IReadOnlyList<ApprovalDelegationResponse>>> Handle(
        GetApprovalDelegationsQuery query,
        CancellationToken cancellationToken)
    {
        Guid userId = userContext.UserId;
        DateOnly today = ApproverRules.Today(dateTimeProvider.UtcNow);

        List<ApprovalDelegation> delegations = await context.ApprovalDelegations
            .AsNoTracking()
            .Where(d => query.AllUsers || d.DelegatorUserId == userId || d.DelegateUserId == userId)
            .Where(d => query.IncludeEnded || d.RevokedAtUtc == null && d.ToDate >= today)
            .OrderByDescending(d => d.FromDate)
            .ToListAsync(cancellationToken);

        Guid[] userIds = [.. delegations.SelectMany(d => new[] { d.DelegatorUserId, d.DelegateUserId }).Distinct()];

        var names = await context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        string NameOf(Guid id) => names.TryGetValue(id, out var u) ? UserNames.Of(u.FirstName, u.LastName, u.Email) : id.ToString();

        return delegations
            .Select(d => new ApprovalDelegationResponse(
                d.Id,
                d.DelegatorUserId,
                NameOf(d.DelegatorUserId),
                d.DelegateUserId,
                NameOf(d.DelegateUserId),
                d.FromDate,
                d.ToDate,
                d.Reason,
                StatusOf(d, today),
                d.CreatedAtUtc))
            .ToList();
    }

    private static string StatusOf(ApprovalDelegation delegation, DateOnly today)
    {
        if (delegation.RevokedAtUtc is not null)
        {
            return "Revoked";
        }

        if (delegation.ToDate < today)
        {
            return "Ended";
        }

        return delegation.FromDate > today ? "Scheduled" : "Active";
    }
}
