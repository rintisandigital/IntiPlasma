using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Caching.Hybrid;
using Web.App.Controllers;
using Web.App.Infrastructure.Auth;

namespace Web.App.Infrastructure.Forms;

/// <summary>
/// Double-submit protection for create forms (the Web.App counterpart of Web.Api's Idempotency-Key): a form
/// rendered with <c>&lt;form-token /&gt;</c> posts a one-time token. When the same token is posted again
/// within 24 hours (double click, refresh, back + resubmit) the user is redirected to where the first post
/// led instead of creating a second document.
/// </summary>
internal sealed class FormTokenFilter(HybridCache cache, ITempDataDictionaryFactory tempDataFactory) : IAsyncResourceFilter
{
    public const string FieldName = "__FormToken";
    public const string AlreadySubmittedMessage = "This form was already submitted.";

    private static readonly HybridCacheEntryOptions StoreOptions = new() { Expiration = TimeSpan.FromHours(24) };
    private static readonly HybridCacheEntryOptions LookupOptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableLocalCacheWrite | HybridCacheEntryFlags.DisableDistributedCacheWrite
    };

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        HttpContext httpContext = context.HttpContext;

        if (!TryGetToken(httpContext, out Guid token))
        {
            await next();
            return;
        }

        string key = $"formtoken:{httpContext.User.GetUserId()}:{token}";
        CancellationToken cancellationToken = httpContext.RequestAborted;

        string? firstResult = await cache.GetOrCreateAsync<string?>(
            key,
            _ => ValueTask.FromResult<string?>(null),
            LookupOptions,
            cancellationToken: cancellationToken);

        if (firstResult is not null)
        {
            tempDataFactory.GetTempData(httpContext)[AppController.ErrorKey] = AlreadySubmittedMessage;
            context.Result = new RedirectResult(firstResult);
            return;
        }

        ResourceExecutedContext executed = await next();

        // Only a successful post (PRG redirect) consumes the token; a re-rendered form with errors keeps it.
        string location = executed.HttpContext.Response.Headers.Location.ToString();
        if (executed.Exception is null && IsRedirect(executed.HttpContext.Response.StatusCode) && location.Length > 0)
        {
            await cache.SetAsync(key, location, StoreOptions, cancellationToken: cancellationToken);
        }
    }

    private static bool TryGetToken(HttpContext httpContext, out Guid token)
    {
        token = Guid.Empty;

        return HttpMethods.IsPost(httpContext.Request.Method) &&
               httpContext.Request.HasFormContentType &&
               httpContext.Request.Form.TryGetValue(FieldName, out Microsoft.Extensions.Primitives.StringValues value) &&
               Guid.TryParse(value, CultureInfo.InvariantCulture, out token);
    }

    private static bool IsRedirect(int statusCode) => statusCode is >= 300 and < 400;
}
