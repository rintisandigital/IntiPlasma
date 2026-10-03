using Application.Abstractions.Authentication;
using Application.Abstractions.Behaviors;
using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.UnitTests.Abstractions;
using Application.Users.Manage;
using Domain.Access;
using Domain.Auditing;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Auditing;

public sealed class AuditDecoratorTests : BaseHandlerTest
{
    [Fact]
    public async Task AuditedCommand_Should_RecordAccessChange_WithCommandValues()
    {
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);
        var handler = new AuditDecorator.CommandBaseHandler<SetUserAccessCommand>(
            new SetUserAccessCommandHandler(context, Substitute.For<ICacheInvalidator>()),
            new TestAuditTrail(context),
            context);

        Result result = await handler.Handle(
            new SetUserAccessCommand(user.Id, MenuAccessProfile.FullAccessId, null, null), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        AuditLog entry = await context.AuditLogs.SingleAsync();
        entry.Category.ShouldBe(AuditCategory.Access);
        entry.Action.ShouldBe("SetUserAccess");
        entry.Summary.ShouldBe("Set user access (User)");
        entry.EntityType.ShouldBe("User");
        entry.EntityId.ShouldBe(user.Id);
        entry.Details!.ShouldContain(MenuAccessProfile.FullAccessId.ToString());
    }

    [Fact]
    public async Task AuditedCommand_Should_UseCreatedId_AsEntityId()
    {
        await using TestDbContext context = CreateDbContext();
        context.MenuAccessProfiles.Add(MenuAccessProfile.CreateFullAccess());
        await context.SaveChangesAsync();
        IPasswordHasher hasher = Substitute.For<IPasswordHasher>();
        hasher.Hash(Arg.Any<string>()).Returns("hash");
        var handler = new AuditDecorator.CommandHandler<CreateUserCommand, Guid>(
            new CreateUserCommandHandler(context, hasher),
            new TestAuditTrail(context),
            context);

        Result<Guid> result = await handler.Handle(
            new CreateUserCommand("staff@example.com", "Siti", "Rahma", "Password123",
                MenuAccessProfile.FullAccessId, null, null, []),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        AuditLog entry = await context.AuditLogs.SingleAsync();
        entry.Action.ShouldBe("CreateUser");
        entry.EntityId.ShouldBe(result.Value);
    }

    [Fact]
    public async Task FailedCommand_Should_NotBeRecorded()
    {
        await using TestDbContext context = CreateDbContext();
        var handler = new AuditDecorator.CommandBaseHandler<UnlockUserCommand>(
            new UnlockUserCommandHandler(context),
            new TestAuditTrail(context),
            context);

        Result result = await handler.Handle(new UnlockUserCommand(Guid.NewGuid()), CancellationToken.None);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        (await context.AuditLogs.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task CommandWithoutMarker_Should_NotBeRecorded()
    {
        await using TestDbContext context = CreateDbContext();
        ICommandHandler<PlainCommand> inner = Substitute.For<ICommandHandler<PlainCommand>>();
        inner.Handle(Arg.Any<PlainCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        var handler = new AuditDecorator.CommandBaseHandler<PlainCommand>(inner, new TestAuditTrail(context), context);

        (await handler.Handle(new PlainCommand(), CancellationToken.None)).IsSuccess.ShouldBeTrue();

        (await context.AuditLogs.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task UnlockUser_Should_LiftLockout()
    {
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);
        user.RegisterFailedSignIn(DateTime.UtcNow, 1, TimeSpan.FromMinutes(15));
        await context.SaveChangesAsync();

        Result result = await new UnlockUserCommandHandler(context).Handle(
            new UnlockUserCommand(user.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        user.IsLockedOut(DateTime.UtcNow).ShouldBeFalse();
    }

    public sealed record PlainCommand : ICommand;

    private static async Task<User> SeedUserAsync(TestDbContext context)
    {
        context.MenuAccessProfiles.Add(MenuAccessProfile.CreateFullAccess());
        var user = User.Create($"{Guid.NewGuid():N}@example.com", "Test", "User", "hash");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }
}
