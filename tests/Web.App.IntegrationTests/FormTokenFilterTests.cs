using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using NSubstitute;
using Web.App.Infrastructure.Forms;

namespace Web.App.IntegrationTests;

public sealed class FormTokenFilterTests
{
    private readonly HybridCache _cache = CreateCache();
    private readonly ITempDataDictionaryFactory _tempDataFactory = Substitute.For<ITempDataDictionaryFactory>();
    private readonly ITempDataDictionary _tempData = Substitute.For<ITempDataDictionary>();

    public FormTokenFilterTests()
    {
        _tempDataFactory.GetTempData(Arg.Any<HttpContext>()).Returns(_tempData);
    }

    [Fact]
    public async Task SecondPost_WithSameToken_Should_RedirectToFirstResult_WithoutRunningAction()
    {
        var filter = new FormTokenFilter(_cache, _tempDataFactory);
        var token = Guid.CreateVersion7();

        int executions = await PostAsync(filter, token, redirectTo: "/purchase-orders/1");
        (int secondExecutions, IActionResult? secondResult) = await PostWithResultAsync(filter, token, redirectTo: "/purchase-orders/2");

        executions.ShouldBe(1);
        secondExecutions.ShouldBe(0);
        secondResult.ShouldBeOfType<RedirectResult>().Url.ShouldBe("/purchase-orders/1");
        _tempData.Received()[Web.App.Controllers.AppController.ErrorKey] = FormTokenFilter.AlreadySubmittedMessage;
    }

    [Fact]
    public async Task FailedPost_Should_NotConsumeToken()
    {
        var filter = new FormTokenFilter(_cache, _tempDataFactory);
        var token = Guid.CreateVersion7();

        await PostAsync(filter, token, redirectTo: null); // re-rendered form (validation errors)
        int executions = await PostAsync(filter, token, redirectTo: "/purchase-orders/1");

        executions.ShouldBe(1);
    }

    [Fact]
    public async Task PostWithoutToken_Should_RunNormally()
    {
        var filter = new FormTokenFilter(_cache, _tempDataFactory);

        (int executions, _) = await PostWithResultAsync(filter, token: null, redirectTo: "/x");
        (int again, _) = await PostWithResultAsync(filter, token: null, redirectTo: "/x");

        (executions + again).ShouldBe(2);
    }

    private static async Task<int> PostAsync(FormTokenFilter filter, Guid token, string? redirectTo) =>
        (await PostWithResultAsync(filter, token, redirectTo)).Executions;

    private static async Task<(int Executions, IActionResult? Result)> PostWithResultAsync(
        FormTokenFilter filter,
        Guid? token,
        string? redirectTo)
    {
        var httpContext = new DefaultHttpContext { Request = { Method = HttpMethods.Post } };
        httpContext.Request.ContentType = "application/x-www-form-urlencoded";
        httpContext.Request.Form = new FormCollection(token is null
            ? []
            : new Dictionary<string, StringValues> { [FormTokenFilter.FieldName] = token.Value.ToString() });

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var executing = new ResourceExecutingContext(actionContext, [], []);

        int executions = 0;

        await filter.OnResourceExecutionAsync(executing, () =>
        {
            executions++;
            if (redirectTo is not null)
            {
                httpContext.Response.StatusCode = StatusCodes.Status302Found;
                httpContext.Response.Headers.Location = redirectTo;
            }

            return Task.FromResult(new ResourceExecutedContext(actionContext, []));
        });

        return (executions, executing.Result);
    }

    private static HybridCache CreateCache()
    {
        var services = new ServiceCollection();

#pragma warning disable EXTEXP0018
        services.AddHybridCache();
#pragma warning restore EXTEXP0018

        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }
}
