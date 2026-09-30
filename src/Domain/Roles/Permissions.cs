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
        CyclesManage
    ];

    public static bool Exists(string permission) => All.Contains(permission);
}
