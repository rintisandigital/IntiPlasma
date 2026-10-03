using SharedKernel;

namespace Domain.Access;

/// <summary>
/// "Akses Cabang" (W-7, W-15): the branches a user may work in, chosen per user and applied by every use case
/// in Web.Api and Web.App. One profile can be used by many users (W-18). The system profile All Branches
/// covers every branch, including branches created later, and is read-only.
/// </summary>
public sealed class BranchAccessProfile : AggregateRoot
{
    /// <summary>
    /// Fixed id of the system profile, referenced by the PhaseW1 migration and the seeder.
    /// </summary>
    public static readonly Guid AllBranchesId = Guid.Parse("0199a3c0-0000-7000-8000-000000000002");

    public const string AllBranchesName = "All Branches";

    private readonly List<BranchAccessProfileBranch> _branches = [];

    private BranchAccessProfile(Guid id, string name, string? description, bool allBranches, bool isSystem)
        : base(id)
    {
        Name = name;
        Description = description;
        AllBranches = allBranches;
        IsSystem = isSystem;
    }

    private BranchAccessProfile()
    {
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }

    /// <summary>
    /// Every branch, including branches created later; the branch list is then empty.
    /// </summary>
    public bool AllBranches { get; private set; }

    public bool IsSystem { get; private set; }

    public IReadOnlyCollection<BranchAccessProfileBranch> Branches => [.. _branches];

    public static Result<BranchAccessProfile> Create(
        string name,
        string? description,
        bool allBranches,
        IEnumerable<Guid> branchIds)
    {
        var profile = new BranchAccessProfile(Guid.CreateVersion7(), name.Trim(), Normalize(description), allBranches, isSystem: false);

        Result result = profile.ApplyBranches(allBranches, branchIds);

        return result.IsSuccess ? profile : Result.Failure<BranchAccessProfile>(result.Error);
    }

    public static BranchAccessProfile CreateAllBranches() =>
        new(AllBranchesId, AllBranchesName, "Every branch, including branches created later (system profile).",
            allBranches: true, isSystem: true);

    public Result Update(string name, string? description, bool allBranches, IEnumerable<Guid> branchIds)
    {
        if (IsSystem)
        {
            return Result.Failure(BranchAccessProfileErrors.SystemReadOnly);
        }

        Name = name.Trim();
        Description = Normalize(description);

        return ApplyBranches(allBranches, branchIds);
    }

    public Result EnsureDeletable() =>
        IsSystem ? Result.Failure(BranchAccessProfileErrors.SystemReadOnly) : Result.Success();

    /// <summary>
    /// True when the profile lets its users work in the branch (the caller checks that the branch is active).
    /// </summary>
    public bool Covers(Guid branchId) => AllBranches || _branches.Exists(b => b.BranchId == branchId);

    /// <summary>
    /// The caller is responsible for rejecting unknown branch ids.
    /// </summary>
    private Result ApplyBranches(bool allBranches, IEnumerable<Guid> branchIds)
    {
        ArgumentNullException.ThrowIfNull(branchIds);

        HashSet<Guid> desired = allBranches ? [] : branchIds.ToHashSet();

        if (!allBranches && desired.Count == 0)
        {
            return Result.Failure(BranchAccessProfileErrors.BranchRequired);
        }

        AllBranches = allBranches;

        _branches.RemoveAll(b => !desired.Contains(b.BranchId));

        foreach (Guid branchId in desired.Where(id => _branches.TrueForAll(b => b.BranchId != id)))
        {
            _branches.Add(new BranchAccessProfileBranch(Id, branchId));
        }

        return Result.Success();
    }

    private static string? Normalize(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}

public sealed class BranchAccessProfileBranch
{
    internal BranchAccessProfileBranch(Guid profileId, Guid branchId)
    {
        ProfileId = profileId;
        BranchId = branchId;
    }

    private BranchAccessProfileBranch()
    {
    }

    public Guid ProfileId { get; private set; }
    public Guid BranchId { get; private set; }
}
