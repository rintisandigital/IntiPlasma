using Domain.Access;

namespace Web.App.Infrastructure.Authorization;

/// <summary>
/// Menu codes used by <see cref="MenuAccessAttribute"/> and views.
/// </summary>
public static class MenuCodes
{
    public const string AdminUsers = "admin.users";
    public const string AdminMenuAccess = "admin.menu-access";
    public const string AdminBranchAccess = "admin.branch-access";
    public const string AdminMenus = "admin.menus";
    public const string AdminApiRoles = "admin.api-roles";
    public const string AdminBranches = "admin.branches";

    public const string MasterUoms = "master.uoms";
    public const string MasterTaxCodes = "master.tax-codes";
    public const string MasterItems = "master.items";
    public const string MasterWarehouses = "master.warehouses";
    public const string MasterVendors = "master.vendors";
    public const string MasterCustomers = "master.customers";
    public const string PartnershipFarmers = "partnership.farmers";
    public const string PartnershipCoops = "partnership.coops";
    public const string PartnershipContracts = "partnership.contracts";
    public const string FinanceAccounts = "finance.accounts";
    public const string FinanceCostCenters = "finance.cost-centers";
    public const string FinanceFiscalPeriods = "finance.fiscal-periods";
    public const string FinanceJournalTemplates = "finance.journal-templates";
    public const string FinanceJournalMappings = "finance.journal-mappings";
    public const string FinanceCashBankAccounts = "finance.cash-bank-accounts";
    public const string FinanceVendorInvoices = "finance.vendor-invoices";
    public const string FinancePaymentVouchers = "finance.payment-vouchers";
    public const string FinanceCashTransactions = "finance.cash-transactions";
    public const string FinanceBankTransfers = "finance.bank-transfers";
    public const string FinanceBankReconciliations = "finance.bank-reconciliations";
    public const string FinancePayables = "finance.payables";
    public const string FinanceJournals = "finance.journals";
    public const string ProcurementPurchaseOrders = "procurement.purchase-orders";
    public const string InventoryGoodsReceipts = "inventory.goods-receipts";
    public const string InventoryStockTransfers = "inventory.stock-transfers";
    public const string InventoryStockReturns = "inventory.stock-returns";
    public const string InventoryFeedMutations = "inventory.feed-mutations";
    public const string InventoryStock = "inventory.stock";
    public const string ProductionCycles = "production.cycles";
    public const string ProductionRecordings = "production.recordings";
    public const string ProductionHarvests = "production.harvests";
    public const string SalesOrders = "sales.orders";
    public const string SalesDeliveries = "sales.deliveries";
    public const string SalesInvoices = "sales.invoices";
    public const string SalesCreditNotes = "sales.credit-notes";
    public const string SalesReceipts = "sales.receipts";
    public const string SalesReceivables = "sales.receivables";
    public const string CostingCycleCosts = "costing.cycle-costs";
    public const string CostingSettlements = "costing.settlements";
    public const string ReportsGeneralLedger = "reports.general-ledger";
    public const string ReportsTrialBalance = "reports.trial-balance";
    public const string ReportsIncomeStatement = "reports.income-statement";
    public const string ReportsBalanceSheet = "reports.balance-sheet";
    public const string ReportsCashFlow = "reports.cash-flow";
    public const string ReportsProfitability = "reports.profitability";
    public const string ReportsTax = "reports.tax";
    public const string AdminFailedEvents = "admin.failed-events";
    public const string AdminAuditLogs = "admin.audit-logs";
}

/// <summary>
/// The Web.App menu catalog (PLAN-WEBAPP §11.7). Synchronized into <c>identity.menus</c> at startup; the
/// administrator may rename, re-order, re-icon or deactivate menus, while codes, routes, rights and release
/// state come from here. Unreleased pages (<c>IsAvailable = false</c>) are hidden from the sidebar but can
/// already be granted in menu access profiles (W-25). The Dashboard is not in the catalog: every signed-in
/// user sees it.
/// </summary>
public static class MenuCatalog
{
    private const MenuRights Crud = MenuRights.Create | MenuRights.Edit | MenuRights.Delete | MenuRights.Export;
    private const MenuRights CreateExport = MenuRights.Create | MenuRights.Export;
    private const MenuRights CreateEditExport = MenuRights.Create | MenuRights.Edit | MenuRights.Export;

    public static readonly IReadOnlyList<MenuDefinition> Definitions = Build();

    public static bool Contains(string code) => Definitions.Any(d => d.Code == code);

    private static List<MenuDefinition> Build()
    {
        var menus = new List<MenuDefinition>();

        void Group(string code, string name, string icon, int order) =>
            menus.Add(new MenuDefinition(code, null, name, icon, null, order, MenuRights.None, IsAvailable: true));

        void Page(string group, string code, string name, string route, MenuRights supports, bool released) =>
            menus.Add(new MenuDefinition(
                code, group, name, null, route, menus.Count(m => m.ParentCode == group) * 10 + 10, supports, released));

        Group("master", "Master Data", "ti ti-database", 20);
        Page("master", "master.uoms", "Units of Measure", "/MasterData/Uoms", CreateEditExport, true);
        Page("master", "master.tax-codes", "Tax Codes", "/MasterData/TaxCodes", CreateEditExport, true);
        Page("master", "master.items", "Items", "/MasterData/Items", CreateEditExport, true);
        Page("master", "master.warehouses", "Warehouses", "/MasterData/Warehouses", CreateEditExport, true);
        Page("master", "master.vendors", "Vendors", "/MasterData/Vendors", CreateEditExport, true);
        Page("master", "master.customers", "Customers", "/MasterData/Customers", CreateEditExport, true);

        Group("partnership", "Partnership", "ti ti-users-group", 30);
        Page("partnership", "partnership.farmers", "Farmers", "/Partnership/Farmers", CreateEditExport, true);
        Page("partnership", "partnership.coops", "Farms", "/Partnership/Coops", CreateEditExport, true);
        Page("partnership", "partnership.contracts", "Contracts", "/Partnership/Contracts", CreateEditExport, true);

        Group("procurement", "Procurement", "ti ti-shopping-cart", 40);
        Page("procurement", "procurement.purchase-orders", "Purchase Orders", "/Procurement/PurchaseOrders", CreateEditExport, true);

        Group("inventory", "Inventory", "ti ti-building-warehouse", 50);
        Page("inventory", "inventory.goods-receipts", "Goods Receipts", "/Inventory/GoodsReceipts", CreateExport, true);
        Page("inventory", "inventory.stock-transfers", "Stock Transfers", "/Inventory/StockTransfers", CreateExport, true);
        Page("inventory", "inventory.stock-returns", "Stock Returns", "/Inventory/StockReturns", CreateExport, true);
        Page("inventory", "inventory.feed-mutations", "Feed Mutations", "/Inventory/FeedMutations", CreateExport, true);
        Page("inventory", "inventory.stock", "Stock Balance & Card", "/Inventory/Stock", MenuRights.Export, true);

        Group("production", "Production", "ti ti-egg", 60);
        Page("production", "production.cycles", "Cycles & Chick-in", "/Production/Cycles", CreateEditExport, true);
        Page("production", "production.recordings", "Daily Recordings", "/Production/Recordings", CreateEditExport, true);
        Page("production", "production.harvests", "Harvests", "/Production/Harvests", CreateEditExport, true);

        Group("sales", "Sales", "ti ti-receipt", 70);
        Page("sales", "sales.orders", "Sales Orders", "/Sales/SalesOrders", CreateEditExport, true);
        Page("sales", "sales.deliveries", "Delivery Orders", "/Sales/DeliveryOrders", CreateEditExport, true);
        Page("sales", "sales.invoices", "Sales Invoices", "/Sales/SalesInvoices", CreateEditExport, true);
        Page("sales", "sales.credit-notes", "Credit Notes", "/Sales/CreditNotes", CreateExport, true);
        Page("sales", "sales.receipts", "Customer Receipts", "/Sales/Receipts", CreateEditExport, true);
        Page("sales", "sales.receivables", "Receivable Ledger & Aging", "/Sales/Receivables", MenuRights.Export, true);

        Group("finance", "Finance", "ti ti-building-bank", 80);
        Page("finance", "finance.accounts", "Chart of Accounts", "/Finance/Accounts", CreateEditExport, true);
        Page("finance", "finance.cost-centers", "Cost Centers", "/Finance/CostCenters", CreateEditExport, true);
        Page("finance", "finance.fiscal-periods", "Fiscal Periods", "/Finance/FiscalPeriods", CreateEditExport, true);
        Page("finance", "finance.journal-templates", "Journal Templates", "/Finance/JournalTemplates", CreateEditExport, true);
        Page("finance", "finance.journal-mappings", "Auto Journal Mappings", "/Finance/JournalMappings", CreateEditExport, true);
        Page("finance", "finance.cash-bank-accounts", "Cash/Bank Accounts", "/Finance/CashBankAccounts", CreateEditExport, true);
        Page("finance", MenuCodes.FinanceVendorInvoices, "Vendor Invoices", "/Finance/VendorInvoices", CreateEditExport, true);
        Page("finance", MenuCodes.FinancePaymentVouchers, "Payment Vouchers", "/Finance/PaymentVouchers", CreateEditExport, true);
        Page("finance", MenuCodes.FinanceCashTransactions, "Cash In/Out", "/Finance/CashTransactions", CreateEditExport, true);
        Page("finance", MenuCodes.FinanceBankTransfers, "Bank Transfers", "/Finance/BankTransfers", CreateExport, true);
        Page("finance", MenuCodes.FinanceBankReconciliations, "Bank Reconciliations", "/Finance/BankReconciliations", CreateEditExport, true);
        Page("finance", MenuCodes.FinancePayables, "Payable Ledger & Aging", "/Finance/Payables", MenuRights.Export, true);
        Page("finance", MenuCodes.FinanceJournals, "Journals", "/Finance/Journals", Crud, true);

        Group("costing", "Costing", "ti ti-calculator", 90);
        Page("costing", MenuCodes.CostingCycleCosts, "Cycle Cost", "/Costing/CycleCosts", MenuRights.Export, true);
        Page("costing", MenuCodes.CostingSettlements, "Plasma Settlements", "/Costing/Settlements", CreateEditExport, true);

        Group("reports", "Reports", "ti ti-report-analytics", 100);
        Page("reports", "reports.general-ledger", "General Ledger", "/Reports/GeneralLedger", MenuRights.Export, true);
        Page("reports", "reports.trial-balance", "Trial Balance", "/Reports/TrialBalance", MenuRights.Export, true);
        Page("reports", "reports.income-statement", "Income Statement", "/Reports/IncomeStatement", MenuRights.Export, true);
        Page("reports", "reports.balance-sheet", "Balance Sheet", "/Reports/BalanceSheet", MenuRights.Export, true);
        Page("reports", "reports.cash-flow", "Cash Flow", "/Reports/CashFlow", MenuRights.Export, true);
        Page("reports", "reports.profitability", "Profitability", "/Reports/Profitability", MenuRights.Export, true);
        Page("reports", "reports.tax", "Tax Recap", "/Reports/Tax", MenuRights.Export, true);

        Group("admin", "Administration", "ti ti-settings", 110);
        Page("admin", MenuCodes.AdminUsers, "Users", "/Admin/Users", Crud, true);
        Page("admin", MenuCodes.AdminMenuAccess, "Menu Access", "/Admin/MenuAccess", Crud, true);
        Page("admin", MenuCodes.AdminBranchAccess, "Branch Access", "/Admin/BranchAccess", Crud, true);
        Page("admin", MenuCodes.AdminMenus, "Menus", "/Admin/Menus", MenuRights.Edit, true);
        Page("admin", MenuCodes.AdminApiRoles, "API Roles", "/Admin/ApiRoles", CreateEditExport, true);
        Page("admin", MenuCodes.AdminBranches, "Branches", "/Admin/Branches", CreateEditExport, true);
        Page("admin", MenuCodes.AdminFailedEvents, "Failed Events", "/Admin/FailedEvents", MenuRights.Edit, true);
        Page("admin", MenuCodes.AdminAuditLogs, "Audit Log", "/Admin/AuditLogs", MenuRights.Export, true);

        return menus;
    }
}
