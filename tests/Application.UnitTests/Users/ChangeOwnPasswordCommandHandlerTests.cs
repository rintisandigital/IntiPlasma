using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Caching;
using Application.UnitTests.Abstractions;
using Application.Users.ChangePassword;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class ChangeOwnPasswordCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnFailure_WhenCurrentPasswordIsWrong()
    {
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);
        IPasswordHasher hasher = Substitute.For<IPasswordHasher>();
        hasher.Verify("wrong", "hash").Returns(false);

        ChangeOwnPasswordCommandHandler handler = CreateHandler(context, user, hasher, Substitute.For<ICacheInvalidator>());

        Result result = await handler.Handle(new ChangeOwnPasswordCommand("wrong", "NewPassword1"), CancellationToken.None);

        result.Error.ShouldBe(UserErrors.InvalidCurrentPassword);
        (await context.Users.SingleAsync()).PasswordHash.ShouldBe("hash");
    }

    [Fact]
    public async Task Handle_Should_ReplacePassword_RevokeRefreshTokens_AndInvalidateSession()
    {
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);
        string oldStamp = user.SecurityStamp;
        context.RefreshTokens.Add(RefreshToken.Create("token", user.Id, DateTime.UtcNow.AddDays(7)));
        await context.SaveChangesAsync();

        IPasswordHasher hasher = Substitute.For<IPasswordHasher>();
        hasher.Verify("current", "hash").Returns(true);
        hasher.Hash("NewPassword1").Returns("new-hash");
        ICacheInvalidator cache = Substitute.For<ICacheInvalidator>();

        ChangeOwnPasswordCommandHandler handler = CreateHandler(context, user, hasher, cache);

        Result result = await handler.Handle(new ChangeOwnPasswordCommand("current", "NewPassword1"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        User saved = await context.Users.SingleAsync();
        saved.PasswordHash.ShouldBe("new-hash");
        saved.SecurityStamp.ShouldNotBe(oldStamp);
        (await context.RefreshTokens.AnyAsync()).ShouldBeFalse();
        await cache.Received(1).RemoveAsync(PermissionCacheKeys.SessionForUser(user.Id), Arg.Any<CancellationToken>());
    }

    private static ChangeOwnPasswordCommandHandler CreateHandler(
        TestDbContext context,
        User user,
        IPasswordHasher hasher,
        ICacheInvalidator cache)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(user.Id);

        return new ChangeOwnPasswordCommandHandler(context, userContext, hasher, cache);
    }

    private static async Task<User> SeedUserAsync(TestDbContext context)
    {
        var user = User.Create("ppl@example.com", "Budi", "Santoso", "hash");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }
}
