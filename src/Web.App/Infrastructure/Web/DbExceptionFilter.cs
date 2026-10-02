using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Web.App.Controllers;

namespace Web.App.Infrastructure.Web;

/// <summary>
/// Turns optimistic-concurrency conflicts (<c>xmin</c>) and unique-index races into a friendly message on the
/// page the user came from, instead of an error page (same cases Web.Api answers with 409).
/// </summary>
internal sealed partial class DbExceptionFilter(
    ITempDataDictionaryFactory tempDataFactory,
    ILogger<DbExceptionFilter> logger) : IExceptionFilter
{
    public const string ConcurrencyMessage = "This record was changed by another user. Please reload and try again.";
    public const string UniqueViolationMessage = "The data conflicts with an existing record. Please refresh and try again.";

    public void OnException(ExceptionContext context)
    {
        string? message = context.Exception switch
        {
            DbUpdateConcurrencyException => ConcurrencyMessage,
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                UniqueViolationMessage,
            _ => null
        };

        if (message is null)
        {
            return;
        }

        LogConflict(logger, context.Exception);

        HttpContext httpContext = context.HttpContext;

        if (httpContext.Request.IsAjax())
        {
            context.Result = new ConflictObjectResult(new { message });
        }
        else
        {
            tempDataFactory.GetTempData(httpContext)[AppController.ErrorKey] = message;
            context.Result = new RedirectResult(BackUrl(httpContext));
        }

        context.ExceptionHandled = true;
    }

    /// <summary>
    /// The page that posted the form (same host only), otherwise the current GET page or the dashboard.
    /// </summary>
    private static string BackUrl(HttpContext httpContext)
    {
        HttpRequest request = httpContext.Request;

        if (Uri.TryCreate(request.Headers.Referer.ToString(), UriKind.Absolute, out Uri? referer) &&
            string.Equals(referer.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase))
        {
            return referer.PathAndQuery;
        }

        return HttpMethods.IsGet(request.Method)
            ? $"{request.PathBase}{request.Path}{request.QueryString}"
            : $"{request.PathBase}/Main/Dashboard";
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Request conflicted with concurrent changes")]
    private static partial void LogConflict(ILogger logger, Exception exception);
}
