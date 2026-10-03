using Domain.Auditing;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Auditing;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs", Schemas.Infrastructure);

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Action).HasMaxLength(AuditLog.ActionMaxLength);
        builder.Property(a => a.UserEmail).HasMaxLength(AuditLog.UserEmailMaxLength);
        builder.Property(a => a.EntityType).HasMaxLength(AuditLog.EntityTypeMaxLength);
        builder.Property(a => a.Summary).HasMaxLength(AuditLog.SummaryMaxLength);
        builder.Property(a => a.Details).HasColumnType("jsonb");
        builder.Property(a => a.IpAddress).HasMaxLength(AuditLog.IpAddressMaxLength);
        builder.Property(a => a.Source).HasMaxLength(AuditLog.SourceMaxLength);

        builder.HasIndex(a => a.OccurredAtUtc).IsDescending();
        builder.HasIndex(a => new { a.UserId, a.OccurredAtUtc });
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
    }
}
