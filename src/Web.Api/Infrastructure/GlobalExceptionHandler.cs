using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Web.Api.Infrastructure;

internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    private const string UniqueViolation = "23505";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ProblemDetails problemDetails = exception switch
        {
            // Two requests changed the same aggregate (e.g. the same stock balance) at once: safe to retry.
            DbUpdateConcurrencyException => Conflict(
                "Concurrency.Conflict",
                "The data was changed by another request at the same time. Please retry."),

            // A race slipped past an application-level uniqueness check; the database index caught it.
            DbUpdateException { InnerException: PostgresException { SqlState: UniqueViolation } } => Conflict(
                "Database.UniqueViolation",
                "The data conflicts with an existing record. Please refresh and retry."),

            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1",
                Title = "Server failure"
            }
        };

        if (problemDetails.Status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception occurred");
        }
        else
        {
            logger.LogWarning(exception, "Request conflicted with concurrent changes");
        }

        httpContext.Response.StatusCode = problemDetails.Status!.Value;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static ProblemDetails Conflict(string title, string detail) => new()
    {
        Status = StatusCodes.Status409Conflict,
        Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8",
        Title = title,
        Detail = detail
    };
}
