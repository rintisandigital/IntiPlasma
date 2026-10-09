using MobileApp.Core.Contracts;
using MobileApp.Core.Session;

namespace MobileApp.Core.Navigation;

/// <summary>
/// A menu entry of the app. <see cref="Phase"/> names the PLAN-MOBILE phase that builds the screen; until then the
/// route is <c>/fitur/{key}</c>, a "coming soon" page.
/// </summary>
public sealed record AppFeature(
    string Key,
    string Title,
    string Icon,
    string Route,
    string? Phase,
    Func<CurrentUser, bool> IsAvailable);

/// <summary>
/// Menus per user, decided from permissions (PLAN-MOBILE M-10, §6.1). The bottom bar takes the first four
/// available entries of <see cref="BottomCandidates"/> plus "Lainnya", which lists everything else. With the
/// default roles this gives PPL: Beranda · Kandang · Input · Antrean · Lainnya and Manager: Beranda · Kandang ·
/// Approval · Stok Ayam · Lainnya.
/// </summary>
public static class AppFeatures
{
    public const int BottomSlots = 4;

    public static readonly AppFeature Home = new("beranda", "Beranda", "icon-house-fill", "/", null, _ => true);

    public static readonly AppFeature Coops = new(
        "kandang", "Kandang", "icon-buildings", "/kandang", "M1", u => u.Has(AppPermissions.FarmersRead));

    public static readonly AppFeature DailyInput = new(
        "input", "Input", "icon-edit", "/input", "M2",
        u => u.Has(AppPermissions.ProductionRecord)
            || u.Has(AppPermissions.ProductionStockReport)
            || u.Has(AppPermissions.InventoryRequestFeed));

    public static readonly AppFeature Approvals = new(
        "approval", "Approval", "icon-CheckCircle", "/fitur/approval", "M6", u => u.Has(AppPermissions.ApprovalsDecide));

    public static readonly AppFeature SyncQueue = new(
        "antrean", "Antrean", "icon-upload", "/antrean", "M2",
        u => u.Has(AppPermissions.ProductionRecord) || u.Has(AppPermissions.ProductionStockReport));

    public static readonly AppFeature LiveBirdStock = new(
        "stok-ayam", "Stok Ayam", "icon-chart", "/stok-ayam", "M3", u => u.Has(AppPermissions.ProductionRead));

    public static readonly AppFeature More = new("lainnya", "Lainnya", "icon-listBullets", "/lainnya", null, _ => true);

    public static readonly AppFeature Farmers = new(
        "peternak", "Peternak", "icon-user-line", "/peternak", "M1", u => u.Has(AppPermissions.FarmersRead));

    public static readonly AppFeature Contracts = new(
        "kontrak", "Kontrak", "icon-bookTxt", "/kontrak", "M1", u => u.Has(AppPermissions.ContractsRead));

    public static readonly AppFeature Performance = new(
        "grafik", "Grafik Produksi", "icon-chart", "/fitur/grafik", "M4", u => u.Has(AppPermissions.ProductionRead));

    public static readonly AppFeature FeedRequests = new(
        "request-pakan", "Request Pakan", "icon-download", "/fitur/request-pakan", "M9",
        u => u.Has(AppPermissions.InventoryRead) || u.Has(AppPermissions.InventoryRequestFeed));

    public static readonly AppFeature FeedMutations = new(
        "mutasi-pakan", "Mutasi Pakan", "icon-shareNetwork", "/fitur/mutasi-pakan", "M8",
        u => u.Has(AppPermissions.InventoryRequestFeedMutation));

    public static readonly AppFeature MySubmissions = new(
        "pengajuan", "Pengajuan Saya", "icon-timeline", "/fitur/pengajuan", "M6",
        u => u.Has(AppPermissions.FarmersManage)
            || u.Has(AppPermissions.ContractsManage)
            || u.Has(AppPermissions.ProductionRevise)
            || u.Has(AppPermissions.InventoryRequestFeedMutation));

    public static readonly AppFeature Profile = new("profil", "Profil", "icon-profile", "/profil", null, _ => true);

    public static readonly AppFeature Settings = new("pengaturan", "Pengaturan", "icon-setting", "/pengaturan", null, _ => true);

    /// <summary>
    /// Bottom bar candidates in priority order.
    /// </summary>
    public static readonly IReadOnlyList<AppFeature> BottomCandidates =
        [Home, Coops, DailyInput, Approvals, SyncQueue, LiveBirdStock];

    /// <summary>
    /// Entries that only appear in "Lainnya".
    /// </summary>
    public static readonly IReadOnlyList<AppFeature> MoreEntries =
        [Farmers, Contracts, Performance, FeedRequests, FeedMutations, MySubmissions, Profile, Settings];

    public static readonly IReadOnlyList<AppFeature> All = [.. BottomCandidates, More, .. MoreEntries];

    public static IReadOnlyList<AppFeature> BottomBar(CurrentUser user) =>
        [.. BottomCandidates.Where(f => f.IsAvailable(user)).Take(BottomSlots), More];

    public static IReadOnlyList<AppFeature> MoreMenu(CurrentUser user) =>
    [
        .. BottomCandidates.Where(f => f.IsAvailable(user)).Skip(BottomSlots),
        .. MoreEntries.Where(f => f.IsAvailable(user))
    ];

    public static AppFeature? Find(string key) => All.FirstOrDefault(f => f.Key == key);
}
