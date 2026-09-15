using Amaliyotchi.Domain.ValueObjects;

namespace Amaliyotchi.Application.Features.Admin.Tutors;

/// <summary>Create/Update/ResetPassword buyruqlari uchun umumiy validatsiya qoidalari.</summary>
internal static class TutorValidationRules
{
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;

    public const string IdRequiredMessage = "Tyutor ko'rsatilmagan.";
    public const string FullNameRequiredMessage = "FISH ni kiriting.";
    public const string FullNameLengthMessage = "FISH 2–150 belgi bo'lishi kerak.";
    public const string HemisIdRequiredMessage = "HEMIS ID ni kiriting.";
    public static readonly string HemisIdFormatMessage =
        $"HEMIS ID {HemisId.MinLength}–{HemisId.MaxLength} ta raqamdan iborat bo'lishi kerak.";
    public const string PhoneFormatMessage = "Telefon raqami noto'g'ri. Namuna: +998901234567";
    public const string PasswordRequiredMessage = "Parolni kiriting.";
    public static readonly string PasswordLengthMessage =
        $"Parol {PasswordMinLength}–{PasswordMaxLength} ta belgidan iborat bo'lishi kerak.";
    public const string FacultyRequiredMessage = "Fakultet ko'rsatilmagan.";

    public static bool IsValidFullName(string fullName) => fullName.Trim().Length is >= 2 and <= 150;

    public static bool IsValidHemisId(string hemisId) => HemisId.TryNormalize(hemisId, out _);

    /// <summary>Bo'sh telefon — ruxsat (ixtiyoriy maydon); to'ldirilgan bo'lsa O'zbekiston formati.</summary>
    public static bool IsValidOptionalPhone(string? phone)
        => string.IsNullOrWhiteSpace(phone) || PhoneNumber.TryNormalize(phone, out _);

    public static bool IsValidPassword(string password)
        => password.Length is >= PasswordMinLength and <= PasswordMaxLength;
}
