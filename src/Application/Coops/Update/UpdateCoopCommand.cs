using Application.Abstractions.Messaging;
using Domain.MasterData.Coops;

namespace Application.Coops.Update;

/// <remarks>
/// <see cref="FieldOfficerUserId"/> replaces the assignment (null = none), except for a user limited to assigned
/// data, who cannot reassign.
/// </remarks>
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
    CoopProfile? Profile = null,
    Guid? FieldOfficerUserId = null) : ICommand;
