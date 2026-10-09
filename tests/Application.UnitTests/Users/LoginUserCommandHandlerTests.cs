using Application.Abstractions.Authentication;
using Application.Users;
using Application.Users.Login;
using Application.UnitTests.Abstractions;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class LoginUserCommandHandlerTests : BaseHandlerTest
{
    private const string Email = "test@example.com";
    private const string Password = "Password123";

    [Fact]
    public async Task Handle_Should_ReturnFailure_WhenUserDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        LoginUserCommandHandler handler = Handler(context, verifies: true);

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Email, Password),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFoundByEmail);
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_WhenPasswordIsInvalid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context);
        LoginUserCommandHandler handler = Handler(context, verifies: false);

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Email, Password),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFoundByEmail);
    }

    [Fact]
    public async Task Handle_Should_ReturnLockedOut_AfterMaxFailedAttempts()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context);
        LoginUserCommandHandler wrong = Handler(context, verifies: false);

        // Act
        for (int attempt = 1; attempt < 3; attempt++)
        {
            await wrong.Handle(new LoginUserCommand(Email, Password), CancellationToken.None);
        }

        Result<AccessTokensResponse> locking = await wrong.Handle(
            new LoginUserCommand(Email, Password), CancellationToken.None);
        Result<AccessTokensResponse> correctWhileLocked = await Handler(context, verifies: true).Handle(
            new LoginUserCommand(Email, Password), CancellationToken.None);

        // Assert
        locking.Error.Code.ShouldBe("Users.LockedOut");
        correctWhileLocked.Error.Code.ShouldBe("Users.LockedOut");
        (await context.RefreshTokens.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_ReturnTokensAndPersistRefreshToken_WhenCredentialsAreValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context);
        LoginUserCommandHandler handler = Handler(context, verifies: true);

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Email, Password),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("access-token");
        result.Value.RefreshToken.ShouldBe("refresh-token");

        RefreshToken refreshToken = await context.RefreshTokens.SingleAsync();
        refreshToken.Token.ShouldBe("refresh-token");
        refreshToken.ExpiresOnUtc.ShouldBeGreaterThan(DateTime.UtcNow);
        (await context.AuditLogs.SingleAsync()).Action.ShouldBe(CredentialVerifier.SignedIn);
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_WhenUserIsInactive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context, active: false);
        LoginUserCommandHandler handler = Handler(context, verifies: true);

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Email, Password),
            CancellationToken.None);

        // Assert
        result.Error.ShouldBe(UserErrors.Inactive);
        (await context.RefreshTokens.AnyAsync()).ShouldBeFalse();
    }

    private static LoginUserCommandHandler Handler(TestDbContext context, bool verifies)
    {
        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.Create(Arg.Any<User>()).Returns("access-token");
        tokenProvider.GenerateRefreshToken().Returns("refresh-token");

        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(_ => DateTime.UtcNow);

        return new LoginUserCommandHandler(
            context,
            CreateCredentialVerifier(context, verifies, DateTime.UtcNow, maxFailedAttempts: 3, lockoutMinutes: 15),
            tokenProvider,
            dateTimeProvider,
            new RefreshTokenOptions());
    }

    private static async Task SeedUserAsync(TestDbContext context, bool active = true)
    {
        var user = User.Create(Email, "Test", "User", "hash");
        if (!active)
        {
            user.Deactivate();
        }

        context.Users.Add(user);

        await context.SaveChangesAsync();
    }
}
