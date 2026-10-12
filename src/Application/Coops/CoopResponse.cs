using Application.Approvals;
using Domain.MasterData.Coops;

namespace Application.Coops;

public sealed record CoopResponse : IHasLatestApproval
{
    /// <summary>
    /// Lampiran: attachment ids; metadata via <c>GET /attachments?ids=</c>.
    /// </summary>
    public Guid[] Documents { get; init; } = [];

    public static readonly string Select =
        $"""
        SELECT c.id AS Id, c.code AS Code, c.name AS Name, c.farmer_id AS FarmerId, f.code AS FarmerCode,
               f.name AS FarmerName, f.type AS FarmerType, c.branch_id AS BranchId, b.code AS BranchCode,
               c.capacity AS Capacity, c.house_type AS HouseType, c.address AS Address,
               c.latitude AS Latitude, c.longitude AS Longitude, c.is_active AS IsActive,
               (SELECT pc.id FROM partnership.production_cycles pc
                WHERE pc.coop_id = c.id AND pc.status IN ('Planned', 'Active', 'Harvesting')) AS OpenCycleId,
               c.documents AS Documents,
               (SELECT w.id FROM master.warehouses w WHERE w.coop_id = c.id LIMIT 1) AS WarehouseId,
               c.field_officer_user_id AS FieldOfficerUserId,
               NULLIF(TRIM(CONCAT(fo.first_name, ' ', fo.last_name)), '') AS FieldOfficerName,
               c.registration_status AS RegistrationStatus,
               {ApprovalSql.LatestColumns}
        FROM master.coops c
        JOIN master.farmers f ON f.id = c.farmer_id
        JOIN master.branches b ON b.id = c.branch_id
        LEFT JOIN identity.users fo ON fo.id = c.field_officer_user_id
        {ApprovalSql.LatestJoin("c", "Coop")}
        """;

    public Guid Id { get; init; }

    public string Code { get; init; }

    public string Name { get; init; }

    public Guid FarmerId { get; init; }

    public string FarmerCode { get; init; }

    public string FarmerName { get; init; }

    public string FarmerType { get; init; }

    public Guid BranchId { get; init; }

    public string BranchCode { get; init; }

    public int Capacity { get; init; }

    public string HouseType { get; init; }

    public string? Address { get; init; }

    public decimal? Latitude { get; init; }

    public decimal? Longitude { get; init; }

    public bool IsActive { get; init; }

    /// <summary>
    /// Draft, PendingApproval, Rejected or Approved (PLAN-MOBILE M-81).
    /// </summary>
    public string RegistrationStatus { get; init; }

    public Guid? ApprovalRequestId { get; init; }

    public string? ApprovalDocumentType { get; init; }

    public string? ApprovalStatus { get; init; }

    public int? ApprovalLevel { get; init; }

    public int? ApprovalLevelCount { get; init; }

    public string? ApprovalLevelName { get; init; }

    public string? ApprovalRejectReason { get; init; }

    /// <summary>
    /// The planned or running production cycle, if any.
    /// </summary>
    public Guid? OpenCycleId { get; init; }

    /// <summary>
    /// The farm warehouse (gudang kandang GK-…), once created; stock via <c>inventory/stock-balances?warehouseId=</c>.
    /// </summary>
    public Guid? WarehouseId { get; init; }

    /// <summary>
    /// PPL responsible for the coop (PLAN-MOBILE M-11).
    /// </summary>
    public Guid? FieldOfficerUserId { get; init; }

    public string? FieldOfficerName { get; init; }

    /// <summary>
    /// Survey data (building, equipment, production plan, …); only filled by the get-by-id query.
    /// </summary>
    public CoopProfile Profile { get; init; } = new();
}
