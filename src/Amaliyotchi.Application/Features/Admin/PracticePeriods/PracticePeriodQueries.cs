using System.Globalization;
using Amaliyotchi.Application.Common.Exceptions;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Organization;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.Settings;
using Amaliyotchi.Domain.Students;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.PracticePeriods;

/// <summary>Davr yaratilishida global sozlamalardan nusxalanadigan qiymatlar.</summary>
internal sealed record PeriodDefaults(CheckInRules Rules, WorkDays WorkDays, bool DailyReportRequired);

/// <summary>Amaliyot davrlari buyruq/so'rovlari uchun umumiy yuklash va tekshiruvlar (bir joyda — takrorlanmasin).</summary>
internal static class PracticePeriodQueries
{
    public const string NotFoundMessage = "Amaliyot davri topilmadi.";
    public const string GroupIdsKey = "GroupIds";

    public static async Task<PracticePeriodDetail> LoadDetailAsync(
        IApplicationDbContext db, IClock clock, Guid id, CancellationToken cancellationToken)
    {
        var period = await db.PracticePeriods
            .AsNoTracking()
            .Include(p => p.Groups)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);

        var groupIds = period.Groups.Select(g => g.StudentGroupId).ToList();

        // Guruh keyin o'chirilgan bo'lsa ham davr tafsilotida ko'rinsin — tarix. IgnoreQueryFilters butun so'rovga
        // ta'sir qiladi, shuning uchun talabalar sanog'ida o'chirilganlar qo'lda chiqariladi.
        var groups = await (from g in db.StudentGroups.IgnoreQueryFilters().AsNoTracking()
                            join d in db.Directions.IgnoreQueryFilters() on g.DirectionId equals d.Id
                            join dept in db.Departments.IgnoreQueryFilters() on d.DepartmentId equals dept.Id
                            join f in db.Faculties.IgnoreQueryFilters() on dept.FacultyId equals f.Id
                            where groupIds.Contains(g.Id)
                            orderby g.Name
                            select new PracticePeriodGroupDto(
                                g.Id, g.Name, g.Course,
                                db.StudentProfiles.Count(s => s.StudentGroupId == g.Id && !s.IsDeleted && s.Status == StudentStatus.Active),
                                f.Id, f.Name, dept.Id, dept.Name, d.Id, d.Name))
            .ToListAsync(cancellationToken);

        var today = clock.LocalToday();
        return new PracticePeriodDetail(
            period.Id,
            period.Name,
            period.StartDate,
            period.EndDate,
            period.EffectiveStatus(today),
            groups.Count,
            groups.Sum(g => g.StudentsCount),
            period.CreatedAt,
            period.DailyStart.ToString("HH:mm", CultureInfo.InvariantCulture),
            period.DailyEnd.ToString("HH:mm", CultureInfo.InvariantCulture),
            period.WorkDays.ToCsv(),
            period.RequiredDays,
            period.DailyReportRequired,
            groups);
    }

    /// <summary>Kuzatiladigan (tracked) davr — yozish amallari uchun. Yo'q/o'chirilgan → 404.</summary>
    public static async Task<PracticePeriod> FindTrackedAsync(
        IApplicationDbContext db, Guid id, CancellationToken cancellationToken)
        => await db.PracticePeriods
               .Include(p => p.Groups)
               .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
           ?? throw new NotFoundException(NotFoundMessage);

    /// <summary>Ustma-ust tushish: berilgan guruhlardan biri [start, end] bilan kesishadigan boshqa (yopilmagan,
    /// o'chirilmagan) davrga biriktirilgan bo'lsa → 409, ro'yxat bilan.</summary>
    public static async Task EnsureNoOverlapAsync(
        IApplicationDbContext db, IReadOnlyCollection<Guid> groupIds, DateOnly startDate, DateOnly endDate,
        Guid? excludePeriodId, CancellationToken cancellationToken)
    {
        if (groupIds.Count == 0)
            return;

        var conflicts = await (from link in db.PracticePeriodGroups.AsNoTracking()
                               join p in db.PracticePeriods on link.PeriodId equals p.Id
                               join g in db.StudentGroups on link.StudentGroupId equals g.Id
                               where groupIds.Contains(link.StudentGroupId)
                                     && p.Status != PracticePeriodStatus.Closed
                                     && (excludePeriodId == null || p.Id != excludePeriodId)
                                     && p.StartDate <= endDate && p.EndDate >= startDate
                               orderby g.Name, p.StartDate
                               select new { Group = g.Name, Period = p.Name })
            .ToListAsync(cancellationToken);

        if (conflicts.Count == 0)
            return;

        var list = string.Join(", ", conflicts.Select(c => $"{c.Group} ({c.Period})"));
        throw new ConflictException($"Quyidagi guruhlar shu sanalarda boshqa davrga biriktirilgan: {list}");
    }

    /// <summary>Yangi biriktiriladigan guruhlar mavjud (o'chirilmagan) va faol bo'lishi shart → aks holda 400 <c>errors.GroupIds</c>.</summary>
    public static async Task EnsureGroupsAttachableAsync(
        IApplicationDbContext db, IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken)
    {
        if (groupIds.Count == 0)
            return;

        var found = await db.StudentGroups.AsNoTracking()
            .Where(g => groupIds.Contains(g.Id))
            .Select(g => new { g.Id, g.Name, g.IsActive })
            .ToListAsync(cancellationToken);

        var missing = groupIds.Count(id => found.All(g => g.Id != id));
        var inactive = found.Where(g => !g.IsActive).Select(g => g.Name).Order().ToList();

        var messages = new List<string>();
        if (missing > 0)
            messages.Add($"{missing} ta guruh topilmadi.");
        if (inactive.Count > 0)
            messages.Add($"Faol bo'lmagan guruhlar: {string.Join(", ", inactive)}.");

        if (messages.Count > 0)
        {
            var message = string.Join(" ", messages);
            throw new ValidationException(new Dictionary<string, string[]> { [GroupIdsKey] = [message] }, message);
        }
    }

    /// <summary>Kunlik ish vaqtini davrning daqiqa qoidalari bilan birga tekshiradi (<see cref="CheckInRules"/>).
    /// Domain xatosi (tugash boshlanishdan oldin, check-in oynasi sig'maydi) → 400 <c>errors.DailyEnd</c>.</summary>
    public static CheckInRules BuildRules(
        TimeOnly dailyStart, TimeOnly dailyEnd, int lateToleranceMinutes, int checkInWindowMinutes,
        int checkoutGraceMinutes, double minAccuracyM)
    {
        try
        {
            return new CheckInRules(
                dailyStart, dailyEnd, lateToleranceMinutes, checkInWindowMinutes, checkoutGraceMinutes, minAccuracyM);
        }
        catch (DomainException ex)
        {
            throw new ValidationException(
                new Dictionary<string, string[]> { [PracticePeriodValidationRules.DailyEndKey] = [ex.Message] }, ex.Message);
        }
    }

    /// <summary>Validator o'tkazgan ixtiyoriy qiymatlar: null → <paramref name="fallback"/>.</summary>
    public static TimeOnly TimeOr(string? value, TimeOnly fallback)
        => value is not null && PracticePeriodValidationRules.TryParseTime(value, out var time) ? time : fallback;

    public static WorkDays WorkDaysOr(string? value, WorkDays fallback)
    {
        var days = value is null ? null : PracticePeriodValidationRules.TryParseWorkDays(value);
        return days is null or WorkDays.None ? fallback : days.Value;
    }

    /// <summary>[start, end] dagi ish kunlari (bayramlarsiz) — <c>requiredDays</c>.</summary>
    public static async Task<int> CountRequiredDaysAsync(
        IApplicationDbContext db, DateOnly startDate, DateOnly endDate, WorkDays workDays, CancellationToken cancellationToken)
    {
        var holidays = await db.Holidays.AsNoTracking().ToListAsync(cancellationToken);
        return PracticePeriod.CountWorkDays(startDate, endDate, workDays, date => holidays.Any(h => h.AppliesTo(date)));
    }

    /// <summary>Global sozlamalar (bazada yo'q kalit → standart qiymat). Kunlik boshlanish/tugash vaqti uchun
    /// sozlama kaliti yo'q — <see cref="CheckInRules.Default"/> (09:00–17:00) olinadi.</summary>
    public static async Task<PeriodDefaults> LoadDefaultsAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        string[] keys =
        [
            SettingKeys.LateTolerance, SettingKeys.CheckInWindow, SettingKeys.AutoCheckout,
            SettingKeys.MinGpsAccuracy, SettingKeys.WorkDays, SettingKeys.DailyReportRequired
        ];
        var stored = await db.AppSettings.AsNoTracking()
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        string Raw(string key) => stored.TryGetValue(key, out var value) ? value : SettingKeys.Get(key).DefaultValue;
        int Int(string key) => int.TryParse(Raw(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? n
            : int.Parse(SettingKeys.Get(key).DefaultValue, CultureInfo.InvariantCulture);

        var defaults = CheckInRules.Default;
        var rules = new CheckInRules(
            defaults.DailyStart,
            defaults.DailyEnd,
            Int(SettingKeys.LateTolerance),
            Int(SettingKeys.CheckInWindow),
            Int(SettingKeys.AutoCheckout),
            Int(SettingKeys.MinGpsAccuracy));

        return new PeriodDefaults(
            rules,
            WorkDaysExtensions.Parse(Raw(SettingKeys.WorkDays)),
            string.Equals(Raw(SettingKeys.DailyReportRequired), "true", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Joriy o'quv yili tanlash qoidasi: faol, eng kech boshlangani birinchi (<c>FirstOrDefault</c> bilan olinadi).
    /// Davr yaratish va sidebar konteksti (<c>GET /api/admin/nav</c>) bir xil yilni ko'rsin.</summary>
    public static IQueryable<AcademicYear> CurrentAcademicYear(IApplicationDbContext db)
        => db.AcademicYears.AsNoTracking()
            .Where(y => y.IsActive)
            .OrderByDescending(y => y.StartDate);

    /// <summary>Joriy o'quv yili (faol). Yo'q bo'lsa → 400.</summary>
    public static async Task<Guid> CurrentAcademicYearIdAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var yearId = await CurrentAcademicYear(db)
            .Select(y => (Guid?)y.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return yearId ?? throw new DomainException("Joriy (faol) o'quv yili yo'q — avval o'quv yilini faollashtiring.");
    }

    /// <summary>Berilgan guruhlardan qaysilarining talabalari shu davrda davomat yozuviga ega (guruh id → nomi).</summary>
    public static async Task<Dictionary<Guid, string>> GroupsWithAttendanceAsync(
        IApplicationDbContext db, Guid periodId, IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken)
    {
        if (groupIds.Count == 0)
            return [];

        return await (from a in db.DailyAttendances.AsNoTracking()
                      join s in db.StudentProfiles.IgnoreQueryFilters() on a.StudentUserId equals s.UserId
                      join g in db.StudentGroups.IgnoreQueryFilters() on s.StudentGroupId equals g.Id
                      where a.PeriodId == periodId && groupIds.Contains(s.StudentGroupId)
                      select new { g.Id, g.Name })
            .Distinct()
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
    }
}
