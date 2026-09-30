using System.Text.Json;
using Microsoft.Extensions.Caching.Hybrid;

namespace Web.Api.Infrastructure;

/// <summary>
/// Replays the first response for a repeated <c>Idempotency-Key</c> header, so clients with unreliable
/// connections (e.g. the PPL mobile app syncing offline data) can safely retry create commands.
/// The header is optional; requests without it are processed normally.
/// </summary>
/// <remarks>
/// Responses are kept in HybridCache. Without a distributed cache backend this is per instance;
/// add a Redis L2 cache before scaling out to multiple instances.
/// </remarks>
internal sealed class IdempotencyFilter(HybridCache cache) : IEndpointFilter
{
    public const string HeaderName = "Idempotency-Key";

    private static readonly HybridCacheEntryOptions CacheOptions = new() { Expiration = TimeSpan.FromHours(24) };

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        HttpContext httpContext = context.HttpContext;

        if (!httpContext.Request.Headers.TryGetValue(HeaderName, out Microsoft.Extensions.Primitives.StringValues header) ||
            !Guid.TryParse(header, out Guid idempotencyKey))
        {
            return await next(context);
        }

        string cacheKey = $"idempotency:{httpContext.Request.Method}:{httpContext.Request.Path}:{idempotencyKey}";

        IdempotentResponse? cached = await cache.GetOrCreateAsync<IdempotentResponse?>(
            cacheKey,
            _ => ValueTask.FromResult<IdempotentResponse?>(null),
            new HybridCacheEntryOptions { Flags = HybridCacheEntryFlags.DisableLocalCacheWrite | HybridCacheEntryFlags.DisableDistributedCacheWrite },
            cancellationToken: httpContext.RequestAborted);

        if (cached is not null)
        {
            return Results.Json(JsonSerializer.Deserialize<JsonElement>(cached.Body), statusCode: cached.StatusCode);
        }

        object? result = await next(context);

        if (result is IStatusCodeHttpResult { StatusCode: >= 200 and < 300 } statusResult)
        {
            object? value = (result as IValueHttpResult)?.Value;

            await cache.SetAsync(
                cacheKey,
                new IdempotentResponse(statusResult.StatusCode.Value, JsonSerializer.Serialize(value)),
                CacheOptions,
                cancellationToken: httpContext.RequestAborted);
        }

        return result;
    }

    internal sealed record IdempotentResponse(int StatusCode, string Body);
}
