using Application.Abstractions.Authentication;
using Domain.Access;
using Domain.Approvals;
using Domain.Roles;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Web.Api;

namespace IntegrationTests;

public sealed class IntegrationTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@intiplasma.test";
    public const string AdminPassword = "Admin123!";

    /// <summary>
    /// The approver of the default one-level flows seeded for every document type (PLAN-MOBILE M6).
    /// </summary>
    public const string ApproverEmail = "approver@intiplasma.test";
    public const string ApproverPassword = "Approver123!";

    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("clean-architecture")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Database", _dbContainer.GetConnectionString());

        // Provide deterministic JWT settings so tokens can be issued and validated in tests.
        builder.UseSetting("Jwt:Secret", "super-duper-secret-value-that-should-be-in-user-secrets");
        builder.UseSetting("Jwt:Issuer", "clean-architecture");
        builder.UseSetting("Jwt:Audience", "developers");
        builder.UseSetting("Jwt:ExpirationInMinutes", "60");

        builder.UseSetting("Seed:Admin:Email", AdminEmail);
        builder.UseSetting("Seed:Admin:Password", AdminPassword);

        // Relax rate limiting so the test suite is not throttled.
        builder.UseSetting("RateLimiting:Global:PermitLimit", "100000");
        builder.UseSetting("RateLimiting:Authentication:PermitLimit", "100000");

        // Without Web.App the API itself must process the outbox (jobs are off by default in Web.Api).
        builder.UseSetting("BackgroundJobs:Enabled", "true");

        // Uploaded attachments go to a throw-away folder.
        builder.UseSetting("FileStorage:RootPath", Path.Combine(Path.GetTempPath(), $"intiplasma-tests-{Guid.NewGuid():N}"));
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();

        await DatabaseSeeder.SeedAsync(Services);
        await SeedApprovalFlowsAsync();
    }

    /// <summary>
    /// Farmers, coops and contracts need an approval flow (M-31): a default one-level flow decided by a Manager.
    /// </summary>
    private async Task SeedApprovalFlowsAsync()
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IPasswordHasher hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

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
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}
