using Infrastructure.Database;
using Infrastructure.Database.DemoData;
using Microsoft.EntityFrameworkCore;

namespace Web.Api.Extensions;

public static class MigrationExtensions
{
    public static void ApplyMigrations(this IApplicationBuilder app)
    {
        using IServiceScope scope = app.ApplicationServices.CreateScope();

        using ApplicationDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        dbContext.Database.Migrate();
    }

    public static Task SeedDatabaseAsync(this IApplicationBuilder app) =>
        DatabaseSeeder.SeedAsync(app.ApplicationServices);

    public static Task SeedDemoDataAsync(this IApplicationBuilder app) =>
        DemoDataSeeder.SeedAsync(app.ApplicationServices);
}
