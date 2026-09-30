using Application.Abstractions.Authentication;
using Domain.Roles;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Database;

public static class DatabaseSeeder
{
    /// <summary>
    /// Idempotent seed: keeps the Administrator role in sync with the permission catalog and creates the
    /// initial administrator from the "Seed:Admin" configuration section (if configured and not yet present).
    /// </summary>
    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = serviceProvider.CreateScope();

        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IConfiguration configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        IPasswordHasher passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        Role? administrator = await dbContext.Roles
            .Include(r => r.Permissions)
            .SingleOrDefaultAsync(r => r.IsSystem && r.Name == Role.AdministratorName, cancellationToken);

        if (administrator is null)
        {
            administrator = Role.CreateAdministrator();
            dbContext.Roles.Add(administrator);
        }
        else
        {
            administrator.SyncSystemPermissions();
        }

        string? email = configuration["Seed:Admin:Email"];
        string? password = configuration["Seed:Admin:Password"];

        if (!string.IsNullOrWhiteSpace(email) &&
            !string.IsNullOrWhiteSpace(password) &&
            !await dbContext.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            var admin = User.Create(email, "System", "Administrator", passwordHasher.Hash(password));
            admin.SetRoles([administrator.Id]);

            dbContext.Users.Add(admin);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
