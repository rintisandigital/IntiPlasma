using Domain.Costing.PlasmaSettlements;
using Domain.MasterData.Branches;
using Domain.MasterData.Farmers;
using Domain.MasterData.TaxCodes;
using Domain.Partnership.Contracts;
using Domain.Partnership.Cycles;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Costing;

internal sealed class PlasmaSettlementConfiguration : IEntityTypeConfiguration<PlasmaSettlement>
{
    public void Configure(EntityTypeBuilder<PlasmaSettlement> builder)
    {
        builder.ToTable("plasma_settlements", Schemas.Costing);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Number).HasMaxLength(50);
        builder.Property(s => s.Notes).HasMaxLength(1000);
        builder.Property(s => s.CancellationReason).HasMaxLength(500);
        builder.Property(s => s.IncomeTaxRatePercent).HasPrecision(7, 4);
        builder.ComplexMoney(s => s.GrossIncome, "gross_income");
        builder.ComplexMoney(s => s.IncomeTaxAmount, "income_tax_amount");
        builder.ComplexMoney(s => s.DebtDeduction, "debt_deduction");
        builder.ComplexMoney(s => s.NetPayable, "net_payable");
        builder.ComplexMoney(s => s.Deficit, "deficit");
        builder.ComplexMoney(s => s.PaidAmount, "paid_amount");
        builder.HasIndex(s => s.Number).IsUnique();
        builder.HasIndex(s => new { s.FarmerId, s.Status });
        builder.HasIndex(s => new { s.BranchId, s.SettlementDate });

        // One settlement per cycle (a cancelled draft frees the cycle).
        builder.HasIndex(s => s.CycleId)
            .IsUnique()
            .HasFilter("status <> 'Cancelled'")
            .HasDatabaseName("ix_plasma_settlements_cycle_id_active");

        builder.HasOne<Branch>().WithMany().HasForeignKey(s => s.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProductionCycle>().WithMany().HasForeignKey(s => s.CycleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Farmer>().WithMany().HasForeignKey(s => s.FarmerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PartnershipContract>().WithMany().HasForeignKey(s => s.ContractId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaxCode>().WithMany().HasForeignKey(s => s.IncomeTaxCodeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Lines).WithOne().HasForeignKey(l => l.PlasmaSettlementId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PlasmaSettlementLineConfiguration : IEntityTypeConfiguration<PlasmaSettlementLine>
{
    public void Configure(EntityTypeBuilder<PlasmaSettlementLine> builder)
    {
        builder.ToTable("plasma_settlement_lines", Schemas.Costing);
        builder.HasKey(l => new { l.PlasmaSettlementId, l.LineNumber });
        builder.Property(l => l.Description).HasMaxLength(250);
        builder.Property(l => l.Quantity).HasPrecision(18, 4);
        builder.Property(l => l.UnitPrice).HasPrecision(ConfigurationExtensions.MoneyPrecision, SharedKernel.Money.Decimals);
        builder.ComplexMoney(l => l.Amount, "amount");
    }
}
