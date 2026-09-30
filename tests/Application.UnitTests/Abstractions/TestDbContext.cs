using System.Text.Json;
using Application.Abstractions.Data;
using Domain.Finance.Accounts;
using Domain.Finance.CostCenters;
using Domain.Finance.FiscalPeriods;
using Domain.Finance.JournalMappings;
using Domain.Finance.Journals;
using Domain.Finance.JournalTemplates;
using Domain.Inventory.GoodsReceipts;
using Domain.Inventory.Stock;
using Domain.Inventory.StockReturns;
using Domain.Inventory.StockTransfers;
using Domain.MasterData.Branches;
using Domain.MasterData.Coops;
using Domain.MasterData.Customers;
using Domain.MasterData.Farmers;
using Domain.MasterData.Items;
using Domain.MasterData.TaxCodes;
using Domain.MasterData.Uoms;
using Domain.MasterData.Vendors;
using Domain.MasterData.Warehouses;
using Domain.Partnership.Contracts;
using Domain.Partnership.Cycles;
using Domain.Procurement.PurchaseOrders;
using Domain.Production.DailyRecordings;
using Domain.Roles;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Abstractions;

/// <summary>
/// A lightweight in-memory <see cref="DbContext"/> that implements <see cref="IApplicationDbContext"/>
/// so Application handlers can be unit tested without referencing the Infrastructure layer.
/// It mirrors only the parts of the real model that the in-memory provider cannot infer by convention.
/// Value objects are mapped as owned types here because the in-memory provider cannot query complex types.
/// </summary>
public sealed class TestDbContext(DbContextOptions<TestDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users { get; set; }

    public DbSet<RefreshToken> RefreshTokens { get; set; }

    public DbSet<Role> Roles { get; set; }

    public DbSet<Branch> Branches { get; set; }

    public DbSet<Uom> Uoms { get; set; }

    public DbSet<TaxCode> TaxCodes { get; set; }

    public DbSet<Item> Items { get; set; }

    public DbSet<Warehouse> Warehouses { get; set; }

    public DbSet<Vendor> Vendors { get; set; }

    public DbSet<Customer> Customers { get; set; }

    public DbSet<Farmer> Farmers { get; set; }

    public DbSet<Coop> Coops { get; set; }

    public DbSet<PartnershipContract> Contracts { get; set; }

    public DbSet<ProductionCycle> ProductionCycles { get; set; }

    public DbSet<Account> Accounts { get; set; }

    public DbSet<CostCenter> CostCenters { get; set; }

    public DbSet<FiscalPeriod> FiscalPeriods { get; set; }

    public DbSet<JournalEntry> JournalEntries { get; set; }

    public DbSet<JournalTemplate> JournalTemplates { get; set; }

    public DbSet<JournalMapping> JournalMappings { get; set; }

    public DbSet<PurchaseOrder> PurchaseOrders { get; set; }

    public DbSet<GoodsReceipt> GoodsReceipts { get; set; }

    public DbSet<StockTransfer> StockTransfers { get; set; }

    public DbSet<StockBalance> StockBalances { get; set; }

    public DbSet<StockLedgerEntry> StockLedgerEntries { get; set; }

    public DbSet<StockReturn> StockReturns { get; set; }

    public DbSet<DailyRecording> DailyRecordings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().Navigation(u => u.Roles).HasField("_roles");
        modelBuilder.Entity<User>().Navigation(u => u.Branches).HasField("_branches");
        modelBuilder.Entity<UserRole>().HasKey(ur => new { ur.UserId, ur.RoleId });
        modelBuilder.Entity<UserBranch>().HasKey(ub => new { ub.UserId, ub.BranchId });

        modelBuilder.Entity<Role>().Navigation(r => r.Permissions).HasField("_permissions");
        modelBuilder.Entity<RolePermission>().HasKey(rp => new { rp.RoleId, rp.Permission });

        modelBuilder.Entity<TaxCode>().Navigation(t => t.Rates).HasField("_rates");
        modelBuilder.Entity<TaxRate>().HasKey(r => new { r.TaxCodeId, r.EffectiveFrom });

        modelBuilder.Entity<Item>().Navigation(i => i.Conversions).HasField("_conversions");
        modelBuilder.Entity<ItemUomConversion>().HasKey(c => new { c.ItemId, c.UomId });

        modelBuilder.Entity<Vendor>().OwnsOne(v => v.TaxIdentity);
        modelBuilder.Entity<Vendor>().OwnsOne(v => v.BankAccount);
        modelBuilder.Entity<Customer>().OwnsOne(c => c.TaxIdentity);
        modelBuilder.Entity<Customer>().OwnsOne(c => c.CreditLimit);
        modelBuilder.Entity<Farmer>().OwnsOne(f => f.TaxIdentity);
        modelBuilder.Entity<Farmer>().OwnsOne(f => f.BankAccount);

        modelBuilder.Entity<PartnershipContract>().Navigation(c => c.InputPrices).HasField("_inputPrices");
        modelBuilder.Entity<PartnershipContract>().Navigation(c => c.LiveBirdPrices).HasField("_liveBirdPrices");
        modelBuilder.Entity<PartnershipContract>().Navigation(c => c.Incentives).HasField("_incentives");
        modelBuilder.Entity<ContractInputPrice>(b =>
        {
            b.HasKey(p => new { p.ContractId, p.ItemId });
            b.OwnsOne(p => p.Price);
        });
        modelBuilder.Entity<ContractLiveBirdPrice>(b =>
        {
            b.HasKey(p => new { p.ContractId, p.MinWeightKg });
            b.OwnsOne(p => p.PricePerKg);
        });
        modelBuilder.Entity<ContractIncentive>(b =>
        {
            b.HasKey(i => new { i.ContractId, i.LineNumber });
            b.OwnsOne(i => i.Amount);
        });

        modelBuilder.Entity<ProductionCycle>().Property(c => c.ContractSnapshot).HasConversion(
            snapshot => JsonSerializer.Serialize(snapshot, JsonSerializerOptions.Web),
            json => JsonSerializer.Deserialize<ContractSnapshot>(json, JsonSerializerOptions.Web));

        modelBuilder.Entity<JournalEntry>().Navigation(j => j.Lines).HasField("_lines");
        modelBuilder.Entity<JournalLine>(b =>
        {
            b.HasKey(l => new { l.JournalEntryId, l.LineNumber });
            b.OwnsOne(l => l.Debit);
            b.OwnsOne(l => l.Credit);
        });

        modelBuilder.Entity<JournalTemplate>().Navigation(t => t.Lines).HasField("_lines");
        modelBuilder.Entity<JournalTemplateLine>().HasKey(l => new { l.JournalTemplateId, l.LineNumber });

        modelBuilder.Entity<JournalMapping>().Navigation(m => m.Lines).HasField("_lines");
        modelBuilder.Entity<JournalMappingLine>().HasKey(l => new { l.JournalMappingId, l.Component });

        modelBuilder.Entity<PurchaseOrder>().Navigation(o => o.Lines).HasField("_lines");
        modelBuilder.Entity<PurchaseOrderLine>(b =>
        {
            b.HasKey(l => new { l.PurchaseOrderId, l.LineNumber });
            b.OwnsOne(l => l.UnitPrice);
        });

        modelBuilder.Entity<GoodsReceipt>().Navigation(r => r.Lines).HasField("_lines");
        modelBuilder.Entity<GoodsReceiptLine>(b =>
        {
            b.HasKey(l => new { l.GoodsReceiptId, l.LineNumber });
            b.OwnsOne(l => l.Value);
        });

        modelBuilder.Entity<StockTransfer>().Navigation(t => t.Lines).HasField("_lines");
        modelBuilder.Entity<StockTransferLine>(b =>
        {
            b.HasKey(l => new { l.StockTransferId, l.LineNumber });
            b.OwnsOne(l => l.Value);
        });

        modelBuilder.Entity<StockBalance>().OwnsOne(b => b.Value);
        modelBuilder.Entity<StockLedgerEntry>(b =>
        {
            b.OwnsOne(e => e.Value);
            b.OwnsOne(e => e.BalanceValue);
        });

        modelBuilder.Entity<StockReturn>().Navigation(r => r.Lines).HasField("_lines");
        modelBuilder.Entity<StockReturnLine>(b =>
        {
            b.HasKey(l => new { l.StockReturnId, l.LineNumber });
            b.OwnsOne(l => l.Value);
        });

        modelBuilder.Entity<ProductionCycle>(b =>
        {
            b.Navigation(c => c.Harvests).HasField("_harvests");
            b.HasMany(c => c.Harvests).WithOne().HasForeignKey(h => h.CycleId);
            b.Property(c => c.ClosingPerformance).HasConversion(
                performance => JsonSerializer.Serialize(performance, JsonSerializerOptions.Web),
                json => JsonSerializer.Deserialize<CyclePerformance>(json, JsonSerializerOptions.Web));
        });

        modelBuilder.Entity<DailyRecording>(b =>
        {
            b.Navigation(r => r.Usages).HasField("_usages");
            b.Navigation(r => r.Revisions).HasField("_revisions");
        });
        modelBuilder.Entity<DailyRecordingUsage>(b =>
        {
            b.HasKey(u => new { u.DailyRecordingId, u.ItemId });
            b.OwnsOne(u => u.Value);
        });
        modelBuilder.Entity<DailyRecordingRevision>().HasKey(v => new { v.DailyRecordingId, v.RevisionNumber });
        modelBuilder.Entity<CycleHarvest>().Property(h => h.Id).ValueGeneratedNever();
    }
}
