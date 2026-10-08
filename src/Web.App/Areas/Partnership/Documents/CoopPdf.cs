using Application.Coops;
using Domain.MasterData.Coops;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Web.App.Areas.MasterData.Models;
using static Web.App.Areas.Partnership.Documents.DataSheetPdf;

namespace Web.App.Areas.Partnership.Documents;

/// <summary>
/// Printed farm data sheet (data kandang peternak): detail, building, equipment, production plan, storage and notes,
/// then the photo gallery of the farm's photo attachments on a new page.
/// </summary>
public static class CoopPdf
{
    public const string DataTitle = "Data Kandang Peternak";

    private static readonly string[] FanHeaders = ["Merk", "Kapasitas", "Jumlah"];
    private static readonly string[] FanTestHeaders = ["Depan (m/s)", "Tengah (m/s)", "Belakang (m/s)", "Rata-rata"];
    private static readonly string[] ThinningHeaders = ["Berat (kg)", "Jumlah (ekor)"];

    public static void ComposeData(IContainer container, CoopResponse coop)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(coop);

        CoopProfile p = coop.Profile;

        container.Column(column =>
        {
            column.Spacing(5);

            Section(column, "Detail Kandang");
            Fields(column,
            [
                ("Peternak", $"{coop.FarmerName} ({coop.FarmerCode})"),
                ("Negara", p.Country),
                ("Kode Kandang", coop.Code),
                ("Provinsi", p.Province),
                ("Nama Kandang", coop.Name),
                ("Kota", p.City),
                ("Jenis Kandang", EnumOptions.Label(coop.HouseType)),
                ("Alamat", coop.Address),
                ("Populasi", WithUnit(Number(coop.Capacity), "ekor")),
                ("Partner Terakhir", p.LastPartner),
                ("Telp", p.Phone),
                ("Cabang", coop.BranchCode),
                ("Lat / Long", coop.Latitude is null && coop.Longitude is null ? null : $"{Coordinate(coop.Latitude)} / {Coordinate(coop.Longitude)}"),
                ("Status", coop.IsActive ? "Aktif" : "Nonaktif")
            ]);

            Section(column, "Bangunan Kandang");
            Fields(column,
            [
                ("Panjang", WithUnit(Number(p.LengthM), "m")),
                ("Luas", WithUnit(Number(p.AreaM2()), "m²")),
                ("Lebar", WithUnit(Number(p.WidthM), "m")),
                ("Volume", WithUnit(Number(p.VolumeM3()), "m³")),
                ("Tinggi", WithUnit(Number(p.HeightM), "m")),
                ("Konstruksi", p.Construction),
                ("Jumlah Lantai", Number(p.Floors))
            ]);

            Section(column, "Peralatan Kandang");

            SubTitle(column, "1. Kipas");
            Table(column, FanHeaders,
                [.. p.Fans.Select(f => new[] { f.Brand, Number(f.Capacity), Number(f.Quantity) })]);

            SubTitle(column, "2. Running Test");
            Table(column, FanTestHeaders,
                [.. p.FanTests.Select(t => new[]
                {
                    Number(t.FrontMs), Number(t.MiddleMs), Number(t.BackMs), WithUnit(Number(t.AverageMs() is { } avg ? Math.Round(avg, 2) : null), "m/s")
                })]);

            SubTitle(column, "3. Tempat Minum");
            Fields(column,
            [
                ("a. Nipple", Units(p.NippleUnits, coop.Capacity)),
                ("b. TMAO", Units(p.TmaoUnits, coop.Capacity))
            ]);

            SubTitle(column, "4. Tempat Pakan");
            Fields(column,
            [
                ("a. Anak Ayam", Units(p.ChickFeederUnits, coop.Capacity)),
                ("c. Super Pakan", Units(p.SuperFeederUnits, coop.Capacity)),
                ("b. TRA", Units(p.TraUnits, coop.Capacity)),
                ("d. Auger", Units(p.AugerUnits, coop.Capacity))
            ]);

            Fields(column, [("5. Tirai", p.Curtain)]);

            SubTitle(column, "6. Genset");
            Fields(column,
            [
                ("Merk", p.GensetBrand),
                ("Kapasitas", WithUnit(Number(p.GensetKva), "kVA")),
                ("Pabrikan", p.GensetFactoryMade switch { true => "Ya", false => "Tidak (rakitan)", null => null })
            ]);

            SubTitle(column, "7. Rencana Produksi");
            Fields(column,
            [
                ("a. Kapasitas Tonase", WithUnit(Number(p.TonnageCapacityKg), "kg")),
                ("b. Kepadatan Awal", Density(p.InitialDensityBirdsPerM2, p.InitialDensityKgPerM2)),
                ("c. Kepadatan Akhir", Density(p.FinalDensityBirdsPerM2, p.FinalDensityKgPerM2))
            ]);
            SubTitle(column, "d. Penjarangan");
            Table(column, ThinningHeaders,
                [.. p.Thinnings.Select(t => new[] { Number(t.WeightKg), Number(t.Birds) })]);

            SubTitle(column, "8. Kapasitas Gudang");
            Fields(column,
            [
                ("a. Pakan", WithUnit(Number(p.FeedStorageKg), "kg")),
                ("b. Sekam", WithUnit(Number(p.HuskStorageSacks), "zak")),
                ("c. Pemanas", WithUnit(Number(p.Heaters), "unit"))
            ]);

            Section(column, "Lain-lain");
            Fields(column, [("Akses Lokasi", p.LocationAccess is { } access ? EnumOptions.Label(access.ToString()) : null)]);
            SubTitle(column, "Informasi Tambahan");
            Fields(column,
            [
                ("a. Pengepul", p.CollectorNotes),
                ("b. Peternak", p.FarmerNotes),
                ("c. Partner Lain", p.OtherPartnerNotes),
                ("d. Lain-lain", p.OtherNotes)
            ]);
            Fields(column, [("Catatan", p.Remarks)]);
        });
    }

    public static void ComposeGallery(IContainer container, CoopResponse coop, IReadOnlyList<Photo> photos)
    {
        ArgumentNullException.ThrowIfNull(coop);
        Gallery(container, $"Foto Kandang — {coop.Name}", photos);
    }

    /// <summary>
    /// "1.840 unit | rasio 8 ekor/unit": the ratio is the population per unit.
    /// </summary>
    private static string? Units(int? units, int population) => units switch
    {
        null => null,
        0 => "0 unit",
        _ => $"{Number(units)} unit  |  rasio {Number(population / units.Value)} ekor/unit"
    };

    private static string Coordinate(decimal? value) =>
        value?.ToString("0.######", System.Globalization.CultureInfo.GetCultureInfo("id-ID")) ?? "—";

    private static string? Density(decimal? birds, decimal? kg) =>
        birds is null && kg is null ? null : $"{Number(birds) ?? "—"} ekor/m²  |  {Number(kg) ?? "—"} kg/m²";
}
