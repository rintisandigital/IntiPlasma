using System.Text.Json;

namespace Application.Cycles;

public sealed record CycleResponse
{
    public const string Select =
        """
        SELECT pc.id AS Id, pc.number AS Number, pc.status AS Status, pc.branch_id AS BranchId, b.code AS BranchCode,
               pc.farmer_id AS FarmerId, f.code AS FarmerCode, f.name AS FarmerName, f.type AS FarmerType,
               pc.coop_id AS CoopId, c.code AS CoopCode, c.name AS CoopName,
               pc.contract_id AS ContractId, ct.code AS ContractCode,
               pc.planned_chick_in_date AS PlannedChickInDate, pc.planned_population AS PlannedPopulation,
               pc.chick_in_date AS ChickInDate, pc.initial_population AS InitialPopulation,
               pc.notes AS Notes, pc.cancellation_reason AS CancellationReason
        FROM partnership.production_cycles pc
        JOIN master.branches b ON b.id = pc.branch_id
        JOIN master.farmers f ON f.id = pc.farmer_id
        JOIN master.coops c ON c.id = pc.coop_id
        LEFT JOIN partnership.contracts ct ON ct.id = pc.contract_id
        """;

    public Guid Id { get; init; }

    public string Number { get; init; }

    public string Status { get; init; }

    public Guid BranchId { get; init; }

    public string BranchCode { get; init; }

    public Guid FarmerId { get; init; }

    public string FarmerCode { get; init; }

    public string FarmerName { get; init; }

    public string FarmerType { get; init; }

    public Guid CoopId { get; init; }

    public string CoopCode { get; init; }

    public string CoopName { get; init; }

    public Guid? ContractId { get; init; }

    public string? ContractCode { get; init; }

    public DateOnly PlannedChickInDate { get; init; }

    public int PlannedPopulation { get; init; }

    public DateOnly? ChickInDate { get; init; }

    public int? InitialPopulation { get; init; }

    public string? Notes { get; init; }

    public string? CancellationReason { get; init; }

    /// <summary>
    /// Contract terms frozen when the cycle was planned (detail endpoint only).
    /// </summary>
    public JsonElement? ContractSnapshot { get; init; }
}
