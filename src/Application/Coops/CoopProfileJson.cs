using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.MasterData.Coops;

namespace Application.Coops;

/// <summary>
/// JSON form of <see cref="CoopProfile"/> in the <c>master.coops.profile</c> column (EF write side and Dapper read side).
/// </summary>
public static class CoopProfileJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(CoopProfile profile) => JsonSerializer.Serialize(profile, Options);

    public static CoopProfile Deserialize(string? json) =>
        string.IsNullOrWhiteSpace(json) ? new CoopProfile() : JsonSerializer.Deserialize<CoopProfile>(json, Options) ?? new CoopProfile();
}
