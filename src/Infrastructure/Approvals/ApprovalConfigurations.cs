using Domain.Approvals;
using Domain.MasterData.Branches;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Approvals;

internal sealed class ApprovalFlowConfiguration : IEntityTypeConfiguration<ApprovalFlow>
{
    public void Configure(EntityTypeBuilder<ApprovalFlow> builder)
    {
        builder.ToTable("approval_flows", Schemas.Approval);
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Name).HasMaxLength(150);

        // One active flow per document type and branch; the default flow (no branch) counts as one "branch".
        builder.HasIndex(f => new { f.DocumentType, f.BranchId })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasFilter("is_active");

        builder.HasMany(f => f.Levels).WithOne().HasForeignKey(l => l.FlowId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(f => f.Levels).HasField("_levels").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<Branch>().WithMany().HasForeignKey(f => f.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ApprovalFlowLevelConfiguration : IEntityTypeConfiguration<ApprovalFlowLevel>
{
    public void Configure(EntityTypeBuilder<ApprovalFlowLevel> builder)
    {
        builder.ToTable("approval_flow_levels", Schemas.Approval);
        builder.HasKey(l => new { l.FlowId, l.LevelNo });
        builder.Property(l => l.Name).HasMaxLength(100);
    }
}

internal sealed class ApprovalRequestConfiguration : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> builder)
    {
        builder.ToTable("approval_requests", Schemas.Approval);
        builder.HasKey(r => r.Id);
        builder.Property(r => r.DocumentCode).HasMaxLength(40);
        builder.Property(r => r.DocumentTitle).HasMaxLength(200);
        builder.Property(r => r.Summary).HasMaxLength(500);
        builder.Property(r => r.Payload).HasColumnType("jsonb");

        // One pending request per document (PLAN-MOBILE §5.1).
        builder.HasIndex(r => new { r.DocumentType, r.DocumentId })
            .IsUnique()
            .HasFilter("status = 'Pending'")
            .HasDatabaseName("ix_approval_requests_document_pending");
        builder.HasIndex(r => new { r.DocumentId, r.DocumentType });
        builder.HasIndex(r => new { r.Status, r.BranchId });
        builder.HasIndex(r => r.RequestedBy);

        builder.HasMany(r => r.Levels).WithOne().HasForeignKey(l => l.RequestId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Levels).HasField("_levels").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(r => r.Decisions).WithOne().HasForeignKey(d => d.RequestId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Decisions).HasField("_decisions").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<Branch>().WithMany().HasForeignKey(r => r.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.RequestedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApprovalFlow>().WithMany().HasForeignKey(r => r.FlowId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class ApprovalRequestLevelConfiguration : IEntityTypeConfiguration<ApprovalRequestLevel>
{
    public void Configure(EntityTypeBuilder<ApprovalRequestLevel> builder)
    {
        builder.ToTable("approval_request_levels", Schemas.Approval);
        builder.HasKey(l => new { l.RequestId, l.LevelNo });
        builder.Property(l => l.Name).HasMaxLength(100);
    }
}

internal sealed class ApprovalDecisionConfiguration : IEntityTypeConfiguration<ApprovalDecision>
{
    public void Configure(EntityTypeBuilder<ApprovalDecision> builder)
    {
        builder.ToTable("approval_decisions", Schemas.Approval);
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Note).HasMaxLength(500);
        builder.HasIndex(d => d.DecidedBy);

        builder.HasOne<User>().WithMany().HasForeignKey(d => d.DecidedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(d => d.OnBehalfOf).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ApprovalDelegationConfiguration : IEntityTypeConfiguration<ApprovalDelegation>
{
    public void Configure(EntityTypeBuilder<ApprovalDelegation> builder)
    {
        builder.ToTable("approval_delegations", Schemas.Approval);
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Reason).HasMaxLength(300);
        builder.HasIndex(d => d.DelegatorUserId);
        builder.HasIndex(d => d.DelegateUserId);

        builder.HasOne<User>().WithMany().HasForeignKey(d => d.DelegatorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(d => d.DelegateUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
