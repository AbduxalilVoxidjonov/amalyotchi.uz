using Amaliyotchi.Domain.Practice;

namespace Amaliyotchi.Application.Features.Admin.PracticePeriods;

/// <summary>Baholar taqsimoti — <c>GradeCalculator</c> ning joriy (jonli) natijasi bo'yicha: 5/4/3/2 va
/// <paramref name="Retake"/> (baho <c>null</c> — davomat yetarli emas, qayta topshiradi).</summary>
public sealed record GradeDistribution(int Excellent, int Good, int Satisfactory, int Unsatisfactory, int Retake)
{
    public static GradeDistribution Empty { get; } = new(0, 0, 0, 0, 0);
}

/// <summary>Talabalar to'plami (guruh yoki butun davr) bo'yicha ko'rsatkichlar. O'rtachalar — talabalar bo'yicha.
/// <paramref name="AttendancePct"/> — talabalar davomat foizlari o'rtachasi (butun songa yaxlitlangan), talaba yo'q → 0.
/// <paramref name="LowAttendanceCount"/> — davomati <c>GradeThresholds.MinAttendancePct</c> dan past talabalar
/// (o'tgan ish kuni bo'lmasa 0). <paramref name="WithCompanyCount"/> — shu davrda tasdiqlangan (yoki yakunlangan)
/// arizasi bor talabalar; <paramref name="PendingApplicationsCount"/> — shu davrdagi eng so'nggi arizasi ko'rib
/// chiqilayotgan (<c>submitted</c>/<c>revisionNeeded</c>) talabalar. <paramref name="DiaryAvgScore"/> — baholangan
/// yozuvlar o'rtachasi (1 xona), yo'q → 0. <paramref name="AvgTotal"/> — jami ball o'rtachasi, talaba yo'q → null.</summary>
public sealed record GroupMetrics(
    int StudentsCount,
    int AttendancePct,
    int LowAttendanceCount,
    int SuspiciousDays,
    int WithCompanyCount,
    int PendingApplicationsCount,
    int DiaryCount,
    int DiaryApprovedCount,
    double DiaryAvgScore,
    double? AvgTotal,
    int FinalizedCount,
    GradeDistribution Grades);

/// <summary>Davrdagi guruh qatori: guruh ma'lumoti + <see cref="GroupMetrics"/> maydonlari (JSON'da bir tekis).</summary>
public sealed record PeriodGroupStats(
    Guid GroupId,
    string Code,
    int Course,
    string DirectionName,
    int StudentsCount,
    int AttendancePct,
    int LowAttendanceCount,
    int SuspiciousDays,
    int WithCompanyCount,
    int PendingApplicationsCount,
    int DiaryCount,
    int DiaryApprovedCount,
    double DiaryAvgScore,
    double? AvgTotal,
    int FinalizedCount,
    GradeDistribution Grades)
{
    public static PeriodGroupStats From(Guid groupId, string code, int course, string directionName, GroupMetrics m)
        => new(groupId, code, course, directionName,
            m.StudentsCount, m.AttendancePct, m.LowAttendanceCount, m.SuspiciousDays, m.WithCompanyCount,
            m.PendingApplicationsCount, m.DiaryCount, m.DiaryApprovedCount, m.DiaryAvgScore, m.AvgTotal,
            m.FinalizedCount, m.Grades);
}

/// <summary><c>GET /api/admin/practice-periods/{id}/stats</c>. <paramref name="ElapsedWorkDays"/> — davr boshidan
/// o'tgan ish kunlari (kechagacha + bugun, check-in oynasi yopilgan bo'lsa; davomat foizi maxrajining asosi).
/// <paramref name="Totals"/> — barcha guruhlar talabalari bo'yicha; <paramref name="Groups"/> — davrdagi har guruh
/// (talabasi yo'q bo'lsa ham), nomi bo'yicha.</summary>
public sealed record PracticePeriodStats(
    Guid PeriodId,
    int ElapsedWorkDays,
    int RequiredDays,
    GroupMetrics Totals,
    IReadOnlyList<PeriodGroupStats> Groups);

public sealed record PeriodStatsPeriodDto(
    Guid Id,
    string Name,
    PracticePeriodStatus Status,
    DateOnly StartDate,
    DateOnly EndDate);

public sealed record PeriodStatsGroupDto(
    Guid Id,
    string Code,
    int Course,
    string FacultyName,
    string DirectionName);

/// <summary>Talabaning shu davrdagi natijasi. <paramref name="Id"/> — <c>User.Id</c>.
/// <paramref name="Company"/> — shu davrdagi tasdiqlangan arizaning korxonasi (eng so'nggi qaror);
/// <paramref name="ApplicationStatus"/> — shu davrdagi eng so'nggi ariza holati, yo'q → null.
/// Kunlar: <paramref name="PresentDays"/> — o'z vaqtida kelgan, <paramref name="LateDays"/> — kech kelgan,
/// <paramref name="AbsentDays"/> — kelmagan (hisobga olinadigan kunlardan kelmagan, sababsiz),
/// <paramref name="ExcusedDays"/> — sababli (maxrajdan chiqariladi). Ballar — <c>GradeCalculator</c>;
/// <paramref name="Grade"/> null — qayta topshiradi.</summary>
public sealed record PeriodGroupStudentRow(
    Guid Id,
    string FullName,
    string HemisId,
    string? Company,
    ApplicationStatus? ApplicationStatus,
    double AttendancePct,
    int PresentDays,
    int LateDays,
    int AbsentDays,
    int ExcusedDays,
    int SuspiciousDays,
    int DiaryCount,
    double DiaryAvg,
    double AttendancePoints,
    double ReportPoints,
    int? TutorPoints,
    int? ReferencePoints,
    double Total,
    int? Grade,
    bool Finalized);

/// <summary><c>GET /api/admin/practice-periods/{id}/groups/{groupId}/students</c>. <paramref name="Metrics"/> —
/// <c>/stats</c> dagi guruh qatori bilan bir xil hisob; <paramref name="Students"/> — FISH bo'yicha.</summary>
public sealed record PeriodGroupStudents(
    PeriodStatsPeriodDto Period,
    PeriodStatsGroupDto Group,
    int ElapsedWorkDays,
    GroupMetrics Metrics,
    IReadOnlyList<PeriodGroupStudentRow> Students);
