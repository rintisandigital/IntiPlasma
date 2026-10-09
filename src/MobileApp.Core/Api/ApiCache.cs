using System.Text.Json;
using MobileApp.Core.Local;

namespace MobileApp.Core.Api;

/// <summary>
/// Read-only cache of list and detail responses (PLAN-MOBILE M-4, §3.5, §3.7). Every successful GET is stored
/// under its path; when the server cannot be reached the stored response is returned with its age ("Data per …").
/// A 403/404 removes the entry, e.g. after a farm was assigned to another PPL. Searches are not stored.
/// </summary>
public sealed class ApiCache(ApiClient api, LocalDb db)
{
    public async Task<CachedResult<T>> GetAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        ApiResult<T> result = await api.GetAsync<T>(path, cancellationToken);

        if (result.IsSuccess)
        {
            if (IsCacheable(path))
            {
                await db.SetCacheAsync(path, JsonSerializer.Serialize(result.Value, ApiClient.JsonOptions), DateTime.UtcNow, cancellationToken);
            }

            return CachedResult<T>.Fresh(result.Value);
        }

        ApiError error = result.Error!;

        if (error.IsNetwork && await ReadAsync<T>(path, cancellationToken) is { } cached)
        {
            return cached;
        }

        if (error.Status is 403 or 404)
        {
            await db.RemoveCacheAsync(path, cancellationToken);
        }

        return CachedResult<T>.Failure(error);
    }

    /// <summary>
    /// The stored response only (shown first while the page refreshes), or null.
    /// </summary>
    public async Task<CachedResult<T>?> ReadAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        CacheEntry? entry = await db.GetCacheAsync(path, cancellationToken);
        if (entry is null)
        {
            return null;
        }

        try
        {
            T? value = JsonSerializer.Deserialize<T>(entry.Json, ApiClient.JsonOptions);

            return value is null ? null : CachedResult<T>.FromCache(value, entry.FetchedAtUtc);
        }
        catch (JsonException)
        {
            // Stored by an older app version with another shape: ignore it.
            return null;
        }
    }

    internal static bool IsCacheable(string path) => !path.Contains("search=", StringComparison.Ordinal);
}

/// <summary>
/// A response from the server, or from the cache when <see cref="CachedAtUtc"/> is set.
/// </summary>
public sealed class CachedResult<T>
{
    private readonly T? _value;

    private CachedResult(T? value, ApiError? error, DateTime? cachedAtUtc)
    {
        _value = value;
        Error = error;
        CachedAtUtc = cachedAtUtc;
    }

    public ApiError? Error { get; }

    public bool IsSuccess => Error is null;

    /// <summary>
    /// When the cached copy was received; null for a fresh answer.
    /// </summary>
    public DateTime? CachedAtUtc { get; }

    public bool IsFromCache => CachedAtUtc is not null;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result has no value.");

    public static CachedResult<T> Fresh(T value) => new(value, null, null);

    public static CachedResult<T> FromCache(T value, DateTime cachedAtUtc) => new(value, null, cachedAtUtc);

    public static CachedResult<T> Failure(ApiError error) => new(default, error, null);
}
