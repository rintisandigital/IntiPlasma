using Domain.Inventory.GoodsReceipts;
using Domain.Inventory.Stock;
using Domain.Inventory.StockTransfers;
using Domain.MasterData.Branches;
using Domain.MasterData.Items;
using Domain.MasterData.TaxCodes;
using Domain.MasterData.Uoms;
using Domain.MasterData.Vendors;
using Domain.MasterData.Warehouses;
using Domain.Partnership.Cycles;
using Domain.Procurement.PurchaseOrders;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Inventory;

internal static class InventoryPrecision
{
    public const int Quantity = 18;
    public const int QuantityScale = 4;
    public const int UnitCost = 18;
}

internal sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("purchase_orders", Schemas.Procurement);
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Number).HasMaxLength(50);
        builder.Property(o => o.Notes).HasMaxLength(1000);
        builder.Property(o => o.CancellationReason).HasMaxLength(500);
        builder.HasIndex(o => o.Number).IsUnique();
        builder.HasIndex(o => new { o.BranchId, o.OrderDate });

        builder.HasOne<Branch>().WithMany().HasForeignKey(o => o.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Vendor>().WithMany().HasForeignKey(o => o.VendorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Lines).WithOne().HasForeignKey(l => l.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.ToTable("purchase_order_lines", Schemas.Procurement);
        builder.HasKey(l => new { l.PurchaseOrderId, l.LineNumber });
        builder.Property(l => l.Quantity).HasPrecision(InventoryPrecision.Quantity, InventoryPrecision.QuantityScale);
        builder.Property(l => l.QuantityReceived).HasPrecision(InventoryPrecision.Quantity, InventoryPrecision.QuantityScale);
        builder.ComplexMoney(l => l.UnitPrice, "unit_price");

        builder.HasOne<Item>().WithMany().HasForeignKey(l => l.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Uom>().WithMany().HasForeignKey(l => l.UomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaxCode>().WithMany().HasForeignKey(l => l.TaxCodeId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class GoodsReceiptConfiguration : IEntityTypeConfiguration<GoodsReceipt>
{
    public void Configure(EntityTypeBuilder<GoodsReceipt> builder)
    {
        builder.ToTable("goods_receipts", Schemas.Inventory);
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Number).HasMaxLength(50);
        builder.Property(r => r.DeliveryNoteNumber).HasMaxLength(50);
        builder.Property(r => r.Notes).HasMaxLength(1000);
        builder.HasIndex(r => r.Number).IsUnique();
        builder.HasIndex(r => new { r.BranchId, r.ReceiptDate });

        builder.HasOne<Branch>().WithMany().HasForeignKey(r => r.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseOrder>().WithMany().HasForeignKey(r => r.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Vendor>().WithMany().HasForeignKey(r => r.VendorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(r => r.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProductionCycle>().WithMany().HasForeignKey(r => r.CycleId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Lines).WithOne().HasForeignKey(l => l.GoodsReceiptId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class GoodsReceiptLineConfiguration : IEntityTypeConfiguration<GoodsReceiptLine>
{
    public void Configure(EntityTypeBuilder<GoodsReceiptLine> builder)
    {
        builder.ToTable("goods_receipt_lines", Schemas.Inventory);
        builder.HasKey(l => new { l.GoodsReceiptId, l.LineNumber });
        builder.Property(l => l.Quantity).HasPrecision(InventoryPrecision.Quantity, InventoryPrecision.QuantityScale);
        builder.Property(l => l.BaseQuantity).HasPrecision(InventoryPrecision.Quantity, InventoryPrecision.QuantityScale);
        builder.Property(l => l.UnitCost).HasPrecision(InventoryPrecision.UnitCost, StockLedgerEntry.UnitCostDecimals);
        builder.ComplexMoney(l => l.Value, "value");

        builder.HasOne<Item>().WithMany().HasForeignKey(l => l.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Uom>().WithMany().HasForeignKey(l => l.UomId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StockTransferConfiguration : IEntityTypeConfiguration<StockTransfer>
{
    public void Configure(EntityTypeBuilder<StockTransfer> builder)
    {
        builder.ToTable("stock_transfers", Schemas.Inventory);
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Number).HasMaxLength(50);
        builder.Property(t => t.Notes).HasMaxLength(1000);
        builder.HasIndex(t => t.Number).IsUnique();
        builder.HasIndex(t => new { t.BranchId, t.TransferDate });
        builder.HasIndex(t => t.CycleId);

        builder.HasOne<Branch>().WithMany().HasForeignKey(t => t.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(t => t.FromWarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(t => t.ToWarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProductionCycle>().WithMany().HasForeignKey(t => t.CycleId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Lines).WithOne().HasForeignKey(l => l.StockTransferId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(t => t.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class StockTransferLineConfiguration : IEntityTypeConfiguration<StockTransferLine>
{
    public void Configure(EntityTypeBuilder<StockTransferLine> builder)
    {
        builder.ToTable("stock_transfer_lines", Schemas.Inventory);
        builder.HasKey(l => new { l.StockTransferId, l.LineNumber });
        builder.Property(l => l.Quantity).HasPrecision(InventoryPrecision.Quantity, InventoryPrecision.QuantityScale);
        builder.Property(l => l.BaseQuantity).HasPrecision(InventoryPrecision.Quantity, InventoryPrecision.QuantityScale);
        builder.Property(l => l.UnitCost).HasPrecision(InventoryPrecision.UnitCost, StockLedgerEntry.UnitCostDecimals);
        builder.ComplexMoney(l => l.Value, "value");

        builder.HasOne<Item>().WithMany().HasForeignKey(l => l.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Uom>().WithMany().HasForeignKey(l => l.UomId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StockBalanceConfiguration : IEntityTypeConfiguration<StockBalance>
{
    public void Configure(EntityTypeBuilder<StockBalance> builder)
    {
        builder.ToTable("stock_balances", Schemas.Inventory);
        builder.HasKey(b => b.Id);
        builder.HasIndex(b => new { b.WarehouseId, b.ItemId }).IsUnique();
        builder.Property(b => b.Quantity).HasPrecision(InventoryPrecision.Quantity, InventoryPrecision.QuantityScale);
        builder.ComplexMoney(b => b.Value, "value");

        builder.HasOne<Warehouse>().WithMany().HasForeignKey(b => b.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Item>().WithMany().HasForeignKey(b => b.ItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StockLedgerEntryConfiguration : IEntityTypeConfiguration<StockLedgerEntry>
{
    public void Configure(EntityTypeBuilder<StockLedgerEntry> builder)
    {
        builder.ToTable("stock_ledger_entries", Schemas.Inventory);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.SourceType).HasMaxLength(50);
        builder.Property(e => e.SourceNumber).HasMaxLength(50);
        builder.Property(e => e.Quantity).HasPrecision(InventoryPrecision.Quantity, InventoryPrecision.QuantityScale);
        builder.Property(e => e.BalanceQuantity).HasPrecision(InventoryPrecision.Quantity, InventoryPrecision.QuantityScale);
        builder.Property(e => e.UnitCost).HasPrecision(InventoryPrecision.UnitCost, StockLedgerEntry.UnitCostDecimals);
        builder.ComplexMoney(e => e.Value, "value");
        builder.ComplexMoney(e => e.BalanceValue, "balance_value");

        builder.HasIndex(e => new { e.WarehouseId, e.ItemId, e.Date });
        builder.HasIndex(e => e.CycleId);
        builder.HasIndex(e => new { e.SourceType, e.SourceId });

        builder.HasOne<Warehouse>().WithMany().HasForeignKey(e => e.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Item>().WithMany().HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProductionCycle>().WithMany().HasForeignKey(e => e.CycleId).OnDelete(DeleteBehavior.Restrict);
    }
}
