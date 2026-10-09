using MobileApp.Core.Abstractions;
using MobileApp.Core.Api;
using MobileApp.Core.Contracts;

namespace MobileApp.Core.Production;

/// <summary>
/// State of a daily task on the dashboard (M-61).
/// </summary>
public enum TaskState
{
    /// <summary>
    /// On the server for today.
    /// </summary>
    Done,

    /// <summary>
    /// Saved on the device, not sent yet.
    /// </summary>
    Queued,

    /// <summary>
    /// Rejected by the server, waiting in the queue.
    /// </summary>
    Failed,

    /// <summary>
    /// Not filled in today.
    /// </summary>
    Missing,

    /// <summary>
    /// Not expected today (stok ayam before the reporting age).
    /// </summary>
    NotExpected
}

/// <summary>
/// A running cycle on the dashboard with today's recording and stok ayam state, including the queue on the device.
/// </summary>
/// <param name="AgeDays">Age today (the cached dashboard can be from an earlier day).</param>
/// <param name="Flags">The server flags without the ones the queue already answers.</param>
public sealed record DashboardTask(
    DashboardCycle Cycle,
    int AgeDays,
    TaskState Recording,
    TaskState Stock,
    IReadOnlyList<string> Flags);

/// <param name="CachedAtUtc">Set when the dashboard came from the stored copy (offline).</param>
public sealed record DashboardView(
    MobileDashboard Dashboard,
    DateTime? CachedAtUtc,
    DateOnly Today,
    IReadOnlyList<DashboardTask> Tasks)
{
    /// <summary>
    /// Cycles with at least one flag, most flags first (Manager, M-62).
    /// </summary>
    public IReadOnlyList<DashboardTask> AtRisk =>
        [.. Tasks.Where(t => t.Flags.Count > 0).OrderByDescending(t => t.Flags.Count).ThenBy(t => t.Cycle.CoopCode, StringComparer.Ordinal)];
}

/// <summary>
/// The dashboard (PLAN-MOBILE M-61 s.d. M-63): the server summary (stored copy when offline) merged with what is
/// still in the queue on the device.
/// </summary>
public sealed class DashboardService(ProductionApi production, RecordingService recordings, StockService stock, IClock clock)
{
    /// <param name="refresh">
    /// False: the stored dashboard, treated as current (after a queue change; the page keeps the age of its last load);
    /// the server only when nothing is stored.
    /// </param>
    public async Task<CachedResult<DashboardView>> LoadAsync(bool refresh = true, CancellationToken cancellationToken = default)
    {
        CachedResult<MobileDashboard>? stored = refresh ? null : await production.ReadDashboardAsync(cancellationToken);
        CachedResult<MobileDashboard> server = stored is null
            ? await production.GetDashboardAsync(cancellationToken)
            : CachedResult<MobileDashboard>.Fresh(stored.Value);
        if (!server.IsSuccess)
        {
            return CachedResult<DashboardView>.Failure(server.Error!);
        }

        IReadOnlyList<LocalRecording> localRecordings = await recordings.GetLocalListAsync(cycleId: null, cancellationToken);
        IReadOnlyList<LocalStock> localStock = await stock.GetLocalListAsync(cycleId: null, cancellationToken);

        DashboardView view = Merge(server.Value, server.CachedAtUtc, localRecordings, localStock, clock.Today);

        return server.CachedAtUtc is { } cachedAt ? CachedResult<DashboardView>.FromCache(view, cachedAt) : CachedResult<DashboardView>.Fresh(view);
    }

    internal static DashboardView Merge(
        MobileDashboard dashboard,
        DateTime? cachedAtUtc,
        IReadOnlyList<LocalRecording> localRecordings,
        IReadOnlyList<LocalStock> localStock,
        DateOnly today)
    {
        List<DashboardTask> tasks =
        [
            .. dashboard.Cycles.Select(cycle =>
            {
                int age = cycle.AgeOn(today);

                TaskState recording = cycle.LastRecordingDate == today
                    ? TaskState.Done
                    : Queued(localRecordings.Where(l => l.Draft.CycleId == cycle.CycleId && l.Draft.Date == today).Select(l => l.State))
                        ?? TaskState.Missing;

                TaskState notReported = age >= dashboard.Thresholds.StockReportFromAgeDays ? TaskState.Missing : TaskState.NotExpected;
                TaskState stockState = cycle.LatestStockDate == today
                    ? TaskState.Done
                    : Queued(localStock.Where(l => l.Draft.CycleId == cycle.CycleId && l.Draft.Date == today).Select(l => l.State))
                        ?? notReported;

                List<string> flags = [.. cycle.Flags];
                if (recording is TaskState.Done or TaskState.Queued)
                {
                    flags.Remove(DashboardFlags.RecordingLate);
                }

                if (stockState is TaskState.Done or TaskState.Queued)
                {
                    flags.Remove(DashboardFlags.NoStockReport);
                }

                return new DashboardTask(cycle, age, recording, stockState, flags);
            })
        ];

        return new DashboardView(dashboard, cachedAtUtc, today, tasks);
    }

    private static TaskState? Queued(IEnumerable<RecordingState> states)
    {
        List<RecordingState> list = [.. states];

        if (list.Count == 0)
        {
            return null;
        }

        return list.Contains(RecordingState.Failed) ? TaskState.Failed : TaskState.Queued;
    }
}
