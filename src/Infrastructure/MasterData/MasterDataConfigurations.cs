using Domain.MasterData.Branches;
using Domain.MasterData.Coops;
using Domain.MasterData.Customers;
using Domain.MasterData.Farmers;
using Domain.MasterData.Items;
using Domain.MasterData.TaxCodes;
using Domain.MasterData.Uoms;
using Domain.MasterData.Vendors;
using Domain.MasterData.Warehouses;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.MasterData;

internal sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("branches", Schemas.MasterData);
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Code).HasMaxLength(10);
        builder.Property(b => b.Name).HasMaxLength(100);
        builder.Property(b => b.Address).HasMaxLength(500);
        builder.Property(b => b.Phone).HasMaxLength(30);
        builder.HasIndex(b => b.Code).IsUnique();
    }
}

internal sealed class UomConfiguration : IEntityTypeConfiguration<Uom>
{
    public void Configure(EntityTypeBuilder<Uom> builder)
    {
        builder.ToTable("uoms", Schemas.MasterData);
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Code).HasMaxLength(10);
        builder.Property(u => u.Name).HasMaxLength(50);
        builder.HasIndex(u => u.Code).IsUnique();
    }
}

internal sealed class TaxCodeConfiguration : IEntityTypeConfiguration<TaxCode>
{
    public void Configure(EntityTypeBuilder<TaxCode> builder)
    {
        builder.ToTable("tax_codes", Schemas.MasterData);
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Code).HasMaxLength(20);
        builder.Property(t => t.Name).HasMaxLength(100);
        builder.HasIndex(t => t.Code).IsUnique();

        builder.HasMany(t => t.Rates).WithOne().HasForeignKey(r => r.TaxCodeId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(t => t.Rates).HasField("_rates").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class TaxRateConfiguration : IEntityTypeConfiguration<TaxRate>
{
    public void Configure(EntityTypeBuilder<TaxRate> builder)
    {
        builder.ToTable("tax_rates", Schemas.MasterData);
        builder.HasKey(r => new { r.TaxCodeId, r.EffectiveFrom });
        builder.Property(r => r.RatePercent).HasPrecision(7, 4);
        builder.Property(r => r.TaxBaseRatio).HasPrecision(10, 8);
    }
}

internal sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("items", Schemas.MasterData);
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Code).HasMaxLength(30);
        builder.Property(i => i.Name).HasMaxLength(150);
        builder.HasIndex(i => i.Code).IsUnique();

        builder.HasOne<Uom>().WithMany().HasForeignKey(i => i.BaseUomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaxCode>().WithMany().HasForeignKey(i => i.TaxCodeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.Conversions).WithOne().HasForeignKey(c => c.ItemId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(i => i.Conversions).HasField("_conversions").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ItemUomConversionConfiguration : IEntityTypeConfiguration<ItemUomConversion>
{
    public void Configure(EntityTypeBuilder<ItemUomConversion> builder)
    {
        builder.ToTable("item_uom_conversions", Schemas.MasterData);
        builder.HasKey(c => new { c.ItemId, c.UomId });
        builder.Property(c => c.Factor).HasPrecision(18, 6);
        builder.HasOne<Uom>().WithMany().HasForeignKey(c => c.UomId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("warehouses", Schemas.MasterData);
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Code).HasMaxLength(40);
        builder.Property(w => w.Name).HasMaxLength(150);
        builder.Property(w => w.Address).HasMaxLength(500);
        builder.HasIndex(w => w.Code).IsUnique();

        // One coop warehouse per coop.
        builder.HasIndex(w => w.CoopId).IsUnique().HasFilter("coop_id IS NOT NULL");

        builder.HasOne<Branch>().WithMany().HasForeignKey(w => w.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Coop>().WithMany().HasForeignKey(w => w.CoopId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> builder)
    {
        builder.ToTable("vendors", Schemas.MasterData);
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Code).HasMaxLength(30);
        builder.Property(v => v.Name).HasMaxLength(150);
        builder.Property(v => v.Address).HasMaxLength(500);
        builder.Property(v => v.Phone).HasMaxLength(30);
        builder.Property(v => v.Email).HasMaxLength(256);
        builder.Property(v => v.PriceTolerancePercent).HasPrecision(5, 2);
        builder.HasIndex(v => v.Code).IsUnique();
        builder.ComplexTaxIdentity(v => v.TaxIdentity);
        builder.ComplexBankAccount(v => v.BankAccount);
    }
}

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers", Schemas.MasterData);
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Code).HasMaxLength(30);
        builder.Property(c => c.Name).HasMaxLength(150);
        builder.Property(c => c.Address).HasMaxLength(500);
        builder.Property(c => c.Phone).HasMaxLength(30);
        builder.Property(c => c.Email).HasMaxLength(256);
        builder.HasIndex(c => c.Code).IsUnique();
        builder.ComplexTaxIdentity(c => c.TaxIdentity);
        builder.ComplexMoney(c => c.CreditLimit, "credit_limit");
    }
}

internal sealed class FarmerConfiguration : IEntityTypeConfiguration<Farmer>
{
    public void Configure(EntityTypeBuilder<Farmer> builder)
    {
        builder.ToTable("farmers", Schemas.MasterData);
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Code).HasMaxLength(30);
        builder.Property(f => f.Name).HasMaxLength(150);
        builder.Property(f => f.Nik).HasMaxLength(16);
        builder.Property(f => f.Address).HasMaxLength(500);
        builder.Property(f => f.Phone).HasMaxLength(30);
        builder.HasIndex(f => f.Code).IsUnique();
        builder.HasIndex(f => f.BranchId);
        builder.ComplexTaxIdentity(f => f.TaxIdentity);
        builder.ComplexBankAccount(f => f.BankAccount);

        builder.HasOne<Branch>().WithMany().HasForeignKey(f => f.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CoopConfiguration : IEntityTypeConfiguration<Coop>
{
    public void Configure(EntityTypeBuilder<Coop> builder)
    {
        builder.ToTable("coops", Schemas.MasterData);
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Code).HasMaxLength(25);
        builder.Property(c => c.Name).HasMaxLength(100);
        builder.Property(c => c.Address).HasMaxLength(500);
        builder.Property(c => c.Latitude).HasPrecision(9, 6);
        builder.Property(c => c.Longitude).HasPrecision(9, 6);
        builder.HasIndex(c => c.Code).IsUnique();
        builder.HasIndex(c => c.BranchId);

        builder.HasOne<Farmer>().WithMany().HasForeignKey(c => c.FarmerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(c => c.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}
