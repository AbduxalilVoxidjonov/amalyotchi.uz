using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Practice;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Application.Features.Admin.Companies;
using Amaliyotchi.Application.Features.Admin.Faculties;
using Amaliyotchi.Application.Features.Admin.Groups;
using Amaliyotchi.Application.Features.Admin.Students;
using Amaliyotchi.Application.Features.Admin.Tutors;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Practice;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Dashboard;

/// <summary>Kontrakt v2 <c>AdminDashboard.stats</c> — faqat xom raqamlar (matn va foiz formati frontend'da).
/// Har ko'rsatkich loyiha qoidalari bilan bir manbadan:
/// <list type="bullet">
/// <item><b>Talabalar/fakultetlar/guruhlar</b> — ro'yxat sahifalari (va sidebar nav) manbalari: <c>StudentsTotal</c> ==
/// nav <c>students</c>, <c>Faculties</c> == nav <c>faculties</c>. <c>StudentsLinked</c> — shu talabalardan Telegram
/// hisobi bog'langanlar, <c>StudentsUnlinked</c> = jami − bog'langan.</item>
/// <item><b>Korxonalar</b> — <c>CompaniesActive</c>: katalogda faol (<c>isActive</c>) korxonalar (talabaga bog'liq emas);
/// <c>CompaniesWithInterns</c>: "aktiv korxona" qoidasi (<c>ActiveCompanyQueries</c>) bo'yicha bugun kamida bitta
/// amaliyotchisi bor korxonalar.</item>
/// <item><b>Arizalar va shartnomalar</b> — faqat ochiq (yopilmagan, tugamagan: davom etayotgan yoki kelgusi) davrlar
/// (<c>OpenPeriodQueries</c>). <c>Transferred</c> hech qayerda sanalmaydi.</item>
/// <item><b>Bugun/kecha davomati</b> — faqat shu kuni davom etayotgan (yopilmagan, sanalar ichida) davr ish kuni bo'lgan
/// guruhlar talabalari (<c>DailyAttendanceTally</c>); yopilgan davr talabalari "kelmadi" bo'lib sanalmaydi. Foiz =
/// keldi / (kutilgan − sababli). <c>OngoingPeriods</c> — bugun davom etayotgan davrlar soni (0 bo'lsa frontend
/// "davom etayotgan amaliyot yo'q" holatini ko'rsatadi).</item>
/// </list></summary>
public sealed record DashboardStatsDto(
    int StudentsTotal,
    int StudentsLinked,
    int StudentsUnlinked,
    int Faculties,
    int Groups,
    int CompaniesActive,
    int CompaniesWithInterns,
    int ApplicationsPending,
    int ApplicationsOverdue,
    int ContractsApproved,
    int ContractsRevision,
    int ContractsRejected,
    int ContractsMissing,
    int OngoingPeriods,
    int ExpectedToday,
    int PresentToday,
    int LateToday,
    int AbsentToday,
    int ExcusedToday,
    int NoDiaryToday,
    int AttendanceTodayPct,
    int ExpectedYesterday,
    int AttendanceYesterdayPct);

/// <summary>Fakultet kesimida bugungi davomat — fakultetlar ro'yxati bilan bir hisob (<c>DailyAttendanceTally</c>).
/// <paramref name="StudentCount"/> — fakultetdagi barcha talabalar; <paramref name="ExpectedToday"/> — bugun kutilganlar
/// (0 bo'lsa bugun bu fakultetda davom etayotgan amaliyot ish kuni yo'q); <paramref name="AttendancePct"/> =
/// keldi / (kutilgan − sababli).</summary>
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

        // Bugungi davomat bir marta hisoblanadi — umumiy statistika ham, fakultet kesimi ham shundan (yig'indi mos keladi).
        var todayByFaculty = await DailyAttendanceTally.LoadByFacultyAsync(
            db, calendar, today, calendar.GroupsExpectedToday, facultyIds: null, withDiary: true, cancellationToken);

        var stats = await LoadStatsAsync(calendar, todayByFaculty, now, cancellationToken);
        var faculties = await LoadFacultiesAsync(todayByFaculty, cancellationToken);
        var tutors = await LoadTutorsAsync(now, cancellationToken);

        var audit = await db.AuditLogs
            .AsNoTracking()
            .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id)
            .SelectEntries(db)
            .Take(AdminThresholds.DashboardAuditCount)
            .ToListAsync(cancellationToken);

        return new AdminDashboardDto(today, stats, faculties, tutors, audit);
    }

    private async Task<DashboardStatsDto> LoadStatsAsync(
        PracticeCalendar calendar,
        IReadOnlyDictionary<Guid, DayTally> todayByFaculty,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var today = calendar.Today;

        // Ro'yxat sahifalari (va sidebar nav) bilan bir manba — sonlar farq qilmaydi.
        var students = await GetAdminStudentsQueryHandler.Source(db)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Linked = g.Count(x => x.User.TelegramUserId != null)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var facultyCount = await GetFacultiesQueryHandler.Source(db).CountAsync(cancellationToken);
        var groupCount = await GroupRowQueries.Source(db).CountAsync(cancellationToken);
        var companiesActive = await GetCompaniesQueryHandler.Source(db).CountAsync(c => c.IsActive, cancellationToken);
        var companiesWithInterns = (await CompanyQueries.LoadActivePlacementsAsync(db, scope: null, companyIds: null, today, cancellationToken))
            .Select(p => p.CompanyId)
            .Distinct()
            .Count();

        // Arizalar/shartnomalar — faqat ochiq davrlar; Transferred (tarix) hech bir guruhga kirmaydi.
        var overdueBefore = now - AdminThresholds.PendingApplicationLateAfter;
        var applications = await db.OpenPeriodApplications(today)
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

        // Shartnomasi yo'q: ochiq davrga biriktirilgan guruhdagi talaba, shu davrda ko'rib chiqiladigan arizasi
        // (Submitted/RevisionNeeded/Approved/Rejected/Completed) yo'q — (talaba, davr) juftligi bo'yicha.
        var openPeriods = db.OpenPeriods(today);
        var contractsMissing = await (from s in GetAdminStudentsQueryHandler.Source(db)
                                      from p in openPeriods
                                      where p.Groups.Any(g => g.StudentGroupId == s.Profile.StudentGroupId)
                                            && !db.PracticeApplications.Any(a =>
                                                a.StudentUserId == s.Profile.UserId && a.PeriodId == p.Id
                                                && a.Status != ApplicationStatus.Transferred && a.Status != ApplicationStatus.Draft)
                                      select s.Profile.UserId)
            .CountAsync(cancellationToken);

        var ongoingPeriods = await openPeriods.CountAsync(p => p.StartDate <= today, cancellationToken);

        var todayTally = todayByFaculty.Values.Aggregate(DayTally.Empty, (acc, t) => acc.Add(t));
        var yesterdayTally = (await DailyAttendanceTally.LoadByFacultyAsync(
                db, calendar, today.AddDays(-1), calendar.GroupsExpectedYesterday, facultyIds: null, withDiary: false,
                cancellationToken))
            .Values.Aggregate(DayTally.Empty, (acc, t) => acc.Add(t));

        var total = students?.Total ?? 0;
        var linked = students?.Linked ?? 0;

        return new DashboardStatsDto(
            StudentsTotal: total,
            StudentsLinked: linked,
            StudentsUnlinked: total - linked,
            Faculties: facultyCount,
            Groups: groupCount,
            CompaniesActive: companiesActive,
            CompaniesWithInterns: companiesWithInterns,
            ApplicationsPending: applications?.Pending ?? 0,
            ApplicationsOverdue: applications?.Overdue ?? 0,
            ContractsApproved: applications?.Approved ?? 0,
            ContractsRevision: applications?.Revision ?? 0,
            ContractsRejected: applications?.Rejected ?? 0,
            ContractsMissing: contractsMissing,
            OngoingPeriods: ongoingPeriods,
            ExpectedToday: todayTally.Expected,
            PresentToday: todayTally.Present,
            LateToday: todayTally.Late,
            AbsentToday: todayTally.Absent,
            ExcusedToday: todayTally.Excused,
            NoDiaryToday: todayTally.NoDiary,
            AttendanceTodayPct: todayTally.Pct,
            ExpectedYesterday: yesterdayTally.Expected,
            AttendanceYesterdayPct: yesterdayTally.Pct);
    }

    private async Task<List<FacultyAttendanceDto>> LoadFacultiesAsync(
        IReadOnlyDictionary<Guid, DayTally> todayByFaculty, CancellationToken cancellationToken)
    {
        var faculties = await GetFacultiesQueryHandler.Source(db)
            .OrderBy(f => f.Name)
            .Select(f => new { f.Id, f.Name, f.Code })
            .ToListAsync(cancellationToken);

        if (faculties.Count == 0)
            return [];

        var studentsByFaculty = await GetAdminStudentsQueryHandler.Source(db)
            .GroupBy(x => x.Faculty.Id)
            .Select(g => new { FacultyId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.FacultyId, x => x.Count, cancellationToken);

        return faculties.Select(f =>
        {
            var tally = todayByFaculty.GetValueOrDefault(f.Id) ?? DayTally.Empty;
            return new FacultyAttendanceDto(
                f.Id, f.Name, f.Code, studentsByFaculty.GetValueOrDefault(f.Id),
                tally.Expected, tally.Attended, tally.Pct);
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
