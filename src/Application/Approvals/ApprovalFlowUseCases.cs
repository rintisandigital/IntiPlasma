using Application.Abstractions.Auditing;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Access;
using Domain.Approvals;
using Domain.Auditing;
using Domain.MasterData.Branches;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Approvals;

/// <param name="BranchId">Null for the default flow of every branch (M-30).</param>
public sealed record CreateApprovalFlowCommand(
    ApprovalDocumentType DocumentType,
    Guid? BranchId,
    string Name,
    IReadOnlyList<ApprovalLevelDefinition> Levels) : ICommand<Guid>, IAuditedCommand
{
    public string AuditEntityType => "ApprovalFlow";
}

public sealed record UpdateApprovalFlowCommand(
    Guid FlowId,
    string Name,
    bool IsActive,
    IReadOnlyList<ApprovalLevelDefinition> Levels) : ICommand, IAuditedCommand
{
    public string AuditEntityType => "ApprovalFlow";
}

/// <summary>
/// Sets up the flows of a (new) branch in one go (M-97): the same levels for each chosen document type, updating the
/// branch's active flow of a type or creating it. With <paramref name="GrantBranchAccess"/>, approvers whose branch
/// access profile does not cover the branch get it added to their profile.
/// </summary>
public sealed record SetUpBranchApprovalFlowsCommand(
    Guid BranchId,
    IReadOnlyList<ApprovalDocumentType> DocumentTypes,
    IReadOnlyList<ApprovalLevelDefinition> Levels,
    bool GrantBranchAccess) : ICommand<int>, IAuditedCommand
{
    public string AuditEntityType => "ApprovalFlow";
}

public sealed record GetApprovalFlowsQuery(ApprovalDocumentType? DocumentType = null, Guid? BranchId = null)
    : IQuery<IReadOnlyList<ApprovalFlowResponse>>;

public sealed record GetApprovalFlowByIdQuery(Guid FlowId) : IQuery<ApprovalFlowResponse>;

/// <summary>
/// Users that can be chosen as approver (M-86); <see cref="ApproverCandidateResponse.CoversBranch"/> tells whether
/// their branch access covers <paramref name="BranchId"/>.
/// </summary>
public sealed record GetApproverCandidatesQuery(Guid? BranchId = null) : IQuery<IReadOnlyList<ApproverCandidateResponse>>;

public sealed record ApprovalFlowResponse(
    Guid Id,
    string DocumentType,
    Guid? BranchId,
    string? BranchCode,
    string? BranchName,
    string Name,
    bool IsActive,
    IReadOnlyList<ApprovalFlowLevelResponse> Levels)
{
    /// <summary>
    /// A level without any approver who can decide (M-99).
    /// </summary>
    public bool HasStuckLevel => Levels.Any(l => l.IsStuck);
}

public sealed record ApprovalFlowLevelResponse(int LevelNo, string Name, IReadOnlyList<ApproverResponse> Approvers)
{
    public bool IsStuck => !Approvers.Any(a => a.CanDecide);
}

public sealed record ApproverCandidateResponse(Guid Id, string Name, string Email, bool CoversBranch);

internal sealed class ApprovalLevelsValidator : AbstractValidator<IReadOnlyList<ApprovalLevelDefinition>>
{
    public ApprovalLevelsValidator()
    {
        RuleFor(l => l.Count).InclusiveBetween(1, ApprovalFlow.MaxLevels);
        RuleForEach(l => l).ChildRules(level =>
        {
            level.RuleFor(l => l.Name).NotEmpty().MaximumLength(100);
            level.RuleFor(l => l.ApproverUserIds).NotEmpty();
        });
    }
}

internal sealed class CreateApprovalFlowCommandValidator : AbstractValidator<CreateApprovalFlowCommand>
{
    public CreateApprovalFlowCommandValidator()
    {
        RuleFor(c => c.DocumentType).IsInEnum();
        RuleFor(c => c.Name).MaximumLength(150);
        RuleFor(c => c.Levels).SetValidator(new ApprovalLevelsValidator());
    }
}

internal sealed class UpdateApprovalFlowCommandValidator : AbstractValidator<UpdateApprovalFlowCommand>
{
    public UpdateApprovalFlowCommandValidator()
    {
        RuleFor(c => c.Name).MaximumLength(150);
        RuleFor(c => c.Levels).SetValidator(new ApprovalLevelsValidator());
    }
}

internal sealed class SetUpBranchApprovalFlowsCommandValidator : AbstractValidator<SetUpBranchApprovalFlowsCommand>
{
    public SetUpBranchApprovalFlowsCommandValidator()
    {
        RuleFor(c => c.BranchId).NotEmpty();
        RuleFor(c => c.DocumentTypes).NotEmpty();
        RuleForEach(c => c.DocumentTypes).IsInEnum();
        RuleFor(c => c.Levels).SetValidator(new ApprovalLevelsValidator());
    }
}

internal static class ApprovalFlowRules
{
    public static async Task<Result> EnsureBranchAsync(
        IApplicationDbContext context,
        IBranchAccess branchAccess,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        if (branchId is not { } id)
        {
            return Result.Success();
        }

        if (!await context.Branches.AnyAsync(b => b.Id == id, cancellationToken))
        {
            return Result.Failure(BranchErrors.NotFound(id));
        }

        return await branchAccess.EnsureAccessAsync(id, cancellationToken);
    }

    public static Task<bool> HasOtherActiveFlowAsync(
        IApplicationDbContext context,
        ApprovalDocumentType documentType,
        Guid? branchId,
        Guid exceptId,
        CancellationToken cancellationToken) =>
        context.ApprovalFlows.AnyAsync(
            f => f.DocumentType == documentType && f.BranchId == branchId && f.IsActive && f.Id != exceptId,
            cancellationToken);
}

internal sealed class CreateApprovalFlowCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<CreateApprovalFlowCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateApprovalFlowCommand command, CancellationToken cancellationToken)
    {
        Result branch = await ApprovalFlowRules.EnsureBranchAsync(context, branchAccess, command.BranchId, cancellationToken);
        if (branch.IsFailure)
        {
            return Result.Failure<Guid>(branch.Error);
        }

        Result approvers = await ApproverRules.EnsureEligibleAsync(
            context, command.Levels.SelectMany(l => l.ApproverUserIds), cancellationToken);
        if (approvers.IsFailure)
        {
            return Result.Failure<Guid>(approvers.Error);
        }

        if (await ApprovalFlowRules.HasOtherActiveFlowAsync(
                context, command.DocumentType, command.BranchId, Guid.Empty, cancellationToken))
        {
            return Result.Failure<Guid>(ApprovalErrors.FlowAlreadyExists);
        }

        Result<ApprovalFlow> flow = ApprovalFlow.Create(command.DocumentType, command.BranchId, command.Name, command.Levels);
        if (flow.IsFailure)
        {
            return Result.Failure<Guid>(flow.Error);
        }

        context.ApprovalFlows.Add(flow.Value);

        await context.SaveChangesAsync(cancellationToken);

        return flow.Value.Id;
    }
}

internal sealed class UpdateApprovalFlowCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<UpdateApprovalFlowCommand>
{
    public async Task<Result> Handle(UpdateApprovalFlowCommand command, CancellationToken cancellationToken)
    {
        ApprovalFlow? flow = await context.ApprovalFlows
            .Include(f => f.Levels)
            .SingleOrDefaultAsync(f => f.Id == command.FlowId, cancellationToken);

        if (flow is null)
        {
            return Result.Failure(ApprovalErrors.FlowNotFound(command.FlowId));
        }

        Result branch = await ApprovalFlowRules.EnsureBranchAsync(context, branchAccess, flow.BranchId, cancellationToken);
        if (branch.IsFailure)
        {
            return branch;
        }

        Result approvers = await ApproverRules.EnsureEligibleAsync(
            context, command.Levels.SelectMany(l => l.ApproverUserIds), cancellationToken);
        if (approvers.IsFailure)
        {
            return approvers;
        }

        if (command.IsActive && await ApprovalFlowRules.HasOtherActiveFlowAsync(
                context, flow.DocumentType, flow.BranchId, flow.Id, cancellationToken))
        {
            return Result.Failure(ApprovalErrors.FlowAlreadyExists);
        }

        Result result = flow.Update(command.Name, command.IsActive, command.Levels);
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class SetUpBranchApprovalFlowsCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IAuditTrail auditTrail) : ICommandHandler<SetUpBranchApprovalFlowsCommand, int>
{
    public async Task<Result<int>> Handle(SetUpBranchApprovalFlowsCommand command, CancellationToken cancellationToken)
    {
        Result branch = await ApprovalFlowRules.EnsureBranchAsync(context, branchAccess, command.BranchId, cancellationToken);
        if (branch.IsFailure)
        {
            return Result.Failure<int>(branch.Error);
        }

        Guid[] approverIds = [.. command.Levels.SelectMany(l => l.ApproverUserIds).Distinct()];

        Result approvers = await ApproverRules.EnsureEligibleAsync(context, approverIds, cancellationToken);
        if (approvers.IsFailure)
        {
            return Result.Failure<int>(approvers.Error);
        }

        List<ApprovalFlow> existing = await context.ApprovalFlows
            .Include(f => f.Levels)
            .Where(f => f.BranchId == command.BranchId && f.IsActive)
            .ToListAsync(cancellationToken);

        string branchName = await context.Branches
            .Where(b => b.Id == command.BranchId)
            .Select(b => b.Name)
            .SingleAsync(cancellationToken);

        foreach (ApprovalDocumentType documentType in command.DocumentTypes.Distinct())
        {
            ApprovalFlow? flow = existing.Find(f => f.DocumentType == documentType);
            Result result;

            if (flow is null)
            {
                Result<ApprovalFlow> created = ApprovalFlow.Create(
                    documentType, command.BranchId, $"{documentType} — {branchName}", command.Levels);
                result = created;
                if (created.IsSuccess)
                {
                    context.ApprovalFlows.Add(created.Value);
                }
            }
            else
            {
                result = flow.Update(flow.Name, isActive: true, command.Levels);
            }

            if (result.IsFailure)
            {
                return Result.Failure<int>(result.Error);
            }
        }

        if (command.GrantBranchAccess)
        {
            await GrantBranchAccessAsync(command.BranchId, approverIds, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);

        return command.DocumentTypes.Distinct().Count();
    }

    private async Task GrantBranchAccessAsync(Guid branchId, Guid[] approverIds, CancellationToken cancellationToken)
    {
        List<Guid> profileIds = await context.Users
            .Where(u => approverIds.Contains(u.Id) && u.BranchAccessProfileId != null)
            .Select(u => u.BranchAccessProfileId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        List<BranchAccessProfile> profiles = await context.BranchAccessProfiles
            .Include(p => p.Branches)
            .Where(p => profileIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        foreach (BranchAccessProfile profile in profiles.Where(p => !p.Covers(branchId) && !p.IsSystem))
        {
            profile.Update(profile.Name, profile.Description, profile.AllBranches, [.. profile.Branches.Select(b => b.BranchId), branchId]);

            auditTrail.Record(new AuditEntry(
                AuditCategory.Access,
                "GrantBranchAccess",
                $"Branch added to branch access profile '{profile.Name}' for its approvers")
            {
                EntityType = "BranchAccessProfile",
                EntityId = profile.Id,
                Details = new { BranchId = branchId }
            });
        }
    }
}

internal sealed class GetApprovalFlowsQueryHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : IQueryHandler<GetApprovalFlowsQuery, IReadOnlyList<ApprovalFlowResponse>>
{
    public async Task<Result<IReadOnlyList<ApprovalFlowResponse>>> Handle(
        GetApprovalFlowsQuery query,
        CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        List<ApprovalFlow> flows = await context.ApprovalFlows
            .AsNoTracking()
            .Include(f => f.Levels)
            .Where(f => query.DocumentType == null || f.DocumentType == query.DocumentType)
            .Where(f => query.BranchId == null || f.BranchId == query.BranchId)
            .ToListAsync(cancellationToken);

        flows = flows.FindAll(f => f.BranchId is not { } branchId || scope.CanAccess(branchId));

        return Result.Success(await ApprovalFlowReader.ToResponsesAsync(context, flows, cancellationToken));
    }
}

internal sealed class GetApprovalFlowByIdQueryHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : IQueryHandler<GetApprovalFlowByIdQuery, ApprovalFlowResponse>
{
    public async Task<Result<ApprovalFlowResponse>> Handle(GetApprovalFlowByIdQuery query, CancellationToken cancellationToken)
    {
        ApprovalFlow? flow = await context.ApprovalFlows
            .AsNoTracking()
            .Include(f => f.Levels)
            .SingleOrDefaultAsync(f => f.Id == query.FlowId, cancellationToken);

        if (flow is null)
        {
            return Result.Failure<ApprovalFlowResponse>(ApprovalErrors.FlowNotFound(query.FlowId));
        }

        if (flow.BranchId is { } branchId)
        {
            Result access = await branchAccess.EnsureAccessAsync(branchId, cancellationToken);
            if (access.IsFailure)
            {
                return Result.Failure<ApprovalFlowResponse>(access.Error);
            }
        }

        return (await ApprovalFlowReader.ToResponsesAsync(context, [flow], cancellationToken))[0];
    }
}

internal sealed class GetApproverCandidatesQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetApproverCandidatesQuery, IReadOnlyList<ApproverCandidateResponse>>
{
    public async Task<Result<IReadOnlyList<ApproverCandidateResponse>>> Handle(
        GetApproverCandidatesQuery query,
        CancellationToken cancellationToken)
    {
        var users = await context.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .Where(u => u.Roles.Any(ur => context.Roles.Any(r =>
                r.Id == ur.RoleId && r.Permissions.Any(p => p.Permission == ApproverRules.DecidePermission))))
            .Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                Covers = query.BranchId == null || context.BranchAccessProfiles.Any(p =>
                    p.Id == u.BranchAccessProfileId
                    && (p.AllBranches || p.Branches.Any(b => b.BranchId == query.BranchId)))
            })
            .ToListAsync(cancellationToken);

        return users
            .Select(u => new ApproverCandidateResponse(u.Id, UserNames.Of(u.FirstName, u.LastName, u.Email), u.Email, u.Covers))
            .OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

internal static class ApprovalFlowReader
{
    public static async Task<IReadOnlyList<ApprovalFlowResponse>> ToResponsesAsync(
        IApplicationDbContext context,
        List<ApprovalFlow> flows,
        CancellationToken cancellationToken)
    {
        Guid[] userIds = [.. flows.SelectMany(f => f.Levels).SelectMany(l => l.ApproverUserIds).Distinct()];
        Guid[] branchIds = [.. flows.Where(f => f.BranchId != null).Select(f => f.BranchId!.Value).Distinct()];

        var users = await context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                u.IsActive,
                CanDecide = u.IsActive && u.Roles.Any(ur => context.Roles.Any(r =>
                    r.Id == ur.RoleId && r.Permissions.Any(p => p.Permission == ApproverRules.DecidePermission)))
            })
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var branches = await context.Branches
            .AsNoTracking()
            .Where(b => branchIds.Contains(b.Id))
            .Select(b => new { b.Id, b.Code, b.Name })
            .ToDictionaryAsync(b => b.Id, cancellationToken);

        return
        [
            .. flows
                .OrderBy(f => f.DocumentType)
                .ThenBy(f => f.BranchId == null ? string.Empty : branches.GetValueOrDefault(f.BranchId.Value)?.Code)
                .Select(f => new ApprovalFlowResponse(
                    f.Id,
                    f.DocumentType.ToString(),
                    f.BranchId,
                    f.BranchId is { } b ? branches.GetValueOrDefault(b)?.Code : null,
                    f.BranchId is { } b2 ? branches.GetValueOrDefault(b2)?.Name : null,
                    f.Name,
                    f.IsActive,
                    [.. f.Levels.Select(l => new ApprovalFlowLevelResponse(
                        l.LevelNo,
                        l.Name,
                        [.. l.ApproverUserIds.Select(id => users.TryGetValue(id, out var u)
                            ? new ApproverResponse(id, UserNames.Of(u.FirstName, u.LastName, u.Email), u.IsActive, u.CanDecide)
                            : new ApproverResponse(id, id.ToString(), false, false))]))]))
        ];
    }
}
