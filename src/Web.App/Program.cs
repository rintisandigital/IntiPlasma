using Application;
using HealthChecks.UI.Client;
using Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Web.App;
using Web.App.Infrastructure.Web;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfig) => loggerConfig.ReadFrom.Configuration(context.Configuration));

builder.Services
    .AddApplication()
    .AddInfrastructureCore(builder.Configuration)
    .AddBackgroundJobs(builder.Configuration)
    .AddWebApp(builder.Configuration);

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Error/{0}");

app.UseSecurityHeaders();

app.UseHttpsRedirection();

app.UseRequestLocalization();

app.UseSerilogRequestLogging();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.MapStaticAssets();

app.MapHealthChecks("health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Main}/{action=Index}/{id?}")
    .WithStaticAssets();

await app.RunAsync();

// REMARK: Required for functional and integration tests to work.
namespace Web.App
{
    public partial class Program;
}
