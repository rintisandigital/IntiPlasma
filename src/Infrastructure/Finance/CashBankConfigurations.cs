using Domain.Finance.Accounts;
using Domain.Finance.CashBank;
using Domain.Finance.CostCenters;
using Domain.Finance.Journals;
using Domain.MasterData.Branches;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Finance;

internal sealed class CashBankAccountConfiguration : IEntityTypeConfiguration<CashBankAccount>
{
    public void Configure(EntityTypeBuilder<CashBankAccount> builder)
    {
        builder.ToTable("cash_bank_accounts", Schemas.Finance);
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Code).HasMaxLength(20);
        builder.Property(a => a.Name).HasMaxLength(150);
        builder.Property(a => a.BankName).HasMaxLength(100);
        builder.Property(a => a.AccountNumber).HasMaxLength(40);
        builder.HasIndex(a => a.Code).IsUnique();

        // One cash/bank account per ledger account, so the cash & bank ledger is the account's general ledger.
        builder.HasIndex(a => a.AccountId).IsUnique();
        builder.HasIndex(a => a.BranchId);

        builder.HasOne<Branch>().WithMany().HasForeignKey(a => a.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(a => a.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CashTransactionConfiguration : IEntityTypeConfiguration<CashTransaction>
{
    public void Configure(EntityTypeBuilder<CashTransaction> builder)
    {
        builder.ToTable("cash_transactions", Schemas.Finance);
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Number).HasMaxLength(50);
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.Property(t => t.Reference).HasMaxLength(100);
        builder.Property(t => t.CancellationReason).HasMaxLength(500);
        builder.ComplexMoney(t => t.Amount, "amount");
        builder.HasIndex(t => t.Number).IsUnique().HasFilter("number IS NOT NULL");
        builder.HasIndex(t => new { t.CashBankAccountId, t.Date });
        builder.HasIndex(t => new { t.BranchId, t.Date });

        builder.HasOne<Branch>().WithMany().HasForeignKey(t => t.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashBankAccount>().WithMany().HasForeignKey(t => t.CashBankAccountId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Lines).WithOne().HasForeignKey(l => l.CashTransactionId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(t => t.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class CashTransactionLineConfiguration : IEntityTypeConfiguration<CashTransactionLine>
{
    public void Configure(EntityTypeBuilder<CashTransactionLine> builder)
    {
        builder.ToTable("cash_transaction_lines", Schemas.Finance);
        builder.HasKey(l => new { l.CashTransactionId, l.LineNumber });
        builder.Property(l => l.Description).HasMaxLength(250);
        builder.ComplexMoney(l => l.Amount, "amount");

        builder.HasOne<Account>().WithMany().HasForeignKey(l => l.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CostCenter>().WithMany().HasForeignKey(l => l.CostCenterId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class BankTransferConfiguration : IEntityTypeConfiguration<BankTransfer>
{
    public void Configure(EntityTypeBuilder<BankTransfer> builder)
    {
        builder.ToTable("bank_transfers", Schemas.Finance);
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Number).HasMaxLength(50);
        builder.Property(t => t.Reference).HasMaxLength(100);
        builder.Property(t => t.Notes).HasMaxLength(1000);
        builder.ComplexMoney(t => t.Amount, "amount");
        builder.HasIndex(t => t.Number).IsUnique();
        builder.HasIndex(t => new { t.BranchId, t.Date });

        builder.HasOne<Branch>().WithMany().HasForeignKey(t => t.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashBankAccount>().WithMany().HasForeignKey(t => t.FromCashBankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashBankAccount>().WithMany().HasForeignKey(t => t.ToCashBankAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class BankReconciliationConfiguration : IEntityTypeConfiguration<BankReconciliation>
{
    public void Configure(EntityTypeBuilder<BankReconciliation> builder)
    {
        builder.ToTable("bank_reconciliations", Schemas.Finance);
        builder.HasKey(r => r.Id);
        builder.ComplexMoney(r => r.StatementBalance, "statement_balance");
        builder.HasIndex(r => new { r.CashBankAccountId, r.StatementDate });

        // At most one reconciliation in progress per bank account.
        builder.HasIndex(r => r.CashBankAccountId)
            .IsUnique()
            .HasFilter("status = 'InProgress'")
            .HasDatabaseName("ix_bank_reconciliations_cash_bank_account_id_in_progress");

        builder.HasOne<Branch>().WithMany().HasForeignKey(r => r.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashBankAccount>().WithMany().HasForeignKey(r => r.CashBankAccountId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Lines).WithOne().HasForeignKey(l => l.BankReconciliationId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class BankStatementLineConfiguration : IEntityTypeConfiguration<BankStatementLine>
{
    public void Configure(EntityTypeBuilder<BankStatementLine> builder)
    {
        builder.ToTable("bank_statement_lines", Schemas.Finance);
        builder.HasKey(l => new { l.BankReconciliationId, l.LineNumber });
        builder.Property(l => l.Description).HasMaxLength(250);
        builder.ComplexMoney(l => l.Amount, "amount");

        // A ledger line clears on one bank statement line only, across all reconciliations.
        builder.HasIndex(l => new { l.MatchedJournalEntryId, l.MatchedJournalLineNumber })
            .IsUnique()
            .HasFilter("matched_journal_entry_id IS NOT NULL");

        builder.HasOne<JournalEntry>().WithMany().HasForeignKey(l => l.MatchedJournalEntryId).OnDelete(DeleteBehavior.Restrict);
    }
}
