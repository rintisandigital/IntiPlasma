namespace Application.Warehouses;

public sealed record WarehouseResponse
{
    public Guid Id { get; init; }

    public string Code { get; init; }

    public string Name { get; init; }

    public Guid BranchId { get; init; }

    public string BranchCode { get; init; }

    public string Type { get; init; }

    public Guid? CoopId { get; init; }

    public string? CoopCode { get; init; }

    public string? Address { get; init; }

    public bool IsActive { get; init; }
}
