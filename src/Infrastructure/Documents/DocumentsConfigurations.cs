using Domain.Documents.Attachments;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Documents;

internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("attachments", Schemas.Documents);
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.FileName).HasMaxLength(Attachment.FileNameMaxLength);
        builder.Property(a => a.StoredFileName).HasMaxLength(60);
        builder.Property(a => a.Extension).HasMaxLength(10);
        builder.Property(a => a.ContentType).HasMaxLength(100);
        builder.Property(a => a.StoragePath).HasMaxLength(200);
        builder.Property(a => a.Checksum).HasMaxLength(64);
        builder.Property(a => a.Description).HasMaxLength(Attachment.DescriptionMaxLength);
        builder.HasIndex(a => a.Checksum);

        // Purge job: temporary attachments by age.
        builder.HasIndex(a => new { a.Status, a.UnlinkedAtUtc });
    }
}

internal sealed class AttachmentLinkConfiguration : IEntityTypeConfiguration<AttachmentLink>
{
    public void Configure(EntityTypeBuilder<AttachmentLink> builder)
    {
        builder.ToTable("attachment_links", Schemas.Documents);
        builder.HasKey(l => new { l.OwnerType, l.OwnerId, l.OwnerKey, l.AttachmentId });
        builder.Property(l => l.OwnerType).HasMaxLength(AttachmentOwner.TypeMaxLength);
        builder.Property(l => l.OwnerKey).HasMaxLength(AttachmentOwner.KeyMaxLength);
        builder.HasIndex(l => l.AttachmentId);

        builder.HasOne<Attachment>().WithMany().HasForeignKey(l => l.AttachmentId).OnDelete(DeleteBehavior.Cascade);
    }
}
