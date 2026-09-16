using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Tutor.Common;
using Amaliyotchi.Domain.Grading;
using Amaliyotchi.Domain.Leave;
using Amaliyotchi.Domain.Practice;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Tutor.Students;

public enum StudentState
{
    Active = 1,
    RedFlag = 2,
    Suspicious = 3
}

/// <summary>Talaba holati qoidasi — ro'yxat (<c>GET /api/tutor/students</c>) va profil
/// (<c>GET /api/tutor/students/{id}</c>) bir xil hisoblasin.</summary>
public static class StudentStateRule
{
    /// <summary>Kamida shuncha shubhali kun bo'lsa "shubhali" holat.</summary>
    public const int SuspiciousMinCount = 1;

    /// <summary>Davomat &lt; 70% → <c>redFlag</c>; shubhali kunlar bor → <c>suspicious</c>; aks holda <c>active</c>.</summary>
    public static StudentState For(StudentStats stats)
        => stats.TotalDays > 0 && stats.AttendancePct < GradeThresholds.MinAttendancePct
            ? StudentState.RedFlag
            : stats.SuspiciousCount >= SuspiciousMinCount
                ? StudentState.Suspicious
                : StudentState.Active;
}

public sealed record TutorStudent(
    Guid Id,
    string Name,
    string HemisId,
    string Group,
    string? Company,
    double AttendancePct,
    int AttendedDays,
    int TotalDays,
    int DiaryCount,
    double DiaryAvg,
    StudentState State,
    int SuspiciousCount);

/// <summary><c>GET /api/tutor/students</c> — ko'lamdagi talabalar, faol davr bo'yicha davomat va kundalik ko'rsatkichlari.
/// Holat: davomat &lt; 70% → <c>redFlag</c>; shubhali kunlar bor → <c>suspicious</c>; aks holda <c>active</c>.</summary>
public sealed record GetTutorStudentsQuery : IRequest<IReadOnlyList<TutorStudent>>;

internal sealed class GetTutorStudentsQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver, IClock clock)
    : IRequestHandler<GetTutorStudentsQuery, IReadOnlyList<TutorStudent>>
{
    public async Task<IReadOnlyList<TutorStudent>> Handle(GetTutorStudentsQuery request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        var today = clock.LocalToday();
        var localNow = clock.LocalTime();

        var students = await db.LoadScopedStudentsAsync(scope, cancellationToken);
        var periods = await PeriodLookup.LoadAsync(db, today, cancellationToken);
        var periodIds = periods.PeriodIds;

        var attendance = (await db.DailyAttendances.AsNoTracking().InScope(scope)
                .Where(a => periodIds.Contains(a.PeriodId))
                .SelectSnapshot()
                .ToListAsync(cancellationToken))
            .ToLookup(a => a.StudentUserId);

        var leaves = (await db.LeaveRequests.AsNoTracking().InScope(scope)
                .Where(l => l.Status == LeaveRequestStatus.Approved && periodIds.Contains(l.PeriodId))
                .Select(l => new { l.StudentUserId, l.DateFrom, l.DateTo })
                .ToListAsync(cancellationToken))
            .ToLookup(l => l.StudentUserId, l => (l.DateFrom, l.DateTo));

        var diaries = (await db.DiaryEntries.AsNoTracking().InScope(scope)
                .Where(d => periodIds.Contains(d.PeriodId))
                .Select(d => new { d.StudentUserId, d.Score })
                .ToListAsync(cancellationToken))
            .ToLookup(d => d.StudentUserId, d => d.Score);

        var companies = (await db.PracticeApplications.AsNoTracking().InScope(scope)
                .Where(a => a.Status == ApplicationStatus.Approved && periodIds.Contains(a.PeriodId))
                .Select(a => new { a.StudentUserId, a.Company.Name })
                .ToListAsync(cancellationToken))
            .GroupBy(a => a.StudentUserId)
            .ToDictionary(g => g.Key, g => g.First().Name);

        var result = new List<TutorStudent>(students.Count);
        foreach (var student in students)
        {
            var period = periods.ForGroup(student.GroupId);
            var stats = StudentStatsCalculator.ComputeAttendance(
                period, attendance[student.UserId].ToList(), leaves[student.UserId].ToList(), today, localNow);
            var diary = StudentStatsCalculator.ComputeDiary(diaries[student.UserId].ToList());

            var state = StudentStateRule.For(stats);

            result.Add(new TutorStudent(
                student.UserId, student.FullName, student.HemisId, student.GroupName,
                companies.GetValueOrDefault(student.UserId),
                stats.AttendancePct, stats.AttendedDays, stats.TotalDays,
                diary.Count, diary.Avg, state, stats.SuspiciousCount));
        }

        return result;
    }
}
