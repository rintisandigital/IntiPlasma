using System.Net;
using MobileApp.Core.Api;
using MobileApp.Core.Contracts;
using MobileApp.Core.Session;

namespace MobileApp.UnitTests.Api;

public sealed class AuthHandlerTests
{
    [Fact]
    public async Task Should_SendTheBearerToken()
    {
        // Arrange
        using FakeApi api = await SignedInAsync(
            (_, _) => StubHttpHandler.Json(new { ok = true }),
            (_, _) => throw new InvalidOperationException("No refresh expected."));

        // Act
        await api.Client.GetAsync<object>("users/me");

        // Assert
        api.Api.Requests.Single().Authorization.ShouldBe("old-access");
    }

    [Fact]
    public async Task Should_RefreshOnce_AndRetryWithTheSameBody_AfterUnauthorized()
    {
        // Arrange
        using FakeApi api = await SignedInAsync(
            AcceptOnlyNewToken,
            (_, _) => StubHttpHandler.Json(new AccessTokens("new-access", "new-refresh")));

        // Act
        ApiResult result = await api.Client.PostAsync(
            "users/me/change-password", new { currentPassword = "a", newPassword = "b" });

        // Assert
        result.IsSuccess.ShouldBeTrue();
        api.Api.Requests.Count.ShouldBe(2);
        api.Api.Requests[1].Authorization.ShouldBe("new-access");
        api.Api.Requests[1].Body.ShouldBe(api.Api.Requests[0].Body);
        api.Auth.Requests.Single().Body!.ShouldContain("old-refresh");
        api.Tokens.AccessToken.ShouldBe("new-access");
        (await api.SecureStore.GetAsync(TokenStore.RefreshTokenKey)).ShouldBe("new-refresh");
    }

    [Fact]
    public async Task Should_RefreshOnlyOnce_ForConcurrentRequests()
    {
        // Arrange
        using FakeApi api = await SignedInAsync(
            AcceptOnlyNewToken,
            (_, _) => StubHttpHandler.Json(new AccessTokens("new-access", "new-refresh")),
            refreshDelay: TimeSpan.FromMilliseconds(50));

        // Act
        ApiResult<object>[] results = await Task.WhenAll(
            Enumerable.Range(0, 5).Select(_ => api.Client.GetAsync<object>("users/me")));

        // Assert
        results.ShouldAllBe(r => r.IsSuccess);
        api.Auth.Requests.Count.ShouldBe(1);
        api.Api.Requests.Count(r => r.Authorization == "new-access").ShouldBe(5);
    }

    [Fact]
    public async Task Should_EndTheSession_WhenTheRefreshTokenIsRejected()
    {
        // Arrange
        using FakeApi api = await SignedInAsync(
            (_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized),
            (_, _) => StubHttpHandler.Problem(HttpStatusCode.BadRequest, "Users.InvalidRefreshToken", "The refresh token is invalid"));

        int expired = 0;
        api.Refresher.SessionExpired += (_, _) => expired++;

        // Act
        ApiResult result = await api.Client.PostAsync("users/logout", new { refreshToken = "x" });

        // Assert
        result.Error!.IsSessionExpired.ShouldBeTrue();
        expired.ShouldBe(1);
        api.Tokens.HasSession.ShouldBeFalse();
        (await api.SecureStore.GetAsync(TokenStore.RefreshTokenKey)).ShouldBeNull();
    }

    [Fact]
    public async Task Should_KeepTheSession_WhenTheRefreshCannotReachTheServer()
    {
        // Arrange
        using FakeApi api = await SignedInAsync(
            (_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized),
            StubHttpHandler.Offline);

        // Act
        ApiResult result = await api.Client.GetAsync<object>("users/me");

        // Assert
        result.Error!.IsNetwork.ShouldBeTrue();
        api.Tokens.RefreshToken.ShouldBe("old-refresh");
    }

    private static HttpResponseMessage AcceptOnlyNewToken(HttpRequestMessage request, string? body) =>
        request.Headers.Authorization?.Parameter == "new-access"
            ? StubHttpHandler.Json(new { ok = true })
            : new HttpResponseMessage(HttpStatusCode.Unauthorized);

    private static async Task<FakeApi> SignedInAsync(
        Func<HttpRequestMessage, string?, HttpResponseMessage> api,
        Func<HttpRequestMessage, string?, HttpResponseMessage> refresh,
        TimeSpan refreshDelay = default)
    {
        var fake = new FakeApi(api, refresh, refreshDelay: refreshDelay);
        await fake.Tokens.SaveAsync(new AccessTokens("old-access", "old-refresh"));

        return fake;
    }
}
