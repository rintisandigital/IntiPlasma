using System.Globalization;
using MobileApp.Core.Contracts;

namespace MobileApp.Core.Api;

/// <summary>
/// Produksi (PLAN-MOBILE M2): the field context for offline forms and the daily recordings of a cycle, read through
/// the cache. Recordings are created by the sync queue (<see cref="Sync.SyncEngine"/>), not here.
/// </summary>
public sealed class ProductionApi(ApiCache cache, ApiClient api)
{
    public const string FieldContextPath = "mobile/field-context";

    public Task<CachedResult<FieldContext>> GetFieldContextAsync(CancellationToken cancellationToken = default) =>
        cache.GetAsync<FieldContext>(FieldContextPath, cancellationToken);

    /// <summary>
    /// The field context stored on the device, without asking the server.
    /// </summary>
    public Task<CachedResult<FieldContext>?> ReadFieldContextAsync(CancellationToken cancellationToken = default) =>
        cache.ReadAsync<FieldContext>(FieldContextPath, cancellationToken);

    public Task<CachedResult<List<DailyRecording>>> GetRecordingsAsync(Guid cycleId, CancellationToken cancellationToken = default) =>
        cache.GetAsync<List<DailyRecording>>(RecordingsPath(cycleId), cancellationToken);

    /// <summary>
    /// The stored list of a cycle, without asking the server.
    /// </summary>
    public Task<CachedResult<List<DailyRecording>>?> ReadRecordingsAsync(Guid cycleId, CancellationToken cancellationToken = default) =>
        cache.ReadAsync<List<DailyRecording>>(RecordingsPath(cycleId), cancellationToken);

    public Task<CachedResult<DailyRecording>> GetRecordingAsync(Guid id, CancellationToken cancellationToken = default) =>
        cache.GetAsync<DailyRecording>($"production/daily-recordings/{id}", cancellationToken);

    /// <summary>
    /// Stok ayam harian of a cycle, every date (M3).
    /// </summary>
    public Task<CachedResult<List<LiveBirdStockEntry>>> GetCycleStockAsync(Guid cycleId, CancellationToken cancellationToken = default) =>
        cache.GetAsync<List<LiveBirdStockEntry>>(StockPath(cycleId), cancellationToken);

    public Task<CachedResult<List<LiveBirdStockEntry>>?> ReadCycleStockAsync(Guid cycleId, CancellationToken cancellationToken = default) =>
        cache.ReadAsync<List<LiveBirdStockEntry>>(StockPath(cycleId), cancellationToken);

    /// <summary>
    /// Rekap cabang per weight range on a date (Manager, M-51).
    /// </summary>
    public Task<CachedResult<LiveBirdStockSummary>> GetStockSummaryAsync(DateOnly date, Guid? branchId, CancellationToken cancellationToken = default) =>
        cache.GetAsync<LiveBirdStockSummary>(
            PartnershipApi.Path(
                "production/live-bird-stocks/summary",
                ("date", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                ("branchId", branchId?.ToString())),
            cancellationToken);

    /// <summary>
    /// Deletes an entry on the server (today or yesterday only, online, M-49).
    /// </summary>
    public Task<ApiResult> DeleteStockAsync(Guid entryId, CancellationToken cancellationToken = default) =>
        api.DeleteAsync($"production/live-bird-stocks/{entryId}", cancellationToken);

    internal static string StockPath(Guid cycleId) => PartnershipApi.Path("production/live-bird-stocks", ("cycleId", cycleId.ToString()));

    internal static string RecordingsPath(Guid cycleId) => PartnershipApi.Path("production/daily-recordings", ("cycleId", cycleId.ToString()));
}
