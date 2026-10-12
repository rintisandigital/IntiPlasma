using System.Security.Claims;
using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Approvals;
using Domain.Access;
using Domain.Approvals;
using Domain.Roles;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;
using Testcontainers.PostgreSql;
using Web.App.Infrastructure.Authorization;

namespace Web.App.IntegrationTests;

/// <summary>
/// Web.App on a throw-away PostgreSQL container. Web.App never migrates, so the factory applies the
/// migrations and the seed (same as Web.Api in development).
/// </summary>
public sealed class WebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@intiplasma.test";
    public const string AdminPassword = "Admin123!";

    /// <summary>
    /// Approver of the default one-level flows seeded for every document type (PLAN-MOBILE M6).
    /// </summary>
    public const string ApproverEmail = "approver@intiplasma.test";
    public const string ApproverPassword = "Approver123!";

    public Guid ApproverId { get; private set; }

    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("intiplasma-webapp")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", _dbContainer.GetConnectionString());
        builder.UseSetting("Seed:Admin:Email", AdminEmail);
        builder.UseSetting("Seed:Admin:Password", AdminPassword);
        builder.UseSetting("BackgroundJobs:Enabled", "false");
        // The test server speaks plain HTTP and every test signs in from the same (unknown) address.
        builder.UseSetting("Security:SecureCookies", "false");
        builder.UseSetting("Security:LoginPermitPerMinute", "100000");
        builder.UseSetting("FileStorage:RootPath", Path.Combine(Path.GetTempPath(), $"intiplasma-webapp-tests-{Guid.NewGuid():N}"));
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();

        await DatabaseSeeder.SeedAsync(Services);

        // The hosted sync may have run before the migration; make the catalog available to every test.
        await MenuCatalogSyncService.SyncAsync(Services.GetRequiredService<IServiceScopeFactory>(), CancellationToken.None);

        await SeedApprovalFlowsAsync(dbContext, scope.ServiceProvider.GetRequiredService<IPasswordHasher>());
    }

    /// <summary>
    /// Approves every level of the document's pending request as the default approver, through the real handler (as
    /// the approver, without a browser session).
    /// </summary>
    public async Task ApproveAsync(Guid documentId)
    {
        while (true)
        {
            using IServiceScope scope = Services.CreateScope();
            ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            Guid? requestId = await dbContext.ApprovalRequests
                .Where(r => r.DocumentId == documentId && r.Status == ApprovalStatus.Pending)
                .Select(r => (Guid?)r.Id)
                .SingleOrDefaultAsync();
            if (requestId is null)
            {
                return;
            }

            scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
            {
                RequestServices = scope.ServiceProvider,
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ApproverId.ToString())], "Test"))
            };

            Result result = await scope.ServiceProvider.GetRequiredService<ICommandHandler<ApproveRequestCommand>>()
                .Handle(new ApproveRequestCommand(requestId.Value), CancellationToken.None);
            if (result.IsFailure)
            {
                throw new InvalidOperationException($"Approval of {documentId} failed: {result.Error.Description}");
            }
        }
    }

    private async Task SeedApprovalFlowsAsync(ApplicationDbContext dbContext, IPasswordHasher hasher)
    {
        Guid managerRole = await dbContext.Roles.Where(r => r.Name == MobileRoles.Manager).Select(r => r.Id).SingleAsync();
        var approver = User.Create(ApproverEmail, "Approver", "Default", hasher.Hash(ApproverPassword));
        approver.SetAccess(MenuAccessProfile.FullAccessId, BranchAccessProfile.AllBranchesId, null);
        approver.SetRoles([managerRole]);
        dbContext.Users.Add(approver);

        foreach (ApprovalDocumentType documentType in Enum.GetValues<ApprovalDocumentType>())
        {
            dbContext.ApprovalFlows.Add(ApprovalFlow.Create(
                documentType, null, $"{documentType} (default)", [new ApprovalLevelDefinition("Manager", [approver.Id])]).Value);
        }

        await dbContext.SaveChangesAsync();
        ApproverId = approver.Id;
    }

    /// <summary>
    /// Adds a user directly in the database. By default the user has Full Access (menus) and no branch profile.
    /// </summary>
    public async Task<User> CreateUserAsync(
        string email, string password, bool active = true, Guid? menuAccessProfileId = null, Guid? branchAccessProfileId = null)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IPasswordHasher hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var user = User.Create(email, "Test", "User", hasher.Hash(password));
        user.SetAccess(menuAccessProfileId ?? MenuAccessProfile.FullAccessId, branchAccessProfileId, null);
        if (!active)
        {
            user.Deactivate();
        }

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        return user;
    }

    /// <summary>
    /// A menu access profile granting the given rights by menu code.
    /// </summary>
    public async Task<Guid> CreateMenuProfileAsync(string name, params (string Code, MenuRights Rights)[] grants)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        List<Menu> menus = await dbContext.Menus.ToListAsync();
        MenuAccessProfile profile = MenuAccessProfile.Create(
            name,
            null,
            grants.Select(g => new MenuAccessGrant(menus.Single(m => m.Code == g.Code).Id, g.Rights)),
            menus).Value;

        dbContext.MenuAccessProfiles.Add(profile);
        await dbContext.SaveChangesAsync();

        return profile.Id;
    }

    public async Task DeactivateAsync(Guid userId)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        User user = await dbContext.Users.SingleAsync(u => u.Id == userId);
        user.Deactivate();

        await dbContext.SaveChangesAsync();
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}

[CollectionDefinition(nameof(WebAppCollection))]
public sealed class WebAppCollection : ICollectionFixture<WebAppFactory>;
