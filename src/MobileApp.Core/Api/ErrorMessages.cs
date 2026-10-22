using System.Globalization;
using System.Text.RegularExpressions;

namespace MobileApp.Core.Api;

/// <summary>
/// Turns Web.Api error codes (English descriptions) into messages in Bahasa Indonesia (PLAN-MOBILE §3.8). Codes that
/// are not mapped fall back to a message per HTTP status followed by the server description.
/// </summary>
public static partial class ErrorMessages
{
    /// <summary>
    /// Error codes with a fixed Indonesian message. Kept in sync with the Domain error catalog by a unit test.
    /// </summary>
    public const string FlowNotConfiguredCode = "Approvals.FlowNotConfigured";

    internal static readonly IReadOnlyDictionary<string, string> Known = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Users.NotFoundByEmail"] = "Email atau password salah.",
        ["Users.InvalidCredentials"] = "Email atau password salah.",
        ["Users.Inactive"] = "Akun Anda tidak aktif. Hubungi administrator.",
        ["Users.InvalidCurrentPassword"] = "Password saat ini salah.",
        ["Users.InvalidRefreshToken"] = "Sesi Anda sudah berakhir. Silakan masuk kembali.",
        ["Users.PhotoNotImage"] = "Foto profil harus berupa gambar JPEG, PNG atau WEBP.",
        ["Users.PhotoNotFound"] = "Belum ada foto profil.",
        // Also the answer for data outside the PPL scope (M-36), so it does not reveal that the data exists.
        ["Farmers.NotFound"] = "Peternak tidak ditemukan atau bukan tanggung jawab Anda.",
        ["Coops.NotFound"] = "Kandang tidak ditemukan atau bukan tanggung jawab Anda.",
        ["Cycles.NotFound"] = "Siklus tidak ditemukan atau bukan tanggung jawab Anda.",
        ["Contracts.NotFound"] = "Kontrak tidak ditemukan.",
        ["Users.NotFieldOfficer"] = "PPL yang dipilih tidak aktif atau tidak memiliki akses ke cabang ini.",
        ["Concurrency.Conflict"] = "Data sudah diubah pengguna lain. Muat ulang lalu coba lagi.",
        ["Database.UniqueViolation"] = "Data yang sama sudah ada.",
        ["Validation.General"] = "Periksa kembali isian Anda.",
        // Daily recording (M2), also shown on failed entries of the sync queue.
        ["DailyRecordings.AlreadyRecorded"] = "Recording untuk tanggal ini sudah ada. Ubah tanggalnya atau hapus entri ini.",
        ["DailyRecordings.FutureDate"] = "Tanggal recording melewati tanggal server. Periksa tanggal & jam perangkat.",
        ["DailyRecordings.IdBelongsToOtherRecording"] = "Entri ini bentrok dengan recording lain di server. Hapus lalu input ulang.",
        ["DailyRecordings.UsageItemNotAllowed"] = "Hanya pakan dan OVK yang bisa dicatat sebagai pemakaian.",
        ["DailyRecordings.InvalidUsage"] = "Setiap item pemakaian hanya boleh sekali dengan jumlah lebih dari 0.",
        ["Cycles.NotRecordable"] = "Siklus sudah tidak berjalan (selesai atau dibatalkan); recording tidak bisa ditambahkan.",
        ["Cycles.BeforeChickIn"] = "Tanggal recording sebelum tanggal chick-in.",
        ["Cycles.PopulationExceeded"] = "Mati + culling melebihi populasi berjalan.",
        ["Stock.Insufficient"] = "Stok di gudang kandang tidak cukup untuk pemakaian ini.",
        ["Items.NotFound"] = "Item pakan/OVK tidak ditemukan. Perbarui data lalu coba lagi.",
        ["Items.UnknownUom"] = "Satuan tidak berlaku untuk item ini. Perbarui data lalu coba lagi.",
        // Stok ayam harian (M3).
        ["LiveBirdStock.AverageOutsideRange"] = "Rata-rata bobot (tonase ÷ ekoran) di luar rentang yang dipilih.",
        ["LiveBirdStock.PopulationExceeded"] = "Total ekor semua rentang melebihi populasi berjalan.",
        ["LiveBirdStock.InvalidQuantity"] = "Ekoran dan tonase harus lebih dari 0.",
        ["LiveBirdStock.FutureDate"] = "Tanggal stok ayam melewati tanggal server. Periksa tanggal & jam perangkat.",
        ["LiveBirdStock.IdBelongsToOtherEntry"] = "Entri ini bentrok dengan entri stok lain di server. Hapus lalu input ulang.",
        ["LiveBirdStock.TooOldToDelete"] = "Hanya stok ayam hari ini atau kemarin yang bisa dihapus.",
        ["LiveBirdStock.NotFound"] = "Entri stok ayam tidak ditemukan atau bukan tanggung jawab Anda.",
        ["WeightRanges.Inactive"] = "Rentang bobot sudah tidak aktif. Perbarui data lalu pilih rentang lain.",
        ["WeightRanges.NotFound"] = "Rentang bobot tidak ditemukan. Perbarui data lalu coba lagi.",
        ["Attachments.TooLarge"] = "Foto terlalu besar (maksimal 10 MB).",
        ["Attachments.UnsupportedType"] = "Jenis file tidak didukung (hanya JPEG, PNG, WEBP, PDF).",
        ["Attachments.IdConflict"] = "Foto bentrok dengan lampiran lain di server. Hapus foto lalu tambahkan lagi.",
        // Approval (M6).
        ["Approvals.NotApprover"] = "Anda bukan approver pada level yang sedang berjalan.",
        ["Approvals.RequesterCannotDecide"] = "Pengaju tidak dapat menyetujui atau menolak pengajuannya sendiri.",
        ["Approvals.AlreadyDecidedOtherLevel"] = "Anda sudah memutuskan level lain pada pengajuan ini; satu orang hanya boleh memutuskan satu level.",
        ["Approvals.NotPending"] = "Pengajuan sudah diputuskan atau dibatalkan. Muat ulang untuk melihat statusnya.",
        ["Approvals.ReasonRequired"] = "Alasan penolakan wajib diisi.",
        ["Approvals.NotFound"] = "Pengajuan tidak ditemukan.",
        ["Approvals.DocumentPending"] = "Dokumen ini masih punya pengajuan yang menunggu keputusan.",
        ["Approvals.OnlyRequesterCanCancel"] = "Hanya pengaju yang dapat membatalkan pengajuan.",
        ["Farmer.PendingApproval"] = "Peternak sedang menunggu approval; batalkan pengajuan dulu untuk mengubah data.",
        ["Coop.PendingApproval"] = "Kandang sedang menunggu approval; batalkan pengajuan dulu untuk mengubah data.",
        ["Farmers.ChangePending"] = "Masih ada perubahan NIK/rekening yang menunggu approval.",
        ["Coops.FarmerNotSubmitted"] = "Peternak kandang ini belum diajukan atau ditolak; ajukan peternaknya dulu.",
        ["Coops.FarmerNotApproved"] = "Kandang baru bisa disetujui setelah peternaknya disetujui.",
        ["ApprovalDelegations.DelegateToSelf"] = "Tidak bisa mendelegasikan ke diri sendiri.",
        ["ApprovalDelegations.InvalidPeriod"] = "Periode delegasi tidak valid (maksimal 90 hari dan belum berakhir).",
        ["ApprovalDelegations.Overlaps"] = "Periode ini bertumpuk dengan delegasi Anda yang lain.",
        ["ApprovalDelegations.Ended"] = "Delegasi sudah berakhir atau dicabut.",
        ["ApprovalDelegations.InvalidDelegate"] = "Penerima delegasi harus approver yang aktif.",
        [ApiError.NetworkCode] = "Tidak dapat terhubung ke server. Periksa koneksi internet Anda.",
        [ApiError.SessionExpiredCode] = "Sesi Anda sudah berakhir. Silakan masuk kembali.",
        [ApiError.UnexpectedCode] = "Terjadi kesalahan yang tidak terduga."
    };

    /// <summary>
    /// The Indonesian message for an error answered by the server.
    /// </summary>
    public static string For(string code, int status, string? detail)
    {
        if (code == "Users.LockedOut")
        {
            return LockedOut(detail);
        }

        // Written in Indonesian by the server, with the document type and branch (M-82).
        if (code == FlowNotConfiguredCode && !string.IsNullOrWhiteSpace(detail))
        {
            return detail;
        }

        if (Known.TryGetValue(code, out string? message))
        {
            return message;
        }

        string general = status switch
        {
            400 => "Permintaan tidak dapat diproses.",
            401 => "Sesi Anda sudah berakhir. Silakan masuk kembali.",
            403 => "Anda tidak memiliki akses untuk tindakan ini.",
            404 => "Data tidak ditemukan.",
            409 => "Data bentrok dengan data lain.",
            429 => "Terlalu banyak permintaan. Tunggu sebentar lalu coba lagi.",
            >= 500 => "Server sedang bermasalah. Coba lagi nanti.",
            _ => Known[ApiError.UnexpectedCode]
        };

        return string.IsNullOrWhiteSpace(detail) || status >= 500 ? general : $"{general} Server: {detail}";
    }

    private static string LockedOut(string? detail)
    {
        Match minutes = MinutesPattern().Match(detail ?? string.Empty);

        return minutes.Success
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"Akun terkunci karena terlalu banyak percobaan masuk yang gagal. Coba lagi dalam {minutes.Groups[1].Value} menit.")
            : "Akun terkunci karena terlalu banyak percobaan masuk yang gagal. Coba lagi nanti.";
    }

    [GeneratedRegex(@"in (\d+) minute", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MinutesPattern();
}
