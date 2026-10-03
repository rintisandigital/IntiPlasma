using Application.Abstractions.Paging;
using SharedKernel;

namespace Web.App.Infrastructure.Export;

/// <summary>
/// Runs the screen's own list query page by page (100 rows, the query maximum) until every row matching the
/// filters is collected, so an export always equals what the list shows, without a separate export query.
/// </summary>
public static class PagedExportRunner
{
    public static Error TooManyRows(int maxRows) => Error.Problem(
        "Export.TooManyRows",
        $"The export is limited to {maxRows:N0} rows; narrow the filters and try again.");

    public static async Task<Result<IReadOnlyList<T>>> CollectAsync<T>(
        Func<PageRequest, CancellationToken, Task<Result<PagedList<T>>>> fetch,
        string? search,
        int maxRows,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fetch);

        var rows = new List<T>();
        int page = 1;

        while (true)
        {
            Result<PagedList<T>> result = await fetch(new PageRequest(page, PageRequest.MaxPageSize, search), cancellationToken);

            if (result.IsFailure)
            {
                return Result.Failure<IReadOnlyList<T>>(result.Error);
            }

            if (result.Value.TotalCount > maxRows)
            {
                return Result.Failure<IReadOnlyList<T>>(TooManyRows(maxRows));
            }

            rows.AddRange(result.Value.Items);

            if (!result.Value.HasNextPage || result.Value.Items.Count == 0)
            {
                return rows;
            }

            page++;
        }
    }
}
