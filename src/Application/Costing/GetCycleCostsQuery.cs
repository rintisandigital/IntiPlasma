using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Domain.Partnership.Cycles;
using SharedKernel;

namespace Application.Costing;

/// <summary>
/// HPP of many cycles at once (list and export), with the same figures as <see cref="GetCycleCostQuery"/>: the frozen
/// closing cost of a closed cycle, otherwise the running cost of the sapronak consumed so far. Search matches the cycle
/// number, coop and farmer; the date range filters on the chick-in date.
/// </summary>
public sealed record GetCycleCostsQuery(
    PageRequest Paging,
    Guid? BranchId,
    CycleStatus? Status,
    DateOnly? From,
    DateOnly? To) : IQuery<PagedList<CycleCostRowResponse>>;

/// <param name="Scheme">The contract scheme, or null for an inti cycle (no contract).</param>
/// <param name="IsFinal">True when the closing cost is frozen; the figures are running estimates otherwise.</param>
/// <param name="CostPerKg">Final cost per kg harvested, or running cost per kg of estimated live weight.</param>
/// <param name="Adjustment">Final cost − recognized cost, journaled at closing (closed cycles only).</param>
/// <param name="PlasmaIncome">Gross income of the approved settlement (plasma cycles only).</param>
public sealed record CycleCostRowResponse
{
    public Guid CycleId { get; init; }

    public string CycleNumber { get; init; }

    public string Status { get; init; }

    public Guid BranchId { get; init; }

    public string BranchCode { get; init; }

    public string CoopCode { get; init; }

    public string CoopName { get; init; }

    public string FarmerCode { get; init; }

    public string FarmerName { get; init; }

    public string? Scheme { get; init; }

    public DateOnly? ChickInDate { get; init; }

    public DateOnly? ClosedDate { get; init; }

    public int InitialPopulation { get; init; }

    public int HarvestedBirds { get; init; }

    public decimal HarvestedWeightKg { get; init; }

    public bool IsFinal { get; init; }

    public decimal DocCost { get; init; }

    public decimal FeedCost { get; init; }

    public decimal OvkCost { get; init; }

    public decimal TotalCost { get; init; }

    public decimal? CostPerKg { get; init; }

    public decimal RecognizedCost { get; init; }

    public decimal? Adjustment { get; init; }

    public decimal? PlasmaIncome { get; init; }
}

internal sealed class GetCycleCostsQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetCycleCostsQuery, PagedList<CycleCostRowResponse>>
{
    private const string From =
        """
        FROM partnership.production_cycles pc
        JOIN master.branches b ON b.id = pc.branch_id
        JOIN master.coops co ON co.id = pc.coop_id
        JOIN master.farmers f ON f.id = pc.farmer_id
        LEFT JOIN partnership.contracts ct ON ct.id = pc.contract_id
        """;

    private const string Filter =
        """
        WHERE (@AllBranches OR pc.branch_id = ANY(@BranchIds))
          AND (@BranchId::uuid IS NULL OR pc.branch_id = @BranchId)
          AND (@Status::text IS NULL OR pc.status = @Status)
          AND (@From::date IS NULL OR pc.chick_in_date >= @From)
          AND (@To::date IS NULL OR pc.chick_in_date <= @To)
          AND (@Search IS NULL OR pc.number ILIKE @Search OR co.code ILIKE @Search OR co.name ILIKE @Search
               OR f.code ILIKE @Search OR f.name ILIKE @Search)
        """;

    // Running figures mirror CycleCosting: consumption = the opposite of the coop's stock card for chick-in and usage;
    // live weight = harvested kg + birds left × latest recorded BW (or the average harvest weight).
    private const string Select =
        """
        SELECT pc.id AS CycleId, pc.number AS CycleNumber, pc.status AS Status, pc.branch_id AS BranchId, b.code AS BranchCode,
               co.code AS CoopCode, co.name AS CoopName, f.code AS FarmerCode, f.name AS FarmerName, ct.scheme AS Scheme,
               pc.chick_in_date AS ChickInDate, pc.closed_date AS ClosedDate, COALESCE(pc.initial_population, 0) AS InitialPopulation,
               pc.harvested_birds AS HarvestedBirds, pc.harvested_weight_kg AS HarvestedWeightKg,
               pc.closing_cost IS NOT NULL AS IsFinal,
               COALESCE((pc.closing_cost ->> 'docCost')::numeric, used.doc) AS DocCost,
               COALESCE((pc.closing_cost ->> 'feedCost')::numeric, used.feed) AS FeedCost,
               COALESCE((pc.closing_cost ->> 'ovkCost')::numeric, used.ovk) AS OvkCost,
               COALESCE((pc.closing_cost ->> 'totalCost')::numeric, used.doc + used.feed + used.ovk) AS TotalCost,
               CASE WHEN pc.closing_cost IS NOT NULL THEN (pc.closing_cost ->> 'costPerKg')::numeric
                    WHEN live.kg > 0 THEN ROUND((used.doc + used.feed + used.ovk) / live.kg, 2)
               END AS CostPerKg,
               recognized.amount AS RecognizedCost,
               (pc.closing_cost ->> 'adjustment')::numeric AS Adjustment,
               settled.income AS PlasmaIncome
        """ + "\n" + From + "\n" +
        """
        LEFT JOIN LATERAL (
            SELECT COALESCE(SUM(-e.value) FILTER (WHERE i.category = 'Doc'), 0) AS doc,
                   COALESCE(SUM(-e.value) FILTER (WHERE i.category = 'Feed'), 0) AS feed,
                   COALESCE(SUM(-e.value) FILTER (WHERE i.category = 'Ovk'), 0) AS ovk
            FROM inventory.stock_ledger_entries e
            JOIN master.items i ON i.id = e.item_id
            WHERE e.cycle_id = pc.id AND e.type IN ('ChickIn', 'Usage', 'UsageReversal')
        ) used ON TRUE
        LEFT JOIN LATERAL (
            SELECT GREATEST(COALESCE(pc.initial_population, 0) - pc.total_mortality - pc.total_culling - pc.harvested_birds, 0) AS birds,
                   (SELECT r.average_body_weight_gram FROM production.daily_recordings r
                    WHERE r.cycle_id = pc.id AND r.average_body_weight_gram IS NOT NULL
                    ORDER BY r.date DESC LIMIT 1) AS bw_gram
        ) population ON TRUE
        LEFT JOIN LATERAL (
            SELECT CASE WHEN population.birds = 0 THEN pc.harvested_weight_kg
                        ELSE pc.harvested_weight_kg + population.birds * COALESCE(
                            population.bw_gram / 1000,
                            CASE WHEN pc.harvested_birds > 0 THEN pc.harvested_weight_kg / pc.harvested_birds END,
                            0)
                   END AS kg
        ) live ON TRUE
        LEFT JOIN LATERAL (
            SELECT COALESCE(SUM(l.cost_amount), 0) AS amount
            FROM sales.sales_invoice_lines l
            JOIN sales.sales_invoices si ON si.id = l.sales_invoice_id
            WHERE l.cycle_id = pc.id AND si.status IN ('Posted', 'PartiallyPaid', 'Paid')
        ) recognized ON TRUE
        LEFT JOIN LATERAL (
            SELECT s.gross_income AS income
            FROM costing.plasma_settlements s
            WHERE s.cycle_id = pc.id AND s.status IN ('Approved', 'PartiallyPaid', 'Paid')
        ) settled ON TRUE
        """;

    public async Task<Result<PagedList<CycleCostRowResponse>>> Handle(GetCycleCostsQuery query, CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        return await connection.QueryPagedAsync<CycleCostRowResponse>(
            $"SELECT COUNT(*) {From} {Filter}",
            $"{Select} {Filter} ORDER BY COALESCE(pc.chick_in_date, pc.planned_chick_in_date) DESC, pc.number DESC LIMIT @PageSize OFFSET @Offset",
            query.Paging,
            new
            {
                scope.AllBranches,
                scope.BranchIds,
                query.BranchId,
                Status = query.Status?.ToString(),
                query.From,
                query.To
            },
            cancellationToken);
    }
}
