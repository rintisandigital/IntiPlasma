using System.Net;
using System.Text.RegularExpressions;
using Domain.Access;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Web.App.Infrastructure.Authorization;

namespace Web.App.IntegrationTests;

/// <summary>
/// PhaseW1: menu access profiles drive the sidebar and every page (W-2, W-15).
/// </summary>
[Collection(nameof(WebAppCollection))]
public sealed partial class AccessTests(WebAppFactory factory)
{
    private const string Password = "Password123";

    [Fact]
    public async Task UserWithoutMenuAccess_Should_NotSignIn()
    {
        string email = UniqueEmail();
        User user = await factory.CreateUserAsync(email, Password);
        await ClearMenuAccessAsync(user.Id);
        HttpClient client = CreateClient();

        HttpResponseMessage response = await LoginAsync(client, email);

        (await response.Content.ReadAsStringAsync()).ShouldContain(UserErrors.NoMenuAccess.Description);
    }

    [Fact]
    public async Task Admin_Should_SeeAdministrationMenus()
    {
        HttpClient client = CreateClient();
        await LoginAsync(client, WebAppFactory.AdminEmail, WebAppFactory.AdminPassword);

        string mainboard = await client.GetStringAsync(new Uri("/Main", UriKind.Relative));

        mainboard.ShouldContain($"data-menu-code=\"{MenuCodes.AdminUsers}\"");
        mainboard.ShouldContain($"data-menu-code=\"{MenuCodes.AdminMenuAccess}\"");
        mainboard.ShouldNotContain("data-menu-code=\"master.uoms\""); // not released yet (W-25)
        (await client.GetAsync(new Uri("/Admin/Users", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync(new Uri("/Admin/MenuAccess", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RestrictedUser_Should_OnlySee_AndOpen_GrantedMenus()
    {
        Guid profileId = await factory.CreateMenuProfileAsync(
            $"Branches viewer {Guid.NewGuid():N}",
            (MenuCodes.AdminBranches, MenuRights.View));
        string email = UniqueEmail();
        await factory.CreateUserAsync(email, Password, menuAccessProfileId: profileId);
        HttpClient client = CreateClient();
        await LoginAsync(client, email);

        string mainboard = await client.GetStringAsync(new Uri("/Main", UriKind.Relative));
        mainboard.ShouldContain($"data-menu-code=\"{MenuCodes.AdminBranches}\"");
        mainboard.ShouldNotContain($"data-menu-code=\"{MenuCodes.AdminUsers}\"");

        HttpResponseMessage branches = await client.GetAsync(new Uri("/Admin/Branches", UriKind.Relative));
        branches.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await branches.Content.ReadAsStringAsync()).ShouldNotContain("Add Branch"); // no Create right

        HttpResponseMessage users = await client.GetAsync(new Uri("/Admin/Users", UriKind.Relative));
        users.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        users.Headers.Location!.ToString().ShouldContain("/Error/403");

        (await client.GetAsync(new Uri("/Admin/Branches/Create", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Admin_Should_CreateUser_ThenDeleteIt_KeepingABackup()
    {
        HttpClient client = CreateClient();
        await LoginAsync(client, WebAppFactory.AdminEmail, WebAppFactory.AdminPassword);
        string email = UniqueEmail();

        string form = await client.GetStringAsync(new Uri("/Admin/Users/Create", UriKind.Relative));
        using var create = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FirstName"] = "Siti",
            ["LastName"] = "Rahma",
            ["Email"] = email,
            ["Password"] = Password,
            ["ConfirmPassword"] = Password,
            ["MenuAccessProfileId"] = MenuAccessProfile.FullAccessId.ToString(),
            ["__RequestVerificationToken"] = AntiforgeryToken(form)
        });
        HttpResponseMessage created = await client.PostAsync(new Uri("/Admin/Users/Create", UriKind.Relative), create);
        created.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        created.Headers.Location!.ToString().ShouldContain("/Admin/Users/Details/");

        Guid userId;
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            userId = (await db.Users.SingleAsync(u => u.Email == email)).Id;
        }

        string details = await client.GetStringAsync(new Uri($"/Admin/Users/Details/{userId}", UriKind.Relative));
        using var delete = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Reason"] = "integration test",
            ["__RequestVerificationToken"] = AntiforgeryToken(details)
        });
        HttpResponseMessage deleted = await client.PostAsync(new Uri($"/Admin/Users/Delete/{userId}", UriKind.Relative), delete);
        deleted.Headers.Location!.ToString().ShouldEndWith("/Admin/Users");

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Users.AnyAsync(u => u.Id == userId)).ShouldBeFalse();
            (await db.DeletedUsers.SingleAsync(d => d.UserId == userId)).Reason.ShouldBe("integration test");
        }
    }

    private async Task ClearMenuAccessAsync(Guid userId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        User user = await db.Users.SingleAsync(u => u.Id == userId);
        user.SetAccess(null, null, null);
        await db.SaveChangesAsync();
    }

    private HttpClient CreateClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password = Password)
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
