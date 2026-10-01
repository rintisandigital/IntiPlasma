using Application.Abstractions.Messaging;
using Domain.MasterData.Coops;

namespace Application.Coops.Create;

public sealed record CreateCoopCommand(
    Guid FarmerId,
    string Code,
    string Name,
    int Capacity,
    HouseType HouseType,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    IReadOnlyList<Guid>? Documents = null) : ICommand<Guid>;
