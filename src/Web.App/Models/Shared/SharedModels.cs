using SharedKernel;

namespace Web.App.Models.Shared;

/// <summary>
/// Model of <c>_PageHeader</c>: title and subtitle at the top of a page in the content iframe.
/// </summary>
public sealed record PageHeaderModel(string Title, string? Subtitle = null);

/// <summary>
/// Model of <c>_FilterBar</c>: the search box of a list page (submitted as <c>?search=</c>).
/// </summary>
public sealed record FilterBarModel(string? Search, string Placeholder = "Search");

/// <summary>
/// Model of <c>_Pagination</c>, built from any <see cref="PagedList{T}"/>.
/// </summary>
public sealed record PaginationModel(int Page, int PageSize, long TotalCount, int TotalPages)
{
    public static PaginationModel From<T>(PagedList<T> list)
    {
        ArgumentNullException.ThrowIfNull(list);

        return new PaginationModel(list.Page, list.PageSize, list.TotalCount, list.TotalPages);
    }

    public long FirstItem => TotalCount == 0 ? 0 : (long)(Page - 1) * PageSize + 1;

    public long LastItem => Math.Min((long)Page * PageSize, TotalCount);
}

/// <summary>
/// One button of <c>_DocumentActions</c>. A <see cref="Post"/> action is submitted as a form (antiforgery
/// included); <see cref="Confirm"/> asks the user first.
/// </summary>
public sealed record DocumentAction(
    string Label,
    string Url,
    string Icon = "ti ti-click",
    string Style = "btn-primary",
    bool Post = true,
    string? Confirm = null);
