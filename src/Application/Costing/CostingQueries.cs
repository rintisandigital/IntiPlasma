using System.Data.Common;
using System.Text.Json;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Dapper;
using Domain.Costing.PlasmaSettlements;
using Domain.Partnership.Cycles;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Costing;

/// <summary>
/// HPP of a cycle: the sapronak consumed so far, the running cost per kg (estimate) or the final cost once closed,
/// the cost already recognized on sales invoices and, after settlement, the plasma's income.
/// </summary>
public sealed record GetCycleCostQuery(Guid CycleId) : IQuery<CycleCostResponse>;

/// <param name="Search">Matches the settlement number, cycle number and farmer code/name.</param>
/// <param name="From">Settlement date from (inclusive).</param>
/// <param name="To">Settlement date to (inclusive).</param>
public sealed record GetPlasmaSettlementsQuery(
    Guid? BranchId,
    Guid? FarmerId,
    Guid? CycleId,
    PlasmaSettlementStatus? Status,
    string? Search = null,
    DateOnly? From = null,
    DateOnly? To = null) : IQuery<IReadOnlyList<PlasmaSettlementResponse>>;

/// <summary>
/// Slip settlement: the calculation lines and totals of one settlement.
/// </summary>
public sealed record GetPlasmaSettlementByIdQuery(Guid PlasmaSettlementId) : IQuery<PlasmaSettlementResponse>;

/// <param name="IsFinal">True once the cycle is closed: the figures are the frozen closing cost.</param>
/// <param name="CostPerKg">Running (estimate) or final cost per kg of live weight.</param>
/// <param name="Adjustment">Final cost − recognized cost, journaled at closing (closed cycles only).</param>
/// <param name="PlasmaIncome">Approved settlement's gross income (plasma cycles only).</param>
public sealed record CycleCostResponse(
    Guid CycleId,
    string CycleNumber,
    string Status,
    bool IsFinal,
    IReadOnlyList<ConsumedInputResponse> Inputs,
    decimal DocCost,
    decimal FeedCost,
    decimal OvkCost,
    decimal TotalCost,
    int HarvestedBirds,
    decimal HarvestedWeightKg,
    decimal LiveWeightKg,
    decimal? CostPerKg,
    decimal? CostPerBird,
    decimal RecognizedCost,
    decimal? Adjustment,
    decimal? PlasmaIncome,
    decimal? TotalCostWithPlasma);

public sealed record ConsumedInputResponse(Guid ItemId, string ItemCode, string Category, decimal Quantity, decimal Cost);

public sealed record PlasmaSettlementResponse
{
    /// <summary>
    /// Lampiran: attachment ids; metadata via <c>GET /attachments?ids=</c>.
    /// </summary>
    public Guid[] Documents { get; init; } = [];

    public const string Select =
        """
        SELECT s.id AS Id, s.number AS Number, s.branch_id AS BranchId, b.code AS BranchCode,
               s.cycle_id AS CycleId, c.number AS CycleNumber, s.farmer_id AS FarmerId, f.code AS FarmerCode, f.name AS FarmerName,
               s.contract_id AS ContractId, k.code AS ContractCode, s.scheme AS Scheme, s.settlement_date AS SettlementDate,
               s.status AS Status, s.notes AS Notes, s.gross_income AS GrossIncome, t.code AS IncomeTaxCode,
               s.income_tax_rate_percent AS IncomeTaxRatePercent, s.income_tax_amount AS IncomeTaxAmount,
               s.debt_deduction AS DebtDeduction, s.net_payable AS NetPayable, s.deficit AS Deficit, s.paid_amount AS PaidAmount,
               (s.net_payable - s.paid_amount) AS Outstanding, s.approved_at_utc AS ApprovedAtUtc,
               s.cancellation_reason AS CancellationReason,
               co.code AS CoopCode, co.name AS CoopName, c.chick_in_date AS ChickInDate, c.closed_date AS ClosedDate,
               c.status AS CycleStatus, NULLIF(TRIM(CONCAT(cu.first_name, ' ', cu.last_name)), '') AS CreatedByName,
               NULLIF(TRIM(CONCAT(au.first_name, ' ', au.last_name)), '') AS ApprovedByName,
               COALESCE(s.modified_at_utc, s.created_at_utc) AS CalculatedAtUtc,
               s.documents AS Documents
        FROM costing.plasma_settlements s
        JOIN master.branches b ON b.id = s.branch_id
        JOIN partnership.production_cycles c ON c.id = s.cycle_id
        JOIN master.coops co ON co.id = c.coop_id
        JOIN master.farmers f ON f.id = s.farmer_id
        JOIN partnership.contracts k ON k.id = s.contract_id
        LEFT JOIN master.tax_codes t ON t.id = s.income_tax_code_id
        LEFT JOIN identity.users cu ON cu.id = s.created_by
        LEFT JOIN identity.users au ON au.id = s.approved_by
        """;

    public Guid Id { get; init; }

    public string Number { get; init; }

    public Guid BranchId { get; init; }

    public string BranchCode { get; init; }

    public Guid CycleId { get; init; }

    public string CycleNumber { get; init; }

    public Guid FarmerId { get; init; }

    public string FarmerCode { get; init; }

    public string FarmerName { get; init; }

    public Guid ContractId { get; init; }

    public string ContractCode { get; init; }

    public string Scheme { get; init; }

    public DateOnly SettlementDate { get; init; }

    public string Status { get; init; }

    public string? Notes { get; init; }

    /// <summary>
    /// Plasma's income before tax and deductions; negative = loss.
    /// </summary>
    public decimal GrossIncome { get; init; }

    public string? IncomeTaxCode { get; init; }

    public decimal IncomeTaxRatePercent { get; init; }

    public decimal IncomeTaxAmount { get; init; }

    public decimal DebtDeduction { get; init; }

    public decimal NetPayable { get; init; }

    /// <summary>
    /// Loss booked as piutang plasma.
    /// </summary>
    public decimal Deficit { get; init; }

    public decimal PaidAmount { get; init; }

    public decimal Outstanding { get; init; }

    public DateTime? ApprovedAtUtc { get; init; }

    public string? CancellationReason { get; init; }

    public string CoopCode { get; init; }

    public string CoopName { get; init; }

    public DateOnly? ChickInDate { get; init; }

    public DateOnly? ClosedDate { get; init; }

    public string CycleStatus { get; init; }

    public string? CreatedByName { get; init; }

    public string? ApprovedByName { get; init; }

    /// <summary>
    /// When the figures were last calculated (created or recalculated).
    /// </summary>
    public DateTime CalculatedAtUtc { get; init; }

    /// <summary>
    /// Only filled by the detail endpoint.
    /// </summary>
    public IReadOnlyList<PlasmaSettlementLineResponse>? Lines { get; init; }

    /// <summary>
    /// The cycle's performance frozen at closing (detail endpoint only).
    /// </summary>
    public CyclePerformance? Performance { get; init; }
}

public sealed record PlasmaSettlementLineResponse(
    int LineNumber,
    string Type,
    string Description,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal Amount);

internal sealed class GetCycleCostQueryHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : IQueryHandler<GetCycleCostQuery, CycleCostResponse>
{
    public async Task<Result<CycleCostResponse>> Handle(GetCycleCostQuery query, CancellationToken cancellationToken)
    {
        ProductionCycle? cycle = await context.ProductionCycles.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == query.CycleId, cancellationToken);

        if (cycle is null)
        {
            return Result.Failure<CycleCostResponse>(CycleErrors.NotFound(query.CycleId));
        }

        Result access = await branchAccess.EnsureAccessAsync(cycle.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<CycleCostResponse>(access.Error);
        }

        List<ConsumedInput> inputs = await CycleCosting.ConsumedInputsAsync(context, cycle.Id, cancellationToken);
        decimal recognized = await CycleCosting.RecognizedCostAsync(context, cycle.Id, cancellationToken);
        decimal liveKg = await CycleCosting.EstimatedLiveWeightKgAsync(context, cycle, cancellationToken);

        decimal? plasmaIncome = await context.PlasmaSettlements.AsNoTracking()
            .Where(s => s.CycleId == cycle.Id &&
                        (s.Status == PlasmaSettlementStatus.Approved || s.Status == PlasmaSettlementStatus.PartiallyPaid ||
                         s.Status == PlasmaSettlementStatus.Paid))
            .Select(s => (decimal?)s.GrossIncome.Amount)
            .SingleOrDefaultAsync(cancellationToken);

        CycleCostSummary? final = cycle.ClosingCost;
        decimal CostOf(Domain.MasterData.Items.ItemCategory category) =>
            inputs.Where(i => i.Category == category).Sum(i => i.Cost.Amount);

        decimal total = final?.TotalCost ?? inputs.Sum(i => i.Cost.Amount);

        return new CycleCostResponse(
            cycle.Id,
            cycle.Number,
            cycle.Status.ToString(),
            final is not null,
            [.. inputs.Select(i => new ConsumedInputResponse(i.ItemId, i.ItemCode, i.Category.ToString(), i.Quantity, i.Cost.Amount))],
            final?.DocCost ?? CostOf(Domain.MasterData.Items.ItemCategory.Doc),
            final?.FeedCost ?? CostOf(Domain.MasterData.Items.ItemCategory.Feed),
            final?.OvkCost ?? CostOf(Domain.MasterData.Items.ItemCategory.Ovk),
            total,
            cycle.HarvestedBirds,
            cycle.HarvestedWeightKg,
            liveKg,
            final?.CostPerKg ?? (liveKg > 0 ? decimal.Round(total / liveKg, 2, MidpointRounding.AwayFromZero) : null),
            final?.CostPerBird,
            recognized,
            final?.Adjustment,
            plasmaIncome,
            plasmaIncome is null ? null : total + Math.Max(plasmaIncome.Value, 0m));
    }
}

internal sealed class GetPlasmaSettlementsQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetPlasmaSettlementsQuery, IReadOnlyList<PlasmaSettlementResponse>>
{
    public async Task<Result<IReadOnlyList<PlasmaSettlementResponse>>> Handle(GetPlasmaSettlementsQuery query, CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql =
            $"""
            {PlasmaSettlementResponse.Select}
            WHERE (@AllBranches OR s.branch_id = ANY(@BranchIds))
              AND (@BranchId::uuid IS NULL OR s.branch_id = @BranchId)
              AND (@FarmerId::uuid IS NULL OR s.farmer_id = @FarmerId)
              AND (@CycleId::uuid IS NULL OR s.cycle_id = @CycleId)
              AND (@Status::text IS NULL OR s.status = @Status)
              AND (@From::date IS NULL OR s.settlement_date >= @From)
              AND (@To::date IS NULL OR s.settlement_date <= @To)
              AND (@Search::text IS NULL OR s.number ILIKE @Search OR c.number ILIKE @Search
                   OR f.code ILIKE @Search OR f.name ILIKE @Search)
            ORDER BY s.settlement_date DESC, s.number DESC;
            """;

        IEnumerable<PlasmaSettlementResponse> rows = await connection.QueryAsync<PlasmaSettlementResponse>(new CommandDefinition(
            sql,
            new
            {
                scope.AllBranches,
                scope.BranchIds,
                query.BranchId,
                query.FarmerId,
                query.CycleId,
                Status = query.Status?.ToString(),
                query.From,
                query.To,
                Search = new PageRequest(null, null, query.Search).SearchPattern
            },
            cancellationToken: cancellationToken));

        return rows.ToList();
    }
}

internal sealed class GetPlasmaSettlementByIdQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetPlasmaSettlementByIdQuery, PlasmaSettlementResponse>
{
    // The closing performance is stored as camelCase JSON (see the production cycle's EF configuration).
    private static readonly JsonSerializerOptions PerformanceJson = new(JsonSerializerDefaults.Web);

    public async Task<Result<PlasmaSettlementResponse>> Handle(GetPlasmaSettlementByIdQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql =
            $"""
            {PlasmaSettlementResponse.Select}
            WHERE s.id = @PlasmaSettlementId;

            SELECT l.line_number AS LineNumber, l.type AS Type, l.description AS Description, l.quantity AS Quantity,
                   l.unit_price AS UnitPrice, l.amount AS Amount
            FROM costing.plasma_settlement_lines l
            WHERE l.plasma_settlement_id = @PlasmaSettlementId
            ORDER BY l.line_number;

            SELECT c.closing_performance::text
            FROM costing.plasma_settlements s
            JOIN partnership.production_cycles c ON c.id = s.cycle_id
            WHERE s.id = @PlasmaSettlementId;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { query.PlasmaSettlementId }, cancellationToken: cancellationToken));

        PlasmaSettlementResponse? settlement = await multi.ReadSingleOrDefaultAsync<PlasmaSettlementResponse>();
        if (settlement is null)
        {
            return Result.Failure<PlasmaSettlementResponse>(PlasmaSettlementErrors.NotFound(query.PlasmaSettlementId));
        }

        Result access = await branchAccess.EnsureAccessAsync(settlement.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<PlasmaSettlementResponse>(access.Error);
        }

        List<PlasmaSettlementLineResponse> lines = [.. await multi.ReadAsync<PlasmaSettlementLineResponse>()];
        string? performance = await multi.ReadSingleOrDefaultAsync<string?>();

        return settlement with
        {
            Lines = lines,
            Performance = performance is null ? null : JsonSerializer.Deserialize<CyclePerformance>(performance, PerformanceJson)
        };
    }
}
