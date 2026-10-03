namespace Infrastructure.Caching;

/// <summary>
/// Whether this process currently listens on the cache invalidation channel (health check, W10).
/// </summary>
internal sealed class CacheInvalidationStatus
{
    private volatile bool _listening;

    public bool Listening
    {
        get => _listening;
        set => _listening = value;
    }
}
