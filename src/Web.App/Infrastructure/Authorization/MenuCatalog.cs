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
    private const MenuRights EditExport = MenuRights.Edit | MenuRights.Export;

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
        Page("master", "master.uoms", "Units of Measure", "/MasterData/Uoms", Crud, false);
        Page("master", "master.tax-codes", "Tax Codes", "/MasterData/TaxCodes", Crud, false);
        Page("master", "master.items", "Items", "/MasterData/Items", Crud, false);
        Page("master", "master.warehouses", "Warehouses", "/MasterData/Warehouses", Crud, false);
        Page("master", "master.vendors", "Vendors", "/MasterData/Vendors", Crud, false);
        Page("master", "master.customers", "Customers", "/MasterData/Customers", Crud, false);

        Group("partnership", "Partnership", "ti ti-users-group", 30);
        Page("partnership", "partnership.farmers", "Farmers", "/Partnership/Farmers", Crud, false);
        Page("partnership", "partnership.coops", "Coops", "/Partnership/Coops", Crud, false);
        Page("partnership", "partnership.contracts", "Contracts", "/Partnership/Contracts", Crud, false);

        Group("procurement", "Procurement", "ti ti-shopping-cart", 40);
        Page("procurement", "procurement.purchase-orders", "Purchase Orders", "/Procurement/PurchaseOrders", Crud, false);

        Group("inventory", "Inventory", "ti ti-building-warehouse", 50);
        Page("inventory", "inventory.goods-receipts", "Goods Receipts", "/Inventory/GoodsReceipts", CreateExport, false);
        Page("inventory", "inventory.stock-transfers", "Stock Transfers", "/Inventory/StockTransfers", CreateExport, false);
        Page("inventory", "inventory.stock-returns", "Stock Returns", "/Inventory/StockReturns", CreateExport, false);
        Page("inventory", "inventory.feed-mutations", "Feed Mutations", "/Inventory/FeedMutations", CreateExport, false);
        Page("inventory", "inventory.stock", "Stock Balance & Card", "/Inventory/Stock", MenuRights.Export, false);

        Group("production", "Production", "ti ti-egg", 60);
        Page("production", "production.cycles", "Cycles & Chick-in", "/Production/Cycles", CreateEditExport, false);
        Page("production", "production.recordings", "Daily Recordings", "/Production/Recordings", CreateEditExport, false);
        Page("production", "production.harvests", "Harvests", "/Production/Harvests", CreateEditExport, false);

        Group("sales", "Sales", "ti ti-receipt", 70);
        Page("sales", "sales.orders", "Sales Orders", "/Sales/Orders", Crud, false);
        Page("sales", "sales.deliveries", "Delivery Orders", "/Sales/Deliveries", CreateEditExport, false);
        Page("sales", "sales.invoices", "Sales Invoices", "/Sales/Invoices", Crud, false);
        Page("sales", "sales.credit-notes", "Credit Notes", "/Sales/CreditNotes", CreateEditExport, false);
        Page("sales", "sales.receipts", "Customer Receipts", "/Sales/Receipts", CreateEditExport, false);
        Page("sales", "sales.receivables", "Receivable Ledger & Aging", "/Sales/Receivables", MenuRights.Export, false);

        Group("finance", "Finance", "ti ti-building-bank", 80);
        Page("finance", "finance.accounts", "Chart of Accounts", "/Finance/Accounts", Crud, false);
        Page("finance", "finance.cost-centers", "Cost Centers", "/Finance/CostCenters", Crud, false);
        Page("finance", "finance.fiscal-periods", "Fiscal Periods", "/Finance/FiscalPeriods", EditExport, false);
        Page("finance", "finance.journal-templates", "Journal Templates", "/Finance/JournalTemplates", Crud, false);
        Page("finance", "finance.journal-mappings", "Auto Journal Mappings", "/Finance/JournalMappings", EditExport, false);
        Page("finance", "finance.cash-bank-accounts", "Cash/Bank Accounts", "/Finance/CashBankAccounts", Crud, false);
        Page("finance", "finance.vendor-invoices", "Vendor Invoices", "/Finance/VendorInvoices", Crud, false);
        Page("finance", "finance.payment-vouchers", "Payment Vouchers", "/Finance/PaymentVouchers", Crud, false);
        Page("finance", "finance.cash-transactions", "Cash In/Out", "/Finance/CashTransactions", Crud, false);
        Page("finance", "finance.bank-transfers", "Bank Transfers", "/Finance/BankTransfers", CreateEditExport, false);
        Page("finance", "finance.bank-reconciliations", "Bank Reconciliations", "/Finance/BankReconciliations", CreateEditExport, false);
        Page("finance", "finance.payables", "Payable Ledger & Aging", "/Finance/Payables", MenuRights.Export, false);
        Page("finance", "finance.journals", "Journals", "/Finance/Journals", Crud, false);

        Group("costing", "Costing", "ti ti-calculator", 90);
        Page("costing", "costing.cycle-costs", "Cycle Cost", "/Costing/CycleCosts", MenuRights.Export, false);
        Page("costing", "costing.settlements", "Plasma Settlements", "/Costing/Settlements", CreateEditExport, false);

        Group("reports", "Reports", "ti ti-report-analytics", 100);
        Page("reports", "reports.general-ledger", "General Ledger", "/Reports/GeneralLedger", MenuRights.Export, false);
        Page("reports", "reports.trial-balance", "Trial Balance", "/Reports/TrialBalance", MenuRights.Export, false);
        Page("reports", "reports.income-statement", "Income Statement", "/Reports/IncomeStatement", MenuRights.Export, false);
        Page("reports", "reports.balance-sheet", "Balance Sheet", "/Reports/BalanceSheet", MenuRights.Export, false);
        Page("reports", "reports.cash-flow", "Cash Flow", "/Reports/CashFlow", MenuRights.Export, false);
        Page("reports", "reports.profitability", "Profitability", "/Reports/Profitability", MenuRights.Export, false);
        Page("reports", "reports.tax", "Tax Recap", "/Reports/Tax", MenuRights.Export, false);

        Group("admin", "Administration", "ti ti-settings", 110);
        Page("admin", MenuCodes.AdminUsers, "Users", "/Admin/Users", Crud, true);
        Page("admin", MenuCodes.AdminMenuAccess, "Menu Access", "/Admin/MenuAccess", Crud, true);
        Page("admin", MenuCodes.AdminBranchAccess, "Branch Access", "/Admin/BranchAccess", Crud, true);
        Page("admin", MenuCodes.AdminMenus, "Menus", "/Admin/Menus", MenuRights.Edit, true);
        Page("admin", MenuCodes.AdminApiRoles, "API Roles", "/Admin/ApiRoles", CreateEditExport, true);
        Page("admin", MenuCodes.AdminBranches, "Branches", "/Admin/Branches", CreateEditExport, true);
        Page("admin", "admin.failed-events", "Failed Events", "/Admin/FailedEvents", MenuRights.Edit, false);

        return menus;
    }
}
