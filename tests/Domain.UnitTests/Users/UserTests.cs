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
}
