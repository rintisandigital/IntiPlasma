using Domain.Users;

namespace Domain.UnitTests.Users;

public sealed class UserTests
{
    [Fact]
    public void Create_Should_RaiseUserRegisteredDomainEvent()
    {
        var user = User.Create("ppl@example.com", "Budi", "Santoso", "hash");

        user.Id.ShouldNotBe(Guid.Empty);
        user.DomainEvents.OfType<UserRegisteredDomainEvent>().Single().UserId.ShouldBe(user.Id);
    }

    [Fact]
    public void SetRoles_Should_ReplaceRoles_WithoutDuplicates()
    {
        var user = User.Create("ppl@example.com", "Budi", "Santoso", "hash");
        var roleA = Guid.NewGuid();
        var roleB = Guid.NewGuid();
        var roleC = Guid.NewGuid();

        user.SetRoles([roleA, roleB, roleB]);
        user.SetRoles([roleB, roleC]);

        user.Roles.Select(r => r.RoleId).ShouldBe([roleB, roleC], ignoreOrder: true);
    }

    [Fact]
    public void Create_Should_StartActive_WithSecurityStamp()
    {
        var user = User.Create("ppl@example.com", "Budi", "Santoso", "hash");

        user.IsActive.ShouldBeTrue();
        user.SecurityStamp.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ChangePassword_Should_ReplaceHash_AndRenewSecurityStamp()
    {
        var user = User.Create("ppl@example.com", "Budi", "Santoso", "hash");
        string stamp = user.SecurityStamp;

        user.ChangePassword("new-hash");

        user.PasswordHash.ShouldBe("new-hash");
        user.SecurityStamp.ShouldNotBe(stamp);
    }

    [Fact]
    public void Deactivate_Should_RenewSecurityStamp_AndActivate_Should_KeepIt()
    {
        var user = User.Create("ppl@example.com", "Budi", "Santoso", "hash");
        string stamp = user.SecurityStamp;

        user.Deactivate();
        string deactivatedStamp = user.SecurityStamp;
        user.Activate();

        user.IsActive.ShouldBeTrue();
        deactivatedStamp.ShouldNotBe(stamp);
        user.SecurityStamp.ShouldBe(deactivatedStamp);
    }

    [Fact]
    public void Deactivate_Should_BeIdempotent()
    {
        var user = User.Create("ppl@example.com", "Budi", "Santoso", "hash");
        user.Deactivate();
        string stamp = user.SecurityStamp;

        user.Deactivate();

        user.IsActive.ShouldBeFalse();
        user.SecurityStamp.ShouldBe(stamp);
    }

    [Fact]
    public void RegisterFailedSignIn_Should_LockOut_WhenMaxAttemptsReached()
    {
        var user = User.Create("ppl@example.com", "Budi", "Santoso", "hash");
        var now = new DateTime(2026, 10, 4, 1, 0, 0, DateTimeKind.Utc);

        for (int attempt = 1; attempt < 5; attempt++)
        {
            user.RegisterFailedSignIn(now, 5, TimeSpan.FromMinutes(15)).ShouldBeFalse();
        }

        user.AccessFailedCount.ShouldBe(4);
        user.IsLockedOut(now).ShouldBeFalse();

        user.RegisterFailedSignIn(now, 5, TimeSpan.FromMinutes(15)).ShouldBeTrue();

        user.AccessFailedCount.ShouldBe(0);
        user.IsLockedOut(now.AddMinutes(14)).ShouldBeTrue();
        user.IsLockedOut(now.AddMinutes(15)).ShouldBeFalse();
    }

    [Fact]
    public void RegisterSuccessfulSignIn_Should_ResetFailedCount()
    {
        var user = User.Create("ppl@example.com", "Budi", "Santoso", "hash");
        DateTime now = DateTime.UtcNow;
        user.RegisterFailedSignIn(now, 5, TimeSpan.FromMinutes(15));
        user.RegisterFailedSignIn(now, 5, TimeSpan.FromMinutes(15));

        user.RegisterSuccessfulSignIn();

        user.AccessFailedCount.ShouldBe(0);
        user.LockoutEndUtc.ShouldBeNull();
    }

    [Fact]
    public void Unlock_ChangePassword_Activate_Should_LiftLockout()
    {
        DateTime now = DateTime.UtcNow;
        User Locked()
        {
            var user = User.Create("ppl@example.com", "Budi", "Santoso", "hash");
            user.RegisterFailedSignIn(now, 1, TimeSpan.FromMinutes(15));
            user.IsLockedOut(now).ShouldBeTrue();
            return user;
        }

        User unlocked = Locked();
        unlocked.Unlock();
        User passwordReset = Locked();
        passwordReset.ChangePassword("new-hash");
        User activated = Locked();
        activated.Deactivate();
        activated.Activate();

        unlocked.IsLockedOut(now).ShouldBeFalse();
        passwordReset.IsLockedOut(now).ShouldBeFalse();
        activated.IsLockedOut(now).ShouldBeFalse();
    }
}
