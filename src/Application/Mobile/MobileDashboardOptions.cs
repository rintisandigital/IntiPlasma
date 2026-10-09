namespace Application.Mobile;

/// <summary>
/// Thresholds of the mobile dashboard (PLAN-MOBILE M-58, M-59), bound from <c>Mobile:Dashboard</c>.
/// </summary>
public sealed class MobileDashboardOptions
{
    public const string SectionName = "Mobile:Dashboard";

    /// <summary>
    /// Number of latest recordings with feed usage averaged for the daily feed need.
    /// </summary>
    public int FeedAverageDays { get; set; } = 3;

    /// <summary>
    /// Feed stock lasting fewer days than this is flagged <c>FeedLow</c>.
    /// </summary>
    public int FeedWarningDays { get; set; } = 3;

    /// <summary>
    /// A cycle whose last recording is more than this many days before the dashboard date is flagged <c>RecordingLate</c>.
    /// </summary>
    public int RecordingLateDays { get; set; } = 1;

    /// <summary>
    /// Cumulative depletion at or above this percentage is flagged <c>HighDepletion</c> (until breed standards, M5).
    /// </summary>
    public decimal DepletionWarningPercent { get; set; } = 5;

    /// <summary>
    /// From this age a cycle is expected to report stok ayam daily; without today's report it is flagged <c>NoStockReport</c>.
    /// </summary>
    public int StockReportFromAgeDays { get; set; } = 21;
}
