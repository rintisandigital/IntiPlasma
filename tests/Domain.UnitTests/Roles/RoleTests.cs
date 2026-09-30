using Domain.Roles;
using SharedKernel;

namespace Domain.UnitTests.Roles;

public sealed class RoleTests
{
    [Fact]
    public void Create_Should_Fail_WhenPermissionIsUnknown()
    {
        Result<Role> result = Role.Create("Gudang", null, [Permissions.UsersRead, "unknown:permission"]);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.UnknownPermission("unknown:permission"));
    }

    [Fact]
    public void Create_Should_DeduplicatePermissions_AndRaiseEvent()
    {
        Result<Role> result = Role.Create("Gudang", null, [Permissions.UsersRead, Permissions.UsersRead]);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Permissions.Count.ShouldBe(1);
        result.Value.DomainEvents.ShouldContain(e => e is RolePermissionsChangedDomainEvent);
    }

    [Fact]
    public void Update_Should_ReplacePermissions()
    {
        Role role = Role.Create("Finance", null, [Permissions.UsersRead]).Value;

        Result result = role.Update("Finance", "Tim keuangan", [Permissions.RolesRead, Permissions.RolesManage]);

        result.IsSuccess.ShouldBeTrue();
        role.Permissions.Select(p => p.Permission)
            .ShouldBe([Permissions.RolesRead, Permissions.RolesManage], ignoreOrder: true);
    }

    [Fact]
    public void Administrator_Should_HaveEveryPermission_AndBeReadOnly()
    {
        var administrator = Role.CreateAdministrator();

        administrator.IsSystem.ShouldBeTrue();
        administrator.Permissions.Select(p => p.Permission).ShouldBe(Permissions.All, ignoreOrder: true);
        administrator.Update("Renamed", null, []).Error.ShouldBe(RoleErrors.SystemRoleIsReadOnly);
    }
}
