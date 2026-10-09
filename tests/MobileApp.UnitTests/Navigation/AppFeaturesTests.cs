using MobileApp.Core.Contracts;
using MobileApp.Core.Navigation;
using MobileApp.Core.Session;

namespace MobileApp.UnitTests.Navigation;

public sealed class AppFeaturesTests
{
    /// <summary>
    /// Default PPL role (PLAN-MOBILE §4.2).
    /// </summary>
    private static readonly string[] FieldOfficer =
    [
        AppPermissions.PartnershipAssignedOnly, AppPermissions.FarmersRead, AppPermissions.FarmersManage,
        AppPermissions.ContractsRead, AppPermissions.ContractsManage, AppPermissions.CyclesRead,
        AppPermissions.ProductionRead, AppPermissions.ProductionRecord, AppPermissions.ProductionRevise,
        AppPermissions.ProductionStockReport, AppPermissions.InventoryRead, AppPermissions.InventoryRequestFeed,
        AppPermissions.InventoryRequestFeedMutation, AppPermissions.AttachmentsUpload, AppPermissions.AttachmentsRead
    ];

    /// <summary>
    /// Default Manager role (PLAN-MOBILE §4.2).
    /// </summary>
    private static readonly string[] Manager =
    [
        AppPermissions.FarmersRead, AppPermissions.ContractsRead, AppPermissions.CyclesRead,
        AppPermissions.ProductionRead, AppPermissions.InventoryRead, AppPermissions.AttachmentsRead,
        AppPermissions.ApprovalsDecide
    ];

    [Fact]
    public void BottomBar_Should_MatchThePlan_ForTheFieldOfficer() =>
        Keys(AppFeatures.BottomBar(User(FieldOfficer))).ShouldBe(["beranda", "kandang", "input", "antrean", "lainnya"]);

    [Fact]
    public void BottomBar_Should_MatchThePlan_ForTheManager() =>
        Keys(AppFeatures.BottomBar(User(Manager))).ShouldBe(["beranda", "kandang", "approval", "stok-ayam", "lainnya"]);

    [Fact]
    public void MoreMenu_Should_ListWhatTheFieldOfficerMayUse()
    {
        string[] keys = Keys(AppFeatures.MoreMenu(User(FieldOfficer)));

        keys.ShouldBe(
        [
            "stok-ayam", "peternak", "kontrak", "grafik", "request-pakan", "mutasi-pakan", "pengajuan", "profil", "pengaturan"
        ]);
    }

    [Fact]
    public void MoreMenu_Should_HideInputAndSubmissions_ForTheManager()
    {
        string[] keys = Keys(AppFeatures.MoreMenu(User(Manager)));

        keys.ShouldBe(["peternak", "kontrak", "grafik", "request-pakan", "profil", "pengaturan"]);
    }

    [Fact]
    public void A_UserWithoutPermissions_Should_OnlySeeHomeAndMore()
    {
        Keys(AppFeatures.BottomBar(User([]))).ShouldBe(["beranda", "lainnya"]);
        Keys(AppFeatures.MoreMenu(User([]))).ShouldBe(["profil", "pengaturan"]);
    }

    [Fact]
    public void Keys_Should_BeUnique() =>
        AppFeatures.All.Select(f => f.Key).ShouldBeUnique();

    private static CurrentUser User(string[] permissions) => new() { Permissions = permissions };

    private static string[] Keys(IEnumerable<AppFeature> features) => [.. features.Select(f => f.Key)];
}
