using System.Reflection;
using Application;
using Infrastructure;
using Infrastructure.Health;
using Serilog;
using Web.Api;
using Web.Api.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfig) => loggerConfig.ReadFrom.Configuration(context.Configuration));

builder.Services.AddSwaggerGenWithAuth();

builder.Services
    .AddApplication()
    .AddPresentation()
    .AddInfrastructureCore(builder.Configuration)
    .AddJwtAuthentication(builder.Configuration)
    .AddBackgroundJobs(builder.Configuration);

builder.Services.AddObservability(builder.Configuration, builder.Environment.ApplicationName);

builder.Services.AddRateLimitingInternal(builder.Configuration);

builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());

WebApplication app = builder.Build();

// Every endpoint is versioned under /api/v1; a breaking change gets a new group (e.g. /api/v2).
app.MapEndpoints(app.MapGroup("api/v1"));

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerWithUi();

    app.ApplyMigrations();

    await app.SeedDatabaseAsync();
}
else if (app.Configuration.GetValue<bool>("Database:SeedOnStartup"))
{
    // First production install (docs/DEPLOY.md): after the migrations were applied, add the system profiles, units,
    // chart of accounts and the Seed:Admin user. Idempotent; turn it off again afterwards.
    await app.SeedDatabaseAsync();
}

if (app.Configuration.GetValue<bool>("Seed:DemoData") && !app.Environment.IsProduction())
{
    // Dummy data for demos and manual testing (docs/DEMO-DATA.md), e.g. `dotnet run -- --Seed:DemoData=true`.
    // Only on a database without branches; never in Production.
    await app.SeedDemoDataAsync();
}

app.MapAppHealthChecks();

app.UseRequestContextLogging();

app.UseSerilogRequestLogging();

app.UseExceptionHandler();

app.UseAuthentication();

app.UseAuthorization();

app.UseRateLimiter();

// REMARK: If you want to use Controllers, you'll need this.
app.MapControllers();

await app.RunAsync();

// REMARK: Required for functional and integration tests to work.
namespace Web.Api
{
    public partial class Program;
}
