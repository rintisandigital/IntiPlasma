using Application.Farmers;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Areas.Partnership.Documents;

/// <summary>
/// Printed farmer data sheet: page 1 holds the farmer, tax and bank data; page 2 the photo gallery of the farmer's
/// photo attachments (KTP, KK, NPWP, house, …).
/// </summary>
public static class FarmerPdf
{
    public const string DataTitle = "Data Peternak";

    public static void ComposeData(IContainer container, FarmerResponse farmer, DisplayFormatter fmt)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(farmer);
        ArgumentNullException.ThrowIfNull(fmt);

        container.Column(column =>
        {
            column.Spacing(6);

            DataSheetPdf.Section(column, "Detail Peternak");
            DataSheetPdf.Fields(column,
            [
                ("Kode", farmer.Code),
                ("Tipe", farmer.Type),
                ("Nama", farmer.Name),
                ("Cabang", farmer.BranchCode),
                ("No. KTP (NIK)", farmer.Nik),
                ("Jumlah Kandang", fmt.Number(farmer.CoopCount)),
                ("Telp", farmer.Phone),
                ("Status", farmer.IsActive ? "Aktif" : "Nonaktif"),
                ("Alamat", farmer.Address)
            ]);

            DataSheetPdf.Section(column, "Data Pajak");
            DataSheetPdf.Fields(column,
            [
                ("NPWP", farmer.TaxIdentity.Npwp),
                ("NITKU", farmer.TaxIdentity.Nitku),
                ("PKP", farmer.TaxIdentity.IsPkp ? "Ya" : "Tidak")
            ]);

            DataSheetPdf.Section(column, "Data Bank");
            DataSheetPdf.Fields(column,
            [
                ("Bank", farmer.BankAccount.BankName),
                ("Atas Nama", farmer.BankAccount.AccountHolderName),
                ("No. Rekening", farmer.BankAccount.AccountNumber)
            ]);
        });
    }

    public static void ComposeGallery(IContainer container, FarmerResponse farmer, IReadOnlyList<DataSheetPdf.Photo> photos)
    {
        ArgumentNullException.ThrowIfNull(farmer);
        DataSheetPdf.Gallery(container, $"Foto Peternak — {farmer.Name}", photos);
    }
}
