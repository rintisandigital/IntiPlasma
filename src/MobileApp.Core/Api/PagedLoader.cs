using MobileApp.Core.Contracts;

namespace MobileApp.Core.Api;

/// <summary>
/// State of a list page (PLAN-MOBILE §3.7): first page, "load more", and whether the data came from the cache.
/// A reload replaces the items; a newer reload wins over an older one that answers late.
/// </summary>
public sealed class PagedLoader<T>(Func<int, CancellationToken, Task<CachedResult<PagedList<T>>>> fetch)
{
    private readonly List<T> _items = [];
    private int _page;
    private int _generation;

    public IReadOnlyList<T> Items => _items;

    public bool IsLoading { get; private set; }

    public bool HasMore { get; private set; }

    public long TotalCount { get; private set; }

    public ApiError? Error { get; private set; }

    /// <summary>
    /// Set when the shown page comes from the cache (offline).
    /// </summary>
    public DateTime? CachedAtUtc { get; private set; }

    public bool IsEmpty => !IsLoading && Error is null && _items.Count == 0;

    public Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        _generation++;
        _page = 0;
        _items.Clear();
        HasMore = false;
        Error = null;
        CachedAtUtc = null;

        return LoadPageAsync(1, _generation, cancellationToken);
    }

    public Task LoadMoreAsync(CancellationToken cancellationToken = default) =>
        IsLoading || !HasMore ? Task.CompletedTask : LoadPageAsync(_page + 1, _generation, cancellationToken);

    private async Task LoadPageAsync(int page, int generation, CancellationToken cancellationToken)
    {
        IsLoading = true;
        try
        {
            CachedResult<PagedList<T>> result = await fetch(page, cancellationToken);

            if (generation != _generation)
            {
                return;
            }

            if (!result.IsSuccess)
            {
                Error = result.Error;
                return;
            }

            _items.AddRange(result.Value.Items);
            _page = page;
            HasMore = result.Value.HasMore;
            TotalCount = result.Value.TotalCount;
            CachedAtUtc = result.CachedAtUtc ?? (page == 1 ? null : CachedAtUtc);
            Error = null;
        }
        finally
        {
            if (generation == _generation)
            {
                IsLoading = false;
            }
        }
    }
}
