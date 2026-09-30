using Domain.Inventory.Stock;
using Domain.Inventory.StockReturns;
using Domain.MasterData.Branches;
using Domain.MasterData.Coops;
using Domain.MasterData.Items;
using Domain.MasterData.Uoms;
using Domain.MasterData.Warehouses;
using Domain.Partnership.Cycles;
using Domain.Production.DailyRecordings;
using Infrastructure.Database;
using Infrastructure.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Production;

internal sealed class DailyRecordingConfiguration : IEntityTypeConfiguration<DailyRecording>
{
    public void Configure(EntityTypeBuilder<DailyRecording> builder)
    {
        builder.ToTable("daily_recordings", Schemas.Production);
        builder.HasKey(r => r.Id);
        builder.Property(r => r.AverageBodyWeightGram).HasPrecision(8, 2);
        builder.Property(r => r.Notes).HasMaxLength(1000);

        // One recording per cycle per day.
        builder.HasIndex(r => new { r.CycleId, r.Date }).IsUnique();
        builder.HasIndex(r => new { r.BranchId, r.Date });

        builder.HasOne<ProductionCycle>().WithMany().HasForeignKey(r => r.CycleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(r => r.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Coop>().WithMany().HasForeignKey(r => r.CoopId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Usages).WithOne().HasForeignKey(u => u.DailyRecordingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(r => r.Revisions).WithOne().HasForeignKey(v => v.DailyRecordingId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Usages).HasField("_usages").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(r => r.Revisions).HasField("_revisions").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class DailyRecordingUsageConfiguration : IEntityTypeConfiguration<DailyRecordingUsage>
{
    public void Configure(EntityTypeBuilder<DailyRecordingUsage> builder)
    {
        builder.ToTable("daily_recording_usages", Schemas.Production);
        builder.HasKey(u => new { u.DailyRecordingId, u.ItemId });
        builder.Property(u => u.Quantity).HasPrecision(InventoryPrecision.Quantity, InventoryPrecision.QuantityScale);
        builder.Property(u => u.BaseQuantity).HasPrecision(InventoryPrecision.Quantity, InventoryPrecision.QuantityScale);
        builder.ComplexMoney(u => u.Value, "value");

        builder.HasOne<Item>().WithMany().HasForeignKey(u => u.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Uom>().WithMany().HasForeignKey(u => u.UomId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DailyRecordingRevisionConfiguration : IEntityTypeConfiguration<DailyRecordingRevision>
{
    public void Configure(EntityTypeBuilder<DailyRecordingRevision> builder)
    {
        builder.ToTable("daily_recording_revisions", Schemas.Production);
        builder.HasKey(v => new { v.DailyRecordingId, v.RevisionNumber });
        builder.Property(v => v.Reason).HasMaxLength(300);
        builder.Property(v => v.PreviousValues).HasColumnType("jsonb");
    }
}

internal sealed class CycleHarvestConfiguration : IEntityTypeConfiguration<CycleHarvest>
{
    public void Configure(EntityTypeBuilder<CycleHarvest> builder)
    {
        builder.ToTable("cycle_harvests", Schemas.Partnership);
        builder.HasKey(h => h.Id);

        // The id is generated in the domain; without this EF would treat a new harvest found through the
        // cycle's collection as an existing row and issue an UPDATE.
        builder.Property(h => h.Id).ValueGeneratedNever();
        builder.Property(h => h.WeightKg).HasPrecision(14, 3);
        builder.Property(h => h.Notes).HasMaxLength(500);
        builder.HasIndex(h => new { h.CycleId, h.Date });
    }
}

internal sealed class StockReturnConfiguration : IEntityTypeConfiguration<StockReturn>
{
    public void Configure(EntityTypeBuilder<StockReturn> builder)
    {
        builder.ToTable("stock_returns", Schemas.Inventory);
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Number).HasMaxLength(50);
        builder.Property(r => r.Reason).HasMaxLength(300);
        builder.Property(r => r.Notes).HasMaxLength(1000);
        builder.HasIndex(r => r.Number).IsUnique();
        builder.HasIndex(r => new { r.BranchId, r.ReturnDate });
        builder.HasIndex(r => r.CycleId);

        builder.HasOne<Branch>().WithMany().HasForeignKey(r => r.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(r => r.FromWarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(r => r.ToWarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProductionCycle>().WithMany().HasForeignKey(r => r.CycleId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Lines).WithOne().HasForeignKey(l => l.StockReturnId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class StockReturnLineConfiguration : IEntityTypeConfiguration<StockReturnLine>
{
    public void Configure(EntityTypeBuilder<StockReturnLine> builder)
    {
        builder.ToTable("stock_return_lines", Schemas.Inventory);
        builder.HasKey(l => new { l.StockReturnId, l.LineNumber });
        builder.Property(l => l.Quantity).HasPrecision(InventoryPrecision.Quantity, InventoryPrecision.QuantityScale);
        builder.Property(l => l.BaseQuantity).HasPrecision(InventoryPrecision.Quantity, InventoryPrecision.QuantityScale);
        builder.Property(l => l.UnitCost).HasPrecision(InventoryPrecision.UnitCost, StockLedgerEntry.UnitCostDecimals);
        builder.ComplexMoney(l => l.Value, "value");

        builder.HasOne<Item>().WithMany().HasForeignKey(l => l.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Uom>().WithMany().HasForeignKey(l => l.UomId).OnDelete(DeleteBehavior.Restrict);
    }
}
