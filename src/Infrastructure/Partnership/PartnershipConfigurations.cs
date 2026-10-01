using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.MasterData.Branches;
using Domain.MasterData.Coops;
using Domain.MasterData.Farmers;
using Domain.MasterData.Items;
using Domain.MasterData.TaxCodes;
using Domain.Partnership.Contracts;
using Domain.Partnership.Cycles;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Partnership;

internal sealed class ContractConfiguration : IEntityTypeConfiguration<PartnershipContract>
{
    public void Configure(EntityTypeBuilder<PartnershipContract> builder)
    {
        builder.ToTable("contracts", Schemas.Partnership);
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Code).HasMaxLength(30);
        builder.Property(c => c.Name).HasMaxLength(150);
        builder.Property(c => c.Notes).HasMaxLength(1000);
        builder.Property(c => c.PlasmaProfitSharePercent).HasPrecision(7, 4);
        builder.HasIndex(c => c.Code).IsUnique();
        builder.HasIndex(c => c.BranchId);

        builder.HasOne<Branch>().WithMany().HasForeignKey(c => c.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaxCode>().WithMany().HasForeignKey(c => c.IncomeTaxCodeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.InputPrices).WithOne().HasForeignKey(p => p.ContractId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(c => c.LiveBirdPrices).WithOne().HasForeignKey(p => p.ContractId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(c => c.Incentives).WithOne().HasForeignKey(i => i.ContractId).OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.InputPrices).HasField("_inputPrices").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(c => c.LiveBirdPrices).HasField("_liveBirdPrices").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(c => c.Incentives).HasField("_incentives").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ContractInputPriceConfiguration : IEntityTypeConfiguration<ContractInputPrice>
{
    public void Configure(EntityTypeBuilder<ContractInputPrice> builder)
    {
        builder.ToTable("contract_input_prices", Schemas.Partnership);
        builder.HasKey(p => new { p.ContractId, p.ItemId });
        builder.ComplexMoney(p => p.Price, "price");
        builder.HasOne<Item>().WithMany().HasForeignKey(p => p.ItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ContractLiveBirdPriceConfiguration : IEntityTypeConfiguration<ContractLiveBirdPrice>
{
    public void Configure(EntityTypeBuilder<ContractLiveBirdPrice> builder)
    {
        builder.ToTable("contract_live_bird_prices", Schemas.Partnership);
        builder.HasKey(p => new { p.ContractId, p.MinWeightKg });
        builder.Property(p => p.MinWeightKg).HasPrecision(10, 3);
        builder.Property(p => p.MaxWeightKg).HasPrecision(10, 3);
        builder.ComplexMoney(p => p.PricePerKg, "price_per_kg");
    }
}

internal sealed class ContractIncentiveConfiguration : IEntityTypeConfiguration<ContractIncentive>
{
    public void Configure(EntityTypeBuilder<ContractIncentive> builder)
    {
        builder.ToTable("contract_incentives", Schemas.Partnership);
        builder.HasKey(i => new { i.ContractId, i.LineNumber });
        builder.Property(i => i.Name).HasMaxLength(100);
        builder.Property(i => i.RangeFrom).HasPrecision(18, 4);
        builder.Property(i => i.RangeTo).HasPrecision(18, 4);
        builder.ComplexMoney(i => i.Amount, "amount");
    }
}

internal sealed class ProductionCycleConfiguration : IEntityTypeConfiguration<ProductionCycle>
{
    private static readonly JsonSerializerOptions SnapshotSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public void Configure(EntityTypeBuilder<ProductionCycle> builder)
    {
        builder.ToTable("production_cycles", Schemas.Partnership);
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Number).HasMaxLength(50);
        builder.Property(c => c.Notes).HasMaxLength(1000);
        builder.Property(c => c.CancellationReason).HasMaxLength(500);
        builder.HasIndex(c => c.Number).IsUnique();
        builder.HasIndex(c => c.BranchId);

        // A coop can only have one open cycle; the database enforces it even under concurrent requests.
        builder.HasIndex(c => c.CoopId)
            .IsUnique()
            .HasFilter("status IN ('Planned', 'Active', 'Harvesting')")
            .HasDatabaseName("ix_production_cycles_coop_id_open");

        builder.Property(c => c.ContractSnapshot)
            .HasColumnType("jsonb")
            .HasConversion(
                snapshot => JsonSerializer.Serialize(snapshot, SnapshotSerializerOptions),
                json => JsonSerializer.Deserialize<ContractSnapshot>(json, SnapshotSerializerOptions));

        builder.Property(c => c.HarvestedWeightKg).HasPrecision(14, 3);

        builder.Property(c => c.ClosingPerformance)
            .HasColumnType("jsonb")
            .HasConversion(
                performance => JsonSerializer.Serialize(performance, SnapshotSerializerOptions),
                json => JsonSerializer.Deserialize<CyclePerformance>(json, SnapshotSerializerOptions));

        builder.Property(c => c.ClosingCost)
            .HasColumnType("jsonb")
            .HasConversion(
                cost => JsonSerializer.Serialize(cost, SnapshotSerializerOptions),
                json => JsonSerializer.Deserialize<CycleCostSummary>(json, SnapshotSerializerOptions));

        builder.HasMany(c => c.Harvests).WithOne().HasForeignKey(h => h.CycleId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Harvests).HasField("_harvests").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<Branch>().WithMany().HasForeignKey(c => c.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Farmer>().WithMany().HasForeignKey(c => c.FarmerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Coop>().WithMany().HasForeignKey(c => c.CoopId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PartnershipContract>().WithMany().HasForeignKey(c => c.ContractId).OnDelete(DeleteBehavior.Restrict);
    }
}
