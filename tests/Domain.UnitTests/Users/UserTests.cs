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
}
