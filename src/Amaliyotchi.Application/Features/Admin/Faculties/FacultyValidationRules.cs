using System.Text.RegularExpressions;

namespace Amaliyotchi.Application.Features.Admin.Faculties;

/// <summary>Create/Update buyruqlari uchun umumiy validatsiya qoidalari (ikkala validator ham shu yerga tayanadi).</summary>
internal static partial class FacultyValidationRules
{
    public const string NameRequiredMessage = "Fakultet nomini kiriting.";
    public const string NameLengthMessage = "Fakultet nomi 2–150 belgi bo'lishi kerak.";
    public const string CodeRequiredMessage = "Fakultet kodini kiriting.";
    public const string CodeFormatMessage = "Fakultet kodi 2–10 ta lotin harf yoki raqamdan iborat bo'lishi kerak.";

    public static bool IsValidName(string name) => name.Trim().Length is >= 2 and <= 150;

    /// <summary>Trim qilingandan so'ng 2–10 ta lotin harf yoki raqam.</summary>
    public static bool IsValidCode(string code)
    {
        var trimmed = code.Trim();
        return trimmed.Length is >= 2 and <= 10 && CodePattern().IsMatch(trimmed);
    }

    [GeneratedRegex("^[A-Za-z0-9]+$")]
    private static partial Regex CodePattern();
}
