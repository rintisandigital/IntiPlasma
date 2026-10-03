using Domain.Access;
using Domain.Users;
using SharedKernel;

namespace Domain.UnitTests.Access;

public sealed class MenuTests
{
    private static MenuDefinition Page(string name = "Users", MenuRights supports = MenuRights.Create | MenuRights.Edit) =>
        new("admin.users", "admin", name, null, "/Admin/Users", 10, supports, IsAvailable: true);

    [Fact]
    public void SupportedRights_Should_IncludeView_AndCatalogRights()
    {
        var menu = Menu.Create(Page());

        menu.SupportedRights.ShouldBe(MenuRights.View | MenuRights.Create | MenuRights.Edit);
        menu.IsUsable.ShouldBeTrue();
    }

    [Fact]
    public void Group_Should_SupportNoRight()
    {
        var group = Menu.Create(new MenuDefinition("admin", null, "Administration", "ti ti-settings", null, 1, MenuRights.None, true));

        group.IsGroup.ShouldBeTrue();
        group.SupportedRights.ShouldBe(MenuRights.None);
    }

    [Fact]
    public void Sync_Should_FollowCatalog_UntilCustomized()
    {
        var menu = Menu.Create(Page());

        menu.Sync(Page(name: "User Accounts")).ShouldBeTrue();
        menu.Name.ShouldBe("User Accounts");

        menu.Customize("Pengguna", "ti ti-user", 5, isActive: true).IsSuccess.ShouldBeTrue();
        menu.Sync(Page(name: "Users", supports: MenuRights.Export));

        menu.Name.ShouldBe("Pengguna");
        menu.SortOrder.ShouldBe(5);
        menu.DefaultName.ShouldBe("Users");
        menu.SupportedRights.ShouldBe(MenuRights.View | MenuRights.Export);
    }

    [Fact]
    public void Sync_Should_ReportNoChange_WhenCatalogIsTheSame()
    {
        var menu = Menu.Create(Page());

        menu.Sync(Page()).ShouldBeFalse();
    }

    [Fact]
    public void RemoveFromCatalog_Should_MakeMenuUnusable()
    {
        var menu = Menu.Create(Page());

        menu.RemoveFromCatalog().ShouldBeTrue();
        menu.RemoveFromCatalog().ShouldBeFalse();
        menu.IsUsable.ShouldBeFalse();
    }

    [Fact]
    public void Customize_Should_RequireName()
    {
        var menu = Menu.Create(Page());

        menu.Customize(" ", null, 1, true).Error.ShouldBe(MenuErrors.NameRequired);
    }
}

public sealed class MenuAccessProfileTests
{
    private static readonly Menu Users = Menu.Create(new MenuDefinition(
        "admin.users", "admin", "Users", null, "/Admin/Users", 10, MenuRights.Create | MenuRights.Edit, true));

    private static readonly Menu Reports = Menu.Create(new MenuDefinition(
        "reports.tb", "reports", "Trial Balance", null, "/Reports/TrialBalance", 10, MenuRights.Export, false));

    private static readonly Menu[] Menus = [Users, Reports];

    [Fact]
    public void Create_Should_KeepGrants_AndDropEmptyOnes()
    {
        Result<MenuAccessProfile> result = MenuAccessProfile.Create(
            " Finance Staff ",
            null,
            [new MenuAccessGrant(Users.Id, MenuRights.View | MenuRights.Edit), new MenuAccessGrant(Reports.Id, MenuRights.None)],
            Menus);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Finance Staff");
        result.Value.Items.Single().Rights.ShouldBe(MenuRights.View | MenuRights.Edit);
    }

    [Fact]
    public void Create_Should_RequireView_ForOtherRights()
    {
        Result<MenuAccessProfile> result = MenuAccessProfile.Create(
            "X", null, [new MenuAccessGrant(Users.Id, MenuRights.Edit)], Menus);

        result.Error.ShouldBe(MenuAccessProfileErrors.ViewRequired("admin.users"));
    }

    [Fact]
    public void Create_Should_RejectRight_TheMenuDoesNotOffer()
    {
        Result<MenuAccessProfile> result = MenuAccessProfile.Create(
            "X", null, [new MenuAccessGrant(Users.Id, MenuRights.View | MenuRights.Delete)], Menus);

        result.Error.ShouldBe(MenuAccessProfileErrors.RightNotSupported("admin.users"));
    }

    [Fact]
    public void Create_Should_RejectUnknownMenu()
    {
        var unknown = Guid.NewGuid();

        Result<MenuAccessProfile> result = MenuAccessProfile.Create(
            "X", null, [new MenuAccessGrant(unknown, MenuRights.View)], Menus);

        result.Error.ShouldBe(MenuErrors.NotFound(unknown));
    }

    [Fact]
    public void Update_Should_ReplaceRights()
    {
        MenuAccessProfile profile = MenuAccessProfile.Create(
            "X", null, [new MenuAccessGrant(Users.Id, MenuRights.View)], Menus).Value;

        profile.Update("X", "desc", [new MenuAccessGrant(Reports.Id, MenuRights.View | MenuRights.Export)], Menus)
            .IsSuccess.ShouldBeTrue();

        profile.Items.Single().MenuId.ShouldBe(Reports.Id);
        profile.Description.ShouldBe("desc");
    }

    [Fact]
    public void FullAccess_Should_BeReadOnly()
    {
        var profile = MenuAccessProfile.CreateFullAccess();

        profile.Id.ShouldBe(MenuAccessProfile.FullAccessId);
        profile.Update("Y", null, [], Menus).Error.ShouldBe(MenuAccessProfileErrors.SystemReadOnly);
        profile.EnsureDeletable().Error.ShouldBe(MenuAccessProfileErrors.SystemReadOnly);
    }

    [Fact]
    public void Duplicate_OfFullAccess_Should_GrantEverySupportedRight()
    {
        MenuAccessProfile copy = MenuAccessProfile.CreateFullAccess().Duplicate("Everything", Menus).Value;

        copy.IsSystem.ShouldBeFalse();
        copy.Items.Single(i => i.MenuId == Users.Id).Rights.ShouldBe(MenuRights.View | MenuRights.Create | MenuRights.Edit);
        copy.Items.Single(i => i.MenuId == Reports.Id).Rights.ShouldBe(MenuRights.View | MenuRights.Export);
    }
}

public sealed class BranchAccessProfileTests
{
    [Fact]
    public void Create_Should_RequireBranch_UnlessAllBranches()
    {
        BranchAccessProfile.Create("X", null, allBranches: false, []).Error.ShouldBe(BranchAccessProfileErrors.BranchRequired);
        BranchAccessProfile.Create("X", null, allBranches: true, []).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void AllBranches_Should_ClearList_AndCoverEveryBranch()
    {
        var branchA = Guid.NewGuid();
        BranchAccessProfile profile = BranchAccessProfile.Create("X", null, false, [branchA]).Value;

        profile.Update("X", null, allBranches: true, [branchA]).IsSuccess.ShouldBeTrue();

        profile.Branches.ShouldBeEmpty();
        profile.Covers(Guid.NewGuid()).ShouldBeTrue();
    }

    [Fact]
    public void Covers_Should_FollowTheList()
    {
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        BranchAccessProfile profile = BranchAccessProfile.Create("X", null, false, [branchA, branchA]).Value;

        profile.Branches.Count.ShouldBe(1);
        profile.Covers(branchA).ShouldBeTrue();
        profile.Covers(branchB).ShouldBeFalse();
    }

    [Fact]
    public void AllBranchesProfile_Should_BeReadOnly()
    {
        var profile = BranchAccessProfile.CreateAllBranches();

        profile.Id.ShouldBe(BranchAccessProfile.AllBranchesId);
        profile.Update("Y", null, false, [Guid.NewGuid()]).Error.ShouldBe(BranchAccessProfileErrors.SystemReadOnly);
        profile.EnsureDeletable().Error.ShouldBe(BranchAccessProfileErrors.SystemReadOnly);
    }
}

public sealed class UserAccessTests
{
    [Fact]
    public void SetAccess_Should_ClearDefaultBranch_WithoutBranchProfile()
    {
        var user = User.Create("a@b.c", "A", "B", "hash");

        user.SetAccess(MenuAccessProfile.FullAccessId, null, Guid.NewGuid());

        user.MenuAccessProfileId.ShouldBe(MenuAccessProfile.FullAccessId);
        user.DefaultBranchId.ShouldBeNull();
    }

    [Fact]
    public void DeletedUser_Should_CopyEverything()
    {
        var user = User.Create("a@b.c", "A", "B", "hash");
        var branch = Guid.NewGuid();
        var role = Guid.NewGuid();
        user.SetAccess(MenuAccessProfile.FullAccessId, BranchAccessProfile.AllBranchesId, branch);
        DateTime now = DateTime.UtcNow;
        var deletedBy = Guid.NewGuid();

        var backup = DeletedUser.From(user, [(role, "Checker")], now, deletedBy, " left the company ");

        backup.UserId.ShouldBe(user.Id);
        backup.Email.ShouldBe("a@b.c");
        backup.PasswordHash.ShouldBe("hash");
        backup.DefaultBranchId.ShouldBe(branch);
        backup.RoleIds.ShouldBe([role]);
        backup.RoleNames.ShouldBe(["Checker"]);
        backup.DeletedAtUtc.ShouldBe(now);
        backup.DeletedBy.ShouldBe(deletedBy);
        backup.Reason.ShouldBe("left the company");
    }
}
