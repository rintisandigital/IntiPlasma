using System.Text;
using Application.Abstractions.Auditing;
using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Caching;
using Application.Abstractions.Data;
using Application.Abstractions.Numbering;
using Application.Abstractions.Storage;
using Application.Mobile;
using Application.Users;
using Dapper;
using Infrastructure.Auditing;
using Infrastructure.Authentication;
using Infrastructure.Authorization;
using Infrastructure.BackgroundJobs;
using Infrastructure.Caching;
using Infrastructure.Database;
using Infrastructure.Database.Interceptors;
using Infrastructure.Documents;
using Infrastructure.DomainEvents;
using Infrastructure.Health;
using Infrastructure.Numbering;
using Infrastructure.Outbox;
using Infrastructure.Time;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using SharedKernel;

namespace Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Everything every host needs: database, storage, current user, branch access and cache invalidation.
    /// Authentication schemes and background jobs are added by the host (see <see cref="AddJwtAuthentication"/>
    /// and <see cref="AddBackgroundJobs"/>).
    /// </summary>
    public static IServiceCollection AddInfrastructureCore(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services
            .AddServices()
            .AddDatabase(configuration)
            .AddHealthChecks(configuration)
            .AddCurrentUser()
            .AddLockout(configuration)
            .AddAccessControl()
            .AddCacheInvalidation();

    /// <summary>
    /// JWT bearer authentication and <c>{module}:{action}</c> permission policies (Web.Api).
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.RequireHttpsMetadata = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Secret"]!)),
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization();

        services.AddTransient<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddTransient<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();

        return services;
    }

    /// <summary>
    /// Outbox processor and attachment cleanup, registered only when <c>BackgroundJobs:Enabled</c> is true
    /// (Web.App by default; Web.Api only when it runs without Web.App).
    /// </summary>
    public static IServiceCollection AddBackgroundJobs(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        BackgroundJobsOptions options = configuration.GetSection(BackgroundJobsOptions.SectionName)
            .Get<BackgroundJobsOptions>() ?? new BackgroundJobsOptions();

        if (!options.Enabled)
        {
            return services;
        }

        services.AddOptions<OutboxOptions>().Bind(configuration.GetSection(OutboxOptions.SectionName));
        services.AddHostedService<OutboxProcessor>();

        services.AddHostedService<AttachmentCleanupJob>();

        services.AddHealthChecks().AddCheck<OutboxHealthCheck>("outbox");

        return services;
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        services.AddTransient<IDomainEventsDispatcher, DomainEventsDispatcher>();

        services.AddScoped<IDocumentNumberGenerator, DocumentNumberGenerator>();

#pragma warning disable EXTEXP0018 // HybridCache is released; the API is stable in .NET 10.
        services.AddHybridCache();
#pragma warning restore EXTEXP0018

        return services;
    }

    private static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("Database")!;

        // One data source shared by EF Core (write side) and Dapper (read side, outbox).
        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();

        DefaultTypeMap.MatchNamesWithUnderscores = true;
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

        services.AddScoped<AuditableEntitiesInterceptor>();
        services.AddSingleton<InsertOutboxMessagesInterceptor>();

        services.AddDbContext<ApplicationDbContext>(
            (sp, options) => options
                .UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>(), npgsqlOptions =>
                    npgsqlOptions
                        .MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Default)
                        .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(
                    sp.GetRequiredService<AuditableEntitiesInterceptor>(),
                    sp.GetRequiredService<InsertOutboxMessagesInterceptor>()));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddOptions<FileStorageOptions>().Bind(configuration.GetSection(FileStorageOptions.SectionName));
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        return services;
    }

    private static IServiceCollection AddHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddHealthChecks()
            .AddNpgSql(configuration.GetConnectionString("Database")!)
            .AddCheck<FileStorageHealthCheck>("storage")
            .AddCheck<CacheInvalidationHealthCheck>("cache-invalidation");

        return services;
    }

    private static IServiceCollection AddCurrentUser(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, UserContext>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        // Needed by the token use cases (login/refresh) that every host registers via AddApplication.
        services.AddSingleton<ITokenProvider, TokenProvider>();

        services.AddScoped<IAuditTrail, AuditTrail>();

        return services;
    }

    private static IServiceCollection AddLockout(this IServiceCollection services, IConfiguration configuration)
    {
        // Same lockout rules for the Web.App sign-in and the Web.Api login (W10).
        services.AddOptions<LockoutOptions>().Bind(configuration.GetSection(LockoutOptions.SectionName));
        services.AddTransient(sp => sp.GetRequiredService<IOptions<LockoutOptions>>().Value);

        // Refresh token lifetime for the Web.Api login (mobile sessions, PLAN-MOBILE M-17).
        services.AddOptions<RefreshTokenOptions>().Bind(configuration.GetSection(RefreshTokenOptions.SectionName));
        services.AddTransient(sp => sp.GetRequiredService<IOptions<RefreshTokenOptions>>().Value);

        // Thresholds of the mobile dashboard (PLAN-MOBILE M-58).
        services.AddOptions<MobileDashboardOptions>().Bind(configuration.GetSection(MobileDashboardOptions.SectionName));
        services.AddTransient(sp => sp.GetRequiredService<IOptions<MobileDashboardOptions>>().Value);

        return services;
    }

    /// <summary>
    /// Data Protection keys (cookies, antiforgery tokens) stored in PostgreSQL, so sessions survive restarts and
    /// work across replicas of the host (W10).
    /// </summary>
    public static IServiceCollection AddSharedDataProtection(this IServiceCollection services, string applicationName)
    {
        services.AddDataProtection()
            .SetApplicationName(applicationName)
            .PersistKeysToDbContext<ApplicationDbContext>();

        return services;
    }

    private static IServiceCollection AddAccessControl(this IServiceCollection services)
    {
        services.AddScoped<PermissionProvider>();

        services.AddScoped<IBranchAccess, BranchAccess>();

        // PPL scope (PLAN-MOBILE §4.5); Web.App replaces it with UnrestrictedFieldScope.
        services.AddScoped<IFieldScope, FieldScopeProvider>();

        services.AddScoped<IMenuAccessProvider, MenuAccessProvider>();

        return services;
    }

    private static IServiceCollection AddCacheInvalidation(this IServiceCollection services)
    {
        services.AddSingleton<ICacheInvalidator, PostgresCacheInvalidator>();
        services.AddSingleton<CacheInvalidationStatus>();

        services.AddHostedService<CacheInvalidationListener>();

        return services;
    }
}
