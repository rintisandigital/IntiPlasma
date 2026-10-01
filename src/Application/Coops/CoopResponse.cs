namespace Application.Coops;

public sealed record CoopResponse
{
    /// <summary>
    /// Lampiran: attachment ids; metadata via <c>GET /attachments?ids=</c>.
    /// </summary>
    public Guid[] Documents { get; init; } = [];

    public const string Select =
        """
        SELECT c.id AS Id, c.code AS Code, c.name AS Name, c.farmer_id AS FarmerId, f.code AS FarmerCode,
               f.name AS FarmerName, f.type AS FarmerType, c.branch_id AS BranchId, b.code AS BranchCode,
               c.capacity AS Capacity, c.house_type AS HouseType, c.address AS Address,
               c.latitude AS Latitude, c.longitude AS Longitude, c.is_active AS IsActive,
               (SELECT pc.id FROM partnership.production_cycles pc
                WHERE pc.coop_id = c.id AND pc.status IN ('Planned', 'Active', 'Harvesting')) AS OpenCycleId,
               c.documents AS Documents
        FROM master.coops c
        JOIN master.farmers f ON f.id = c.farmer_id
        JOIN master.branches b ON b.id = c.branch_id
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
    /// The planned or running production cycle, if any.
    /// </summary>
    public Guid? OpenCycleId { get; init; }
}
