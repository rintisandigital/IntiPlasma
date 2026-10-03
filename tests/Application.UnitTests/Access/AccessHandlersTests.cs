using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Caching;
using Application.Access.BranchAccessProfiles;
using Application.Access.MenuAccessProfiles;
using Application.Access.Menus;
using Application.UnitTests.Abstractions;
using Application.Users.Manage;
using Domain.Access;
using Domain.MasterData.Branches;
using Domain.Roles;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Access;

public sealed class AccessHandlersTests : BaseHandlerTest
{
    private readonly ICacheInvalidator _cache = Substitute.For<ICacheInvalidator>();

    // ---- Users -----------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateUser_Should_SaveUserWithAccessAndRoles()
    {
        await using TestDbContext context = CreateDbContext();
        (Branch branch, BranchAccessProfile branchProfile) = await SeedBranchProfileAsync(context);
        Role role = Role.Create("Checker", null, []).Value;
        context.Roles.Add(role);
        context.MenuAccessProfiles.Add(MenuAccessProfile.CreateFullAccess());
        await context.SaveChangesAsync();

        Result<Guid> result = await CreateUserHandler(context).Handle(
            new CreateUserCommand("staff@example.com", "Siti", "Rahma", "Password123",
                MenuAccessProfile.FullAccessId, branchProfile.Id, branch.Id, [role.Id]),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        User user = await context.Users.Include(u => u.Roles).SingleAsync(u => u.Id == result.Value);
        user.MenuAccessProfileId.ShouldBe(MenuAccessProfile.FullAccessId);
        user.BranchAccessProfileId.ShouldBe(branchProfile.Id);
        user.DefaultBranchId.ShouldBe(branch.Id);
        user.Roles.Single().RoleId.ShouldBe(role.Id);
    }

    [Fact]
    public async Task CreateUser_Should_RejectDefaultBranch_NotCoveredByProfile()
    {
        await using TestDbContext context = CreateDbContext();
        (_, BranchAccessProfile branchProfile) = await SeedBranchProfileAsync(context);
        var other = Branch.Create("OTH", "Other", null, null);
        context.Branches.Add(other);
        await context.SaveChangesAsync();

        Result<Guid> result = await CreateUserHandler(context).Handle(
            new CreateUserCommand("staff@example.com", "Siti", "Rahma", "Password123", null, branchProfile.Id, other.Id, []),
            CancellationToken.None);

        result.Error.ShouldBe(UserErrors.DefaultBranchNotAccessible);
    }

    [Fact]
    public async Task CreateUser_Should_RejectDuplicateEmail()
    {
        await using TestDbContext context = CreateDbContext();
        context.Users.Add(User.Create("staff@example.com", "A", "B", "hash"));
        await context.SaveChangesAsync();

        Result<Guid> result = await CreateUserHandler(context).Handle(
            new CreateUserCommand("staff@example.com", "Siti", "Rahma", "Password123", null, null, null, []),
            CancellationToken.None);

        result.Error.ShouldBe(UserErrors.EmailNotUnique);
    }

    [Fact]
    public async Task SetUserAccess_Should_KeepTheLastAdministrator()
    {
        await using TestDbContext context = CreateDbContext();
        context.MenuAccessProfiles.Add(MenuAccessProfile.CreateFullAccess());
        User admin = await SeedUserAsync(context, fullAccess: true);

        Result result = await new SetUserAccessCommandHandler(context, _cache).Handle(
            new SetUserAccessCommand(admin.Id, null, null, null), CancellationToken.None);

        result.Error.ShouldBe(UserErrors.LastAdministrator);
    }

    [Fact]
    public async Task SetUserAccess_Should_InvalidateEveryCachedEntryOfTheUser()
    {
        await using TestDbContext context = CreateDbContext();
        (Branch branch, BranchAccessProfile branchProfile) = await SeedBranchProfileAsync(context);
        User user = await SeedUserAsync(context);

        Result result = await new SetUserAccessCommandHandler(context, _cache).Handle(
            new SetUserAccessCommand(user.Id, null, branchProfile.Id, branch.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        foreach (string key in PermissionCacheKeys.AllForUser(user.Id))
        {
            await _cache.Received(1).RemoveAsync(key, Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task DeactivateUser_Should_RejectOwnAccount()
    {
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);

        Result result = await new DeactivateUserCommandHandler(context, CurrentUser(user.Id), _cache).Handle(
            new DeactivateUserCommand(user.Id), CancellationToken.None);

        result.Error.ShouldBe(UserErrors.CannotDeactivateSelf);
    }

    [Fact]
    public async Task DeleteUser_Should_WriteBackupWithRoles_ThenRemoveUserAndTokens()
    {
        await using TestDbContext context = CreateDbContext();
        Role role = Role.Create("Checker", null, []).Value;
        context.Roles.Add(role);
        User user = await SeedUserAsync(context);
        user.SetRoles([role.Id]);
        context.RefreshTokens.Add(RefreshToken.Create("token", user.Id, DateTime.UtcNow.AddDays(7)));
        await context.SaveChangesAsync();
        var adminId = Guid.NewGuid();

        Result result = await DeleteUserHandler(context, adminId).Handle(
            new DeleteUserCommand(user.Id, "left the company"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        (await context.Users.AnyAsync(u => u.Id == user.Id)).ShouldBeFalse();
        (await context.RefreshTokens.AnyAsync()).ShouldBeFalse();

        DeletedUser backup = await context.DeletedUsers.SingleAsync();
        backup.UserId.ShouldBe(user.Id);
        backup.Email.ShouldBe(user.Email);
        backup.RoleNames.ShouldBe(["Checker"]);
        backup.DeletedBy.ShouldBe(adminId);
        backup.Reason.ShouldBe("left the company");
        await _cache.Received(1).RemoveAsync(PermissionCacheKeys.SessionForUser(user.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteUser_Should_RejectOwnAccount_AndLastAdministrator()
    {
        await using TestDbContext context = CreateDbContext();
        User admin = await SeedUserAsync(context, fullAccess: true);

        (await DeleteUserHandler(context, admin.Id).Handle(new DeleteUserCommand(admin.Id, null), CancellationToken.None))
            .Error.ShouldBe(UserErrors.CannotDeleteSelf);

        (await DeleteUserHandler(context, Guid.NewGuid()).Handle(new DeleteUserCommand(admin.Id, null), CancellationToken.None))
            .Error.ShouldBe(UserErrors.LastAdministrator);

        (await context.DeletedUsers.AnyAsync()).ShouldBeFalse();
    }

    // ---- Profiles --------------------------------------------------------------------------------------

    [Fact]
    public async Task DeleteMenuAccessProfile_Should_BeRejected_WhileInUse()
    {
        await using TestDbContext context = CreateDbContext();
        MenuAccessProfile profile = MenuAccessProfile.Create("Staff", null, [], []).Value;
        context.MenuAccessProfiles.Add(profile);
        var user = User.Create("a@b.c", "A", "B", "hash");
        user.SetAccess(profile.Id, null, null);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        Result result = await new DeleteMenuAccessProfileCommandHandler(context).Handle(
            new DeleteMenuAccessProfileCommand(profile.Id), CancellationToken.None);

        result.Error.ShouldBe(MenuAccessProfileErrors.InUse(1));
    }

    [Fact]
    public async Task UpdateBranchAccessProfile_Should_ClearDefaultBranch_NoLongerCovered()
    {
        await using TestDbContext context = CreateDbContext();
        (Branch branchA, BranchAccessProfile profile) = await SeedBranchProfileAsync(context);
        var branchB = Branch.Create("B", "Bravo", null, null);
        context.Branches.Add(branchB);
        User user = await SeedUserAsync(context);
        user.SetAccess(null, profile.Id, branchA.Id);
        await context.SaveChangesAsync();

        Result result = await new UpdateBranchAccessProfileCommandHandler(context, _cache).Handle(
            new UpdateBranchAccessProfileCommand(profile.Id, "Area", null, false, [branchB.Id]), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        (await context.Users.SingleAsync()).DefaultBranchId.ShouldBeNull();
        await _cache.Received(1).RemoveByTagAsync(PermissionCacheKeys.Tag, Arg.Any<CancellationToken>());
    }

    // ---- Menu catalog ----------------------------------------------------------------------------------

    [Fact]
    public async Task SyncMenuCatalog_Should_BeIdempotent_KeepCustomizations_AndRetireMissingMenus()
    {
        await using TestDbContext context = CreateDbContext();
        var handler = new SyncMenuCatalogCommandHandler(context, _cache);
        MenuDefinition group = new("admin", null, "Administration", "ti ti-settings", null, 1, MenuRights.None, true);
        MenuDefinition users = new("admin.users", "admin", "Users", null, "/Admin/Users", 10, MenuRights.Create, true);
        MenuDefinition old = new("admin.old", "admin", "Old", null, "/Admin/Old", 20, MenuRights.None, true);

        (await handler.Handle(new SyncMenuCatalogCommand([group, users, old]), CancellationToken.None)).Value.ShouldBe(3);
        (await handler.Handle(new SyncMenuCatalogCommand([group, users, old]), CancellationToken.None)).Value.ShouldBe(0);

        Menu usersMenu = await context.Menus.SingleAsync(m => m.Code == "admin.users");
        usersMenu.Customize("Pengguna", null, 5, true);
        await context.SaveChangesAsync();

        MenuDefinition renamed = users with { Name = "User Accounts" };
        (await handler.Handle(new SyncMenuCatalogCommand([group, renamed]), CancellationToken.None)).Value.ShouldBe(2);

        (await context.Menus.SingleAsync(m => m.Code == "admin.users")).Name.ShouldBe("Pengguna");
        (await context.Menus.SingleAsync(m => m.Code == "admin.users")).DefaultName.ShouldBe("User Accounts");
        (await context.Menus.SingleAsync(m => m.Code == "admin.old")).InCatalog.ShouldBeFalse();
    }

    // ---- Helpers ---------------------------------------------------------------------------------------

    private static CreateUserCommandHandler CreateUserHandler(TestDbContext context)
    {
        IPasswordHasher hasher = Substitute.For<IPasswordHasher>();
        hasher.Hash(Arg.Any<string>()).Returns("hash");

        return new CreateUserCommandHandler(context, hasher);
    }

    private DeleteUserCommandHandler DeleteUserHandler(TestDbContext context, Guid currentUserId)
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTime.UtcNow);

        return new DeleteUserCommandHandler(context, CurrentUser(currentUserId), clock, _cache);
    }

    private static IUserContext CurrentUser(Guid userId)
    {
        IUserContext user = Substitute.For<IUserContext>();
        user.UserId.Returns(userId);
        return user;
    }

    private static async Task<User> SeedUserAsync(TestDbContext context, bool fullAccess = false)
    {
        var user = User.Create($"{Guid.NewGuid():N}@example.com", "Test", "User", "hash");
        if (fullAccess)
        {
            user.SetAccess(MenuAccessProfile.FullAccessId, null, null);
        }

        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<(Branch Branch, BranchAccessProfile Profile)> SeedBranchProfileAsync(TestDbContext context)
    {
        var branch = Branch.Create("A", "Alpha", null, null);
        context.Branches.Add(branch);
        BranchAccessProfile profile = BranchAccessProfile.Create("Area Alpha", null, false, [branch.Id]).Value;
        context.BranchAccessProfiles.Add(profile);
        await context.SaveChangesAsync();
        return (branch, profile);
    }
}
