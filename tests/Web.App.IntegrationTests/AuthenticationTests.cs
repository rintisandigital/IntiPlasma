using System.Net;
using System.Text.RegularExpressions;
using Application.Abstractions.Authorization;
using Domain.Users;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Web.App.IntegrationTests;

[Collection(nameof(WebAppCollection))]
public sealed partial class AuthenticationTests(WebAppFactory factory)
{
    [Fact]
    public async Task Mainboard_Should_RedirectToLogin_WhenSignedOut()
    {
        HttpClient client = CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/Main", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldStartWith("http://localhost/Auth/Login");
    }

    [Fact]
    public async Task Pages_Should_OnlyAllowFramingBySameOrigin()
    {
        HttpClient client = CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/Auth/Login", UriKind.Relative));

        response.Headers.GetValues("X-Frame-Options").ShouldContain("SAMEORIGIN");
        response.Headers.GetValues("Content-Security-Policy").ShouldContain("frame-ancestors 'self'");
    }

    [Fact]
    public async Task Login_Should_ShowError_WhenPasswordIsWrong()
    {
        HttpClient client = CreateClient();

        HttpResponseMessage response = await PostLoginAsync(client, WebAppFactory.AdminEmail, "wrong-password");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain(UserErrors.InvalidCredentials.Description);
    }

    [Fact]
    public async Task Login_Should_RejectInactiveUser()
    {
        string email = UniqueEmail();
        await factory.CreateUserAsync(email, "Password123", active: false);
        HttpClient client = CreateClient();

        HttpResponseMessage response = await PostLoginAsync(client, email, "Password123");

        (await response.Content.ReadAsStringAsync()).ShouldContain(UserErrors.Inactive.Description);
    }

    [Fact]
    public async Task Login_Should_OpenMainboard_AndLogout_Should_EndSession()
    {
        HttpClient client = CreateClient();

        HttpResponseMessage login = await PostLoginAsync(client, WebAppFactory.AdminEmail, WebAppFactory.AdminPassword);
        login.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        HttpResponseMessage mainboard = await client.GetAsync(new Uri("/Main", UriKind.Relative));
        mainboard.StatusCode.ShouldBe(HttpStatusCode.OK);
        string html = await mainboard.Content.ReadAsStringAsync();
        html.ShouldContain("name=\"content-frame\"");
        html.ShouldContain("data-menu-code=\"dashboard\"");

        (await client.GetAsync(new Uri("/Main/Dashboard", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.OK);

        string token = AntiforgeryToken(html);
        using var logoutForm = new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token });
        HttpResponseMessage logout = await client.PostAsync(new Uri("/Auth/Logout", UriKind.Relative), logoutForm);
        logout.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        (await client.GetAsync(new Uri("/Main", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Session_Should_End_WhenUserIsDeactivated()
    {
        string email = UniqueEmail();
        User user = await factory.CreateUserAsync(email, "Password123");
        HttpClient client = CreateClient();
        await PostLoginAsync(client, email, "Password123");
        (await client.GetAsync(new Uri("/Main", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.OK);

        await factory.DeactivateAsync(user.Id);
        await factory.Services.GetRequiredService<HybridCache>().RemoveAsync(PermissionCacheKeys.SessionForUser(user.Id));

        HttpResponseMessage response = await client.GetAsync(new Uri("/Main", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldContain("/Auth/Login");
    }

    [Fact]
    public async Task Ajax_Should_Get401_InsteadOfLoginRedirect()
    {
        HttpClient client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/Main/Dashboard", UriKind.Relative));
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnknownPage_Should_RenderNotFoundPage()
    {
        HttpClient client = CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/Error/404", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Page not found");
    }

    private HttpClient CreateClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private static async Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string email, string password)
    {
        string page = await client.GetStringAsync(new Uri("/Auth/Login", UriKind.Relative));

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = AntiforgeryToken(page)
        });

        return await client.PostAsync(new Uri("/Auth/Login", UriKind.Relative), form);
    }

    private static string AntiforgeryToken(string html) => TokenRegex().Match(html).Groups[1].Value;

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@intiplasma.test";

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();
}
