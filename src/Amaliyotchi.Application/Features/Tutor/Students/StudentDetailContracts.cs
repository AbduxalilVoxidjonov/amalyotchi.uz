using System.Globalization;
using Amaliyotchi.Application.Features.Tutor.Applications;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.Students;

namespace Amaliyotchi.Application.Features.Tutor.Students;

/// <summary>Talabaning amaliyot korxonasi (tasdiqlangan arizadan) — geofence markazi va radiusi bilan.</summary>
public sealed record StudentCompany(
    Guid Id,
    string Name,
    string Tin,
    string Activity,
    string Address,
    string SupervisorName,
    string SupervisorPhone,
    string? MentorName,
    string? MentorPhone,
    double Lat,
    double Lng,
    int RadiusM);

/// <summary>Talabaning oxirgi arizasi (holatidan qat'i nazar) + shartnoma fayli.</summary>
public sealed record StudentApplication(
    Guid Id,
    ApplicationStatus Status,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? DecidedAt,
    string? Comment,
    ApplicationContract? Contract);

/// <param name="DailyStart">"HH:mm" (Toshkent).</param>
/// <param name="WorkDays">Ish kunlari raqamlari: 1 = Dushanba … 7 = Yakshanba.</param>
public sealed record StudentPeriod(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string DailyStart,
    string DailyEnd,
    IReadOnlyList<int> WorkDays,
    int RequiredDays);

/// <param name="TotalDays">Hisobga olinadigan ish kunlari (sababli kunlarsiz).</param>
/// <param name="AbsentDays">Hisobga olinadigan kunlardan kelmaganlari.</param>
public sealed record AttendanceSummary(
    int TotalDays,
    int AttendedDays,
    int LateDays,
    int ExcusedDays,
    int AbsentDays,
    int SuspiciousDays,
    double AttendancePct);

public sealed record DiarySummary(int Count, int ScoredCount, double Avg);

/// <param name="Grade">2–5 yoki null (davomat yetarli emas — qayta topshiradi).</param>
public sealed record StudentGrade(double Total, int? Grade);

/// <summary><c>GET /api/tutor/students/{id}</c> javobi: profil + korxona + ariza + davr + statistika + baho.
/// <c>company</c> — faqat tasdiqlangan (yoki yakunlangan) arizada bo'ladi; <c>period</c>/<c>grade</c> —
/// talaba guruhining faol davri bo'lmasa null.</summary>
public sealed record TutorStudentDetail(
    Guid Id,
    string Name,
    string HemisId,
    string Group,
    int Course,
    string Faculty,
    string Direction,
    StudentStatus Status,
    string? Phone,
    StudentState State,
    int SuspiciousCount,
    StudentCompany? Company,
    StudentApplication? Application,
    StudentPeriod? Period,
    AttendanceSummary Attendance,
    DiarySummary Diary,
    StudentGrade? Grade);

internal static class WorkDayNumbers
{
    /// <summary>Bitmask → [1..7] raqamlar. Manba — <see cref="WorkDaysExtensions.ToCsv"/> (sozlama formati bilan
    /// bir xil konvensiya), shuning uchun bit tartibi ikkinchi joyda takrorlanmaydi.</summary>
    public static IReadOnlyList<int> Of(WorkDays days)
        => days.ToCsv()
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(n => int.Parse(n, CultureInfo.InvariantCulture))
            .ToArray();
}
