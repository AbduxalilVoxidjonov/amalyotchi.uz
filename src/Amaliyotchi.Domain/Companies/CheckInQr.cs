using System.Security.Cryptography;
using System.Text;

namespace Amaliyotchi.Domain.Companies;

/// <summary>Korxonada osilgan check-in QR kodi: payload <c>AMLQR:1:{token}</c>, token — 32 belgili kichik hex
/// (<see cref="RandomNumberGenerator"/>, 128 bit). Talaba TWA'da skanerlagan satr serverda shu yerda tekshiriladi.</summary>
public static class CheckInQr
{
    public const string Prefix = "AMLQR:1:";

    /// <summary>Token uzunligi (16 bayt → 32 hex belgi).</summary>
    public const int TokenLength = 32;

    /// <summary>Bazadagi ustun chegarasi — kelajakdagi format o'zgarishi uchun zaxira bilan.</summary>
    public const int TokenMaxLength = 64;

    /// <summary>Yangi tasodifiy token (kichik harfli hex).</summary>
    public static string NewToken() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(TokenLength / 2));

    /// <summary>QR ichidagi satr.</summary>
    public static string ToPayload(string token) => Prefix + token;

    /// <summary>Payload'dan tokenni ajratadi: prefiks aniq (<c>AMLQR:1:</c>), token 32 belgili hex bo'lishi shart.
    /// Chetdagi bo'shliqlar kechiriladi; hex registri muhim emas.</summary>
    public static bool TryParse(string? payload, out string token)
    {
        token = string.Empty;
        var trimmed = payload?.Trim();
        if (string.IsNullOrEmpty(trimmed) || !trimmed.StartsWith(Prefix, StringComparison.Ordinal))
            return false;

        var candidate = trimmed[Prefix.Length..];
        if (candidate.Length != TokenLength || !candidate.All(char.IsAsciiHexDigit))
            return false;

        token = candidate.ToLowerInvariant();
        return true;
    }

    /// <summary>Skanerlangan payload korxona tokeniga mosmi. Taqqoslash doimiy vaqtli
    /// (<see cref="CryptographicOperations.FixedTimeEquals"/>). Format noto'g'ri → <c>false</c>.</summary>
    public static bool Matches(string? payload, string companyToken)
    {
        if (string.IsNullOrEmpty(companyToken) || !TryParse(payload, out var token))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(token), Encoding.ASCII.GetBytes(companyToken.ToLowerInvariant()));
    }
}
