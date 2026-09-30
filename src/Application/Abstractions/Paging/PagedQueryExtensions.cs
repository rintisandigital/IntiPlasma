using System.Data.Common;
using Dapper;
using SharedKernel;

namespace Application.Abstractions.Paging;

public static class PagedQueryExtensions
{
    /// <summary>
    /// Runs a count query and a page query in one round trip. The page SQL must use
    /// <c>LIMIT @PageSize OFFSET @Offset</c>; both statements receive <paramref name="parameters"/>
    /// plus @PageSize, @Offset and @Search (the ILIKE pattern).
    /// </summary>
    public static async Task<PagedList<T>> QueryPagedAsync<T>(
        this DbConnection connection,
        string countSql,
        string pageSql,
        PageRequest pageRequest,
        object? parameters,
        CancellationToken cancellationToken)
    {
        var dynamicParameters = new DynamicParameters(parameters);
        dynamicParameters.Add("PageSize", pageRequest.PageSize);
        dynamicParameters.Add("Offset", pageRequest.Offset);
        dynamicParameters.Add("Search", pageRequest.SearchPattern, System.Data.DbType.String);

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            new CommandDefinition($"{countSql};\n{pageSql};", dynamicParameters, cancellationToken: cancellationToken));

        long totalCount = await multi.ReadSingleAsync<long>();
        List<T> items = [.. await multi.ReadAsync<T>()];

        return new PagedList<T>(items, pageRequest.Page, pageRequest.PageSize, totalCount);
    }
}
