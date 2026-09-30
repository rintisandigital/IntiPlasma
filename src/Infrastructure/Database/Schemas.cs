namespace Infrastructure.Database;

/// <summary>
/// One PostgreSQL schema per bounded context.
/// </summary>
internal static class Schemas
{
    public const string Default = "public";
    public const string Identity = "identity";
    public const string Infrastructure = "infrastructure";
}
