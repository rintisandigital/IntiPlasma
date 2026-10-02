using Application.Abstractions.Authentication;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Web.App.IntegrationTests;

/// <summary>
/// Web.App on a throw-away PostgreSQL container. Web.App never migrates, so the factory applies the
/// migrations and the seed (same as Web.Api in development).
/// </summary>
public sealed class WebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@intiplasma.test";
    public const string AdminPassword = "Admin123!";

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
        builder.UseSetting("FileStorage:RootPath", Path.Combine(Path.GetTempPath(), $"intiplasma-webapp-tests-{Guid.NewGuid():N}"));
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();

        await DatabaseSeeder.SeedAsync(Services);
    }

    /// <summary>
    /// Adds a user directly in the database (user management screens arrive in W1).
    /// </summary>
    public async Task<User> CreateUserAsync(string email, string password, bool active = true)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IPasswordHasher hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var user = User.Create(email, "Test", "User", hasher.Hash(password));
        if (!active)
        {
            user.Deactivate();
        }

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        return user;
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
