using System.Data.Common;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using Domain.Finance.Accounts;
using SharedKernel;

namespace Application.Finance.Accounts;

/// <summary>
/// The chart of accounts in code order, with each account's depth in the hierarchy (1 = top level).
/// </summary>
public sealed record GetAccountsQuery(AccountType? Type, string? Search, bool PostableOnly) : IQuery<IReadOnlyList<AccountResponse>>;

public sealed record AccountResponse
{
    public Guid Id { get; init; }

    public string Code { get; init; }

    public string Name { get; init; }

    public string Type { get; init; }

    public string NormalBalance { get; init; }

    public Guid? ParentId { get; init; }

    public string? ParentCode { get; init; }

    public int Level { get; init; }

    public bool IsPostable { get; init; }

    public bool IsActive { get; init; }

    public string CashFlowCategory { get; init; }
}

internal sealed class GetAccountsQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetAccountsQuery, IReadOnlyList<AccountResponse>>
{
    public async Task<Result<IReadOnlyList<AccountResponse>>> Handle(GetAccountsQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        const string sql =
            """
            WITH RECURSIVE tree AS (
                SELECT a.id, 1 AS level FROM finance.accounts a WHERE a.parent_id IS NULL
                UNION ALL
                SELECT c.id, t.level + 1 FROM finance.accounts c JOIN tree t ON c.parent_id = t.id
            )
            SELECT a.id AS Id, a.code AS Code, a.name AS Name, a.type AS Type, a.normal_balance AS NormalBalance,
                   a.parent_id AS ParentId, p.code AS ParentCode, t.level AS Level,
                   a.is_postable AS IsPostable, a.is_active AS IsActive, a.cash_flow_category AS CashFlowCategory
            FROM finance.accounts a
            JOIN tree t ON t.id = a.id
            LEFT JOIN finance.accounts p ON p.id = a.parent_id
            WHERE (@Type::text IS NULL OR a.type = @Type)
              AND (@Search::text IS NULL OR a.code ILIKE @Search OR a.name ILIKE @Search)
              AND (NOT @PostableOnly OR (a.is_postable AND a.is_active))
            ORDER BY a.code
            """;

        IEnumerable<AccountResponse> accounts = await connection.QueryAsync<AccountResponse>(new CommandDefinition(
            sql,
            new
            {
                Type = query.Type?.ToString(),
                Search = string.IsNullOrWhiteSpace(query.Search) ? null : $"%{query.Search.Trim()}%",
                query.PostableOnly
            },
            cancellationToken: cancellationToken));

        return accounts.ToList();
    }
}
