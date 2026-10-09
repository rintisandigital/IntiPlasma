using MobileApp.Core.Api;
using MobileApp.Core.Contracts;

namespace MobileApp.UnitTests.Api;

public sealed class PagedLoaderTests
{
    [Fact]
    public async Task LoadMore_Should_AppendPages_UntilTheEnd()
    {
        // Arrange: 5 items, 2 per page.
        var loader = new PagedLoader<int>((page, _) => Task.FromResult(Page(page, 2, 5)));

        // Act
        await loader.ReloadAsync();
        await loader.LoadMoreAsync();
        await loader.LoadMoreAsync();
        await loader.LoadMoreAsync();

        // Assert
        loader.Items.ShouldBe([1, 2, 3, 4, 5]);
        loader.HasMore.ShouldBeFalse();
        loader.TotalCount.ShouldBe(5);
    }

    [Fact]
    public async Task Reload_Should_ReplaceTheItems_AndReportTheCache()
    {
        // Arrange
        bool offline = false;
        var loader = new PagedLoader<int>((page, _) => Task.FromResult(offline
            ? CachedResult<PagedList<int>>.FromCache(Page(page, 2, 2).Value, new DateTime(2026, 10, 9, 1, 0, 0, DateTimeKind.Utc))
            : Page(page, 2, 2)));
        await loader.ReloadAsync();

        // Act
        offline = true;
        await loader.ReloadAsync();

        // Assert
        loader.Items.ShouldBe([1, 2]);
        loader.CachedAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task Error_Should_BeKept_WithoutItems()
    {
        // Arrange
        var loader = new PagedLoader<int>((_, _) => Task.FromResult(
            CachedResult<PagedList<int>>.Failure(new ApiError { Code = ApiError.NetworkCode, Message = "offline" })));

        // Act
        await loader.ReloadAsync();

        // Assert
        loader.Error!.IsNetwork.ShouldBeTrue();
        loader.IsEmpty.ShouldBeFalse();
        loader.IsLoading.ShouldBeFalse();
    }

    [Fact]
    public async Task OlderReload_Should_NotOverwriteANewerOne()
    {
        // Arrange: the first request answers after the second one.
        var slow = new TaskCompletionSource<CachedResult<PagedList<int>>>();
        int calls = 0;
        var loader = new PagedLoader<int>((page, _) => ++calls == 1 ? slow.Task : Task.FromResult(Page(page, 2, 1)));

        // Act
        Task first = loader.ReloadAsync();
        await loader.ReloadAsync();
        slow.SetResult(CachedResult<PagedList<int>>.Fresh(new PagedList<int> { Items = [99], Page = 1, PageSize = 2, TotalCount = 1 }));
        await first;

        // Assert
        loader.Items.ShouldBe([1]);
    }

    private static CachedResult<PagedList<int>> Page(int page, int pageSize, int total) =>
        CachedResult<PagedList<int>>.Fresh(new PagedList<int>
        {
            Items = [.. Enumerable.Range((page - 1) * pageSize + 1, Math.Max(0, Math.Min(pageSize, total - (page - 1) * pageSize)))],
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
}
