using System.Net;
using System.Net.Sockets;
using HealthChecks.UI.Client;
using Infrastructure.Caching;
using Infrastructure.Documents;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Infrastructure.Health;

/// <summary>
/// Health endpoints of Web.Api and Web.App (W10, PLAN-WEBAPP §23.7):
/// <list type="bullet">
/// <item><c>/health/live</c> — the process answers (no dependency checked); for container restarts.</item>
/// <item><c>/health/ready</c> — database, attachment storage, cache invalidation listener and (Web.App) outbox.</item>
/// <item><c>/health</c> — the same checks with details, shown only to requests from the local/private network.</item>
/// </list>
/// </summary>
public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapAppHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("health/live", new HealthCheckOptions { Predicate = _ => false })
            .AllowAnonymous();

        endpoints.MapHealthChecks("health/ready").AllowAnonymous();

        endpoints.MapHealthChecks("health", new HealthCheckOptions
        {
            ResponseWriter = (context, report) => IsInternal(context.Connection.RemoteIpAddress)
                ? UIResponseWriter.WriteHealthCheckUIResponse(context, report)
                : context.Response.WriteAsync(report.Status.ToString())
        })
        .AllowAnonymous();

        return endpoints;
    }

    /// <summary>
    /// Loopback and private (RFC 1918 / unique local) addresses: the container network, the reverse proxy and
    /// the administrators' LAN — not the internet.
    /// </summary>
    internal static bool IsInternal(IPAddress? address)
    {
        if (address is null || IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal || address.IsIPv6UniqueLocal || address.IsIPv6SiteLocal;
        }

        byte[] bytes = address.GetAddressBytes();

        return bytes[0] switch
        {
            10 => true,
            172 => bytes[1] is >= 16 and <= 31,
            192 => bytes[1] == 168,
            _ => false
        };
    }
}

/// <summary>
/// The attachment folder (shared volume) exists and is writable.
/// </summary>
internal sealed class FileStorageHealthCheck(IOptions<FileStorageOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        string root = Path.GetFullPath(options.Value.RootPath, AppContext.BaseDirectory);
        string probe = Path.Combine(root, $".health-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(probe, "ok", cancellationToken);
            File.Delete(probe);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return HealthCheckResult.Unhealthy($"Attachment storage '{root}' is not writable.", ex);
        }
    }
}

/// <summary>
/// Degraded while the <c>LISTEN cache_invalidation</c> connection is down: access changes made in another process
/// then reach this one only when the cache entries expire.
/// </summary>
internal sealed class CacheInvalidationHealthCheck(CacheInvalidationStatus status) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(status.Listening
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Degraded("Not listening for cache invalidations; reconnecting."));
}
