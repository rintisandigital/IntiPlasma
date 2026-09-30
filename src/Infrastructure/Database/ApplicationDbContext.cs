using System.Linq.Expressions;
using Application.Abstractions.Data;
using Domain.Roles;
using Domain.Users;
using Infrastructure.Numbering;
using Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.Database;

// Domain events are not dispatched here: InsertOutboxMessagesInterceptor stores them in the outbox
// in the same transaction, and OutboxProcessor publishes them afterwards (at-least-once delivery).
public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    internal const string ConcurrencyTokenProperty = "Version";

    public DbSet<User> Users { get; set; }

    public DbSet<RefreshToken> RefreshTokens { get; set; }

    public DbSet<Role> Roles { get; set; }

    internal DbSet<OutboxMessage> OutboxMessages { get; set; }

    internal DbSet<DocumentSequence> DocumentSequences { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        modelBuilder.HasDefaultSchema(Schemas.Default);

        foreach (Type clrType in modelBuilder.Model.GetEntityTypes().Select(e => e.ClrType).ToList())
        {
            // Optimistic concurrency on every aggregate, mapped to PostgreSQL's xmin system column.
            if (clrType.IsAssignableTo(typeof(AggregateRoot)))
            {
                modelBuilder.Entity(clrType).Property<uint>(ConcurrencyTokenProperty).IsRowVersion();
            }

            if (clrType.IsAssignableTo(typeof(ISoftDeletable)))
            {
                modelBuilder.Entity(clrType).HasQueryFilter(CreateSoftDeleteFilter(clrType));
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
