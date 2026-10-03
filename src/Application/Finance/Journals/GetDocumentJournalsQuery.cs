using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using SharedKernel;

namespace Application.Finance.Journals;

/// <summary>
/// The journals of one business document (automatic journals have the document as their source, e.g. a goods receipt,
/// an invoice or its void/advance application), with their lines, in the accessible branches. Oldest first.
/// </summary>
public sealed record GetDocumentJournalsQuery(IReadOnlyCollection<Guid> SourceIds) : IQuery<IReadOnlyList<JournalResponse>>;

internal sealed class GetDocumentJournalsQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetDocumentJournalsQuery, IReadOnlyList<JournalResponse>>
{
    public async Task<Result<IReadOnlyList<JournalResponse>>> Handle(GetDocumentJournalsQuery query, CancellationToken cancellationToken)
    {
        if (query.SourceIds.Count == 0)
        {
            return Array.Empty<JournalResponse>();
        }

        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql =
            $"""
            {JournalResponse.Select}
            WHERE j.source_id = ANY(@SourceIds) AND (@AllBranches OR j.branch_id = ANY(@BranchIds))
            ORDER BY j.date, j.posted_at_utc NULLS LAST, j.number;

            SELECT l.journal_entry_id AS JournalId, l.line_number AS LineNumber, l.account_id AS AccountId, a.code AS AccountCode,
                   a.name AS AccountName, l.cost_center_id AS CostCenterId, c.code AS CostCenterCode, l.description AS Description,
                   l.debit AS Debit, l.credit AS Credit
            FROM finance.journal_lines l
            JOIN finance.journal_entries j ON j.id = l.journal_entry_id
            JOIN finance.accounts a ON a.id = l.account_id
            LEFT JOIN finance.cost_centers c ON c.id = l.cost_center_id
            WHERE j.source_id = ANY(@SourceIds) AND (@AllBranches OR j.branch_id = ANY(@BranchIds))
            ORDER BY l.line_number;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new { SourceIds = query.SourceIds.ToArray(), scope.AllBranches, scope.BranchIds },
            cancellationToken: cancellationToken));

        List<JournalResponse> journals = [.. await multi.ReadAsync<JournalResponse>()];
        ILookup<Guid, LineRow> lines = (await multi.ReadAsync<LineRow>()).ToLookup(l => l.JournalId);

        return journals
            .Select(j => j with
            {
                Lines = [.. lines[j.Id].Select(l => new JournalLineResponse(
                    l.LineNumber, l.AccountId, l.AccountCode, l.AccountName, l.CostCenterId, l.CostCenterCode, l.Description, l.Debit, l.Credit))]
            })
            .ToList();
    }

    private sealed record LineRow(
        Guid JournalId,
        int LineNumber,
        Guid AccountId,
        string AccountCode,
        string AccountName,
        Guid? CostCenterId,
        string? CostCenterCode,
        string? Description,
        decimal Debit,
        decimal Credit);
}
