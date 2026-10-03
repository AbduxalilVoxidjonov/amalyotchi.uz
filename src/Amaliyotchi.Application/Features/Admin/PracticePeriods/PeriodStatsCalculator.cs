using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Tutor.Common;
using Amaliyotchi.Domain.Diary;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Grading;
using Amaliyotchi.Domain.Leave;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.Students;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.PracticePeriods;

/// <summary>Davrga biriktirilgan guruh (o'chirilgani ham — tarix).</summary>
internal sealed record PeriodStatsGroup(Guid Id, string Code, int Course, string FacultyName, string DirectionName);

/// <summary>Talabaning shu davr bo'yicha hisoblangan natijasi — ikkala endpoint va agregat shundan quriladi.</summary>
internal sealed record PeriodStudentResult(
    Guid GroupId,
    PeriodGroupStudentRow Row,
    int DiaryApprovedCount,
    IReadOnlyList<int?> DiaryScores,
    bool WithCompany,
    bool PendingApplication);

/// <summary>Yuklangan davr + guruhlar + talabalar natijalari.</summary>
internal sealed record PeriodStatsData(
    PeriodContext Period,
    DateOnly Today,
    int ElapsedWorkDays,
    IReadOnlyList<PeriodStatsGroup> Groups,
    IReadOnlyList<PeriodStudentResult> Students);

/// <summary>Admin davr statistikasi uchun yagona hisob manbai: faqat ANIQ shu davr (<c>PeriodId == id</c>) yozuvlari,
/// sukut bo'yicha davr tanlash (<c>PeriodSelection</c>) ishlatilmaydi. Davomat — <see cref="StudentStatsCalculator"/>,
/// ballar — <see cref="GradeCalculator"/> (tyutor baholash jadvali bilan bir xil formula). Talabalar — guruhdagi joriy
/// faol (<c>StudentStatus.Active</c>, o'chirilmagan) profillar (<c>PracticePeriodDetail.StudentsCount</c> bilan bir xil).
/// Ma'lumot guruh bo'yicha emas, bir necha batch so'rov bilan yuklanadi.</summary>
internal static class PeriodStatsCalculator
{
    public const string GroupNotFoundMessage = "Guruh shu amaliyot davriga biriktirilmagan.";

    /// <summary>Davr yo'q/o'chirilgan → 404; <paramref name="groupId"/> berilsa va davrga biriktirilmagan bo'lsa → 404.</summary>
    public static async Task<PeriodStatsData> LoadAsync(
        IApplicationDbContext db, IClock clock, Guid periodId, Guid? groupId, CancellationToken cancellationToken)
    {
        var period = await db.PracticePeriods
            .AsNoTracking()
            .Include(p => p.Groups)
            .FirstOrDefaultAsync(p => p.Id == periodId, cancellationToken)
            ?? throw new NotFoundException(PracticePeriodQueries.NotFoundMessage);

        var groupIds = period.Groups.Select(g => g.StudentGroupId).ToList();
        if (groupId is { } only)
        {
            if (!groupIds.Contains(only))
                throw new NotFoundException(GroupNotFoundMessage);
            groupIds = [only];
        }

        var context = await PeriodLookup.ContextAsync(db, period, cancellationToken);
        var today = clock.LocalToday();
        var localNow = clock.LocalTime();
        var elapsed = StudentStatsCalculator.ElapsedWorkDays(context, today, localNow).Count();

        // Guruh keyin o'chirilgan bo'lsa ham davr statistikasida ko'rinsin (LoadDetailAsync bilan bir xil).
        var groups = await (from g in db.StudentGroups.IgnoreQueryFilters().AsNoTracking()
                            join d in db.Directions.IgnoreQueryFilters() on g.DirectionId equals d.Id
                            join dept in db.Departments.IgnoreQueryFilters() on d.DepartmentId equals dept.Id
                            join f in db.Faculties.IgnoreQueryFilters() on dept.FacultyId equals f.Id
                            where groupIds.Contains(g.Id)
                            orderby g.Name
                            select new PeriodStatsGroup(g.Id, g.Name, g.Course, f.Name, d.Name))
            .ToListAsync(cancellationToken);

        var students = await (from s in db.StudentProfiles.IgnoreQueryFilters().AsNoTracking()
                              join u in db.Users.IgnoreQueryFilters() on s.UserId equals u.Id
                              where groupIds.Contains(s.StudentGroupId) && !s.IsDeleted && s.Status == StudentStatus.Active
                              orderby u.FullName, s.HemisId
                              select new
                              {
                                  s.UserId, s.StudentGroupId, u.FullName, s.HemisId,
                                  s.WorkStart, s.WorkEnd, s.WorkHoursEffectiveFrom, s.PreviousWorkStart, s.PreviousWorkEnd
                              })
            .ToListAsync(cancellationToken);

        if (students.Count == 0)
            return new PeriodStatsData(context, today, elapsed, groups, []);

        var studentIds = students.Select(s => s.UserId).ToList();

        var attendance = (await db.DailyAttendances.AsNoTracking()
                .Where(a => a.PeriodId == periodId && studentIds.Contains(a.StudentUserId))
                .SelectSnapshot()
                .ToListAsync(cancellationToken))
            .ToLookup(a => a.StudentUserId);

        var leaves = (await db.LeaveRequests.AsNoTracking()
                .Where(l => l.PeriodId == periodId && studentIds.Contains(l.StudentUserId) && l.Status == LeaveRequestStatus.Approved)
                .Select(l => new { l.StudentUserId, l.DateFrom, l.DateTo })
                .ToListAsync(cancellationToken))
            .ToLookup(l => l.StudentUserId, l => (l.DateFrom, l.DateTo));

        var diaries = (await db.DiaryEntries.AsNoTracking()
                .Where(d => d.PeriodId == periodId && studentIds.Contains(d.StudentUserId))
                .Select(d => new { d.StudentUserId, d.Status, d.Score })
                .ToListAsync(cancellationToken))
            .ToLookup(d => d.StudentUserId);

        var grades = (await db.PracticeGrades.AsNoTracking()
                .Where(g => g.PeriodId == periodId && studentIds.Contains(g.StudentUserId))
                .Select(g => new { g.StudentUserId, g.TutorPoints, g.ReferencePoints, Finalized = g.FinalizedAt != null })
                .ToListAsync(cancellationToken))
            .ToDictionary(g => g.StudentUserId);

        // Korxona keyin o'chirilgan bo'lsa ham ariza (va korxona nomi) tarixda qolsin — ariza o'zi soft-delete emas.
        var applications = (await db.PracticeApplications.IgnoreQueryFilters().AsNoTracking()
                .Where(a => a.PeriodId == periodId && studentIds.Contains(a.StudentUserId))
                .Select(a => new { a.StudentUserId, a.Status, a.CreatedAt, a.SubmittedAt, a.DecidedAt, Company = a.Company.Name })
                .ToListAsync(cancellationToken))
            .ToLookup(a => a.StudentUserId);

        var results = new List<PeriodStudentResult>(students.Count);
        foreach (var student in students)
        {
            var stats = StudentStatsCalculator.ComputeAttendance(
                context, attendance[student.UserId].ToList(), leaves[student.UserId].ToList(), today, localNow,
                StudentProfile.ResolveHours(
                    student.WorkStart, student.WorkEnd, student.WorkHoursEffectiveFrom,
                    student.PreviousWorkStart, student.PreviousWorkEnd, today));

            var studentDiaries = diaries[student.UserId].ToList();
            var scores = studentDiaries.Select(d => d.Score).ToList();
            var diary = StudentStatsCalculator.ComputeDiary(scores);

            var grade = grades.GetValueOrDefault(student.UserId);
            var result = GradeCalculator.Compute(
                stats.AttendancePct, diary.ScoredCount, diary.Avg, grade?.TutorPoints, grade?.ReferencePoints);

            var studentApps = applications[student.UserId].ToList();
            var latest = studentApps
                .Where(a => a.Status != ApplicationStatus.Transferred)
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.SubmittedAt)
                .FirstOrDefault();
            var placement = studentApps
                .Where(a => a.Status is ApplicationStatus.Approved or ApplicationStatus.Completed)
                .OrderByDescending(a => a.DecidedAt)
                .FirstOrDefault();

            var row = new PeriodGroupStudentRow(
                student.UserId,
                student.FullName,
                student.HemisId,
                placement?.Company,
                latest?.Status,
                stats.AttendancePct,
                stats.AttendedDays - stats.LateDays,
                stats.LateDays,
                stats.TotalDays - stats.AttendedDays,
                stats.ExcusedDays,
                stats.SuspiciousCount,
                diary.Count,
                diary.Avg,
                result.AttendancePoints,
                result.ReportPoints,
                grade?.TutorPoints,
                grade?.ReferencePoints,
                result.Total,
                result.Grade,
                grade?.Finalized ?? false);

            results.Add(new PeriodStudentResult(
                student.StudentGroupId,
                row,
                studentDiaries.Count(d => d.Status == DiaryStatus.Approved),
                scores,
                placement is not null,
                latest?.Status is ApplicationStatus.Submitted or ApplicationStatus.RevisionNeeded));
        }

        return new PeriodStatsData(context, today, elapsed, groups, results);
    }

    /// <summary>Talabalar to'plami bo'yicha agregat. O'rtachalar talabalar bo'yicha; kundalik o'rtachasi — barcha
    /// baholangan yozuvlar bo'yicha (<see cref="StudentStatsCalculator.ComputeDiary"/>).</summary>
    public static GroupMetrics Aggregate(IReadOnlyCollection<PeriodStudentResult> students, int elapsedWorkDays)
    {
        if (students.Count == 0)
            return new GroupMetrics(0, 0, 0, 0, 0, 0, 0, 0, 0, null, 0, GradeDistribution.Empty);

        var rows = students.Select(s => s.Row).ToList();
        var diary = StudentStatsCalculator.ComputeDiary(students.SelectMany(s => s.DiaryScores).ToList());

        return new GroupMetrics(
            rows.Count,
            (int)Math.Round(rows.Average(r => r.AttendancePct), MidpointRounding.AwayFromZero),
            elapsedWorkDays > 0 ? rows.Count(r => r.AttendancePct < GradeThresholds.MinAttendancePct) : 0,
            rows.Sum(r => r.SuspiciousDays),
            students.Count(s => s.WithCompany),
            students.Count(s => s.PendingApplication),
            diary.Count,
            students.Sum(s => s.DiaryApprovedCount),
            diary.Avg,
            Math.Round(rows.Average(r => r.Total), 1, MidpointRounding.AwayFromZero),
            rows.Count(r => r.Finalized),
            new GradeDistribution(
                rows.Count(r => r.Grade == 5),
                rows.Count(r => r.Grade == 4),
                rows.Count(r => r.Grade == 3),
                rows.Count(r => r.Grade == 2),
                rows.Count(r => r.Grade is null)));
    }
}
