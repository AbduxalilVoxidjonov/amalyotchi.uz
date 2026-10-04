namespace Amaliyotchi.Application.Common.Security;

/// <summary>Avtorizatsiya siyosatlari nomlari. Controller'larda rol satri
/// ("Admin") yozilmaydi — faqat shu konstantalar ishlatiladi.</summary>
public static class Policies
{
    public const string AdminOnly = "admin.only";

    /// <summary>Bosh admin: rol Admin + <c>is_super_admin</c> claim'i (login/refresh'da bazadan yoziladi).
    /// Handler'lar qo'shimcha ravishda bazadan ham tekshiradi.</summary>
    public const string SuperAdminOnly = "super.admin.only";

    public const string StudentOnly = "student.only";
    public const string Authenticated = "authenticated";
}
