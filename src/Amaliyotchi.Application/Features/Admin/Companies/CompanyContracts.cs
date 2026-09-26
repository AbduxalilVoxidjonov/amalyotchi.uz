using System.Globalization;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Practice;
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

/// <summary>Korxonada hozir davom etayotgan ochiq davr kesimi: shu davrda shu korxonada aktiv amaliyot o'tayotgan
/// talabalar soni (§4.7; 0 bo'lsa davr ro'yxatga kirmaydi).</summary>
public sealed record CompanyPeriod(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, int Students);

/// <summary>Korxona tafsiloti (admin va tyutor uchun bir xil shakl).
/// <paramref name="Students"/> — so'rovchi ko'lamidagi AKTIV talabalar (§4.7; admin uchun = <paramref name="TotalStudents"/>),
/// <paramref name="TotalStudents"/> — butun tizim bo'yicha aktiv talabalar (STIR nazorati shu songa tayanadi).</summary>
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

/// <summary>Korxonada hozir amaliyot o'tayotgan talaba: profil, tyutor, ariza holati (doim approved) va aktiv davr ko'rsatkichlari.
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

/// <summary>Aktiv joylashuv (§4.7): talaba HOZIR shu korxonada, shu (ochiq, davom etayotgan) davrda.</summary>
internal sealed record ActivePlacement(Guid StudentUserId, Guid CompanyId, Guid PeriodId, string PeriodName);

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

    /// <summary>Aktiv joylashuvlar (§4.7): talaba HOZIR amaliyot o'tayotgan korxona va davr — har talabaga ko'pi bilan
    /// bitta (<see cref="ActiveCompanyQueries.LoadActiveCompaniesAsync"/> bilan aynan bir xil tanlov, shu bois talabalar
    /// ro'yxatidagi <c>company</c> va korxona sahifasidagi sonlar zid kelmaydi).
    /// <paramref name="scope"/> <c>null</c> — butun tizim; <paramref name="companyIds"/> <c>null</c> — barcha korxonalar.
    /// Ikki so'rov: nomzod talabalar → ularning aktiv korxonasi (talabaning boshqa korxonadagi yangiroq aktiv arizasi
    /// bo'lsa, u shu korxonada hisoblanmaydi).</summary>
    public static async Task<IReadOnlyList<ActivePlacement>> LoadActivePlacementsAsync(
        IApplicationDbContext db,
        DataScope? scope,
        IReadOnlyCollection<Guid>? companyIds,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var candidates = db.ActiveApplications(today).AsNoTracking();
        if (scope is not null)
            candidates = candidates.InScope(scope);
        if (companyIds is not null)
        {
            var ids = companyIds.ToArray();
            candidates = candidates.Where(a => ids.Contains(a.CompanyId));
        }

        var studentIds = await candidates.Select(a => a.StudentUserId).Distinct().ToListAsync(cancellationToken);
        if (studentIds.Count == 0)
            return [];

        var active = await db.LoadActiveCompaniesAsync(studentIds, today, cancellationToken);
        return active
            .Where(kv => companyIds is null || companyIds.Contains(kv.Value.Id))
            .Select(kv => new ActivePlacement(kv.Key, kv.Value.Id, kv.Value.PeriodId, kv.Value.PeriodName))
            .ToList();
    }

    /// <summary>Korxona bo'yicha shubhali davomat kunlari — faqat aktiv joylashuvning (talaba, aktiv davr) juftligi
    /// bo'yicha. Joylashuvlar allaqachon ko'lamga kesilgan bo'lishi kerak.</summary>
    public static async Task<Dictionary<Guid, int>> CountSuspiciousDaysAsync(
        IApplicationDbContext db, IReadOnlyCollection<ActivePlacement> placements, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, int>();
        if (placements.Count == 0)
            return result;

        var companyByPair = placements.ToDictionary(p => (p.StudentUserId, p.PeriodId), p => p.CompanyId);
        var studentIds = placements.Select(p => p.StudentUserId).Distinct().ToArray();
        var periodIds = placements.Select(p => p.PeriodId).Distinct().ToArray();

        var rows = await db.DailyAttendances
            .AsNoTracking()
            .Where(d => d.IsSuspicious && studentIds.Contains(d.StudentUserId) && periodIds.Contains(d.PeriodId))
            .Select(d => new { d.StudentUserId, d.PeriodId })
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            if (companyByPair.TryGetValue((row.StudentUserId, row.PeriodId), out var companyId))
                result[companyId] = result.GetValueOrDefault(companyId) + 1;
        }

        return result;
    }

    /// <summary>Tyutor ko'lami darvozasi: ko'lamda shu korxonaga (istalgan davrda) tasdiqlangan arizasi bor talaba
    /// bo'lmasa korxona "yo'q" hisoblanadi (404, mavjudligi oshkor qilinmaydi). Tarix bo'yicha — sonlar esa aktiv
    /// qoida bo'yicha (0 bo'lishi mumkin).</summary>
    private static async Task EnsureScopedAccessAsync(
        IApplicationDbContext db, DataScope scope, Guid companyId, CancellationToken cancellationToken)
    {
        var hasScoped = await db.PracticeApplications.AsNoTracking().InScope(scope)
            .AnyAsync(a => a.CompanyId == companyId && a.Status == ApplicationStatus.Approved, cancellationToken);
        if (!hasScoped)
            throw new NotFoundException("Korxona", companyId);
    }

    /// <summary>Korxona tafsiloti. Talabalar sonlari, shubhali kunlar va <c>periods</c> — faqat AKTIV joylashuvlar
    /// (§4.7): hozir davom etayotgan ochiq davr(lar), har birida kamida bitta aktiv talaba.
    /// <paramref name="requireScopedStudents"/> — tyutor uchun ko'lam darvozasi (<see cref="EnsureScopedAccessAsync"/>).</summary>
    public static async Task<CompanyDetail> LoadDetailAsync(
        IApplicationDbContext db,
        DataScope scope,
        Guid companyId,
        bool requireScopedStudents,
        DateOnly today,
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

        if (requireScopedStudents)
            await EnsureScopedAccessAsync(db, scope, companyId, cancellationToken);

        Guid[] companyIds = [companyId];
        var all = await LoadActivePlacementsAsync(db, scope: null, companyIds, today, cancellationToken);
        var scoped = scope.IsUnrestricted ? all : all.Where(p => scope.Includes(p.StudentUserId)).ToList();

        var students = scoped.Count;
        var totalStudents = all.Count;
        var suspiciousDays = (await CountSuspiciousDaysAsync(db, scoped, cancellationToken)).GetValueOrDefault(companyId);

        var periodIds = scoped.Select(p => p.PeriodId).Distinct().ToArray();
        var periodInfo = periodIds.Length == 0
            ? []
            : await db.PracticePeriods
                .AsNoTracking()
                .Where(p => periodIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name, p.StartDate, p.EndDate })
                .ToListAsync(cancellationToken);

        var countByPeriod = scoped.GroupBy(p => p.PeriodId).ToDictionary(g => g.Key, g => g.Count());
        var periods = periodInfo
            .Select(p => new CompanyPeriod(p.Id, p.Name, p.StartDate, p.EndDate, countByPeriod.GetValueOrDefault(p.Id)))
            .Where(p => p.Students > 0)
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

    /// <summary>Korxonada HOZIR amaliyot o'tayotgan (aktiv korxonasi = shu korxona, §4.7) ko'lamdagi talabalar,
    /// FISH bo'yicha. <c>applicationStatus</c> doim <c>approved</c>, <c>periodName</c> va ko'rsatkichlar — aktiv davr
    /// bo'yicha. Barcha ko'rsatkichlar bir nechta ommaviy so'rov bilan yuklanadi — talaba bo'yicha aylanma so'rov yo'q.</summary>
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
            await EnsureScopedAccessAsync(db, scope, companyId, cancellationToken);

        Guid[] companyIds = [companyId];
        var placements = await LoadActivePlacementsAsync(db, scope, companyIds, today, cancellationToken);
        if (placements.Count == 0)
            return [];

        var byStudent = placements.ToDictionary(p => p.StudentUserId);
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

        // Statistika — talabaning aktiv davri bo'yicha (ko'rsatilgan <c>periodName</c> bilan bir xil).
        var periods = await PeriodLookup.LoadAsync(db, today, cancellationToken);
        var activePeriodIds = placements.Select(p => p.PeriodId).Distinct().ToList();

        var attendance = (await db.DailyAttendances.AsNoTracking().InScope(scope)
                .Where(a => studentIds.Contains(a.StudentUserId) && activePeriodIds.Contains(a.PeriodId))
                .SelectSnapshot()
                .ToListAsync(cancellationToken))
            .ToLookup(a => (a.StudentUserId, a.PeriodId));

        var leaves = (await db.LeaveRequests.AsNoTracking().InScope(scope)
                .Where(l => l.Status == LeaveRequestStatus.Approved
                    && studentIds.Contains(l.StudentUserId) && activePeriodIds.Contains(l.PeriodId))
                .Select(l => new { l.StudentUserId, l.PeriodId, l.DateFrom, l.DateTo })
                .ToListAsync(cancellationToken))
            .ToLookup(l => (l.StudentUserId, l.PeriodId), l => (l.DateFrom, l.DateTo));

        var diaries = (await db.DiaryEntries.AsNoTracking().InScope(scope)
                .Where(d => studentIds.Contains(d.StudentUserId) && activePeriodIds.Contains(d.PeriodId))
                .Select(d => new { d.StudentUserId, d.PeriodId, d.Score })
                .ToListAsync(cancellationToken))
            .ToLookup(d => (d.StudentUserId, d.PeriodId), d => d.Score);

        var result = new List<CompanyStudent>(profiles.Count);
        foreach (var profile in profiles)
        {
            var placement = byStudent[profile.UserId];
            var key = (profile.UserId, placement.PeriodId);
            var stats = StudentStatsCalculator.ComputeAttendance(
                periods.ForPeriod(placement.PeriodId),
                attendance[key].ToList(),
                leaves[key].ToList(),
                today,
                localNow);
            var diary = StudentStatsCalculator.ComputeDiary(diaries[key].ToList());

            result.Add(new CompanyStudent(
                profile.UserId, profile.FullName, profile.HemisId, profile.GroupName, profile.Course, profile.Faculty,
                tutorByGroup.GetValueOrDefault(profile.GroupId),
                ApplicationStatus.Approved,
                placement.PeriodName,
                stats.AttendancePct, stats.AttendedDays, stats.TotalDays, diary.Count,
                StudentStateRule.For(stats), stats.SuspiciousCount));
        }

        return result;
    }
}
