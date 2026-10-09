namespace MobileApp.Core.Session;

/// <summary>
/// Web.Api permission strings the mobile app looks at (copy of <c>Domain.Roles.Permissions</c>; a unit test keeps
/// them in sync). The app shows menus from permissions, never from role names (PLAN-MOBILE M-10).
/// </summary>
public static class AppPermissions
{
    public const string FarmersRead = "farmers:read";
    public const string FarmersManage = "farmers:manage";
    public const string ContractsRead = "contracts:read";
    public const string ContractsManage = "contracts:manage";
    public const string CyclesRead = "cycles:read";
    public const string ProductionRead = "production:read";
    public const string ProductionRecord = "production:record";
    public const string ProductionRevise = "production:revise";
    public const string ProductionStockReport = "production:stock-report";
    public const string InventoryRead = "inventory:read";
    public const string InventoryRequestFeed = "inventory:request-feed";
    public const string InventoryRequestFeedMutation = "inventory:request-feed-mutation";
    public const string AttachmentsUpload = "attachments:upload";
    public const string AttachmentsRead = "attachments:read";
    public const string ApprovalsDecide = "approvals:decide";
    public const string PartnershipAssignedOnly = "partnership:assigned-only";

    internal static readonly IReadOnlyList<string> All =
    [
        FarmersRead,
        FarmersManage,
        ContractsRead,
        ContractsManage,
        CyclesRead,
        ProductionRead,
        ProductionRecord,
        ProductionRevise,
        ProductionStockReport,
        InventoryRead,
        InventoryRequestFeed,
        InventoryRequestFeedMutation,
        AttachmentsUpload,
        AttachmentsRead,
        ApprovalsDecide,
        PartnershipAssignedOnly
    ];
}
