using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Outbox;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages", Schemas.Infrastructure);

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type).HasMaxLength(500);

        builder.Property(m => m.Content).HasColumnType("jsonb");

        builder.HasIndex(m => m.OccurredOnUtc)
            .HasFilter("processed_on_utc IS NULL")
            .HasDatabaseName("ix_outbox_messages_unprocessed");
    }
}
