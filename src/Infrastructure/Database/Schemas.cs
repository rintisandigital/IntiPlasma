namespace Infrastructure.Database;

/// <summary>
/// One PostgreSQL schema per bounded context.
/// </summary>
internal static class Schemas
{
    public const string Default = "public";
    public const string Identity = "identity";
    public const string Infrastructure = "infrastructure";
    public const string MasterData = "master";
    public const string Partnership = "partnership";
    public const string Finance = "finance";
    public const string Procurement = "procurement";
    public const string Inventory = "inventory";
    public const string Production = "production";
}
