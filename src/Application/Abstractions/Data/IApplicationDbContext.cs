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

namespace Application.Abstractions.Data;

/// <summary>
/// Write side of CQRS: command handlers load aggregates through these sets, call domain methods and save.
/// Read side query handlers should prefer <see cref="IDbConnectionFactory"/> (Dapper + SQL).
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Role> Roles { get; }

    DbSet<Branch> Branches { get; }
    DbSet<Uom> Uoms { get; }
    DbSet<TaxCode> TaxCodes { get; }
    DbSet<Item> Items { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<Vendor> Vendors { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Farmer> Farmers { get; }
    DbSet<Coop> Coops { get; }

    DbSet<PartnershipContract> Contracts { get; }
    DbSet<ProductionCycle> ProductionCycles { get; }

    DbSet<Account> Accounts { get; }
    DbSet<CostCenter> CostCenters { get; }
    DbSet<FiscalPeriod> FiscalPeriods { get; }
    DbSet<JournalEntry> JournalEntries { get; }
    DbSet<JournalTemplate> JournalTemplates { get; }
    DbSet<JournalMapping> JournalMappings { get; }

    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<GoodsReceipt> GoodsReceipts { get; }
    DbSet<StockTransfer> StockTransfers { get; }
    DbSet<StockBalance> StockBalances { get; }
    DbSet<StockLedgerEntry> StockLedgerEntries { get; }
    DbSet<StockReturn> StockReturns { get; }

    DbSet<DailyRecording> DailyRecordings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
