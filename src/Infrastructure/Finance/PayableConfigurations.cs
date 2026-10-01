using Domain.Finance.CashBank;
using Domain.Finance.Payables;
using Domain.Inventory.GoodsReceipts;
using Domain.MasterData.Branches;
using Domain.MasterData.Items;
using Domain.MasterData.TaxCodes;
using Domain.MasterData.Uoms;
using Domain.MasterData.Vendors;
using Domain.Procurement.PurchaseOrders;
using Infrastructure.Database;
using Infrastructure.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Finance;

internal sealed class VendorInvoiceConfiguration : IEntityTypeConfiguration<VendorInvoice>
{
    public void Configure(EntityTypeBuilder<VendorInvoice> builder)
    {
        builder.ToTable("vendor_invoices", Schemas.Finance);
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Number).HasMaxLength(50);
        builder.Property(i => i.VendorInvoiceNumber).HasMaxLength(50);
        builder.Property(i => i.TaxInvoiceNumber).HasMaxLength(50);
        builder.Property(i => i.Notes).HasMaxLength(1000);
        builder.Property(i => i.PriceVarianceApprovalReason).HasMaxLength(500);
        builder.Property(i => i.CancellationReason).HasMaxLength(500);
        builder.Property(i => i.IncomeTaxRatePercent).HasPrecision(7, 4);
        builder.Property(i => i.MaxPriceDeviationPercent).HasPrecision(9, 4);
        builder.ComplexMoney(i => i.Subtotal, "subtotal");
        builder.ComplexMoney(i => i.GoodsValue, "goods_value");
        builder.ComplexMoney(i => i.VatAmount, "vat_amount");
        builder.ComplexMoney(i => i.IncomeTaxAmount, "income_tax_amount");
        builder.ComplexMoney(i => i.Total, "total");
        builder.ComplexMoney(i => i.PaidAmount, "paid_amount");
        builder.HasIndex(i => i.Number).IsUnique().HasFilter("number IS NOT NULL");
        builder.HasIndex(i => new { i.BranchId, i.InvoiceDate });
        builder.HasIndex(i => new { i.VendorId, i.Status });

        // A vendor's invoice number is registered once (a cancelled draft frees it).
        builder.HasIndex(i => new { i.VendorId, i.VendorInvoiceNumber })
            .IsUnique()
            .HasFilter("status <> 'Cancelled'")
            .HasDatabaseName("ix_vendor_invoices_vendor_id_vendor_invoice_number_active");

        builder.HasOne<Branch>().WithMany().HasForeignKey(i => i.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Vendor>().WithMany().HasForeignKey(i => i.VendorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaxCode>().WithMany().HasForeignKey(i => i.IncomeTaxCodeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.Lines).WithOne().HasForeignKey(l => l.VendorInvoiceId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(i => i.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class VendorInvoiceLineConfiguration : IEntityTypeConfiguration<VendorInvoiceLine>
{
    public void Configure(EntityTypeBuilder<VendorInvoiceLine> builder)
    {
        builder.ToTable("vendor_invoice_lines", Schemas.Finance);
        builder.HasKey(l => new { l.VendorInvoiceId, l.LineNumber });
        builder.Property(l => l.Quantity).HasPrecision(InventoryPrecision.Quantity, InventoryPrecision.QuantityScale);
        builder.Property(l => l.VatRatePercent).HasPrecision(7, 4);
        builder.Property(l => l.PriceDeviationPercent).HasPrecision(9, 4);
        builder.ComplexMoney(l => l.OrderUnitPrice, "order_unit_price");
        builder.ComplexMoney(l => l.UnitPrice, "unit_price");
        builder.ComplexMoney(l => l.Amount, "amount");
        builder.ComplexMoney(l => l.GoodsValue, "goods_value");
        builder.ComplexMoney(l => l.VatTaxBase, "vat_tax_base");
        builder.ComplexMoney(l => l.VatAmount, "vat_amount");
        builder.HasIndex(l => new { l.GoodsReceiptId, l.GoodsReceiptLineNumber });

        builder.HasOne<GoodsReceipt>().WithMany().HasForeignKey(l => l.GoodsReceiptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseOrder>().WithMany().HasForeignKey(l => l.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Item>().WithMany().HasForeignKey(l => l.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Uom>().WithMany().HasForeignKey(l => l.UomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaxCode>().WithMany().HasForeignKey(l => l.TaxCodeId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PaymentVoucherConfiguration : IEntityTypeConfiguration<PaymentVoucher>
{
    public void Configure(EntityTypeBuilder<PaymentVoucher> builder)
    {
        builder.ToTable("payment_vouchers", Schemas.Finance);
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Number).HasMaxLength(50);
        builder.Property(p => p.Reference).HasMaxLength(100);
        builder.Property(p => p.Notes).HasMaxLength(1000);
        builder.Property(p => p.CancellationReason).HasMaxLength(500);
        builder.ComplexMoney(p => p.Amount, "amount");
        builder.HasIndex(p => p.Number).IsUnique();
        builder.HasIndex(p => new { p.BranchId, p.PaymentDate });
        builder.HasIndex(p => new { p.VendorId, p.Status });

        builder.HasOne<Branch>().WithMany().HasForeignKey(p => p.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Vendor>().WithMany().HasForeignKey(p => p.VendorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashBankAccount>().WithMany().HasForeignKey(p => p.CashBankAccountId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Allocations).WithOne().HasForeignKey(a => a.PaymentVoucherId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Allocations).HasField("_allocations").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PaymentVoucherAllocationConfiguration : IEntityTypeConfiguration<PaymentVoucherAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentVoucherAllocation> builder)
    {
        builder.ToTable("payment_voucher_allocations", Schemas.Finance);
        builder.HasKey(a => new { a.PaymentVoucherId, a.VendorInvoiceId });
        builder.ComplexMoney(a => a.Amount, "amount");
        builder.HasIndex(a => a.VendorInvoiceId);

        builder.HasOne<VendorInvoice>().WithMany().HasForeignKey(a => a.VendorInvoiceId).OnDelete(DeleteBehavior.Restrict);
    }
}
