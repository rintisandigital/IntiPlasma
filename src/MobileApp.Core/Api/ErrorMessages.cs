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
    internal static readonly IReadOnlyDictionary<string, string> Known = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Users.NotFoundByEmail"] = "Email atau password salah.",
        ["Users.InvalidCredentials"] = "Email atau password salah.",
        ["Users.Inactive"] = "Akun Anda tidak aktif. Hubungi administrator.",
        ["Users.InvalidCurrentPassword"] = "Password saat ini salah.",
        ["Users.InvalidRefreshToken"] = "Sesi Anda sudah berakhir. Silakan masuk kembali.",
        ["Concurrency.Conflict"] = "Data sudah diubah pengguna lain. Muat ulang lalu coba lagi.",
        ["Database.UniqueViolation"] = "Data yang sama sudah ada.",
        ["Validation.General"] = "Periksa kembali isian Anda.",
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
