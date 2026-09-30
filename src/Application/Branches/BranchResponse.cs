namespace Application.Branches;

public sealed record BranchResponse
{
    public Guid Id { get; init; }

    public string Code { get; init; }

    public string Name { get; init; }

    public string? Address { get; init; }

    public string? Phone { get; init; }

    public bool IsActive { get; init; }
}
