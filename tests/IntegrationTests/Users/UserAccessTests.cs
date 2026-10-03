using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Users;

/// <summary>
/// PhaseW1: branch scope comes from branch access profiles; user administration endpoints.
/// </summary>
public sealed class UserAccessTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private static readonly string[] WarehousePermissions = ["warehouses:manage", "warehouses:read"];

    private readonly IntegrationTestWebAppFactory _factory = factory;

    [Fact]
    public async Task BranchScope_Should_FollowTheBranchAccessProfile_WithoutNewLogin()
    {
        await AuthenticateAsAdminAsync();
        string suffix = Guid.NewGuid().ToString("N")[..6];
        Guid branchA = await PostAsync<Guid>("branches", new { code = $"A{suffix}", name = "Alpha" });
        Guid branchB = await PostAsync<Guid>("branches", new { code = $"B{suffix}", name = "Bravo" });
        Guid roleId = await PostAsync<Guid>("roles", new { name = $"Warehouse {suffix}", permissions = WarehousePermissions });
        Guid profileId = await PostAsync<Guid>("branch-access-profiles", new { name = $"Area {suffix}", allBranches = false, branchIds = new[] { branchA } });

        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        await AuthenticateAsAdminAsync();
        (await HttpClient.PutAsJsonAsync($"users/{userId}/roles", new { roleIds = new[] { roleId } })).EnsureSuccessStatusCode();
        (await HttpClient.PutAsJsonAsync($"users/{userId}/access", new { branchAccessProfileId = profileId, defaultBranchId = branchA }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        string userToken = (await LoginAsync(email)).AccessToken;

        (await PostAsUserAsync(userToken, branchA, suffix + "1")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PostAsUserAsync(userToken, branchB, suffix + "2")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // The profile now also covers Bravo: the same token works for it right away (cache invalidated).
        await AuthenticateAsAdminAsync();
        (await HttpClient.PutAsJsonAsync($"branch-access-profiles/{profileId}",
            new { name = $"Area {suffix}", allBranches = false, branchIds = new[] { branchA, branchB } }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await PostAsUserAsync(userToken, branchB, suffix + "3")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UserWithoutBranchProfile_Should_SeeNoBranch()
    {
        await AuthenticateAsAdminAsync();
        string suffix = Guid.NewGuid().ToString("N")[..6];
        Guid branch = await PostAsync<Guid>("branches", new { code = $"C{suffix}", name = "Charlie" });
        Guid roleId = await PostAsync<Guid>("roles", new { name = $"Wh {suffix}", permissions = WarehousePermissions });
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        await AuthenticateAsAdminAsync();
        (await HttpClient.PutAsJsonAsync($"users/{userId}/roles", new { roleIds = new[] { roleId } })).EnsureSuccessStatusCode();

        string userToken = (await LoginAsync(email)).AccessToken;

        (await PostAsUserAsync(userToken, branch, suffix)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FormerBranchesEndpoint_Should_BeGone()
    {
        await AuthenticateAsAdminAsync();
        Guid userId = await RegisterUserAsync(UniqueEmail());
        await AuthenticateAsAdminAsync();

        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"users/{userId}/branches", new { branchIds = Array.Empty<Guid>() });

        response.IsSuccessStatusCode.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteUser_Should_KeepBackup_AndRevokeTokens()
    {
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        AccessTokens tokens = await LoginAsync(email);
        await AuthenticateAsAdminAsync();

        HttpResponseMessage response = await HttpClient.DeleteAsync(new Uri($"users/{userId}?reason=test", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        HttpResponseMessage refresh = await HttpClient.PostAsJsonAsync("users/refresh-token", new { refreshToken = tokens.RefreshToken });
        refresh.IsSuccessStatusCode.ShouldBeFalse();

        using IServiceScope scope = _factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Users.AnyAsync(u => u.Id == userId)).ShouldBeFalse();
        (await db.DeletedUsers.SingleAsync(d => d.UserId == userId)).Reason.ShouldBe("test");
    }

    [Fact]
    public async Task ListUsers_Should_ReturnPagedUsers()
    {
        await AuthenticateAsAdminAsync();

        HttpResponseMessage response = await HttpClient.GetAsync(new Uri("users?search=admin", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain(IntegrationTestWebAppFactory.AdminEmail);
    }

    private async Task<T> PostAsync<T>(string url, object body)
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(url, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<HttpResponseMessage> PostAsUserAsync(string token, Guid branchId, string code)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "warehouses")
        {
            Content = JsonContent.Create(new { code = $"W{code}", name = "Gudang Induk", branchId, address = (string?)null })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await HttpClient.SendAsync(request);
    }
}
