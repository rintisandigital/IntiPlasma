using System.Linq.Expressions;
using Application.Abstractions.Data;
using Domain.Finance.Accounts;
using Domain.Finance.CostCenters;
using Domain.Finance.FiscalPeriods;
using Domain.Finance.JournalMappings;
using Domain.Finance.Journals;
using Domain.Finance.JournalTemplates;
using Domain.MasterData.Branches;
using Domain.MasterData.Coops;
using Domain.MasterData.Customers;
using Domain.MasterData.Farmers;
using Domain.MasterData.Items;
using Domain.MasterData.TaxCodes;
using Domain.MasterData.Uoms;
using Domain.MasterData.Vendors;
using Domain.MasterData.Warehouses;
using Domain.Partnership.Contracts;
using Domain.Partnership.Cycles;
using Domain.Roles;
using Domain.Users;
using Infrastructure.Numbering;
using Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SharedKernel;

namespace Infrastructure.Database;

// Domain events are not dispatched here: InsertOutboxMessagesInterceptor stores them in the outbox
// in the same transaction, and OutboxProcessor publishes them afterwards (at-least-once delivery).
public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    internal const string ConcurrencyTokenProperty = "Version";

    private const int EnumMaxLength = 30;

    public DbSet<User> Users { get; set; }

    public DbSet<RefreshToken> RefreshTokens { get; set; }

    public DbSet<Role> Roles { get; set; }

    public DbSet<Branch> Branches { get; set; }

    public DbSet<Uom> Uoms { get; set; }

    public DbSet<TaxCode> TaxCodes { get; set; }

    public DbSet<Item> Items { get; set; }

    public DbSet<Warehouse> Warehouses { get; set; }

    public DbSet<Vendor> Vendors { get; set; }

    public DbSet<Customer> Customers { get; set; }

    public DbSet<Farmer> Farmers { get; set; }

    public DbSet<Coop> Coops { get; set; }

    public DbSet<PartnershipContract> Contracts { get; set; }

    public DbSet<ProductionCycle> ProductionCycles { get; set; }

    public DbSet<Account> Accounts { get; set; }

    public DbSet<CostCenter> CostCenters { get; set; }

    public DbSet<FiscalPeriod> FiscalPeriods { get; set; }

    public DbSet<JournalEntry> JournalEntries { get; set; }

    public DbSet<JournalTemplate> JournalTemplates { get; set; }

    public DbSet<JournalMapping> JournalMappings { get; set; }

    internal DbSet<OutboxMessage> OutboxMessages { get; set; }

    internal DbSet<DocumentSequence> DocumentSequences { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        modelBuilder.HasDefaultSchema(Schemas.Default);

        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            Type clrType = entityType.ClrType;

            // Optimistic concurrency on every aggregate, mapped to PostgreSQL's xmin system column.
            if (clrType.IsAssignableTo(typeof(AggregateRoot)))
            {
                modelBuilder.Entity(clrType).Property<uint>(ConcurrencyTokenProperty).IsRowVersion();
            }

            if (clrType.IsAssignableTo(typeof(ISoftDeletable)))
            {
                modelBuilder.Entity(clrType).HasQueryFilter(CreateSoftDeleteFilter(clrType));
            }

            // Enums are stored by name: readable in SQL reports and safe against reordering.
            foreach (IMutableProperty property in entityType.GetProperties()
                         .Where(p => (Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType).IsEnum))
            {
                property.SetProviderClrType(typeof(string));
                property.SetMaxLength(EnumMaxLength);
            }
        }
    }

    private static LambdaExpression CreateSoftDeleteFilter(Type clrType)
    {
        ParameterExpression parameter = Expression.Parameter(clrType, "e");
        MemberExpression isDeleted = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));

        return Expression.Lambda(Expression.Not(isDeleted), parameter);
    }
}
