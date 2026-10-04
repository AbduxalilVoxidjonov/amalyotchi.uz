using Amaliyotchi.Domain.ValueObjects;

namespace Amaliyotchi.Application.Features.Admin.Common;

/// <summary>Hisob (admin, talaba paroli, o'z parolini almashtirish) buyruqlari uchun umumiy validatsiya qoidalari.</summary>
internal static class AccountValidationRules
{
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;

    public const string FullNameRequiredMessage = "FISH ni kiriting.";
    public const string FullNameLengthMessage = "FISH 2–150 belgi bo'lishi kerak.";
    public const string PhoneFormatMessage = "Telefon raqami noto'g'ri. Namuna: +998901234567";
    public const string PasswordRequiredMessage = "Parolni kiriting.";
    public static readonly string PasswordLengthMessage =
        $"Parol {PasswordMinLength}–{PasswordMaxLength} ta belgidan iborat bo'lishi kerak.";

    public static bool IsValidFullName(string fullName) => fullName.Trim().Length is >= 2 and <= 150;

    /// <summary>Bo'sh telefon — ruxsat (ixtiyoriy maydon); to'ldirilgan bo'lsa O'zbekiston formati.</summary>
    public static bool IsValidOptionalPhone(string? phone)
        => string.IsNullOrWhiteSpace(phone) || PhoneNumber.TryNormalize(phone, out _);

    /// <summary>Ixtiyoriy telefon: bo'sh → null, aks holda E.164 (<c>+998…</c>).</summary>
    public static string? NormalizePhoneOrNull(string? phone)
        => string.IsNullOrWhiteSpace(phone) ? null : PhoneNumber.Normalize(phone);

    public static bool IsValidPassword(string password)
        => password.Length is >= PasswordMinLength and <= PasswordMaxLength;
}
