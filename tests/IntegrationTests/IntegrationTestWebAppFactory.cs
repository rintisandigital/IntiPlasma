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
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}
