using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Application.Features.Admin.Tutors;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Practice;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Dashboard;

/// <summary>Kontrakt v2 <c>AdminDashboard.stats</c> — faqat xom raqamlar (matn va foiz formati frontend'da).
/// "Bugun" — Toshkent kuni; kutilgan talabalar — bugun ish kuni bo'lgan faol davrlarga biriktirilgan guruhlardagi talabalar.</summary>
public sealed record DashboardStatsDto(
    int StudentsTotal,
    int StudentsLinked,
    int StudentsUnlinked,
    int Faculties,
    int Groups,
    int CompaniesActive,
    int ApplicationsPending,
    int ApplicationsOverdue,
    int ContractsApproved,
    int ContractsRevision,
    int ContractsRejected,
    int ContractsMissing,
    int ExpectedToday,
    int PresentToday,
    int LateToday,
    int AbsentToday,
    int ExcusedToday,
    int NoDiaryToday,
    int AttendanceTodayPct,
    int AttendanceYesterdayPct);

public sealed record FacultyAttendanceDto(
    Guid Id,
    string Name,
    string Code,
    int StudentCount,
    int ExpectedToday,
    int AttendedToday,
    int AttendancePct);

public sealed record TutorActivityDto(
    Guid Id,
    string Name,
    string? FacultyCode,
    IReadOnlyList<string> Groups,
    int StudentCount,
    int PendingCount,
    DateTimeOffset? OldestPendingAt,
    double? AvgDecisionHours,
    DateTimeOffset? LastActiveAt,
    TutorStatus Status);

public sealed record AdminDashboardDto(
    DateOnly Date,
    DashboardStatsDto Stats,
    IReadOnlyList<FacultyAttendanceDto> Faculties,
    IReadOnlyList<TutorActivityDto> Tutors,
    IReadOnlyList<AuditEntryDto> Audit);

/// <summary><c>GET /api/admin/dashboard</c>. Bo'sh bazada ham 200 — hamma qiymat 0 / bo'sh ro'yxat.</summary>
public sealed record GetAdminDashboardQuery : IRequest<AdminDashboardDto>;

internal sealed class GetAdminDashboardQueryHandler(IApplicationDbContext db, IClock clock)
    : IRequestHandler<GetAdminDashboardQuery, AdminDashboardDto>
{
    public async Task<AdminDashboardDto> Handle(GetAdminDashboardQuery request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var calendar = await PracticeCalendar.LoadAsync(db, clock, cancellationToken);
        var today = calendar.Today;

        var stats = await LoadStatsAsync(calendar, now, cancellationToken);
        var faculties = await LoadFacultiesAsync(calendar, cancellationToken);
        var tutors = await LoadTutorsAsync(now, cancellationToken);

        var audit = await db.AuditLogs
            .AsNoTracking()
            .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id)
            .SelectEntries(db)
            .Take(AdminThresholds.DashboardAuditCount)
            .ToListAsync(cancellationToken);

        return new AdminDashboardDto(today, stats, faculties, tutors, audit);
    }

    private async Task<DashboardStatsDto> LoadStatsAsync(PracticeCalendar calendar, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var today = calendar.Today;
        var yesterday = today.AddDays(-1);

        var students = await db.StudentProfiles
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Linked = g.Count(p => p.User.TelegramUserId != null)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var facultyCount = await db.Faculties.CountAsync(cancellationToken);
        var groupCount = await db.StudentGroups.CountAsync(cancellationToken);
        var companiesActive = await db.Companies.CountAsync(c => c.IsActive, cancellationToken);

        var overdueBefore = now - AdminThresholds.PendingApplicationLateAfter;
        var applications = await db.PracticeApplications
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Pending = g.Count(a => a.Status == ApplicationStatus.Submitted),
                Overdue = g.Count(a => a.Status == ApplicationStatus.Submitted && a.SubmittedAt < overdueBefore),
                Approved = g.Count(a => a.Status == ApplicationStatus.Approved || a.Status == ApplicationStatus.Completed),
                Revision = g.Count(a => a.Status == ApplicationStatus.RevisionNeeded),
                Rejected = g.Count(a => a.Status == ApplicationStatus.Rejected)
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Arizasi yo'q talabalar — har guruh o'zining sukut bo'yicha davri kesimida (davrlar soni kichik — davr bo'yicha so'rov).
        var contractsMissing = 0;
        foreach (var byPeriod in calendar.ByGroup.GroupBy(kv => kv.Value.PeriodId))
        {
            var periodId = byPeriod.Key;
            var groupIds = byPeriod.Select(kv => kv.Key).ToList();
            contractsMissing += await db.StudentProfiles
                .AsNoTracking()
                .CountAsync(p => groupIds.Contains(p.StudentGroupId)
                                 && !db.PracticeApplications.Any(a => a.StudentUserId == p.UserId && a.PeriodId == periodId),
                    cancellationToken);
        }

        var expectedGroups = calendar.GroupsExpectedToday.ToList();
        var expectedToday = expectedGroups.Count == 0
            ? 0
            : await db.StudentProfiles.AsNoTracking().CountAsync(p => expectedGroups.Contains(p.StudentGroupId), cancellationToken);

        var todayAttendance = await db.DailyAttendances
            .AsNoTracking()
            .Where(a => a.Date == today)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Present = g.Count(a => a.Status == AttendanceStatus.Present),
                Late = g.Count(a => a.Status == AttendanceStatus.Late),
                Excused = g.Count(a => a.Status == AttendanceStatus.Excused),
                Absent = g.Count(a => a.Status == AttendanceStatus.Absent)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var diariesToday = await db.DiaryEntries.AsNoTracking().CountAsync(d => d.Date == today, cancellationToken);

        var yesterdayGroups = calendar.GroupsExpectedYesterday.ToList();
        var expectedYesterday = yesterdayGroups.Count == 0
            ? 0
            : await db.StudentProfiles.AsNoTracking().CountAsync(p => yesterdayGroups.Contains(p.StudentGroupId), cancellationToken);
        var attendedYesterday = expectedYesterday == 0
            ? 0
            : await db.DailyAttendances.AsNoTracking().CountAsync(
                a => a.Date == yesterday && (a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late),
                cancellationToken);

        var present = todayAttendance?.Present ?? 0;
        var late = todayAttendance?.Late ?? 0;
        var excused = todayAttendance?.Excused ?? 0;
        var attended = present + late;
        var absent = Math.Max(todayAttendance?.Absent ?? 0, expectedToday - attended - excused);

        return new DashboardStatsDto(
            StudentsTotal: students?.Total ?? 0,
            StudentsLinked: students?.Linked ?? 0,
            StudentsUnlinked: (students?.Total ?? 0) - (students?.Linked ?? 0),
            Faculties: facultyCount,
            Groups: groupCount,
            CompaniesActive: companiesActive,
            ApplicationsPending: applications?.Pending ?? 0,
            ApplicationsOverdue: applications?.Overdue ?? 0,
            ContractsApproved: applications?.Approved ?? 0,
            ContractsRevision: applications?.Revision ?? 0,
            ContractsRejected: applications?.Rejected ?? 0,
            ContractsMissing: contractsMissing,
            ExpectedToday: expectedToday,
            PresentToday: present,
            LateToday: late,
            AbsentToday: Math.Max(0, absent),
            ExcusedToday: excused,
            NoDiaryToday: Math.Max(0, attended - diariesToday),
            AttendanceTodayPct: PracticeCalendar.AttendancePct(attended, expectedToday, 0),
            AttendanceYesterdayPct: PracticeCalendar.AttendancePct(attendedYesterday, expectedYesterday, 0));
    }

    private async Task<List<FacultyAttendanceDto>> LoadFacultiesAsync(PracticeCalendar calendar, CancellationToken cancellationToken)
    {
        var faculties = await db.Faculties
            .AsNoTracking()
            .OrderBy(f => f.Name)
            .Select(f => new { f.Id, f.Name, f.Code })
            .ToListAsync(cancellationToken);

        if (faculties.Count == 0)
            return [];

        var studentsByFaculty = await (from p in db.StudentProfiles.AsNoTracking()
                                       join g in db.StudentGroups on p.StudentGroupId equals g.Id
                                       join d in db.Directions on g.DirectionId equals d.Id
                                       join dept in db.Departments on d.DepartmentId equals dept.Id
                                       group p by dept.FacultyId into grp
                                       select new { FacultyId = grp.Key, Count = grp.Count() })
            .ToDictionaryAsync(x => x.FacultyId, x => x.Count, cancellationToken);

        var expectedGroups = calendar.GroupsExpectedToday.ToList();
        var today = calendar.Today;
        var expectedByFaculty = new Dictionary<Guid, int>();
        var attendedByFaculty = new Dictionary<Guid, int>();

        if (expectedGroups.Count > 0)
        {
            expectedByFaculty = await (from p in db.StudentProfiles.AsNoTracking()
                                       join g in db.StudentGroups on p.StudentGroupId equals g.Id
                                       join d in db.Directions on g.DirectionId equals d.Id
                                       join dept in db.Departments on d.DepartmentId equals dept.Id
                                       where expectedGroups.Contains(p.StudentGroupId)
                                       group p by dept.FacultyId into grp
                                       select new { FacultyId = grp.Key, Count = grp.Count() })
                .ToDictionaryAsync(x => x.FacultyId, x => x.Count, cancellationToken);

            attendedByFaculty = await (from a in db.DailyAttendances.AsNoTracking()
                                       join p in db.StudentProfiles on a.StudentUserId equals p.UserId
                                       join g in db.StudentGroups on p.StudentGroupId equals g.Id
                                       join d in db.Directions on g.DirectionId equals d.Id
                                       join dept in db.Departments on d.DepartmentId equals dept.Id
                                       where a.Date == today
                                             && (a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late)
                                             && expectedGroups.Contains(p.StudentGroupId)
                                       group a by dept.FacultyId into grp
                                       select new { FacultyId = grp.Key, Count = grp.Count() })
                .ToDictionaryAsync(x => x.FacultyId, x => x.Count, cancellationToken);
        }

        return faculties.Select(f =>
        {
            var expected = expectedByFaculty.GetValueOrDefault(f.Id);
            var attended = attendedByFaculty.GetValueOrDefault(f.Id);
            return new FacultyAttendanceDto(
                f.Id, f.Name, f.Code, studentsByFaculty.GetValueOrDefault(f.Id),
                expected, attended, PracticeCalendar.AttendancePct(attended, expected, 0));
        }).ToList();
    }

    private async Task<List<TutorActivityDto>> LoadTutorsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var tutors = await db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Tutor && u.IsActive)
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.FullName, u.LastLoginAt })
            .ToListAsync(cancellationToken);

        if (tutors.Count == 0)
            return [];

        var ids = tutors.Select(t => t.Id).ToList();
        var stats = await TutorStatsLoader.LoadAsync(db, ids, now, cancellationToken);
        // Tyutor bir nechta fakultetga biriktirilishi mumkin — kodlar vergul bilan ("AT, IM"), nom bo'yicha tartib.
        var faculties = await TutorFacultyQueries.LoadRefsAsync(db, ids, cancellationToken);

        return tutors
            .Select(t =>
            {
                var s = stats.GetValueOrDefault(t.Id) ?? TutorStats.Empty;
                var facultyCode = faculties.TryGetValue(t.Id, out var refs)
                    ? string.Join(", ", refs.Select(f => f.Code))
                    : null;
                return new TutorActivityDto(
                    t.Id, t.FullName, facultyCode, s.Groups, s.StudentCount, s.PendingCount, s.OldestPendingAt,
                    s.AvgDecisionHours, GetTutorsQueryHandler.Latest(t.LastLoginAt, s.LastAuditAt), s.Status(now));
            })
            .OrderByDescending(t => t.Status == TutorStatus.Late)
            .ThenByDescending(t => t.PendingCount)
            .ThenBy(t => t.Name, StringComparer.Ordinal)
            .ToList();
    }
}
