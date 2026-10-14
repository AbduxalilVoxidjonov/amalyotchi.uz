using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Practice;

namespace Amaliyotchi.Application.Features.Student.Profile;

/// <summary>Talabaning ish vaqti (kelish/ketish) — <c>GET /api/student/profile</c> dagi <c>workHours</c> (faqat o'qish)
/// va <c>GET /api/admin/students/{id}</c> dagi <c>workHours</c>. Ish vaqtini FAQAT admin belgilaydi
/// (<c>PUT /api/admin/students/{id}/work-hours</c>). Barcha vaqtlar "HH:mm" (Toshkent).</summary>
/// <param name="Start">Admin oxirgi belgilagan kelish vaqti; null — davr soatlari ishlatiladi.</param>
/// <param name="End">Admin oxirgi belgilagan ketish vaqti; null — davr soatlari.</param>
/// <param name="EffectiveFrom"><c>start/end</c> shu sanadan (yyyy-MM-dd) amal qiladi; hech qachon o'rnatilmagan bo'lsa null.
/// O'zgarish har doim ERTADAN kuchga kiradi.</param>
/// <param name="TodayStart">Bugun amaldagi kelish vaqti (admin belgilagan yoki davr/standart).</param>
/// <param name="TodayEnd">Bugun amaldagi ketish vaqti.</param>
/// <param name="PeriodStart">Sukut bo'yicha davr soati; davr yo'q → null.</param>
/// <param name="PeriodEnd">Sukut bo'yicha davr tugash soati; davr yo'q → null.</param>
public sealed record StudentWorkHoursDto(
    string? Start,
    string? End,
    DateOnly? EffectiveFrom,
    string TodayStart,
    string TodayEnd,
    string? PeriodStart,
    string? PeriodEnd);

internal static class StudentWorkHoursMapper
{
    /// <summary>Profil + sukut bo'yicha davr (<see cref="PeriodPurpose.Default"/>) dan DTO. Bugungi soatlar —
    /// <see cref="StudentPractice.Rules"/> (check-in/check-out bilan bir xil manba).</summary>
    public static StudentWorkHoursDto ToDto(StudentPractice practice)
    {
        var student = practice.Student;
        var rules = practice.Rules;
        return new StudentWorkHoursDto(
            student.WorkStart is { } start ? PracticeTime.Hm(start) : null,
            student.WorkEnd is { } end ? PracticeTime.Hm(end) : null,
            student.WorkHoursEffectiveFrom,
            PracticeTime.Hm(rules.DailyStart),
            PracticeTime.Hm(rules.DailyEnd),
            practice.Period is { } period ? PracticeTime.Hm(period.DailyStart) : null,
            practice.Period is { } p ? PracticeTime.Hm(p.DailyEnd) : null);
    }

    public static async Task<StudentWorkHoursDto> LoadAsync(
        IApplicationDbContext db, Guid userId, DateOnly today, CancellationToken cancellationToken)
        => ToDto(await db.LoadStudentPracticeAsync(userId, today, PeriodPurpose.Default, cancellationToken));
}
