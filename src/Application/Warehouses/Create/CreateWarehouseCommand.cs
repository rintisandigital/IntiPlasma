using Application.Abstractions.Messaging;

namespace Application.Warehouses.Create;

/// <summary>
/// Creates a central warehouse (gudang induk). Coop warehouses are created automatically with their coop.
/// </summary>
public sealed record CreateWarehouseCommand(string Code, string Name, Guid BranchId, string? Address) : ICommand<Guid>;
