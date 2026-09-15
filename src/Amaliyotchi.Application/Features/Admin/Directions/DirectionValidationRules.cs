using System.Text.RegularExpressions;

namespace Amaliyotchi.Application.Features.Admin.Directions;

/// <summary>Create/Update buyruqlari uchun umumiy validatsiya qoidalari (ikkala validator ham shu yerga tayanadi).</summary>
internal static partial class DirectionValidationRules
{
    public const string NameRequiredMessage = "Nomni kiriting.";
    public const string NameLengthMessage = "Nom 2–150 belgi bo'lishi kerak.";
    public const string CodeRequiredMessage = "Kodni kiriting.";
    public const string CodeFormatMessage = "Kod 2–20 ta lotin harf, raqam yoki '-' dan iborat bo'lishi kerak.";

    public static bool IsValidName(string name) => name.Trim().Length is >= 2 and <= 150;

    /// <summary>Trim qilingandan so'ng 2–20 ta lotin harf, raqam yoki tire.</summary>
    public static bool IsValidCode(string code)
    {
        var trimmed = code.Trim();
        return trimmed.Length is >= 2 and <= 20 && CodePattern().IsMatch(trimmed);
    }

    [GeneratedRegex("^[A-Za-z0-9-]+$")]
    private static partial Regex CodePattern();
}
