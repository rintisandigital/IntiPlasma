using Domain.Finance.Accounts;
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
        builder.ComplexMoney(r => r.Amount, "amount");
        builder.HasIndex(r => r.Number).IsUnique();
        builder.HasIndex(r => new { r.BranchId, r.ReceiptDate });
        builder.HasIndex(r => new { r.CustomerId, r.ReceiptDate });

        builder.HasOne<Branch>().WithMany().HasForeignKey(r => r.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(r => r.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(r => r.CashAccountId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Allocations).WithOne().HasForeignKey(a => a.CustomerReceiptId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Allocations).HasField("_allocations").UsePropertyAccessMode(PropertyAccessMode.Field);
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
