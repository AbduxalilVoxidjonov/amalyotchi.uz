using System.Security.Cryptography;
using System.Text;

namespace Amaliyotchi.Application.Common.Security;

/// <summary>Refresh token bazada ochiq holda emas, SHA-256 xeshi (base64, 44 belgi) ko'rinishida saqlanadi:
/// baza (yoki zaxira nusxa) sizib chiqsa ham undagi qiymat bilan sessiya olib bo'lmaydi. Token 48 bayt
/// tasodifiy qiymat — tuz/PBKDF2 kerak emas, oddiy SHA-256 yetarli va qidiruv (unikal indeks) bo'yicha ishlaydi.
/// Mijozga faqat xom token beriladi; bazaga yozish va bazadan qidirish — hamisha shu xesh orqali.</summary>
public static class RefreshTokenHash
{
    public static string Of(string rawToken)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    /// <summary>Ixtiyoriy token (masalan "joriy sessiyani saqlab qol") — bo'sh bo'lsa <c>null</c>.</summary>
    public static string? OfOptional(string? rawToken)
        => string.IsNullOrWhiteSpace(rawToken) ? null : Of(rawToken);
}
