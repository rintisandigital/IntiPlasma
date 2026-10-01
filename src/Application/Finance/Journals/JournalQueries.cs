using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Dapper;
using Domain.Finance.Journals;
using SharedKernel;

namespace Application.Finance.Journals;

public sealed record GetJournalsQuery(
    PageRequest Paging,
    Guid? BranchId,
    JournalStatus? Status,
    JournalSource? Source,
    DateOnly? From,
    DateOnly? To) : IQuery<PagedList<JournalResponse>>;

public sealed record GetJournalByIdQuery(Guid JournalId) : IQuery<JournalResponse>;

public sealed record JournalResponse
{
    /// <summary>
    /// Lampiran: attachment ids; metadata via <c>GET /attachments?ids=</c>.
    /// </summary>
    public Guid[] Documents { get; init; } = [];

    public const string Select =
        """
        SELECT j.id AS Id, j.number AS Number, j.branch_id AS BranchId, b.code AS BranchCode, j.date AS Date,
               j.description AS Description, j.source AS Source, j.source_type AS SourceType, j.source_id AS SourceId,
               j.status AS Status,
               (SELECT COALESCE(SUM(l.debit), 0) FROM finance.journal_lines l WHERE l.journal_entry_id = j.id) AS TotalDebit,
               (SELECT COALESCE(SUM(l.credit), 0) FROM finance.journal_lines l WHERE l.journal_entry_id = j.id) AS TotalCredit,
               j.created_by AS CreatedBy, j.approved_by AS ApprovedBy, j.approved_at_utc AS ApprovedAtUtc,
               j.posted_by AS PostedBy, j.posted_at_utc AS PostedAtUtc,
               j.reversal_of_id AS ReversalOfId, j.reversed_by_id AS ReversedById,
               j.documents AS Documents
        FROM finance.journal_entries j
        JOIN master.branches b ON b.id = j.branch_id
        """;

    public Guid Id { get; init; }

    /// <summary>
    /// Assigned when the journal is posted.
    /// </summary>
    public string? Number { get; init; }

    public Guid BranchId { get; init; }

    public string BranchCode { get; init; }

    public DateOnly Date { get; init; }

    public string Description { get; init; }

    public string Source { get; init; }

    public string? SourceType { get; init; }

    public Guid? SourceId { get; init; }

    public string Status { get; init; }

    public decimal TotalDebit { get; init; }

    public decimal TotalCredit { get; init; }

    public Guid? CreatedBy { get; init; }

    public Guid? ApprovedBy { get; init; }

    public DateTime? ApprovedAtUtc { get; init; }

    public Guid? PostedBy { get; init; }

    public DateTime? PostedAtUtc { get; init; }

    public Guid? ReversalOfId { get; init; }

    public Guid? ReversedById { get; init; }

    /// <summary>
    /// Only filled by the detail endpoint.
    /// </summary>
    public IReadOnlyList<JournalLineResponse>? Lines { get; init; }
}

public sealed record JournalLineResponse(
    int LineNumber,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    Guid? CostCenterId,
    string? CostCenterCode,
    string? Description,
    decimal Debit,
    decimal Credit);

internal sealed class GetJournalsQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetJournalsQuery, PagedList<JournalResponse>>
{
    private const string Filter =
        """
        WHERE (@AllBranches OR j.branch_id = ANY(@BranchIds))
          AND (@BranchId::uuid IS NULL OR j.branch_id = @BranchId)
          AND (@Status::text IS NULL OR j.status = @Status)
          AND (@Source::text IS NULL OR j.source = @Source)
          AND (@From::date IS NULL OR j.date >= @From)
          AND (@To::date IS NULL OR j.date <= @To)
          AND (@Search IS NULL OR j.number ILIKE @Search OR j.description ILIKE @Search)
        """;

    public async Task<Result<PagedList<JournalResponse>>> Handle(GetJournalsQuery query, CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        return await connection.QueryPagedAsync<JournalResponse>(
            $"SELECT COUNT(*) FROM finance.journal_entries j {Filter}",
            $"{JournalResponse.Select} {Filter} ORDER BY j.date DESC, j.number DESC NULLS FIRST LIMIT @PageSize OFFSET @Offset",
            query.Paging,
            new
            {
                scope.AllBranches,
                scope.BranchIds,
                query.BranchId,
                Status = query.Status?.ToString(),
                Source = query.Source?.ToString(),
                query.From,
                query.To
            },
            cancellationToken);
    }
}

internal sealed class GetJournalByIdQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetJournalByIdQuery, JournalResponse>
{
    public async Task<Result<JournalResponse>> Handle(GetJournalByIdQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql =
            $"""
            {JournalResponse.Select}
            WHERE j.id = @JournalId;

            SELECT l.line_number AS LineNumber, l.account_id AS AccountId, a.code AS AccountCode, a.name AS AccountName,
                   l.cost_center_id AS CostCenterId, c.code AS CostCenterCode, l.description AS Description,
                   l.debit AS Debit, l.credit AS Credit
            FROM finance.journal_lines l
            JOIN finance.accounts a ON a.id = l.account_id
            LEFT JOIN finance.cost_centers c ON c.id = l.cost_center_id
            WHERE l.journal_entry_id = @JournalId
            ORDER BY l.line_number;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { query.JournalId }, cancellationToken: cancellationToken));

        JournalResponse? journal = await multi.ReadSingleOrDefaultAsync<JournalResponse>();

        if (journal is null)
        {
            return Result.Failure<JournalResponse>(JournalErrors.NotFound(query.JournalId));
        }

        Result access = await branchAccess.EnsureAccessAsync(journal.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<JournalResponse>(access.Error);
        }

        return journal with { Lines = [.. await multi.ReadAsync<JournalLineResponse>()] };
    }
}
