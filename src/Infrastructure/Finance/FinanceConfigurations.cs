using Domain.Finance.Accounts;
using Domain.Finance.CostCenters;
using Domain.Finance.FiscalPeriods;
using Domain.Finance.JournalMappings;
using Domain.Finance.Journals;
using Domain.Finance.JournalTemplates;
using Domain.MasterData.Branches;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Finance;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts", Schemas.Finance);
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Code).HasMaxLength(20);
        builder.Property(a => a.Name).HasMaxLength(150);
        builder.HasIndex(a => a.Code).IsUnique();
        builder.HasOne<Account>().WithMany().HasForeignKey(a => a.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CostCenterConfiguration : IEntityTypeConfiguration<CostCenter>
{
    public void Configure(EntityTypeBuilder<CostCenter> builder)
    {
        builder.ToTable("cost_centers", Schemas.Finance);
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Code).HasMaxLength(20);
        builder.Property(c => c.Name).HasMaxLength(100);
        builder.HasIndex(c => c.Code).IsUnique();
    }
}

internal sealed class FiscalPeriodConfiguration : IEntityTypeConfiguration<FiscalPeriod>
{
    public void Configure(EntityTypeBuilder<FiscalPeriod> builder)
    {
        builder.ToTable("fiscal_periods", Schemas.Finance);
        builder.HasKey(p => p.Id);
        builder.HasIndex(p => new { p.Year, p.Month }).IsUnique();
        builder.HasIndex(p => p.StartDate).IsUnique();
    }
}

internal sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("journal_entries", Schemas.Finance);
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Number).HasMaxLength(50);
        builder.Property(j => j.Description).HasMaxLength(500);
        builder.Property(j => j.SourceType).HasMaxLength(80);

        builder.HasIndex(j => j.Number).IsUnique().HasFilter("number IS NOT NULL");

        // The auto journal engine creates at most one journal per (event, source document).
        builder.HasIndex(j => new { j.SourceType, j.SourceId }).IsUnique().HasFilter("source_id IS NOT NULL");

        builder.HasIndex(j => new { j.BranchId, j.Date });
        builder.HasIndex(j => new { j.Status, j.Date });

        builder.HasOne<Branch>().WithMany().HasForeignKey(j => j.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<JournalEntry>().WithMany().HasForeignKey(j => j.ReversalOfId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<JournalEntry>().WithMany().HasForeignKey(j => j.ReversedById).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(j => j.Lines).WithOne().HasForeignKey(l => l.JournalEntryId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(j => j.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class JournalLineConfiguration : IEntityTypeConfiguration<JournalLine>
{
    public void Configure(EntityTypeBuilder<JournalLine> builder)
    {
        builder.ToTable("journal_lines", Schemas.Finance);
        builder.HasKey(l => new { l.JournalEntryId, l.LineNumber });
        builder.Property(l => l.Description).HasMaxLength(250);
        builder.ComplexMoney(l => l.Debit, "debit");
        builder.ComplexMoney(l => l.Credit, "credit");
        builder.HasIndex(l => l.AccountId);

        builder.HasOne<Account>().WithMany().HasForeignKey(l => l.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CostCenter>().WithMany().HasForeignKey(l => l.CostCenterId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class JournalTemplateConfiguration : IEntityTypeConfiguration<JournalTemplate>
{
    public void Configure(EntityTypeBuilder<JournalTemplate> builder)
    {
        builder.ToTable("journal_templates", Schemas.Finance);
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(100);
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.HasIndex(t => t.Name).IsUnique();

        builder.HasMany(t => t.Lines).WithOne().HasForeignKey(l => l.JournalTemplateId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(t => t.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class JournalTemplateLineConfiguration : IEntityTypeConfiguration<JournalTemplateLine>
{
    public void Configure(EntityTypeBuilder<JournalTemplateLine> builder)
    {
        builder.ToTable("journal_template_lines", Schemas.Finance);
        builder.HasKey(l => new { l.JournalTemplateId, l.LineNumber });
        builder.Property(l => l.Description).HasMaxLength(250);
        builder.HasOne<Account>().WithMany().HasForeignKey(l => l.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CostCenter>().WithMany().HasForeignKey(l => l.CostCenterId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class JournalMappingConfiguration : IEntityTypeConfiguration<JournalMapping>
{
    public void Configure(EntityTypeBuilder<JournalMapping> builder)
    {
        builder.ToTable("journal_mappings", Schemas.Finance);
        builder.HasKey(m => m.Id);
        builder.Property(m => m.EventType).HasMaxLength(50);
        builder.Property(m => m.Description).HasMaxLength(500);

        // One default mapping (branch_id NULL) and at most one override per branch for each event.
        builder.HasIndex(m => new { m.EventType, m.BranchId }).IsUnique().AreNullsDistinct(false);

        builder.HasOne<Branch>().WithMany().HasForeignKey(m => m.BranchId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(m => m.Lines).WithOne().HasForeignKey(l => l.JournalMappingId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(m => m.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class JournalMappingLineConfiguration : IEntityTypeConfiguration<JournalMappingLine>
{
    public void Configure(EntityTypeBuilder<JournalMappingLine> builder)
    {
        builder.ToTable("journal_mapping_lines", Schemas.Finance);
        builder.HasKey(l => new { l.JournalMappingId, l.Component });
        builder.Property(l => l.Component).HasMaxLength(50);
        builder.HasOne<Account>().WithMany().HasForeignKey(l => l.DebitAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(l => l.CreditAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CostCenter>().WithMany().HasForeignKey(l => l.CostCenterId).OnDelete(DeleteBehavior.Restrict);
    }
}
