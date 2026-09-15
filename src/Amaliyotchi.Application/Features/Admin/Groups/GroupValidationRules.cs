using System.Text.RegularExpressions;
using Amaliyotchi.Domain.Organization;

namespace Amaliyotchi.Application.Features.Admin.Groups;

/// <summary>Create/Update buyruqlari uchun umumiy validatsiya qoidalari (ikkala validator ham shu yerga tayanadi).</summary>
internal static partial class GroupValidationRules
{
    public const string NameRequiredMessage = "Guruh nomini kiriting.";
    public const string NameFormatMessage = "Guruh nomi 2–20 ta lotin harf, raqam yoki '-' dan iborat bo'lishi kerak.";
    public static readonly string CourseRangeMessage =
        $"Kurs {StudentGroup.MinCourse}–{StudentGroup.MaxCourse} oralig'ida bo'lishi kerak.";

    /// <summary>Trim qilingandan so'ng 2–20 ta lotin harf, raqam yoki tire, masalan "412-22".</summary>
    public static bool IsValidName(string name)
    {
        var trimmed = name.Trim();
        return trimmed.Length is >= 2 and <= 20 && NamePattern().IsMatch(trimmed);
    }

    public static bool IsValidCourse(int course) => course is >= StudentGroup.MinCourse and <= StudentGroup.MaxCourse;

    [GeneratedRegex("^[A-Za-z0-9-]+$")]
    private static partial Regex NamePattern();
}
