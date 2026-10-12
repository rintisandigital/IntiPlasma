using SharedKernel;

namespace Domain.Approvals;

/// <summary>
/// Approval flow of one document type (PLAN-MOBILE §5.1, M-29, M-30, M-96): 1 to 5 named levels, each with one or
/// more approvers of whom any one decides the level. A flow without a branch is the default for every branch; a
/// branch flow overrides it. Requests copy the levels when they are submitted, so later changes only affect new
/// requests (M-32). The caller keeps one active flow per document type and branch.
/// </summary>
public sealed class ApprovalFlow : AggregateRoot
{
    public const int MaxLevels = 5;

    private readonly List<ApprovalFlowLevel> _levels = [];

    private ApprovalFlow(Guid id, ApprovalDocumentType documentType, Guid? branchId)
        : base(id)
    {
        DocumentType = documentType;
        BranchId = branchId;
    }

    private ApprovalFlow()
    {
    }

    public ApprovalDocumentType DocumentType { get; private set; }

    /// <summary>
    /// Null for the default flow of all branches.
    /// </summary>
    public Guid? BranchId { get; private set; }

    public string Name { get; private set; }
    public bool IsActive { get; private set; }

    public IReadOnlyList<ApprovalFlowLevel> Levels => [.. _levels.OrderBy(l => l.LevelNo)];

    public static Result<ApprovalFlow> Create(
        ApprovalDocumentType documentType,
        Guid? branchId,
        string name,
        IReadOnlyList<ApprovalLevelDefinition> levels)
    {
        var flow = new ApprovalFlow(Guid.CreateVersion7(), documentType, branchId);

        Result result = flow.Update(name, isActive: true, levels);

        return result.IsSuccess ? flow : Result.Failure<ApprovalFlow>(result.Error);
    }

    public Result Update(string name, bool isActive, IReadOnlyList<ApprovalLevelDefinition> levels)
    {
        Result valid = Validate(levels);
        if (valid.IsFailure)
        {
            return valid;
        }

        Name = string.IsNullOrWhiteSpace(name) ? DocumentType.ToString() : name.Trim();
        IsActive = isActive;

        // Levels are updated in place so their keys stay tracked.
        _levels.RemoveAll(l => l.LevelNo > levels.Count);

        for (int i = 0; i < levels.Count; i++)
        {
            ApprovalFlowLevel? level = _levels.Find(l => l.LevelNo == i + 1);
            if (level is null)
            {
                _levels.Add(new ApprovalFlowLevel(Id, i + 1, levels[i]));
            }
            else
            {
                level.Set(levels[i]);
            }
        }

        return Result.Success();
    }

    public IReadOnlyList<ApprovalLevelDefinition> ToDefinitions() =>
        [.. Levels.Select(l => new ApprovalLevelDefinition(l.Name, l.ApproverUserIds))];

    /// <summary>
    /// Replaces a departing approver in every level (M-99); a level that already lists the replacement just loses
    /// the old user. Returns whether anything changed.
    /// </summary>
    public bool ReplaceApprover(Guid fromUserId, Guid toUserId)
    {
        bool changed = false;

        foreach (ApprovalFlowLevel level in _levels)
        {
            changed |= level.Replace(fromUserId, toUserId);
        }

        return changed;
    }

    public bool HasApprover(Guid userId) => _levels.Exists(l => l.ApproverUserIds.Contains(userId));

    private static Result Validate(IReadOnlyList<ApprovalLevelDefinition> levels)
    {
        if (levels.Count is 0 or > MaxLevels)
        {
            return Result.Failure(ApprovalErrors.InvalidLevelCount);
        }

        for (int i = 0; i < levels.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(levels[i].Name))
            {
                return Result.Failure(ApprovalErrors.LevelNameRequired(i + 1));
            }

            if (!levels[i].ApproverUserIds.Any(id => id != Guid.Empty))
            {
                return Result.Failure(ApprovalErrors.LevelWithoutApprover(i + 1));
            }
        }

        return Result.Success();
    }
}
