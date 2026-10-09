using MobileApp.Core.Contracts;

namespace MobileApp.Core.Api;

/// <summary>
/// Produksi (PLAN-MOBILE M2): the field context for offline forms and the daily recordings of a cycle, read through
/// the cache. Recordings are created by the sync queue (<see cref="Sync.SyncEngine"/>), not here.
/// </summary>
public sealed class ProductionApi(ApiCache cache)
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

    internal static string RecordingsPath(Guid cycleId) => PartnershipApi.Path("production/daily-recordings", ("cycleId", cycleId.ToString()));
}
