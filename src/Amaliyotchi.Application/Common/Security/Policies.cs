namespace Amaliyotchi.Application.Common.Security;

/// <summary>Avtorizatsiya siyosatlari nomlari. Controller'larda rol satri
/// ("Admin") yozilmaydi — faqat shu konstantalar ishlatiladi.</summary>
public static class Policies
{
    public const string AdminOnly = "admin.only";
    public const string TutorOnly = "tutor.only";
    public const string StudentOnly = "student.only";
    public const string TutorOrAdmin = "tutor.or.admin";
    public const string Authenticated = "authenticated";
}
