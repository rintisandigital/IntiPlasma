using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Application.Abstractions.Auditing;
using Domain.Auditing;
using Infrastructure.Database;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using SharedKernel;

namespace Infrastructure.Auditing;

/// <summary>
/// Adds audit entries to the current <see cref="ApplicationDbContext"/> with the signed-in user (cookie or JWT),
/// the client IP address and the recording application (<c>Web.App</c> / <c>Web.Api</c>).
/// </summary>
internal sealed class AuditTrail(
    ApplicationDbContext context,
    IHttpContextAccessor httpContextAccessor,
    IDateTimeProvider dateTimeProvider,
    IHostEnvironment environment) : IAuditTrail
{
    private const string Masked = "***";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerOptions.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public void Record(AuditEntry entry)
    {
        HttpContext? httpContext = httpContextAccessor.HttpContext;
        ClaimsPrincipal? principal = httpContext?.User;

        Guid? userId = entry.UserId;
        string? userEmail = entry.UserEmail;

        if (principal?.Identity?.IsAuthenticated == true && userId is null)
        {
            userId = Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out Guid id) ? id : null;
            userEmail ??= principal.FindFirstValue(ClaimTypes.Email) ?? principal.FindFirstValue("email");
        }

        context.AuditLogs.Add(AuditLog.Create(
            dateTimeProvider.UtcNow,
            entry.Category,
            entry.Action,
            entry.Summary,
            environment.ApplicationName,
            userId,
            userEmail,
            entry.EntityType,
            entry.EntityId,
            Serialize(entry.Details),
            httpContext?.Connection.RemoteIpAddress?.ToString()));
    }

    internal static string? Serialize(object? details)
    {
        if (details is null)
        {
            return null;
        }

        JsonNode? node = JsonSerializer.SerializeToNode(details, details.GetType(), SerializerOptions);

        MaskPasswords(node);

        return node?.ToJsonString();
    }

    private static void MaskPasswords(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (string key in obj.Select(p => p.Key).ToList())
                {
                    if (key.Contains("password", StringComparison.OrdinalIgnoreCase))
                    {
                        obj[key] = Masked;
                    }
                    else
                    {
                        MaskPasswords(obj[key]);
                    }
                }

                break;
            case JsonArray array:
                foreach (JsonNode? item in array)
                {
                    MaskPasswords(item);
                }

                break;
        }
    }
}
