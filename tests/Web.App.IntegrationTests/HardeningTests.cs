using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Domain.Auditing;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Web.App.IntegrationTests;

/// <summary>
/// PhaseW10: security headers (CSP with nonce), account lockout and unlock, login rate limit, audit trail
/// (access changes, exports, sign-ins), health endpoints, Data Protection keys in the database, session keep-alive.
/// </summary>
[Collection(nameof(WebAppCollection))]
public sealed partial class HardeningTests(WebAppFactory factory)
{
    private const string Password = "Password123";

    [Fact]
    public async Task HtmlPages_Should_SendContentSecurityPolicy_WithNonceOnInlineScripts()
    {
        HttpClient client = CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/Auth/Login", UriKind.Relative));
        string html = await response.Content.ReadAsStringAsync();

        string policy = response.Headers.GetValues("Content-Security-Policy").Single();
        policy.ShouldContain("default-src 'self'");
        policy.ShouldContain("object-src 'none'");
        policy.ShouldContain("frame-ancestors 'self'");
        string nonce = NonceRegex().Match(policy).Groups[1].Value;
        nonce.ShouldNotBeNullOrEmpty();
        html.ShouldContain($"<script nonce=\"{nonce}\">");
        html.ShouldNotContain("<script>");

        response.Headers.GetValues("Permissions-Policy").Single().ShouldContain("camera=()");
        response.Headers.GetValues("Cross-Origin-Opener-Policy").Single().ShouldBe("same-origin");
        response.Headers.GetValues("X-Content-Type-Options").Single().ShouldBe("nosniff");
    }

    [Fact]
    public async Task Nonce_Should_ChangePerRequest()
    {
        HttpClient client = CreateClient();

        string first = (await client.GetAsync(new Uri("/Auth/Login", UriKind.Relative))).Headers.GetValues("Content-Security-Policy").Single();
        string second = (await client.GetAsync(new Uri("/Auth/Login", UriKind.Relative))).Headers.GetValues("Content-Security-Policy").Single();

        NonceRegex().Match(first).Groups[1].Value.ShouldNotBe(NonceRegex().Match(second).Groups[1].Value);
    }

    [Fact]
    public async Task Files_Should_KeepOnlyTheFramingPolicy()
    {
        HttpClient client = await AdminClientAsync();

        HttpResponseMessage response = await client.GetAsync(new Uri("/MasterData/Uoms/Export?format=pdf", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.GetValues("Content-Security-Policy").Single().ShouldBe("frame-ancestors 'self'");
    }

    [Fact]
    public async Task Pages_Should_NotUseInlineEventHandlers()
    {
        HttpClient client = await AdminClientAsync();

        foreach (string path in new[] { "/Main", "/Main/Dashboard", "/Finance/FiscalPeriods", "/Finance/Journals/Create", "/Procurement/PurchaseOrders/Create" })
        {
            string html = await client.GetStringAsync(new Uri(path, UriKind.Relative));

            InlineHandlerRegex().IsMatch(html).ShouldBeFalse(path);
            html.ShouldNotContain("javascript:", customMessage: path);
        }
    }

    [Fact]
    public async Task Lockout_Should_RefuseSignIn_AfterFiveWrongPasswords_UntilUnlocked()
    {
        string email = UniqueEmail();
        User user = await factory.CreateUserAsync(email, Password);
        HttpClient client = CreateClient();

        for (int attempt = 1; attempt <= 4; attempt++)
        {
            (await PostLoginAsync(client, email, "wrong-password")).ShouldContain(UserErrors.InvalidCredentials.Description);
        }

        (await PostLoginAsync(client, email, "wrong-password")).ShouldContain("The account is locked");
        (await PostLoginAsync(client, email, Password)).ShouldContain("The account is locked");

        // The administrator sees the lock and lifts it.
        HttpClient admin = await AdminClientAsync();
        string details = await admin.GetStringAsync(new Uri($"/Admin/Users/Details/{user.Id}", UriKind.Relative));
        details.ShouldContain("Locked until");
        string users = await admin.GetStringAsync(new Uri($"/Admin/Users?search={email}", UriKind.Relative));
        users.ShouldContain(">Locked<");

        using var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = CsrfToken(details) });
        HttpResponseMessage unlock = await admin.PostAsync(new Uri($"/Admin/Users/Unlock/{user.Id}", UriKind.Relative), form);
        unlock.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        HttpClient again = CreateClient();
        await LoginAsync(again, email, Password);

        List<AuditLog> entries = await AuditEntriesAsync(a => a.UserId == user.Id || a.EntityId == user.Id);
        entries.Count(a => a.Action == "SignInFailed").ShouldBe(4);
        entries.ShouldContain(a => a.Action == "LockedOut" && a.Category == AuditCategory.SignIn);
        entries.ShouldContain(a => a.Action == "SignInRejected");
        entries.ShouldContain(a => a.Action == "UnlockUser" && a.Category == AuditCategory.Access && a.Source == "Web.App");
        entries.ShouldContain(a => a.Action == "SignedIn");
    }

    [Fact]
    public async Task LoginRateLimit_Should_Return429_PerClient()
    {
        using WebApplicationFactory<Program> limited = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Security:LoginPermitPerMinute", "2"));
        HttpClient client = limited.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        string page = await client.GetStringAsync(new Uri("/Auth/Login", UriKind.Relative));

        var codes = new HttpStatusCode[3];
        for (int i = 0; i < codes.Length; i++)
        {
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = UniqueEmail(),
                ["Password"] = "wrong-password",
                ["__RequestVerificationToken"] = AntiforgeryToken(page)
            });
            HttpResponseMessage response = await client.PostAsync(new Uri("/Auth/Login", UriKind.Relative), form);
            codes[i] = response.StatusCode;

            if (i == codes.Length - 1)
            {
                (await response.Content.ReadAsStringAsync()).ShouldContain("Too many attempts");
            }
        }

        codes.ShouldBe([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests]);
    }

    [Fact]
    public async Task AccessChange_Should_BeAudited_WithPasswordMasked()
    {
        string email = UniqueEmail();
        User user = await factory.CreateUserAsync(email, Password);
        HttpClient admin = await AdminClientAsync();
        string details = await admin.GetStringAsync(new Uri($"/Admin/Users/Details/{user.Id}", UriKind.Relative));

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["NewPassword"] = "Secret-New-Password-1",
            ["__RequestVerificationToken"] = CsrfToken(details)
        });
        (await admin.PostAsync(new Uri($"/Admin/Users/ResetPassword/{user.Id}", UriKind.Relative), form))
            .StatusCode.ShouldBe(HttpStatusCode.Redirect);

        AuditLog entry = (await AuditEntriesAsync(a => a.EntityId == user.Id && a.Action == "ResetUserPassword")).Single();
        entry.UserEmail.ShouldBe(WebAppFactory.AdminEmail);
        entry.Summary.ShouldBe("Reset user password (User)");
        entry.Details!.ShouldNotContain("Secret-New-Password-1");
        using var json = JsonDocument.Parse(entry.Details!);
        json.RootElement.GetProperty("newPassword").GetString().ShouldBe("***");
        json.RootElement.GetProperty("userId").GetGuid().ShouldBe(user.Id);

        // The audit log screen lists and opens the entry; the list can be exported.
        string list = await admin.GetStringAsync(new Uri("/Admin/AuditLogs?category=Access&search=Reset+user+password", UriKind.Relative));
        list.ShouldContain("Reset user password (User)");
        string page = await admin.GetStringAsync(new Uri($"/Admin/AuditLogs/Details/{entry.Id}", UriKind.Relative));
        page.ShouldContain("&quot;newPassword&quot;: &quot;***&quot;");
        HttpResponseMessage export = await admin.GetAsync(new Uri("/Admin/AuditLogs/Export?format=xlsx&category=Access", UriKind.Relative));
        export.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Export_Should_BeAudited()
    {
        HttpClient admin = await AdminClientAsync();
        DateTime before = DateTime.UtcNow.AddSeconds(-1);

        (await admin.GetAsync(new Uri("/MasterData/Uoms/Export?format=xlsx&search=KG", UriKind.Relative)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        AuditLog entry = (await AuditEntriesAsync(a =>
            a.Category == AuditCategory.Export && a.EntityType == "master.uoms" && a.OccurredAtUtc >= before)).Single();
        entry.Action.ShouldBe("Excel");
        entry.Summary.ShouldStartWith("Excel export of Units of Measure");
        entry.Details!.ShouldContain("Search: KG");
        entry.UserEmail.ShouldBe(WebAppFactory.AdminEmail);
    }

    [Fact]
    public async Task UserWithoutAuditLogRight_Should_GetForbidden()
    {
        Guid profile = await factory.CreateMenuProfileAsync($"Users only {Guid.NewGuid():N}", ("admin.users", Domain.Access.MenuRights.View));
        string email = UniqueEmail();
        await factory.CreateUserAsync(email, Password, menuAccessProfileId: profile);
        HttpClient client = CreateClient();
        await LoginAsync(client, email, Password);

        HttpResponseMessage response = await client.GetAsync(new Uri("/Admin/AuditLogs", UriKind.Relative));

        response.Headers.Location!.ToString().ShouldContain("/Error/403");
    }

    [Fact]
    public async Task HealthEndpoints_Should_Respond()
    {
        HttpClient client = CreateClient();

        HttpResponseMessage live = await client.GetAsync(new Uri("/health/live", UriKind.Relative));
        HttpResponseMessage ready = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));
        HttpResponseMessage details = await client.GetAsync(new Uri("/health", UriKind.Relative));

        live.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await live.Content.ReadAsStringAsync()).ShouldBe("Healthy");
        ready.StatusCode.ShouldBe(HttpStatusCode.OK);
        string json = await details.Content.ReadAsStringAsync();
        json.ShouldContain("\"storage\"");
        json.ShouldContain("\"cache-invalidation\"");
        json.ShouldContain("\"npgsql\"");
    }

    [Fact]
    public async Task DataProtectionKeys_Should_BeStoredInDatabase()
    {
        HttpClient client = await AdminClientAsync();
        (await client.GetAsync(new Uri("/Main", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.OK);

        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        (await db.DataProtectionKeys.CountAsync()).ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task KeepAlive_Should_ReturnSessionExpiry()
    {
        HttpClient client = await AdminClientAsync();
        string mainboard = await client.GetStringAsync(new Uri("/Main", UriKind.Relative));
        mainboard.ShouldContain("data-session-expires=\"1");

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/Main/KeepAlive", UriKind.Relative));
        request.Headers.Add("X-CSRF-TOKEN", CsrfToken(mainboard));
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        long expires = json.RootElement.GetProperty("expires").GetInt64();
        DateTimeOffset.FromUnixTimeMilliseconds(expires).ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddHours(7));
    }

    private async Task<List<AuditLog>> AuditEntriesAsync(System.Linq.Expressions.Expression<Func<AuditLog, bool>> predicate)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await db.AuditLogs.Where(predicate).OrderBy(a => a.OccurredAtUtc).ToListAsync();
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        HttpClient client = CreateClient();
        await LoginAsync(client, WebAppFactory.AdminEmail, WebAppFactory.AdminPassword);
        return client;
    }

    private HttpClient CreateClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        HttpResponseMessage response = await PostLoginResponseAsync(client, email, password);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    }

    private static async Task<string> PostLoginAsync(HttpClient client, string email, string password) =>
        await (await PostLoginResponseAsync(client, email, password)).Content.ReadAsStringAsync();

    private static async Task<HttpResponseMessage> PostLoginResponseAsync(HttpClient client, string email, string password)
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

    private static string CsrfToken(string html) => CsrfRegex().Match(html).Groups[1].Value;

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@intiplasma.test";

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();

    [GeneratedRegex("data-csrf-token=\"([^\"]+)\"")]
    private static partial Regex CsrfRegex();

    [GeneratedRegex("'nonce-([^']+)'")]
    private static partial Regex NonceRegex();

    [GeneratedRegex(@"\son(click|change|submit|load|input|keyup|keydown)=", RegexOptions.IgnoreCase)]
    private static partial Regex InlineHandlerRegex();
}
