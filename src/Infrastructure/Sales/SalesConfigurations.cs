using Domain.MasterData.Branches;
using Domain.MasterData.Customers;
using Domain.MasterData.Items;
using Domain.MasterData.TaxCodes;
using Domain.Partnership.Cycles;
using Domain.Sales.CreditNotes;
using Domain.Sales.DeliveryOrders;
using Domain.Sales.SalesInvoices;
using Domain.Sales.SalesOrders;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Sales;

internal static class SalesPrecision
{
    public const int WeightKg = 14;
    public const int WeightKgScale = 3;
}

internal sealed class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
{
    public void Configure(EntityTypeBuilder<SalesOrder> builder)
    {
        builder.ToTable("sales_orders", Schemas.Sales);
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Number).HasMaxLength(50);
        builder.Property(o => o.Notes).HasMaxLength(1000);
        builder.Property(o => o.CreditOverrideReason).HasMaxLength(500);
        builder.Property(o => o.CancellationReason).HasMaxLength(500);
        builder.HasIndex(o => o.Number).IsUnique();
        builder.HasIndex(o => new { o.BranchId, o.OrderDate });
        builder.HasIndex(o => new { o.CustomerId, o.Status });

        builder.HasOne<Branch>().WithMany().HasForeignKey(o => o.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Lines).WithOne().HasForeignKey(l => l.SalesOrderId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SalesOrderLineConfiguration : IEntityTypeConfiguration<SalesOrderLine>
{
    public void Configure(EntityTypeBuilder<SalesOrderLine> builder)
    {
        builder.ToTable("sales_order_lines", Schemas.Sales);
        builder.HasKey(l => new { l.SalesOrderId, l.LineNumber });
        builder.Property(l => l.EstimatedWeightKg).HasPrecision(SalesPrecision.WeightKg, SalesPrecision.WeightKgScale);
        builder.Property(l => l.DeliveredWeightKg).HasPrecision(SalesPrecision.WeightKg, SalesPrecision.WeightKgScale);
        builder.ComplexMoney(l => l.PricePerKg, "price_per_kg");

        builder.HasOne<Item>().WithMany().HasForeignKey(l => l.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaxCode>().WithMany().HasForeignKey(l => l.TaxCodeId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DeliveryOrderConfiguration : IEntityTypeConfiguration<DeliveryOrder>
{
    public void Configure(EntityTypeBuilder<DeliveryOrder> builder)
    {
        builder.ToTable("delivery_orders", Schemas.Sales);
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Number).HasMaxLength(50);
        builder.Property(d => d.VehicleNumber).HasMaxLength(20);
        builder.Property(d => d.DriverName).HasMaxLength(100);
        builder.Property(d => d.Notes).HasMaxLength(1000);
        builder.Property(d => d.CancellationReason).HasMaxLength(500);
        builder.HasIndex(d => d.Number).IsUnique();
        builder.HasIndex(d => new { d.BranchId, d.DeliveryDate });
        builder.HasIndex(d => new { d.CustomerId, d.Status });
        builder.HasIndex(d => d.SalesInvoiceId);

        builder.HasOne<Branch>().WithMany().HasForeignKey(d => d.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SalesOrder>().WithMany().HasForeignKey(d => d.SalesOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(d => d.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SalesInvoice>().WithMany().HasForeignKey(d => d.SalesInvoiceId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(d => d.Lines).WithOne().HasForeignKey(l => l.DeliveryOrderId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(d => d.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class DeliveryOrderLineConfiguration : IEntityTypeConfiguration<DeliveryOrderLine>
{
    public void Configure(EntityTypeBuilder<DeliveryOrderLine> builder)
    {
        builder.ToTable("delivery_order_lines", Schemas.Sales);
        builder.HasKey(l => new { l.DeliveryOrderId, l.LineNumber });
        builder.Property(l => l.WeightKg).HasPrecision(SalesPrecision.WeightKg, SalesPrecision.WeightKgScale);
        builder.ComplexMoney(l => l.PricePerKg, "price_per_kg");
        builder.ComplexMoney(l => l.Amount, "amount");

        // A harvest can only be on one delivery order that is not cancelled, even under concurrent requests.
        builder.HasIndex(l => l.HarvestId)
            .IsUnique()
            .HasFilter("is_cancelled = false")
            .HasDatabaseName("ix_delivery_order_lines_harvest_id_active");

        builder.HasIndex(l => l.CycleId);

        builder.HasOne<Item>().WithMany().HasForeignKey(l => l.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CycleHarvest>().WithMany().HasForeignKey(l => l.HarvestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProductionCycle>().WithMany().HasForeignKey(l => l.CycleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaxCode>().WithMany().HasForeignKey(l => l.TaxCodeId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SalesInvoiceConfiguration : IEntityTypeConfiguration<SalesInvoice>
{
    public void Configure(EntityTypeBuilder<SalesInvoice> builder)
    {
        builder.ToTable("sales_invoices", Schemas.Sales);
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Number).HasMaxLength(50);
        builder.Property(i => i.Notes).HasMaxLength(1000);
        builder.Property(i => i.CancellationReason).HasMaxLength(500);
        builder.ComplexMoney(i => i.Subtotal, "subtotal");
        builder.ComplexMoney(i => i.VatAmount, "vat_amount");
        builder.ComplexMoney(i => i.Total, "total");
        builder.ComplexMoney(i => i.PaidAmount, "paid_amount");
        builder.ComplexMoney(i => i.CreditedAmount, "credited_amount");
        builder.HasIndex(i => i.Number).IsUnique().HasFilter("number IS NOT NULL");
        builder.HasIndex(i => new { i.BranchId, i.InvoiceDate });
        builder.HasIndex(i => new { i.CustomerId, i.Status });

        builder.HasOne<Branch>().WithMany().HasForeignKey(i => i.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(i => i.CustomerId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.Lines).WithOne().HasForeignKey(l => l.SalesInvoiceId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(i => i.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SalesInvoiceLineConfiguration : IEntityTypeConfiguration<SalesInvoiceLine>
{
    public void Configure(EntityTypeBuilder<SalesInvoiceLine> builder)
    {
        builder.ToTable("sales_invoice_lines", Schemas.Sales);
        builder.HasKey(l => new { l.SalesInvoiceId, l.LineNumber });
        builder.Property(l => l.WeightKg).HasPrecision(SalesPrecision.WeightKg, SalesPrecision.WeightKgScale);
        builder.Property(l => l.VatRatePercent).HasPrecision(7, 4);
        builder.ComplexMoney(l => l.PricePerKg, "price_per_kg");
        builder.ComplexMoney(l => l.Amount, "amount");
        builder.ComplexMoney(l => l.VatTaxBase, "vat_tax_base");
        builder.ComplexMoney(l => l.VatAmount, "vat_amount");
        builder.ComplexMoney(l => l.CreditedAmount, "credited_amount");
        builder.ComplexMoney(l => l.CostAmount, "cost_amount");
        builder.HasIndex(l => l.DeliveryOrderId);
        builder.HasIndex(l => l.CycleId);

        builder.HasOne<DeliveryOrder>().WithMany().HasForeignKey(l => l.DeliveryOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Item>().WithMany().HasForeignKey(l => l.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProductionCycle>().WithMany().HasForeignKey(l => l.CycleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaxCode>().WithMany().HasForeignKey(l => l.TaxCodeId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SalesCreditNoteConfiguration : IEntityTypeConfiguration<SalesCreditNote>
{
    public void Configure(EntityTypeBuilder<SalesCreditNote> builder)
    {
        builder.ToTable("sales_credit_notes", Schemas.Sales);
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Number).HasMaxLength(50);
        builder.Property(n => n.Reason).HasMaxLength(500);
        builder.ComplexMoney(n => n.Subtotal, "subtotal");
        builder.ComplexMoney(n => n.VatAmount, "vat_amount");
        builder.ComplexMoney(n => n.Total, "total");
        builder.HasIndex(n => n.Number).IsUnique();
        builder.HasIndex(n => n.SalesInvoiceId);
        builder.HasIndex(n => new { n.CustomerId, n.Date });

        builder.HasOne<Branch>().WithMany().HasForeignKey(n => n.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(n => n.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SalesInvoice>().WithMany().HasForeignKey(n => n.SalesInvoiceId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(n => n.Lines).WithOne().HasForeignKey(l => l.SalesCreditNoteId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(n => n.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SalesCreditNoteLineConfiguration : IEntityTypeConfiguration<SalesCreditNoteLine>
{
    public void Configure(EntityTypeBuilder<SalesCreditNoteLine> builder)
    {
        builder.ToTable("sales_credit_note_lines", Schemas.Sales);
        builder.HasKey(l => new { l.SalesCreditNoteId, l.InvoiceLineNumber });
        builder.ComplexMoney(l => l.Amount, "amount");
        builder.ComplexMoney(l => l.VatAmount, "vat_amount");
        builder.HasIndex(l => l.CycleId);

        builder.HasOne<ProductionCycle>().WithMany().HasForeignKey(l => l.CycleId).OnDelete(DeleteBehavior.Restrict);
    }
}
