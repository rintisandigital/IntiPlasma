using Application.Abstractions.Messaging;
using Domain.MasterData.Coops;

namespace Application.Coops.Update;

public sealed record UpdateCoopCommand(
    Guid CoopId,
    string Name,
    int Capacity,
    HouseType HouseType,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    bool IsActive,
    IReadOnlyList<Guid>? Documents = null,
    CoopProfile? Profile = null) : ICommand;
