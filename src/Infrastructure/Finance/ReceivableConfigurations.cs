using Domain.Finance.Accounts;
using Domain.Finance.CashBank;
using Domain.Finance.Receivables;
using Domain.MasterData.Branches;
using Domain.MasterData.Customers;
using Domain.Sales.SalesInvoices;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Finance;

internal sealed class CustomerReceiptConfiguration : IEntityTypeConfiguration<CustomerReceipt>
{
    public void Configure(EntityTypeBuilder<CustomerReceipt> builder)
    {
        builder.ToTable("customer_receipts", Schemas.Finance);
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Number).HasMaxLength(50);
        builder.Property(r => r.Reference).HasMaxLength(100);
        builder.Property(r => r.Notes).HasMaxLength(1000);
        builder.Property(r => r.VoidReason).HasMaxLength(500);

        // Receipts recorded before phase 6 are posted.
        builder.Property(r => r.Status).HasDefaultValue(CustomerReceiptStatus.Posted).HasSentinel((CustomerReceiptStatus)0);

        builder.ComplexMoney(r => r.Amount, "amount");
        builder.ComplexMoney(r => r.AdvanceAmount, "advance_amount");
        builder.ComplexMoney(r => r.AppliedAdvanceAmount, "applied_advance_amount");
        builder.HasIndex(r => r.Number).IsUnique();
        builder.HasIndex(r => new { r.BranchId, r.ReceiptDate });
        builder.HasIndex(r => new { r.CustomerId, r.ReceiptDate });

        builder.HasOne<Branch>().WithMany().HasForeignKey(r => r.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(r => r.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(r => r.CashAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashBankAccount>().WithMany().HasForeignKey(r => r.CashBankAccountId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Allocations).WithOne().HasForeignKey(a => a.CustomerReceiptId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Allocations).HasField("_allocations").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(r => r.Applications).WithOne().HasForeignKey(a => a.CustomerReceiptId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Applications).HasField("_applications").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class CustomerReceiptAllocationConfiguration : IEntityTypeConfiguration<CustomerReceiptAllocation>
{
    public void Configure(EntityTypeBuilder<CustomerReceiptAllocation> builder)
    {
        builder.ToTable("customer_receipt_allocations", Schemas.Finance);
        builder.HasKey(a => new { a.CustomerReceiptId, a.SalesInvoiceId });
        builder.ComplexMoney(a => a.Amount, "amount");
        builder.HasIndex(a => a.SalesInvoiceId);

        builder.HasOne<SalesInvoice>().WithMany().HasForeignKey(a => a.SalesInvoiceId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CustomerAdvanceApplicationConfiguration : IEntityTypeConfiguration<CustomerAdvanceApplication>
{
    public void Configure(EntityTypeBuilder<CustomerAdvanceApplication> builder)
    {
        builder.ToTable("customer_advance_applications", Schemas.Finance);
        builder.HasKey(a => a.Id);

        // The id is generated in the domain (it is the journal's source id); without this EF would issue an UPDATE.
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.ComplexMoney(a => a.Amount, "amount");
        builder.HasIndex(a => a.SalesInvoiceId);

        builder.HasOne<SalesInvoice>().WithMany().HasForeignKey(a => a.SalesInvoiceId).OnDelete(DeleteBehavior.Restrict);
    }
}
