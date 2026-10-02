namespace Infrastructure.Caching;

/// <summary>
/// Payload of the PostgreSQL <c>cache_invalidation</c> notification channel: <c>key:&lt;key&gt;</c> or <c>tag:&lt;tag&gt;</c>.
/// </summary>
internal static class CacheInvalidationMessage
{
    public const string Channel = "cache_invalidation";

    private const string KeyPrefix = "key:";
    private const string TagPrefix = "tag:";

    public static string ForKey(string key) => KeyPrefix + key;

    public static string ForTag(string tag) => TagPrefix + tag;

    public static bool TryParse(string payload, out bool isTag, out string value)
    {
        if (payload.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            isTag = false;
            value = payload[KeyPrefix.Length..];
            return value.Length > 0;
        }

        if (payload.StartsWith(TagPrefix, StringComparison.Ordinal))
        {
            isTag = true;
            value = payload[TagPrefix.Length..];
            return value.Length > 0;
        }

        isTag = false;
        value = string.Empty;
        return false;
    }
}
