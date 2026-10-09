using System.Globalization;
using System.Text;
using MobileApp.Core.Contracts;

namespace MobileApp.Core.Api;

/// <summary>
/// Data kemitraan (PLAN-MOBILE M1): farmers, coops, contracts, their cycles, farm stock and attachments. Read-only
/// and cached (<see cref="ApiCache"/>). The server limits a PPL to their assigned farmers and coops; a Manager sees
/// the branch and may filter by PPL.
/// </summary>
public sealed class PartnershipApi(ApiCache cache, ApiClient api)
{
    public const int PageSize = 20;

    public Task<CachedResult<PagedList<Farmer>>> GetFarmersAsync(
        string? search,
        int page,
        Guid? fieldOfficerId,
        CancellationToken cancellationToken = default) =>
        cache.GetAsync<PagedList<Farmer>>(
            Path("farmers", ("search", search), ("page", Text(page)), ("pageSize", Text(PageSize)), ("fieldOfficerId", Text(fieldOfficerId))),
            cancellationToken);

    public Task<CachedResult<Farmer>> GetFarmerAsync(Guid id, CancellationToken cancellationToken = default) =>
        cache.GetAsync<Farmer>($"farmers/{id}", cancellationToken);

    public Task<CachedResult<PagedList<Coop>>> GetCoopsAsync(
        string? search,
        int page,
        Guid? farmerId,
        Guid? fieldOfficerId,
        CancellationToken cancellationToken = default) =>
        cache.GetAsync<PagedList<Coop>>(
            Path(
                "coops",
                ("search", search),
                ("page", Text(page)),
                ("pageSize", Text(PageSize)),
                ("farmerId", Text(farmerId)),
                ("fieldOfficerId", Text(fieldOfficerId))),
            cancellationToken);

    public Task<CachedResult<Coop>> GetCoopAsync(Guid id, CancellationToken cancellationToken = default) =>
        cache.GetAsync<Coop>($"coops/{id}", cancellationToken);

    public Task<CachedResult<PagedList<Contract>>> GetContractsAsync(
        string? search,
        int page,
        string? status,
        CancellationToken cancellationToken = default) =>
        cache.GetAsync<PagedList<Contract>>(
            Path("contracts", ("search", search), ("page", Text(page)), ("pageSize", Text(PageSize)), ("status", status)),
            cancellationToken);

    public Task<CachedResult<Contract>> GetContractAsync(Guid id, CancellationToken cancellationToken = default) =>
        cache.GetAsync<Contract>($"contracts/{id}", cancellationToken);

    /// <summary>
    /// The cycles of a coop, newest first (first page only: the running cycle and recent history).
    /// </summary>
    public Task<CachedResult<PagedList<Cycle>>> GetCoopCyclesAsync(Guid coopId, CancellationToken cancellationToken = default) =>
        cache.GetAsync<PagedList<Cycle>>(
            Path("cycles", ("coopId", Text(coopId)), ("page", "1"), ("pageSize", Text(PageSize))),
            cancellationToken);

    /// <summary>
    /// Items in stock in a warehouse (the farm warehouse of a coop).
    /// </summary>
    public Task<CachedResult<PagedList<StockBalance>>> GetStockAsync(Guid warehouseId, CancellationToken cancellationToken = default) =>
        cache.GetAsync<PagedList<StockBalance>>(
            Path("inventory/stock-balances", ("warehouseId", Text(warehouseId)), ("page", "1"), ("pageSize", "100")),
            cancellationToken);

    public Task<CachedResult<List<Attachment>>> GetAttachmentsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default) =>
        cache.GetAsync<List<Attachment>>(Path("attachments", ("ids", string.Join(',', ids))), cancellationToken);

    /// <summary>
    /// The file of an attachment (photo preview); not cached.
    /// </summary>
    public Task<ApiResult<AttachmentContent>> GetAttachmentContentAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.GetBytesAsync($"attachments/{id}/content", cancellationToken);

    /// <summary>
    /// PPL filter of the Manager: the field officers of a branch.
    /// </summary>
    public Task<CachedResult<List<FieldOfficer>>> GetFieldOfficersAsync(Guid? branchId, CancellationToken cancellationToken = default) =>
        cache.GetAsync<List<FieldOfficer>>(Path("users/field-officers", ("branchId", Text(branchId))), cancellationToken);

    /// <summary>
    /// A relative API path with the non-empty query parameters, escaped.
    /// </summary>
    internal static string Path(string resource, params (string Name, string? Value)[] parameters)
    {
        var builder = new StringBuilder(resource);
        char separator = '?';

        foreach ((string name, string? value) in parameters)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            builder.Append(separator).Append(name).Append('=').Append(Uri.EscapeDataString(value.Trim()));
            separator = '&';
        }

        return builder.ToString();
    }

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string? Text(Guid? value) => value?.ToString();
}
