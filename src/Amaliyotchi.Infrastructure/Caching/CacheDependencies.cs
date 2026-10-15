using Amaliyotchi.Application.Common.Caching;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Auditing;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Diary;
using Amaliyotchi.Domain.Faces;
using Amaliyotchi.Domain.Identity;
using Amaliyotchi.Domain.Leave;
using Amaliyotchi.Domain.Organization;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.Settings;
using Amaliyotchi.Domain.Students;

namespace Amaliyotchi.Infrastructure.Caching;

/// <summary>Entity turi → u o'zgarganda bekor qilinadigan kesh teglari. Keshlangan so'rov yangi jadvalga tayana
/// boshlasa — shu yerga qo'shiladi (aks holda kesh TTL tugaguncha eski qiymat ko'rsatadi).
/// <list type="bullet">
/// <item><see cref="CacheTags.Organization"/> — fakultet/kafedra/yo'nalish/guruh/o'quv yili (filtrlar, guruh pickeri).</item>
/// <item><see cref="CacheTags.AdminNav"/> — ro'yxat jami sonlari (talaba, korxona, fakultet), ko'rib chiqiladigan
/// arizalar, tekshirilmagan kundaliklar, kutilayotgan yuz etalonlari, joriy o'quv yili.</item>
/// <item><see cref="CacheTags.AdminDashboard"/> — nav'dagi hamma narsa + davrlar, davomat, ruxsatlar, bayramlar,
/// sozlamalar (kalendar) va so'nggi audit yozuvlari.</item>
/// <item><see cref="CacheTags.Settings"/> — sozlamalar, bayramlar, hujjat shablonlari.</item>
/// </list></summary>
internal static class CacheDependencies
{
    private static readonly string[] Org = [CacheTags.Organization, CacheTags.AdminNav, CacheTags.AdminDashboard];
    private static readonly string[] NavAndDashboard = [CacheTags.AdminNav, CacheTags.AdminDashboard];
    private static readonly string[] Dashboard = [CacheTags.AdminDashboard];
    private static readonly string[] SettingsAndDashboard = [CacheTags.Settings, CacheTags.AdminDashboard];
    private static readonly string[] SettingsOnly = [CacheTags.Settings];

    private static readonly Dictionary<Type, string[]> Map = new()
    {
        [typeof(Faculty)] = Org,
        [typeof(Department)] = Org,
        [typeof(Direction)] = Org,
        [typeof(StudentGroup)] = Org,
        [typeof(AcademicYear)] = Org,

        [typeof(User)] = NavAndDashboard,
        [typeof(StudentProfile)] = NavAndDashboard,
        [typeof(Company)] = NavAndDashboard,
        [typeof(PracticeApplication)] = NavAndDashboard,
        [typeof(DiaryEntry)] = NavAndDashboard,
        [typeof(StudentFaceEnrollment)] = NavAndDashboard,

        [typeof(PracticePeriod)] = Dashboard,
        [typeof(PracticePeriodGroup)] = Dashboard,
        [typeof(DailyAttendance)] = Dashboard,
        [typeof(LeaveRequest)] = Dashboard,
        [typeof(AuditLog)] = Dashboard,

        [typeof(AppSetting)] = SettingsAndDashboard,
        [typeof(Holiday)] = SettingsAndDashboard,
        [typeof(DocumentTemplate)] = SettingsOnly,
    };

    public static IReadOnlyList<string> For(Type entityType)
        => Map.TryGetValue(entityType, out var tags) ? tags : [];
}
