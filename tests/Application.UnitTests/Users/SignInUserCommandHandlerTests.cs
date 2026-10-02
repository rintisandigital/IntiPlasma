using Application.Abstractions.Authentication;
using Application.UnitTests.Abstractions;
using Application.Users.SignIn;
using Domain.Users;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class SignInUserCommandHandlerTests : BaseHandlerTest
{
    private const string Email = "admin@example.com";
    private const string Password = "Password123";

    [Fact]
    public async Task Handle_Should_ReturnInvalidCredentials_WhenUserDoesNotExist()
    {
        await using TestDbContext context = CreateDbContext();
        var handler = new SignInUserCommandHandler(context, Hasher(verifies: true));

        Result<SignedInUserResponse> result = await handler.Handle(
            new SignInUserCommand(Email, Password), CancellationToken.None);

        result.Error.ShouldBe(UserErrors.InvalidCredentials);
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidCredentials_WhenPasswordIsWrong()
    {
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context);
        var handler = new SignInUserCommandHandler(context, Hasher(verifies: false));

        Result<SignedInUserResponse> result = await handler.Handle(
            new SignInUserCommand(Email, Password), CancellationToken.None);

        result.Error.ShouldBe(UserErrors.InvalidCredentials);
    }

    [Fact]
    public async Task Handle_Should_ReturnInactive_WhenUserIsDeactivated()
    {
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context, active: false);
        var handler = new SignInUserCommandHandler(context, Hasher(verifies: true));

        Result<SignedInUserResponse> result = await handler.Handle(
            new SignInUserCommand(Email, Password), CancellationToken.None);

        result.Error.ShouldBe(UserErrors.Inactive);
    }

    [Fact]
    public async Task Handle_Should_ReturnUserWithSecurityStamp_WhenCredentialsAreValid()
    {
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);
        var handler = new SignInUserCommandHandler(context, Hasher(verifies: true));

        Result<SignedInUserResponse> result = await handler.Handle(
            new SignInUserCommand(Email, Password), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(user.Id);
        result.Value.SecurityStamp.ShouldBe(user.SecurityStamp);
    }

    private static IPasswordHasher Hasher(bool verifies)
    {
        IPasswordHasher hasher = Substitute.For<IPasswordHasher>();
        hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(verifies);
        return hasher;
    }

    private static async Task<User> SeedUserAsync(TestDbContext context, bool active = true)
    {
        var user = User.Create(Email, "System", "Administrator", "hash");
        if (!active)
        {
            user.Deactivate();
        }

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }
}
