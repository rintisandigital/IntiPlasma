using System.Net;
using MobileApp.Core.Api;
using MobileApp.Core.Contracts;

namespace MobileApp.UnitTests.Api;

public sealed class ApiClientTests
{
    [Theory]
    [InlineData("http://10.0.2.2:5000", "users/me", "http://10.0.2.2:5000/api/v1/users/me")]
    [InlineData("https://api.example.co.id/", "/users/me", "https://api.example.co.id/api/v1/users/me")]
    [InlineData(" https://host/base/ ", "farmers?page=1", "https://host/base/api/v1/farmers?page=1")]
    public void BuildUri_Should_CombineServerPrefixAndPath(string server, string path, string expected) =>
        ApiClient.BuildUri(server, path).ToString().ShouldBe(expected);

    [Fact]
    public async Task GetAsync_Should_ReturnTheBody_WhenSuccessful()
    {
        // Arrange
        using var api = new FakeApi((_, _) => StubHttpHandler.Json(new { accessToken = "a", refreshToken = "r" }));

        // Act
        ApiResult<AccessTokens> result = await api.Client.GetAsync<AccessTokens>("users/me");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(new AccessTokens("a", "r"));
        api.Api.Requests.Single().Path.ShouldBe("/api/v1/users/me");
    }

    [Fact]
    public async Task PostAsync_Should_TranslateProblemDetails()
    {
        // Arrange
        using var api = new FakeApi((_, _) => StubHttpHandler.Problem(
            HttpStatusCode.NotFound, "Users.NotFoundByEmail", "The user with the specified email was not found"));

        // Act
        ApiResult<AccessTokens> result = await api.Client.PostAsync<AccessTokens>(
            "users/login", new { email = "x", password = "y" });

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error!.Status.ShouldBe(404);
        result.Error.Code.ShouldBe("Users.NotFoundByEmail");
        result.Error.Message.ShouldBe("Email atau password salah.");
        result.Error.Detail.ShouldBe("The user with the specified email was not found");
    }

    [Fact]
    public async Task PostAsync_Should_KeepValidationErrors()
    {
        // Arrange
        using var api = new FakeApi((_, _) => StubHttpHandler.Problem(
            HttpStatusCode.BadRequest,
            "Validation.General",
            "One or more validation errors occurred",
            [new { code = "NotEmptyValidator", description = "'Email' must not be empty.", type = 2 }]));

        // Act
        ApiResult result = await api.Client.PostAsync("users/login", new { });

        // Assert
        result.Error!.Message.ShouldBe("Periksa kembali isian Anda.");
        result.Error.FieldErrors.ShouldBe([new ApiFieldError("NotEmptyValidator", "'Email' must not be empty.")]);
    }

    [Fact]
    public async Task SendAsync_Should_ReturnNetworkError_WhenTheServerIsUnreachable()
    {
        // Arrange
        using var api = new FakeApi(StubHttpHandler.Offline);

        // Act
        ApiResult<AccessTokens> result = await api.Client.GetAsync<AccessTokens>("users/me");

        // Assert
        result.Error!.IsNetwork.ShouldBeTrue();
        result.Error.Message.ShouldBe("Tidak dapat terhubung ke server. Periksa koneksi internet Anda.");
    }

    [Fact]
    public async Task SendAsync_Should_ReportSessionExpired_OnUnauthorized()
    {
        // Arrange
        using var api = new FakeApi((_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        // Act
        ApiResult result = await api.Client.PostAsync("users/logout", new { refreshToken = "r" });

        // Assert
        result.Error!.IsSessionExpired.ShouldBeTrue();
    }

    [Fact]
    public async Task SendAsync_Should_FallBackToTheStatus_WhenTheBodyIsNotProblemDetails()
    {
        // Arrange
        using var api = new FakeApi((_, _) => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("<html>Bad gateway</html>")
        });

        // Act
        ApiResult result = await api.Client.PostAsync("users/logout", null);

        // Assert
        result.Error!.Code.ShouldBe("Http.502");
        result.Error.Message.ShouldBe("Server sedang bermasalah. Coba lagi nanti.");
    }
}
