using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Features.Supervision.Common;
using Amaliyotchi.Domain.Grading;
using Amaliyotchi.Domain.Leave;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Supervision.Grading;

/// <summary>GET va PUT uchun bitta hisob manbai: davomat/kundalik statistikasi + saqlangan <c>PracticeGrade</c> →
/// <see cref="GradeCalculator"/>. Davr — guruhning sukut bo'yicha davri (<c>PeriodLookup.ForGroup</c>: davom etayotgan →
/// oxirgi tugagan → kelgusi); davri bo'lmagan talaba qatorga kirmaydi.</summary>
internal static class GradingRowBuilder
{
    public static async Task<List<GradingRow>> BuildAsync(
        IApplicationDbContext db,
        DataScope scope,
        IReadOnlyList<ScopedStudent> students,
        PeriodLookup periods,
        DateOnly today,
        TimeOnly localNow,
        CancellationToken cancellationToken)
    {
        var studentIds = students.Select(s => s.UserId).ToList();
        var periodIds = periods.DefaultPeriodIds(students.Select(s => s.GroupId));

        var attendance = (await db.DailyAttendances.AsNoTracking().InScope(scope)
                .Where(a => studentIds.Contains(a.StudentUserId) && periodIds.Contains(a.PeriodId))
                .SelectSnapshot()
                .ToListAsync(cancellationToken))
            .ToLookup(a => a.StudentUserId);

        var leaves = (await db.LeaveRequests.AsNoTracking().InScope(scope)
                .Where(l => studentIds.Contains(l.StudentUserId) && l.Status == LeaveRequestStatus.Approved && periodIds.Contains(l.PeriodId))
                .Select(l => new { l.StudentUserId, l.PeriodId, l.DateFrom, l.DateTo })
                .ToListAsync(cancellationToken))
            .ToLookup(l => (l.StudentUserId, l.PeriodId), l => (l.DateFrom, l.DateTo));

        // Kundalik statistikasi SQL'da agregatlanadi (talaba × davr bo'yicha bitta qator) — har yozuvni xotiraga olib
        // kelmaymiz; o'rtacha qiymat ComputeDiary bilan bir xil yaxlitlanadi (DiaryFromTotals).
        var diaries = (await db.DiaryEntries.AsNoTracking().InScope(scope)
                .Where(d => studentIds.Contains(d.StudentUserId) && periodIds.Contains(d.PeriodId))
                .GroupBy(d => new { d.StudentUserId, d.PeriodId })
                .Select(g => new
                {
                    g.Key.StudentUserId,
                    g.Key.PeriodId,
                    Count = g.Count(),
                    Scored = g.Count(d => d.Score != null),
                    Sum = g.Sum(d => d.Score ?? 0)
                })
                .ToListAsync(cancellationToken))
            .ToDictionary(
                d => (d.StudentUserId, d.PeriodId),
                d => StudentStatsCalculator.DiaryFromTotals(d.Count, d.Scored, d.Sum));

        var grades = await db.PracticeGrades.AsNoTracking().InScope(scope)
            .Where(g => studentIds.Contains(g.StudentUserId) && periodIds.Contains(g.PeriodId))
            .Select(g => new { g.StudentUserId, g.PeriodId, g.TutorPoints, g.ReferencePoints })
            .ToListAsync(cancellationToken);

        var gradeByKey = grades
            .GroupBy(g => (g.StudentUserId, g.PeriodId))
            .ToDictionary(g => g.Key, g => g.First());

        var rows = new List<GradingRow>(students.Count);
        foreach (var student in students)
        {
            var period = periods.ForGroup(student.GroupId);
            if (period is null)
                continue;

            var key = (student.UserId, period.Period.Id);
            var stats = StudentStatsCalculator.ComputeAttendance(
                period, attendance[student.UserId].ToList(), leaves[key].ToList(), today, localNow, student.HoursOn(today));
            var diary = diaries.GetValueOrDefault(key) ?? DiaryStats.Empty;
            var grade = gradeByKey.GetValueOrDefault(key);

            var result = GradeCalculator.Compute(
                stats.AttendancePct, diary.ScoredCount, diary.Avg, grade?.TutorPoints, grade?.ReferencePoints);

            rows.Add(new GradingRow(
                student.UserId,
                student.FullName,
                new GradingAttendance(result.AttendancePoints, stats.AttendancePct),
                new GradingReports(result.ReportPoints, diary.Avg),
                grade?.TutorPoints,
                grade?.ReferencePoints,
                new GradingRecommended(result.RecommendedTutorPoints, result.RecommendedReferencePoints),
                result.Total,
                result.Grade));
        }

        return rows;
    }
}
