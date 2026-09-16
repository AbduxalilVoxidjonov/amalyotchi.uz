using System.Globalization;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Application.Features.Tutor.Common;
using Amaliyotchi.Application.Features.Tutor.Students;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Leave;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Companies;

/// <summary>Korxonada bir amaliyot davri kesimi: shu davrda shu korxonaga biriktirilgan talabalar soni.</summary>
public sealed record CompanyPeriod(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, int Students);

/// <summary>Korxona tafsiloti (admin va tyutor uchun bir xil shakl).
/// <paramref name="Students"/> — so'rovchi ko'lamidagi biriktirilgan talabalar (admin uchun = <paramref name="TotalStudents"/>),
/// <paramref name="TotalStudents"/> — butun tizim bo'yicha (STIR nazorati shu songa tayanadi).</summary>
public sealed record CompanyDetail(
    Guid Id,
    string Name,
    string Tin,
    string Activity,
    string Address,
    double Lat,
    double Lng,
    int RadiusM,
    string SupervisorName,
    string SupervisorPhone,
    string? MentorName,
    string? MentorPhone,
    bool IsActive,
    int Students,
    int TotalStudents,
    int SuspiciousDays,
    int MaxStudents,
    bool OverLimit,
    CompanyFlag? Flag,
    IReadOnlyList<CompanyPeriod> Periods);

/// <summary>Korxonaga ariza bergan talaba: profil, tyutor, ariza holati va faol davr ko'rsatkichlari.
/// <paramref name="State"/> — <c>GET /api/tutor/students</c> dagi qoida bilan bir xil.</summary>
public sealed record CompanyStudent(
    Guid StudentId,
    string Name,
    string HemisId,
    string Group,
    int Course,
    string Faculty,
    string? TutorName,
    ApplicationStatus ApplicationStatus,
    string? PeriodName,
    double AttendancePct,
    int AttendedDays,
    int TotalDays,
    int DiaryCount,
    StudentState State,
    int SuspiciousCount);

/// <summary>Korxona bayrog'ini bitta joyda hisoblaydi.
/// Ustuvorlik: <c>suspicious</c> → <c>tooManyStudents</c> → <c>largeRadius</c> → <c>null</c>.</summary>
internal static class CompanyFlags
{
    public static CompanyFlag? Resolve(int suspiciousDays, int radiusM, bool overLimit)
        => suspiciousDays >= AdminThresholds.SuspiciousCompanyEvents ? CompanyFlag.Suspicious
            : overLimit ? CompanyFlag.TooManyStudents
            : radiusM > AdminThresholds.LargeRadiusM ? CompanyFlag.LargeRadius
            : null;
}

/// <summary>Korxona so'rovlari uchun umumiy yuklovchilar — admin va tyutor bir xil mantiqdan foydalanadi,
/// farqi faqat <see cref="DataScope"/> da.</summary>
internal static class CompanyQueries
{
    /// <summary><c>maxStudentsPerCompany</c> sozlamasi; baza bo'sh bo'lsa ta'rifdagi standart qiymat.</summary>
    public static async Task<int> LoadMaxStudentsAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var definition = SettingKeys.Get(SettingKeys.MaxStudentsPerCompany);
        var raw = await db.AppSettings
            .AsNoTracking()
            .Where(s => s.Key == SettingKeys.MaxStudentsPerCompany)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return int.TryParse(raw ?? definition.DefaultValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : int.Parse(definition.DefaultValue, CultureInfo.InvariantCulture);
    }

    /// <summary>Korxona tafsiloti. <paramref name="requireScopedStudents"/> — tyutor uchun: ko'lamda
    /// bitta ham biriktirilgan talaba bo'lmasa korxona "yo'q" hisoblanadi (404).</summary>
    public static async Task<CompanyDetail> LoadDetailAsync(
        IApplicationDbContext db,
        DataScope scope,
        Guid companyId,
        bool requireScopedStudents,
        CancellationToken cancellationToken)
    {
        var company = await db.Companies
            .AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Tin,
                c.Activity,
                c.Address,
                c.Location.Latitude,
                c.Location.Longitude,
                c.RadiusM,
                c.SupervisorName,
                c.SupervisorPhone,
                c.MentorName,
                c.MentorPhone,
                c.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Korxona", companyId);

        // Ko'lamdagi tasdiqlangan arizalar (davr ma'lumoti bilan) — "davrlar kesimi" ham shundan yig'iladi.
        var approved = await (from a in db.PracticeApplications.AsNoTracking().InScope(scope)
                              where a.CompanyId == companyId && a.Status == ApplicationStatus.Approved
                              join p in db.PracticePeriods on a.PeriodId equals p.Id
                              select new { p.Id, p.Name, p.StartDate, p.EndDate, a.StudentUserId })
            .ToListAsync(cancellationToken);

        var students = approved.Select(a => a.StudentUserId).Distinct().Count();
        if (requireScopedStudents && students == 0)
            throw new NotFoundException("Korxona", companyId);

        var totalStudents = scope.IsUnrestricted
            ? students
            : await db.PracticeApplications
                .AsNoTracking()
                .Where(a => a.CompanyId == companyId && a.Status == ApplicationStatus.Approved)
                .Select(a => a.StudentUserId)
                .Distinct()
                .CountAsync(cancellationToken);

        var suspiciousDays = await db.DailyAttendances
            .AsNoTracking()
            .InScope(scope)
            .CountAsync(d => d.IsSuspicious && db.PracticeApplications.Any(a =>
                a.CompanyId == companyId && a.Status == ApplicationStatus.Approved
                && a.StudentUserId == d.StudentUserId && a.PeriodId == d.PeriodId), cancellationToken);

        var periods = approved
            .GroupBy(a => new { a.Id, a.Name, a.StartDate, a.EndDate })
            .Select(g => new CompanyPeriod(
                g.Key.Id, g.Key.Name, g.Key.StartDate, g.Key.EndDate,
                g.Select(a => a.StudentUserId).Distinct().Count()))
            .OrderByDescending(p => p.StartDate)
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .ToList();

        var maxStudents = await LoadMaxStudentsAsync(db, cancellationToken);
        var overLimit = totalStudents > maxStudents;

        return new CompanyDetail(
            company.Id, company.Name, company.Tin, company.Activity, company.Address,
            company.Latitude, company.Longitude, company.RadiusM,
            company.SupervisorName, company.SupervisorPhone, company.MentorName, company.MentorPhone,
            company.IsActive, students, totalStudents, suspiciousDays, maxStudents, overLimit,
            CompanyFlags.Resolve(suspiciousDays, company.RadiusM, overLimit),
            periods);
    }

    /// <summary>Korxonaga ariza bergan (qoralamadan boshqa) ko'lamdagi talabalar, FISH bo'yicha.
    /// Barcha ko'rsatkichlar bir nechta ommaviy so'rov bilan yuklanadi — talaba bo'yicha aylanma so'rov yo'q.</summary>
    public static async Task<IReadOnlyList<CompanyStudent>> LoadStudentsAsync(
        IApplicationDbContext db,
        DataScope scope,
        Guid companyId,
        bool requireScopedStudents,
        DateOnly today,
        TimeOnly localNow,
        CancellationToken cancellationToken)
    {
        var exists = await db.Companies.AsNoTracking().AnyAsync(c => c.Id == companyId, cancellationToken);
        if (!exists)
            throw new NotFoundException("Korxona", companyId);

        if (requireScopedStudents)
        {
            var hasScoped = await db.PracticeApplications.AsNoTracking().InScope(scope)
                .AnyAsync(a => a.CompanyId == companyId && a.Status == ApplicationStatus.Approved, cancellationToken);
            if (!hasScoped)
                throw new NotFoundException("Korxona", companyId);
        }

        var applications = await db.PracticeApplications
            .AsNoTracking()
            .InScope(scope)
            .Where(a => a.CompanyId == companyId && a.Status != ApplicationStatus.Draft)
            .Select(a => new { a.StudentUserId, a.PeriodId, a.Status, a.SubmittedAt })
            .ToListAsync(cancellationToken);

        if (applications.Count == 0)
            return [];

        // Talaba bir korxonaga bir necha marta ariza bergan bo'lishi mumkin: tasdiqlangani ustun, aks holda eng so'nggisi.
        var byStudent = applications
            .GroupBy(a => a.StudentUserId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(a => a.Status == ApplicationStatus.Approved ? 0 : 1)
                      .ThenByDescending(a => a.SubmittedAt)
                      .First());

        var studentIds = byStudent.Keys.ToList();

        var profiles = await (from p in db.StudentProfiles.AsNoTracking().InScope(scope)
                              where studentIds.Contains(p.UserId)
                              join g in db.StudentGroups on p.StudentGroupId equals g.Id
                              join d in db.Directions on g.DirectionId equals d.Id
                              join dept in db.Departments on d.DepartmentId equals dept.Id
                              join f in db.Faculties on dept.FacultyId equals f.Id
                              orderby p.User.FullName, p.HemisId
                              select new
                              {
                                  p.UserId,
                                  p.User.FullName,
                                  p.HemisId,
                                  GroupId = g.Id,
                                  GroupName = g.Name,
                                  g.Course,
                                  Faculty = f.Name
                              })
            .ToListAsync(cancellationToken);

        if (profiles.Count == 0)
            return [];

        var groupIds = profiles.Select(p => p.GroupId).Distinct().ToList();
        var tutorByGroup = (await (from a in db.TutorAssignments.AsNoTracking()
                                   where a.IsActive && groupIds.Contains(a.StudentGroupId)
                                   join u in db.Users on a.TutorUserId equals u.Id
                                   select new { a.StudentGroupId, u.FullName })
                .ToListAsync(cancellationToken))
            .GroupBy(t => t.StudentGroupId)
            .ToDictionary(g => g.Key, g => g.Select(t => t.FullName).OrderBy(n => n, StringComparer.Ordinal).First());

        var applicationPeriodIds = byStudent.Values.Select(a => a.PeriodId).Distinct().ToList();
        var periodNames = await db.PracticePeriods
            .AsNoTracking()
            .Where(p => applicationPeriodIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name })
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        var periods = await PeriodLookup.LoadAsync(db, today, cancellationToken);
        var activePeriodIds = periods.PeriodIds;

        var attendance = (await db.DailyAttendances.AsNoTracking().InScope(scope)
                .Where(a => studentIds.Contains(a.StudentUserId) && activePeriodIds.Contains(a.PeriodId))
                .SelectSnapshot()
                .ToListAsync(cancellationToken))
            .ToLookup(a => a.StudentUserId);

        var leaves = (await db.LeaveRequests.AsNoTracking().InScope(scope)
                .Where(l => l.Status == LeaveRequestStatus.Approved
                    && studentIds.Contains(l.StudentUserId) && activePeriodIds.Contains(l.PeriodId))
                .Select(l => new { l.StudentUserId, l.DateFrom, l.DateTo })
                .ToListAsync(cancellationToken))
            .ToLookup(l => l.StudentUserId, l => (l.DateFrom, l.DateTo));

        var diaries = (await db.DiaryEntries.AsNoTracking().InScope(scope)
                .Where(d => studentIds.Contains(d.StudentUserId) && activePeriodIds.Contains(d.PeriodId))
                .Select(d => new { d.StudentUserId, d.Score })
                .ToListAsync(cancellationToken))
            .ToLookup(d => d.StudentUserId, d => d.Score);

        var result = new List<CompanyStudent>(profiles.Count);
        foreach (var profile in profiles)
        {
            var application = byStudent[profile.UserId];
            var stats = StudentStatsCalculator.ComputeAttendance(
                periods.ForGroup(profile.GroupId),
                attendance[profile.UserId].ToList(),
                leaves[profile.UserId].ToList(),
                today,
                localNow);
            var diary = StudentStatsCalculator.ComputeDiary(diaries[profile.UserId].ToList());

            result.Add(new CompanyStudent(
                profile.UserId, profile.FullName, profile.HemisId, profile.GroupName, profile.Course, profile.Faculty,
                tutorByGroup.GetValueOrDefault(profile.GroupId),
                application.Status,
                periodNames.GetValueOrDefault(application.PeriodId),
                stats.AttendancePct, stats.AttendedDays, stats.TotalDays, diary.Count,
                StudentStateRule.For(stats), stats.SuspiciousCount));
        }

        return result;
    }
}
