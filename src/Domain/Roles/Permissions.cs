namespace Domain.Roles;

/// <summary>
/// Catalog of every permission in the system. Permissions are granted to roles, roles are assigned to users.
/// Format: "{module}:{action}". New modules add their permissions here.
/// </summary>
public static class Permissions
{
    public const string UsersRead = "users:read";
    public const string UsersManage = "users:manage";
    public const string RolesRead = "roles:read";
    public const string RolesManage = "roles:manage";

    public const string BranchesRead = "branches:read";
    public const string BranchesManage = "branches:manage";

    /// <summary>
    /// Grants access to the data of every branch (head office). Without it a user only sees
    /// the branches assigned to them.
    /// </summary>
    public const string BranchesAccessAll = "branches:access-all";

    /// <summary>
    /// Company-wide reference data: units of measure, items, tax codes, vendors and customers.
    /// </summary>
    public const string MasterDataRead = "master-data:read";
    public const string MasterDataManage = "master-data:manage";

    public const string WarehousesRead = "warehouses:read";
    public const string WarehousesManage = "warehouses:manage";

    /// <summary>
    /// Farmers (peternak) and their coops (kandang).
    /// </summary>
    public const string FarmersRead = "farmers:read";
    public const string FarmersManage = "farmers:manage";

    public const string ContractsRead = "contracts:read";
    public const string ContractsManage = "contracts:manage";

    public const string CyclesRead = "cycles:read";
    public const string CyclesManage = "cycles:manage";

    /// <summary>
    /// Finance setup: chart of accounts, cost centers, fiscal periods, journal templates and auto journal mappings.
    /// </summary>
    public const string FinanceSetupRead = "finance-setup:read";
    public const string FinanceSetupManage = "finance-setup:manage";

    public const string FiscalPeriodsClose = "fiscal-periods:close";

    public const string JournalsRead = "journals:read";

    /// <summary>
    /// Create, edit and delete draft manual journals (maker).
    /// </summary>
    public const string JournalsCreate = "journals:create";

    /// <summary>
    /// Approve draft journals created by someone else (checker).
    /// </summary>
    public const string JournalsApprove = "journals:approve";

    /// <summary>
    /// Post approved journals to the ledger and reverse posted journals.
    /// </summary>
    public const string JournalsPost = "journals:post";

    /// <summary>
    /// General ledger (buku besar) and trial balance (neraca saldo).
    /// </summary>
    public const string FinanceReportsRead = "finance-reports:read";

    public const string PurchasingRead = "purchasing:read";

    /// <summary>
    /// Create, edit, cancel and close purchase orders.
    /// </summary>
    public const string PurchasingManage = "purchasing:manage";

    public const string PurchasingApprove = "purchasing:approve";

    /// <summary>
    /// Stock balances, stock cards, goods receipts and transfers (read only).
    /// </summary>
    public const string InventoryRead = "inventory:read";

    /// <summary>
    /// Post goods receipts (bukti penerimaan barang).
    /// </summary>
    public const string InventoryReceive = "inventory:receive";

    /// <summary>
    /// Post stock transfers, including sapronak deliveries to coops.
    /// </summary>
    public const string InventoryTransfer = "inventory:transfer";

    /// <summary>
    /// Daily recordings, harvests and cycle performance (read only).
    /// </summary>
    public const string ProductionRead = "production:read";

    /// <summary>
    /// Chick-in, daily recording and harvest entry (typically the PPL).
    /// </summary>
    public const string ProductionRecord = "production:record";

    /// <summary>
    /// Revise an existing daily recording (with a reason, kept in the revision history).
    /// </summary>
    public const string ProductionRevise = "production:revise";

    public const string ProductionClose = "production:close";

    /// <summary>
    /// Return leftover sapronak from a coop and move feed between coops via a central warehouse.
    /// </summary>
    public const string InventoryReturn = "inventory:return";

    /// <summary>
    /// Sales orders, delivery orders and sales invoices (read only).
    /// </summary>
    public const string SalesRead = "sales:read";

    /// <summary>
    /// Create, edit, cancel and close sales orders.
    /// </summary>
    public const string SalesManage = "sales:manage";

    /// <summary>
    /// Approve sales orders within the customer's credit limit.
    /// </summary>
    public const string SalesApprove = "sales:approve";

    /// <summary>
    /// Approve sales orders above the customer's credit limit (with a reason).
    /// </summary>
    public const string SalesCreditOverride = "sales:credit-override";

    /// <summary>
    /// Create and cancel delivery orders (surat jalan) of harvested birds.
    /// </summary>
    public const string SalesDeliver = "sales:deliver";

    /// <summary>
    /// Create, post and cancel (draft) sales invoices.
    /// </summary>
    public const string SalesInvoice = "sales:invoice";

    /// <summary>
    /// Customer receipts, receivable ledger and aging (read only).
    /// </summary>
    public const string ReceivablesRead = "receivables:read";

    /// <summary>
    /// Record customer receipts (penerimaan pembayaran).
    /// </summary>
    public const string ReceivablesManage = "receivables:manage";

    /// <summary>
    /// Void customer receipts (reverses their journal).
    /// </summary>
    public const string ReceivablesVoid = "receivables:void";

    /// <summary>
    /// Cash/bank accounts, cash transactions, transfers, cash &amp; bank ledger and reconciliations (read only).
    /// </summary>
    public const string CashBankRead = "cash-bank:read";

    /// <summary>
    /// Manage cash/bank accounts, record cash-in/cash-out (draft), post cash-in and bank transfers.
    /// </summary>
    public const string CashBankManage = "cash-bank:manage";

    /// <summary>
    /// Approve and post cash-out transactions created by someone else (checker).
    /// </summary>
    public const string CashBankApprove = "cash-bank:approve";

    /// <summary>
    /// Bank reconciliation (statement lines, matching, completion).
    /// </summary>
    public const string CashBankReconcile = "cash-bank:reconcile";

    /// <summary>
    /// Vendor invoices, payment vouchers, payable ledger and aging (read only).
    /// </summary>
    public const string PayablesRead = "payables:read";

    /// <summary>
    /// Register and post vendor invoices within the price tolerance; create payment vouchers (maker).
    /// </summary>
    public const string PayablesManage = "payables:manage";

    /// <summary>
    /// Post vendor invoices whose price differs from the purchase order above the vendor's tolerance.
    /// </summary>
    public const string PayablesApproveVariance = "payables:approve-variance";

    /// <summary>
    /// Approve payment vouchers created by someone else (checker).
    /// </summary>
    public const string PayablesApprove = "payables:approve";

    /// <summary>
    /// Pay approved payment vouchers (treasury).
    /// </summary>
    public const string PayablesPay = "payables:pay";

    /// <summary>
    /// Cycle cost (HPP) and plasma settlements (read only).
    /// </summary>
    public const string CostingRead = "costing:read";

    /// <summary>
    /// Calculate, recalculate and cancel draft plasma settlements (maker).
    /// </summary>
    public const string SettlementsManage = "settlements:manage";

    /// <summary>
    /// Approve plasma settlements created by someone else (checker); journals them and locks the cycle.
    /// </summary>
    public const string SettlementsApprove = "settlements:approve";

    /// <summary>
    /// Tax recaps (PPN keluaran/masukan, PPh withheld) and their CSV exports.
    /// </summary>
    public const string TaxReportsRead = "tax-reports:read";

    /// <summary>
    /// See failed automatic journal events and put them back in the queue.
    /// </summary>
    public const string SystemOutbox = "system:outbox";

    /// <summary>
    /// Upload attachments (lampiran: foto/dokumen) to be attached to masters and transactions.
    /// </summary>
    public const string AttachmentsUpload = "attachments:upload";

    /// <summary>
    /// See and download attachments (branch-scoped; temporary attachments only by their uploader).
    /// </summary>
    public const string AttachmentsRead = "attachments:read";

    /// <summary>
    /// Delete attachments that are no longer attached to any document.
    /// </summary>
    public const string AttachmentsDelete = "attachments:delete";

    public static readonly IReadOnlyList<string> All =
    [
        UsersRead,
        UsersManage,
        RolesRead,
        RolesManage,
        BranchesRead,
        BranchesManage,
        BranchesAccessAll,
        MasterDataRead,
        MasterDataManage,
        WarehousesRead,
        WarehousesManage,
        FarmersRead,
        FarmersManage,
        ContractsRead,
        ContractsManage,
        CyclesRead,
        CyclesManage,
        FinanceSetupRead,
        FinanceSetupManage,
        FiscalPeriodsClose,
        JournalsRead,
        JournalsCreate,
        JournalsApprove,
        JournalsPost,
        FinanceReportsRead,
        PurchasingRead,
        PurchasingManage,
        PurchasingApprove,
        InventoryRead,
        InventoryReceive,
        InventoryTransfer,
        InventoryReturn,
        ProductionRead,
        ProductionRecord,
        ProductionRevise,
        ProductionClose,
        SalesRead,
        SalesManage,
        SalesApprove,
        SalesCreditOverride,
        SalesDeliver,
        SalesInvoice,
        ReceivablesRead,
        ReceivablesManage,
        ReceivablesVoid,
        CashBankRead,
        CashBankManage,
        CashBankApprove,
        CashBankReconcile,
        PayablesRead,
        PayablesManage,
        PayablesApproveVariance,
        PayablesApprove,
        PayablesPay,
        CostingRead,
        SettlementsManage,
        SettlementsApprove,
        TaxReportsRead,
        SystemOutbox,
        AttachmentsUpload,
        AttachmentsRead,
        AttachmentsDelete
    ];

    public static bool Exists(string permission) => All.Contains(permission);
}
