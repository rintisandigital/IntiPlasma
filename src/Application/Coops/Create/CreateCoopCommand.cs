using Application.Abstractions.Messaging;
using Domain.MasterData.Coops;

namespace Application.Coops.Create;

/// <param name="FieldOfficerUserId">
/// PPL responsible for the coop; ignored for a user limited to assigned data, who is assigned automatically and
/// may only add coops to farmers in their scope.
/// </param>
public sealed record CreateCoopCommand(
    Guid FarmerId,
    string Code,
    string Name,
    int Capacity,
    HouseType HouseType,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    IReadOnlyList<Guid>? Documents = null,
    CoopProfile? Profile = null,
    Guid? FieldOfficerUserId = null) : ICommand<Guid>;
