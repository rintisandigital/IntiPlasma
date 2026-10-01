using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Storage;
using Application.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel;

namespace Application.UnitTests.Abstractions;

public abstract class BaseHandlerTest
{
    protected static TestDbContext CreateDbContext()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase($"clean-architecture-{Guid.NewGuid()}")
            .Options;

        return new TestDbContext(options);
    }

    /// <summary>
    /// The real attachment service over the test database and an in-memory file storage. By default the user
    /// sees every branch.
    /// </summary>
    protected static IAttachmentService CreateAttachments(
        IApplicationDbContext context,
        IBranchAccess? branchAccess = null,
        Guid? userId = null,
        IFileStorage? storage = null)
    {
        if (branchAccess is null)
        {
            branchAccess = Substitute.For<IBranchAccess>();
            branchAccess.GetScopeAsync(Arg.Any<CancellationToken>()).Returns(BranchScope.All);
        }

        IUserContext user = Substitute.For<IUserContext>();
        user.UserId.Returns(userId ?? Guid.Empty);

        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_ => DateTime.UtcNow);

        return new AttachmentService(
            context, storage ?? new InMemoryFileStorage(), branchAccess, user, clock, NullLogger<AttachmentService>.Instance);
    }

    protected static HybridCache CreateCache()
    {
        var services = new ServiceCollection();

#pragma warning disable EXTEXP0018
        services.AddHybridCache();
#pragma warning restore EXTEXP0018

        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }
}
