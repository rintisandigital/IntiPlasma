using Application.Abstractions.Authentication;
using Application.Users.Logout;
using Application.UnitTests.Abstractions;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class LogoutUserCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_RemoveOnlyTheGivenRefreshToken()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserWithTokensAsync(context, "device-a", "device-b");

        // Act
        Result result = await Handler(context, user.Id).Handle(new LogoutUserCommand("device-a"), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.RefreshTokens.Select(rt => rt.Token).ToListAsync()).ShouldBe(["device-b"]);
    }

    [Fact]
    public async Task Handle_Should_IgnoreTheRefreshTokenOfAnotherUser()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedUserWithTokensAsync(context, "someone-else");

        // Act
        Result result = await Handler(context, Guid.CreateVersion7())
            .Handle(new LogoutUserCommand("someone-else"), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.RefreshTokens.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_Succeed_WhenTheRefreshTokenIsUnknown()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserWithTokensAsync(context);

        // Act
        Result result = await Handler(context, user.Id).Handle(new LogoutUserCommand("unknown"), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    private static LogoutUserCommandHandler Handler(TestDbContext context, Guid userId)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(userId);

        return new LogoutUserCommandHandler(context, userContext);
    }

    private static async Task<User> SeedUserWithTokensAsync(TestDbContext context, params string[] tokens)
    {
        var user = User.Create($"{Guid.NewGuid():N}@example.com", "Test", "User", "hash");
        context.Users.Add(user);

        foreach (string token in tokens)
        {
            context.RefreshTokens.Add(RefreshToken.Create(token, user.Id, DateTime.UtcNow.AddDays(1)));
        }

        await context.SaveChangesAsync();

        return user;
    }
}
