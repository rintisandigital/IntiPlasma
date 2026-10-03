using Application.UnitTests.Abstractions;
using Application.Users;
using Application.Users.SignIn;
using Domain.Access;
using Domain.Auditing;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class SignInUserCommandHandlerTests : BaseHandlerTest
{
    private const string Email = "admin@example.com";
    private const string Password = "Password123";

    private static readonly DateTime Now = new(2026, 10, 4, 2, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_Should_ReturnInvalidCredentials_WhenUserDoesNotExist()
    {
        await using TestDbContext context = CreateDbContext();
        SignInUserCommandHandler handler = Handler(context, verifies: true);

        Result<SignedInUserResponse> result = await handler.Handle(
            new SignInUserCommand(Email, Password), CancellationToken.None);

        result.Error.ShouldBe(UserErrors.InvalidCredentials);
        AuditLog entry = await context.AuditLogs.SingleAsync();
        entry.Action.ShouldBe(CredentialVerifier.SignInFailed);
        entry.UserEmail.ShouldBe(Email);
        entry.UserId.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidCredentials_WhenPasswordIsWrong()
    {
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);
        SignInUserCommandHandler handler = Handler(context, verifies: false);

        Result<SignedInUserResponse> result = await handler.Handle(
            new SignInUserCommand(Email, Password), CancellationToken.None);

        result.Error.ShouldBe(UserErrors.InvalidCredentials);
        user.AccessFailedCount.ShouldBe(1);
        (await context.AuditLogs.SingleAsync()).UserId.ShouldBe(user.Id);
    }

    [Fact]
    public async Task Handle_Should_LockOut_AfterMaxFailedAttempts_AndRefuseCorrectPassword()
    {
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);
        SignInUserCommandHandler wrong = Handler(context, verifies: false);

        for (int attempt = 1; attempt < 3; attempt++)
        {
            (await wrong.Handle(new SignInUserCommand(Email, Password), CancellationToken.None))
                .Error.ShouldBe(UserErrors.InvalidCredentials);
        }

        Result<SignedInUserResponse> locking = await wrong.Handle(
            new SignInUserCommand(Email, Password), CancellationToken.None);
        Result<SignedInUserResponse> correctWhileLocked = await Handler(context, verifies: true).Handle(
            new SignInUserCommand(Email, Password), CancellationToken.None);

        locking.Error.Code.ShouldBe("Users.LockedOut");
        locking.Error.Description.ShouldContain("10 minute(s)");
        correctWhileLocked.Error.Code.ShouldBe("Users.LockedOut");
        user.LockoutEndUtc.ShouldBe(Now.AddMinutes(10));
        (await context.AuditLogs.Select(a => a.Action).ToListAsync()).ShouldBe(
            [CredentialVerifier.SignInFailed, CredentialVerifier.SignInFailed, CredentialVerifier.LockedOut,
             CredentialVerifier.SignInRejected],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Handle_Should_SignIn_AfterLockoutExpired_AndResetCount()
    {
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);
        user.RegisterFailedSignIn(Now.AddMinutes(-11), 1, TimeSpan.FromMinutes(10));
        user.RegisterFailedSignIn(Now.AddMinutes(-11), 3, TimeSpan.FromMinutes(10));
        await context.SaveChangesAsync();

        Result<SignedInUserResponse> result = await Handler(context, verifies: true).Handle(
            new SignInUserCommand(Email, Password), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        user.AccessFailedCount.ShouldBe(0);
        user.LockoutEndUtc.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_ReturnInactive_WhenUserIsDeactivated()
    {
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context, active: false);
        SignInUserCommandHandler handler = Handler(context, verifies: true);

        Result<SignedInUserResponse> result = await handler.Handle(
            new SignInUserCommand(Email, Password), CancellationToken.None);

        result.Error.ShouldBe(UserErrors.Inactive);
        (await context.AuditLogs.SingleAsync()).Action.ShouldBe(CredentialVerifier.SignInRejected);
    }

    [Fact]
    public async Task Handle_Should_ReturnUserWithSecurityStamp_WhenCredentialsAreValid()
    {
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);
        SignInUserCommandHandler handler = Handler(context, verifies: true);

        Result<SignedInUserResponse> result = await handler.Handle(
            new SignInUserCommand(Email, Password), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(user.Id);
        result.Value.SecurityStamp.ShouldBe(user.SecurityStamp);
        AuditLog entry = await context.AuditLogs.SingleAsync();
        entry.Category.ShouldBe(AuditCategory.SignIn);
        entry.Action.ShouldBe(CredentialVerifier.SignedIn);
    }

    [Fact]
    public async Task Handle_Should_ReturnNoMenuAccess_WhenUserHasNoMenuAccessProfile()
    {
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context, menuAccess: false);
        SignInUserCommandHandler handler = Handler(context, verifies: true);

        Result<SignedInUserResponse> result = await handler.Handle(
            new SignInUserCommand(Email, Password), CancellationToken.None);

        result.Error.ShouldBe(UserErrors.NoMenuAccess);
    }

    private static SignInUserCommandHandler Handler(TestDbContext context, bool verifies) =>
        new(CreateCredentialVerifier(context, verifies, Now, maxFailedAttempts: 3, lockoutMinutes: 10));

    private static async Task<User> SeedUserAsync(TestDbContext context, bool active = true, bool menuAccess = true)
    {
        var user = User.Create(Email, "System", "Administrator", "hash");
        if (menuAccess)
        {
            user.SetAccess(MenuAccessProfile.FullAccessId, null, null);
        }

        if (!active)
        {
            user.Deactivate();
        }

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }
}
