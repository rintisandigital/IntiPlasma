namespace Application.Roles.Get;

public sealed record RoleResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; }

    public string? Description { get; init; }

    public bool IsSystem { get; init; }

    public IReadOnlyList<string> Permissions { get; init; } = [];
}
